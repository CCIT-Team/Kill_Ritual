using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual;

namespace KillRitual.Player.Combat
{
    public sealed class KRJakduSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("작두 판정 존. 비워두면 자식에서 자동 탐색합니다.")]
        [SerializeField] private KRJakduZone _jakduZone;

        [Tooltip("KRCombatSystem. 비워두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Tooltip("플레이어 카메라. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private Camera _playerCamera;

        [Header("작두 애니메이션 (샤먼소드)")]
        [Tooltip("작두 발동 시 잠깐 나타나는 샤먼소드 손 오브젝트로, 평소엔 꺼두었다가 작두를 쓸 때만 켜고 스윙이 끝나면 다시 끄며, 비워두면 연출 없이 판정만 동작합니다.")]
        [SerializeField] private GameObject _shamanSwordVisualRoot;

        [Tooltip("샤먼소드의 Animator. 비워두면 _shamanSwordVisualRoot의 자식에서 자동 탐색합니다.")]
        [SerializeField] private Animator _shamanSwordAnimator;

        [Tooltip("Swing.anim 클립 길이(초)로, 이 시간이 지나면 샤먼소드 손을 숨기고 이동 감속도 이 값만큼 지속되므로 클립을 바꾸면 이 값도 함께 맞춰야 합니다.")]
        [Min(0.01f)]
        [SerializeField] private float _shamanSwordSwingClipLength = 0.55f;

        [Header("UI")]
        [Tooltip("작두(처형) 자원의 현재 보유 개수만 텍스트로 표시할 UI입니다(Assets/Project/Features/UI/KRJakduChargeUI.cs, " +
                 "TextMeshProUGUI 기반). 비워두면 UI 갱신을 건너뜁니다(필수 아님). Canvas 하위에 배치한 뒤 여기에 직접 드래그해서 연결하세요.")]
        [SerializeField] private KRJakduChargeUI _jakduChargeUI;

        [Header("작두 자원")]
        [Tooltip("작두 자원 현재 보유량.")]
        [SerializeField] private int _currentResource;
        [Tooltip("작두 자원 최대 보유량.")]
        [Min(1)]
        [SerializeField] private int _maxResource = 3;

        [Header("피해량")]
        [Tooltip("기본 피해량.")]
        [Min(0f)]
        [SerializeField] private float _baseDamage = 300f;

        [Header("등급별 피해 배율")]
        [Range(0f, 2f)][SerializeField] private float _multiplierFodder = 1.0f;
        [Range(0f, 2f)][SerializeField] private float _multiplierHeavy = 0.8f;
        [Range(0f, 2f)][SerializeField] private float _multiplierElite = 0.4f;
        [Range(0f, 2f)][SerializeField] private float _multiplierBoss = 0.2f;

        [Header("드롭 비율 (최대 탄약 대비 %)")]
        [Range(0f, 1f)][SerializeField] private float _dropFodderKill = 0.10f;
        [Range(0f, 1f)][SerializeField] private float _dropEliteHit = 0.05f;
        [Range(0f, 1f)][SerializeField] private float _dropEliteKill = 0.20f;
        [Range(0f, 1f)][SerializeField] private float _dropBossHit = 0.20f;
        [Range(0f, 1f)][SerializeField] private float _dropBossKill = 0.50f;
        [Tooltip("1회 작두 최대 드롭 상한.")]
        [Range(0f, 1f)][SerializeField] private float _dropCap = 0.70f;

        [Header("탄약 오브 프리팹")]
        [Tooltip("[0]=화 [1]=수 [2]=목 [3]=토 [4]=금 순서.")]
        [SerializeField] private GameObject[] _ammoOrbPrefabs = new GameObject[5];

        [Tooltip("프리팹 배열과 같은 순서(화/수/목/토/금)로, 체크를 끄면 프리팹을 지우지 않고도 해당 속성의 드롭을 완전히 끌 수 있어 슬롯이 잠긴 속성에 사용하세요.")]
        [SerializeField] private bool[] _ammoOrbEnabled = { true, true, true, true, true };

        [Header("탄약 오브 흩뿌리기")]
        [Tooltip("초과 자원(remaining)을 오브 하나가 아니라 이 개수만큼 쪼개 균등 고정 각도로 사방에 흩뿌리며, 합계는 항상 remaining과 같습니다.")]
        [Min(1)] [SerializeField] private int _ammoOrbSplitCount = 4;

        [Tooltip("각 조각이 옆으로 튀는 힘의 크기.")]
        [Min(0f)] [SerializeField] private float _ammoOrbOutwardForce = 3.5f;

        [Tooltip("각 조각이 위로 튀어오르는 힘의 크기.")]
        [Min(0f)] [SerializeField] private float _ammoOrbUpForce = 4f;

        [Tooltip("낙하 궤적이 잘 보이도록 스폰 기준 높이를 적 발밑이 아닌 적 머리 정도 높이(평균 고정값)로 올립니다.")]
        [Min(0.1f)] [SerializeField] private float _ammoOrbSpawnHeight = 2f;

        [Header("이동 처리")]
        [Tooltip("판정 전 감속 비율. 0.65 = 현재 속도의 65%.")]
        [Range(0f, 1f)]
        [SerializeField] private float _slowRatio = 0.65f;

        [Tooltip("판정 후 반발 가속 비율. 1.3 = 현재 속도의 130%.")]
        [Min(1f)]
        [SerializeField] private float _reboundRatio = 1.3f;

        [Tooltip("반발 가속 지속 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _reboundDuration = 0.12f;
        private static readonly KRDamageType[] _allElements =
        {
            KRDamageType.Fire, KRDamageType.Water, KRDamageType.Wood,
            KRDamageType.Earth, KRDamageType.Metal
        };

        private bool _isActing;

        private Coroutine _hideShamanSwordCoroutine;

        public float SpeedMultiplier { get; private set; } = 1f;

        public int CurrentResource => _currentResource;
        public int MaxResource => _maxResource;

        public static bool IsSelfExecuting { get; private set; }

        public bool IsActing => _isActing;

        private void Awake()
        {
            if (_jakduZone == null)
                _jakduZone = GetComponentInChildren<KRJakduZone>(includeInactive: true);
            if (_combatSystem == null)
                _combatSystem = GetComponentInParent<KRCombatSystem>();
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_shamanSwordAnimator == null && _shamanSwordVisualRoot != null)
                _shamanSwordAnimator = _shamanSwordVisualRoot.GetComponentInChildren<Animator>(includeInactive: true);

            _currentResource = _maxResource;

            // 시작 시 판정 존 비활성화
            if (_jakduZone != null)
                _jakduZone.gameObject.SetActive(false);

            if (_shamanSwordVisualRoot != null)
                _shamanSwordVisualRoot.SetActive(false);

            UpdateJakduUI();
        }

        private void Update()
        {
            if (_isActing) return;
            if (!Input.GetKeyDown(KeyCode.C)) return;

            if (_currentResource <= 0)
            {
                // TODO: HUD/사운드 피드백
                Debug.Log("[KRJakduSystem] 작두 자원 부족");
                return;
            }

            StartCoroutine(JakduSequence());
        }

        public void AddResource(int amount = 1)
        {
            _currentResource = Mathf.Min(_maxResource, _currentResource + amount);

            UpdateJakduUI();
        }

        private void UpdateJakduUI()
        {
            if (_jakduChargeUI == null) return;
            _jakduChargeUI.SetJakduState(_currentResource, _maxResource);
        }

        // ── 작두 시퀀스 ────────────────────────────────────────────────

        private IEnumerator JakduSequence()
        {
            _isActing = true;

            _currentResource--;
            UpdateJakduUI();

            PlayShamanSwordSwing();

            SpeedMultiplier = _slowRatio;
            yield return new WaitForSeconds(_shamanSwordSwingClipLength);

            if (_jakduZone != null)
            {
                _jakduZone.gameObject.SetActive(true);

                yield return new WaitForFixedUpdate();

                System.Collections.Generic.IReadOnlyCollection<IDamageable> hits = _jakduZone.GetHits();

                // 자원은 이미 위에서 소모했습니다(적중 여부와 무관). 여기서는 실제로 피해를 줄 수
                // 있는(살아있는) 대상이 하나라도 있을 때만 피해/드롭 처리를 진행합니다.
                if (HasValidTarget(hits))
                {
                    ApplyHits(hits);
                }

                _jakduZone.gameObject.SetActive(false);
            }

            // ③ 반발 가속
            SpeedMultiplier = _reboundRatio;
            yield return new WaitForSeconds(_reboundDuration);

            SpeedMultiplier = 1f;
            _isActing = false;
        }

        private static bool HasValidTarget(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets)
        {
            foreach (IDamageable target in targets)
            {
                if (target != null && !target.IsDead) return true;
            }
            return false;
        }

        // ── 샤먼소드 애니메이션 ────────────────────────────────────────

        private void PlayShamanSwordSwing()
        {
            if (_shamanSwordVisualRoot == null) return;

            // 연속으로 빠르게 작두를 쓸 경우, 이전 스윙에 걸려있던 "숨기기 예약"이 방금 시작한
            // 새 스윙을 중간에 꺼버리지 않도록 취소합니다.
            if (_hideShamanSwordCoroutine != null)
                StopCoroutine(_hideShamanSwordCoroutine);

            _combatSystem?.SetCurrentWeaponVisualActive(false);

            _shamanSwordVisualRoot.SetActive(true);
            _shamanSwordAnimator?.Play("Swing", layer: 0, normalizedTime: 0f);

            _hideShamanSwordCoroutine = StartCoroutine(HideShamanSwordAfterSwing());
        }

        private IEnumerator HideShamanSwordAfterSwing()
        {
            yield return new WaitForSeconds(_shamanSwordSwingClipLength);

            if (_shamanSwordVisualRoot != null)
                _shamanSwordVisualRoot.SetActive(false);

            _combatSystem?.SetCurrentWeaponVisualActive(true);

            _hideShamanSwordCoroutine = null;
        }

        // ── 피해 적용 ──────────────────────────────────────────────────

        private void ApplyHits(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets)
        {
            var rewards = new System.Collections.Generic.List<(float ratio, Vector3 position)>();

            Vector3 dropPositionSum = Vector3.zero;
            int dropPositionCount = 0;

            IsSelfExecuting = true;
            try
            {
                foreach (IDamageable target in targets)
                {
                    if (target == null || target.IsDead) continue;

                    EnemyGrade grade = GetGrade(target);
                    float damage = _baseDamage * GetDamageMultiplier(grade);
                    Vector3 targetPosition = target.Position;
                    bool killed = ApplyDamage(target, damage);

                    float dropRatio = CalculateDropRatio(grade, killed);

                    if (dropRatio > 0f)
                        rewards.Add((dropRatio, targetPosition));

                    // TODO(넉백/경직): 기획 3-4 — 튼튼한 잡졸(장거리 넉백)/갑사(중거리 넉백)/
                    // 장령(짧은 경직)/보스(넉백 없음) 반응은 아직 구현하지 않았습니다. 별도 작업 필요.
                }
            }
            finally
            {
                IsSelfExecuting = false;
            }

            rewards.Sort((a, b) => b.ratio.CompareTo(a.ratio));

            float totalDropRatio = 0f;

            foreach (var reward in rewards)
            {
                // 상한을 넘기는 순간부터는 남은(가치가 더 낮은) 보상을 전부 포기합니다.
                if (totalDropRatio + reward.ratio > _dropCap) break;

                totalDropRatio += reward.ratio;
                dropPositionSum += reward.position;
                dropPositionCount++;
            }

            if (totalDropRatio > 0f)
            {
                Vector3 dropPosition = dropPositionCount > 0
                    ? dropPositionSum / dropPositionCount
                    : transform.position + transform.forward;

                float[] waterFilledRatio = ComputeWaterFillingAllocation(totalDropRatio);

                var burstColliders = new System.Collections.Generic.List<Collider>();
                float burstBaseAngle = Random.Range(0f, 360f);
                for (int i = 0; i < _allElements.Length; i++)
                {
                    SpawnAmmoOrb(_allElements[i], waterFilledRatio[i], dropPosition, i, burstBaseAngle, burstColliders);
                }
                IgnoreCollisionsWithinBurst(burstColliders);
            }
        }

        private static void IgnoreCollisionsWithinBurst(System.Collections.Generic.List<Collider> colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] == null) continue;

                for (int j = i + 1; j < colliders.Count; j++)
                {
                    if (colliders[j] == null) continue;
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        private bool ApplyDamage(IDamageable target, float damage)
        {
            bool wasDead = target.IsDead;
            var ctx = new KRDamageContext(
                damage, KRDamageType.Metal,
                target.Position,
                (target.Position - transform.position).normalized);
            target.TakeDamage(ctx);
            return !wasDead && target.IsDead;
        }

        // ── 드롭 처리 ──────────────────────────────────────────────────

        private float CalculateDropRatio(EnemyGrade grade, bool killed)
        {
            return grade switch
            {
                EnemyGrade.Fodder => killed ? _dropFodderKill : 0f,
                EnemyGrade.Heavy => killed ? _dropFodderKill : 0f,
                EnemyGrade.Elite => killed ? _dropEliteKill : _dropEliteHit,
                EnemyGrade.Boss => killed ? _dropBossKill : _dropBossHit,
                _ => 0f,
            };
        }

        private float[] ComputeWaterFillingAllocation(float totalRatioBudget)
        {
            int n = _allElements.Length;
            float[] level = new float[n];      // 현재 채워진 비율 (0~1)
            float[] allocated = new float[n];   // 이번에 배분할 비율
            bool[] disabled = new bool[n];      // 드롭이 아예 꺼진 속성(재분배 대상에서도 제외)
            bool[] capped = new bool[n];        // 더 못 받는 속성(상한 도달 또는 드롭 비활성화)
            int uncappedCount = n;
            int enabledCount = 0;

            for (int i = 0; i < n; i++)
            {
                int elementIdx = (int)_allElements[i];
                bool dropEnabled = _ammoOrbEnabled == null || elementIdx < 0
                    || elementIdx >= _ammoOrbEnabled.Length || _ammoOrbEnabled[elementIdx];

                if (!dropEnabled)
                {
                    disabled[i] = true;
                    capped[i] = true;
                    uncappedCount--;
                    continue;
                }

                enabledCount++;
                level[i] = _combatSystem != null ? _combatSystem.GetResourceRatio(_allElements[i]) : 0f;
            }

            float remaining = totalRatioBudget;

            while (remaining > 0.0001f && uncappedCount > 0)
            {
                // 아직 상한에 안 걸린 속성 중 가장 낮은 수위를 찾습니다.
                float lowest = float.MaxValue;
                for (int i = 0; i < n; i++)
                    if (!capped[i] && level[i] < lowest) lowest = level[i];

                // 그 다음으로 낮은 수위(=이번에 채울 수 있는 상한선)를 찾습니다. 없으면 1.0까지.
                float nextTier = 1f;
                for (int i = 0; i < n; i++)
                    if (!capped[i] && level[i] > lowest && level[i] < nextTier) nextTier = level[i];

                int lowestGroupCount = 0;
                for (int i = 0; i < n; i++)
                    if (!capped[i] && Mathf.Approximately(level[i], lowest)) lowestGroupCount++;

                float costToRaiseGroup = (nextTier - lowest) * lowestGroupCount;

                if (remaining >= costToRaiseGroup)
                {
                    // 예산이 충분하면 이 그룹 전원을 nextTier까지 채우고 다음 단계로 넘어갑니다.
                    for (int i = 0; i < n; i++)
                    {
                        if (capped[i] || !Mathf.Approximately(level[i], lowest)) continue;

                        allocated[i] += nextTier - level[i];
                        level[i] = nextTier;

                        if (level[i] >= 1f - 0.0001f)
                        {
                            capped[i] = true;
                            uncappedCount--;
                        }
                    }
                    remaining -= costToRaiseGroup;
                }
                else
                {
                    // 예산이 모자라면 지금 그룹끼리 남은 예산을 균등하게 나눠 갖고 종료합니다.
                    float share = remaining / lowestGroupCount;
                    for (int i = 0; i < n; i++)
                        if (!capped[i] && Mathf.Approximately(level[i], lowest))
                            allocated[i] += share;

                    remaining = 0f;
                }
            }

            // 모든 속성이 상한(또는 드롭 비활성화)에 도달했는데도 예산이 남으면,
            // 드롭이 켜진 속성들에 균등하게 재분배합니다(오브 형태로 흘러넘치도록).
            // 이게 없으면 최대 탄약에 도달한 순간부터 남은 보상이 그냥 사라져서
            // "처치해도 자원이 하나도 안 나온다" 버그가 재발합니다.
            if (remaining > 0.0001f && enabledCount > 0)
            {
                int spillCount = 0;
                for (int i = 0; i < n; i++)
                    if (!disabled[i]) spillCount++;

                if (spillCount > 0)
                {
                    float share = remaining / spillCount;
                    for (int i = 0; i < n; i++)
                        if (!disabled[i]) allocated[i] += share;
                }

                remaining = 0f;
            }

            return allocated;
        }

        private void SpawnAmmoOrb(
            KRDamageType element, float ratio, Vector3 origin, int elementIndex, float burstBaseAngle,
            System.Collections.Generic.List<Collider> burstColliders)
        {
            if (_combatSystem == null) return;

            int elementIdx = (int)element;
            if (_ammoOrbEnabled != null && elementIdx >= 0 && elementIdx < _ammoOrbEnabled.Length
                && !_ammoOrbEnabled[elementIdx])
            {
                return;
            }

            float maxAmount = _combatSystem.GetMaxResourceAmount(element);
            float dropAmount = maxAmount * ratio;
            if (dropAmount <= 0f) return;

            float current = _combatSystem.GetResourceAmount(element);
            float canReceive = Mathf.Max(0f, maxAmount - current);

            // 기획 4-4: "회수 가능 자원은 플레이어에게 빠르게 흡착" — 즉시 지갑에 채웁니다.
            float instantAmount = Mathf.Min(dropAmount, canReceive);
            if (instantAmount > 0f)
                _combatSystem.RefillResource(element, instantAmount);

            float remaining = dropAmount - instantAmount;
            if (remaining <= 0f) return;

            int idx = elementIdx;
            if (_ammoOrbPrefabs == null || idx < 0 || idx >= _ammoOrbPrefabs.Length) return;
            if (_ammoOrbPrefabs[idx] == null) return;

            Vector3 spawnPos = origin + Vector3.up * _ammoOrbSpawnHeight;
            float perPieceAmount = remaining / _ammoOrbSplitCount;

            float angleStep = 360f / _ammoOrbSplitCount;
            float groupAngleOffset = angleStep / _allElements.Length * elementIndex;
            float startAngle = burstBaseAngle + groupAngleOffset;

            for (int i = 0; i < _ammoOrbSplitCount; i++)
            {
                float angleDeg = startAngle + angleStep * i;
                Vector3 direction = new Vector3(
                    Mathf.Cos(angleDeg * Mathf.Deg2Rad), 0f, Mathf.Sin(angleDeg * Mathf.Deg2Rad));

                SpawnAmmoOrbPiece(_ammoOrbPrefabs[idx], spawnPos, perPieceAmount, direction, burstColliders);
            }
        }

        private void SpawnAmmoOrbPiece(
            GameObject prefab, Vector3 spawnPos, float amount, Vector3 direction,
            System.Collections.Generic.List<Collider> burstColliders)
        {
            GameObject orbInstance = Instantiate(prefab, spawnPos, Quaternion.identity);

            var dropItem = orbInstance.GetComponent<KillRitual.Items.KRDropItem>();
            if (dropItem != null)
            {
                dropItem.ConfigureAmount(amount);
            }
            else
            {
                Debug.LogWarning(
                    $"[KRJakduSystem] {prefab.name} 프리팹에 KRDropItem 컴포넌트가 없습니다. " +
                    "회수 가능한 오브가 아닙니다.");
            }

            if (orbInstance.TryGetComponent(out Collider pieceCollider))
            {
                burstColliders.Add(pieceCollider);
            }

            if (orbInstance.TryGetComponent(out Rigidbody rb))
            {
                float forceVariance = Random.Range(0.85f, 1.15f);
                Vector3 force = (direction * _ammoOrbOutwardForce + Vector3.up * _ammoOrbUpForce) * forceVariance;
                rb.AddForce(force, ForceMode.Impulse);
            }
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────

        private static EnemyGrade GetGrade(IDamageable target)
            => target is KillRitual.Enemies.KREnemyBase enemy ? enemy.Grade : EnemyGrade.Fodder;

        private float GetDamageMultiplier(EnemyGrade grade) => grade switch
        {
            EnemyGrade.Fodder => _multiplierFodder,
            EnemyGrade.Heavy => _multiplierHeavy,
            EnemyGrade.Elite => _multiplierElite,
            EnemyGrade.Boss => _multiplierBoss,
            _ => 1f,
        };
    }
}
