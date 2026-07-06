// Assets/Project/Features/Player/KRJakduSystem.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual; // [2026-07-06 추가] KRJakduChargeUI(Assets/Project/Features/UI/KRJakduChargeUI.cs)가 이 네임스페이스에 있습니다.

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 작두 시스템 — C키 입력 시 전방 광역 타격으로 탄약을 회수하고 공간을 확보합니다.
    ///
    /// [판정 방식]
    /// KRJakduZone(Box Collider Trigger)이 범위 내 적을 수집하고,
    /// 작두 발동 시 1프레임 활성화 후 수집된 적에게 피해를 적용합니다.
    ///
    /// [기획 요약]
    /// - C키 + 작두 자원 1개 소모
    /// - 등급별 피해 + 처치/적중에 따른 탄약 드롭
    /// - 드롭 탄약: 발동 시점 보유 비율 가장 낮은 속성 1종
    /// - 감속 → 판정 → 반발 가속
    ///
    /// [변경 이력 - 2026-07-06 (1차, 폐기됨)]
    /// ApplyHits() / SpawnAmmoOrb() 수정 — "둠 이터널 스타일 드롭" 요청 반영해서
    /// 처치는 KRDropSpawner로, 적중만 비율 드롭으로 나눴었음. 이후 정식 기획서(작두 시스템
    /// 기획 문서 3-5/4-4)를 확인해보니 기획 의도와 반대 방향이라 2차 변경에서 되돌림.
    ///
    /// [변경 이력 - 2026-07-06 (2차, 현재)]
    /// 정식 기획서 기준으로 재작성:
    ///   - 처치/적중 보상을 분리하지 않고, 작두 1회 발동 안에서 발생한 모든 대상의 보상을
    ///     ApplyHits()에서 전부 합산한 뒤 _dropCap(70%) 상한을 적용합니다(기획 3-5 예시와 동일).
    ///   - KRDropSpawner(처형 드롭 파이프라인)는 더 이상 호출하지 않습니다. 작두는 KRDropSpawner가
    ///     아니라 자체 비율 계산 + KRCombatSystem 자원 지갑을 직접 사용하는 독립 시스템입니다.
    ///   - SpawnAmmoOrb()는 "회수 가능한 만큼 즉시 흡수 + 초과분만 대표 오브젝트 하나로 드롭"
    ///     방식입니다(기획 4-4 "다수의 자원은 대표 오브젝트로 묶어서 표현"). 대상마다 오브를
    ///     흩뿌리지 않고, 작두 1회당 오브가 생성되어도 최대 1개입니다.
    /// 남은 과제(기획서 대비 아직 미구현, 별도 작업 필요):
    ///   - 갑사/장령 등급 구분 (현재 EnemyGrade는 Fodder/Heavy/Elite/Boss 4종뿐이라 갑사·장령이
    ///     Elite 하나로 합쳐져 있음. 장령 전용 수치(10%/30%)가 아직 코드에 없음)
    ///   - 등급별 넉백/경직 반응 (기획 3-4) — 현재 데미지만 적용하고 물리 반응 없음
    /// </summary>
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
        [Tooltip("작두 발동 시 잠깐 나타나는 샤먼소드 손 오브젝트입니다. ShamanSword.controller에는 " +
                 "Swing 모션 하나만 있고 별도 Idle(대기) 애니메이션이 없기 때문에, 평소엔 이 오브젝트 " +
                 "자체를 꺼둔 채로 있다가 작두를 쓸 때만 켜고, 스윙이 끝나면 다시 끕니다. " +
                 "비워두면 애니메이션 연출 없이 판정만 그대로 동작합니다(필수 아님).")]
        [SerializeField] private GameObject _shamanSwordVisualRoot;

        [Tooltip("샤먼소드의 Animator. 비워두면 _shamanSwordVisualRoot의 자식에서 자동 탐색합니다.")]
        [SerializeField] private Animator _shamanSwordAnimator;

        [Tooltip("Swing.anim 클립 길이(초)입니다. 이 시간이 지나면 샤먼소드 손을 자동으로 다시 숨깁니다. " +
                 "감속/반발 등 실제 판정 타이밍(약 0.2~0.3초)보다 스윙 애니메이션(약 1.17초)이 더 길어서, " +
                 "게임플레이 타이밍과는 별개로 이 값 기준으로 숨김 처리합니다. Swing.anim을 다른 클립으로 " +
                 "바꾸면 이 값도 그 클립 길이에 맞춰 같이 바꿔주세요. " +
                 "[2026-07-06 정정] 실제 Swing.anim의 m_StopTime을 확인해보니 0.6초가 아니라 " +
                 "1.1666666초였습니다(이전에 잘못 기재됨) — 그 값에 맞춰 수정했습니다.")]
        [Min(0.01f)]
        [SerializeField] private float _shamanSwordSwingClipLength = 1.17f;

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

        [Header("이동 처리")]
        [Tooltip("판정 전 감속 비율. 0.65 = 현재 속도의 65%.")]
        [Range(0f, 1f)]
        [SerializeField] private float _slowRatio = 0.65f;

        [Tooltip("감속 지속 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _slowDuration = 0.1f;

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

        /// <summary>현재 진행 중인 "샤먼소드 숨기기 예약" 코루틴입니다. 작두를 연속으로 빠르게 쓸 때
        /// 이전 예약이 새 스윙을 중간에 꺼버리지 않도록, 새로 시작하기 전에 이전 것을 취소합니다.</summary>
        private Coroutine _hideShamanSwordCoroutine;

        /// <summary>이동 속도 배율. 플레이어 이동 스크립트에서 이 값을 곱해 속도를 조절합니다.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        public int CurrentResource => _currentResource;
        public int MaxResource => _maxResource;

        /// <summary>
        /// [2026-07-06 추가] 작두가 지금 자기 자신의 판정(ApplyHits)으로 대상에게 피해를 주고 있는 동안만
        /// true입니다. KREnemyBase.EnterDead() → RefillJakduResourceOnKill()이 "누가 죽였든 상관없이
        /// 처치 시 작두 자원 +1" 훅을 갖고 있는데, 작두 자신의 처치까지 여기 걸리면 방금 소모한 자원을
        /// 같은 프레임 안에서 스스로 되돌려받는 자기환급 버그가 생깁니다(자원 소모 체감이 안 되는 원인).
        /// KREnemyBase.RefillJakduResourceOnKill()이 이 플래그를 확인해서, 작두 자신의 처치는
        /// 자원 재충전 대상에서 제외합니다. (다른 무기/시스템으로 죽인 처치는 그대로 재충전됩니다.)
        /// </summary>
        public static bool IsSelfExecuting { get; private set; }

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

            // [2026-07-06 추가] 샤먼소드 손도 평소엔 꺼둡니다. 작두를 쓸 때만 잠깐 보여줍니다.
            if (_shamanSwordVisualRoot != null)
                _shamanSwordVisualRoot.SetActive(false);

            // [2026-07-06 추가] 게임 시작 시점의 초기 자원(가득 참)을 UI에 즉시 반영합니다.
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

        /// <summary>외부에서 작두 자원을 추가합니다. 스테이지 매니저 등에서 호출하세요.</summary>
        public void AddResource(int amount = 1)
        {
            _currentResource = Mathf.Min(_maxResource, _currentResource + amount);

            // [2026-07-06 추가] 자원이 늘어난 즉시(예: KREnemyBase.EnterDead()의 처치 보상) UI에 반영합니다.
            UpdateJakduUI();
        }

        /// <summary>
        /// 현재/최대 작두 자원을 _jakduChargeUI(연결돼 있을 때만)에 전달해 텍스트 UI를 갱신합니다.
        /// _jakduChargeUI가 비어 있으면(연결 안 함) 아무 것도 하지 않습니다 — UI 연결은 필수가 아닙니다.
        /// </summary>
        private void UpdateJakduUI()
        {
            if (_jakduChargeUI == null) return;
            _jakduChargeUI.SetJakduState(_currentResource, _maxResource);
        }

        // ── 작두 시퀀스 ────────────────────────────────────────────────

        private IEnumerator JakduSequence()
        {
            _isActing = true;

            // [2026-07-06 변경] 자원 소모 시점을 여기서 아래(실제 판정 이후)로 옮겼습니다.
            // 기존엔 발동 즉시 무조건 1을 소모해서, 앞에 적이 하나도 없는 "허공 헛스윙"에도
            // 자원이 깎이는 문제가 있었습니다. 이제는 실제로 유효한 대상을 맞췄을 때만 소모합니다.

            // [2026-07-06 추가] 작두 애니메이션 트리거 — 평소 꺼져있던 샤먼소드 손을 보여주고
            // Swing 클립을 처음부터 재생합니다.
            PlayShamanSwordSwing();

            // ① 감속
            SpeedMultiplier = _slowRatio;
            yield return new WaitForSeconds(_slowDuration);

            // ② 판정 — 존을 1프레임 활성화해 적을 수집
            KRDamageType dropElement = GetLowestRatioElement();

            if (_jakduZone != null)
            {
                _jakduZone.gameObject.SetActive(true);

                // [2026-07-06 변경] 기존 "yield return null"은 렌더 프레임 1번만 대기했습니다.
                // 물리(Physics) 갱신은 FixedUpdate 고정 타임스텝으로 별도 동작하기 때문에,
                // 렌더 프레임 사이에 물리 스텝이 한 번도 안 도는 경우가 많아 트리거 콜백이
                // 전혀 발생하지 않고 _hits가 항상 비어 있는 문제가 있었습니다.
                // WaitForFixedUpdate()로 바꿔 최소 1번의 물리 스텝을 보장합니다.
                yield return new WaitForFixedUpdate();

                System.Collections.Generic.IReadOnlyCollection<IDamageable> hits = _jakduZone.GetHits();

                // [2026-07-06 추가] 존에 걸린 것 중 실제로 피해를 줄 수 있는(살아있는) 대상이
                // 하나라도 있을 때만 자원을 소모합니다. 허공에 휘두른 경우엔 소모하지 않습니다.
                if (HasValidTarget(hits))
                {
                    _currentResource--;
                    UpdateJakduUI();

                    ApplyHits(hits, dropElement);
                }

                _jakduZone.gameObject.SetActive(false);
            }

            // ③ 반발 가속
            SpeedMultiplier = _reboundRatio;
            yield return new WaitForSeconds(_reboundDuration);

            SpeedMultiplier = 1f;
            _isActing = false;
        }

        /// <summary>존이 수집한 대상 중 실제로 피해를 적용할 수 있는(null이 아니고 아직 살아있는) 대상이 있는지 확인합니다.</summary>
        private static bool HasValidTarget(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets)
        {
            foreach (IDamageable target in targets)
            {
                if (target != null && !target.IsDead) return true;
            }
            return false;
        }

        // ── 샤먼소드 애니메이션 ────────────────────────────────────────

        /// <summary>
        /// 평소 꺼져있던 샤먼소드 손을 켜고 Swing 애니메이션을 처음부터(0초 지점) 재생합니다.
        /// AnimatorController에 트리거/전환을 만드는 대신 Animator.Play()로 직접 재생을 강제합니다 —
        /// ShamanSword.controller에 Idle 상태가 없어서(현재 Swing 하나뿐) 트리거 기반 전환 구조를
        /// 만들 필요 자체가 없기 때문입니다(오브젝트를 껐다 켜는 것 자체가 "대기 상태"를 대신합니다).
        ///
        /// [2026-07-06 추가] 동시에 현재 장착 중인 원소 무기(Fire/Water/Wood 등)의 손 모델도 함께
        /// 숨깁니다. 안 그러면 샤먼소드 손과 원래 들고 있던 무기 손이 동시에 화면에 겹쳐 보입니다
        /// (흡혼 때 KRCombatSystem.SetCurrentWeaponVisualActive()로 무기를 숨긴 것과 같은 이유).
        /// </summary>
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

        /// <summary>
        /// _shamanSwordSwingClipLength(=Swing.anim 길이)만큼 기다린 뒤 샤먼소드 손을 다시 숨기고,
        /// 숨겨뒀던 원래 무기 손을 다시 보여줍니다. 감속/판정/반발로 이어지는 실제 게임플레이
        /// 타이밍(_isActing)과는 독립적으로 동작합니다 — 그래야 짧은 판정 타이밍에 맞춰 스윙
        /// 애니메이션이 잘리지 않고 끝까지 재생됩니다.
        /// </summary>
        private IEnumerator HideShamanSwordAfterSwing()
        {
            yield return new WaitForSeconds(_shamanSwordSwingClipLength);

            if (_shamanSwordVisualRoot != null)
                _shamanSwordVisualRoot.SetActive(false);

            _combatSystem?.SetCurrentWeaponVisualActive(true);

            _hideShamanSwordCoroutine = null;
        }

        // ── 피해 적용 ──────────────────────────────────────────────────

        private void ApplyHits(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets,
            KRDamageType dropElement)
        {
            // 기획 3-5: 적중 보상과 처치 보상은 대상마다 개별 지급하지 않고,
            // 이번 작두 발동에서 발생한 모든 대상의 보상을 전부 합산한 뒤 딱 한 번만 자원으로 환산합니다.
            float totalDropRatio = 0f;

            // [2026-07-06 추가] 이 블록 안에서 발생하는 처치는 전부 "작두 자신에 의한 처치"입니다.
            // IsSelfExecuting을 true로 켜두는 동안 KREnemyBase.TakeDamage() → EnterDead()가 동기적으로
            // 호출되며, RefillJakduResourceOnKill()이 이 플래그를 보고 작두 자원 재충전을 건너뜁니다
            // (작두로 죽여서 방금 쓴 자원을 스스로 되돌려받는 자기환급 버그 수정).
            IsSelfExecuting = true;
            try
            {
                foreach (IDamageable target in targets)
                {
                    if (target == null || target.IsDead) continue;

                    EnemyGrade grade = GetGrade(target);
                    float damage = _baseDamage * GetDamageMultiplier(grade);
                    bool killed = ApplyDamage(target, damage);

                    // 기획 3-5: 적중 보상과 처치 보상은 중복 지급하지 않습니다(killed일 때만 처치 보상).
                    totalDropRatio += CalculateDropRatio(grade, killed);

                    // TODO(넉백/경직): 기획 3-4 — 튼튼한 잡졸(장거리 넉백)/갑사(중거리 넉백)/
                    // 장령(짧은 경직)/보스(넉백 없음) 반응은 아직 구현하지 않았습니다. 별도 작업 필요.
                }
            }
            finally
            {
                IsSelfExecuting = false;
            }

            // 기획 3-5: 1회 작두 최대 드랍 상한(70%) 적용
            totalDropRatio = Mathf.Min(totalDropRatio, _dropCap);

            if (totalDropRatio > 0f)
                SpawnAmmoOrb(dropElement, totalDropRatio);
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

        private KRDamageType GetLowestRatioElement()
        {
            if (_combatSystem == null) return KRDamageType.Fire;

            KRDamageType lowest = _allElements[0];
            float lowestRatio = float.MaxValue;

            foreach (KRDamageType element in _allElements)
            {
                float ratio = _combatSystem.GetResourceRatio(element);
                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    lowest = element;
                }
            }

            return lowest;
        }

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

        /// <summary>
        /// 기획 3-5/4-4 반영: 계산된 비율만큼의 자원 중, 현재 자원 지갑에 회수 가능한 만큼은
        /// 즉시 흡수(RefillResource)하고, 지갑 최대치를 넘어 회수하지 못한 초과분만
        /// 대표 오브젝트 하나(KRDropItem)로 플레이어 앞에 드랍합니다.
        /// 대상마다 오브를 만들지 않고, 작두 1회당 최대 1개만 생성됩니다.
        /// </summary>
        private void SpawnAmmoOrb(KRDamageType element, float ratio)
        {
            if (_combatSystem == null) return;

            float maxAmount = _combatSystem.GetMaxResourceAmount(element);
            float dropAmount = maxAmount * ratio;
            if (dropAmount <= 0f) return;

            float current = _combatSystem.GetResourceAmount(element);
            float canReceive = Mathf.Max(0f, maxAmount - current);

            // 기획 4-4: "회수 가능 자원은 플레이어에게 빠르게 흡착" — 즉시 지갑에 채웁니다.
            float instantAmount = Mathf.Min(dropAmount, canReceive);
            if (instantAmount > 0f)
                _combatSystem.RefillResource(element, instantAmount);

            // 기획 4-4: "초과 자원은 바닥에 잔여 자원으로 유지" — 넘친 만큼만 오브 하나로 드랍합니다.
            float remaining = dropAmount - instantAmount;
            if (remaining <= 0f) return;

            int idx = (int)element;
            if (_ammoOrbPrefabs == null || idx < 0 || idx >= _ammoOrbPrefabs.Length) return;
            if (_ammoOrbPrefabs[idx] == null) return;

            Vector3 spawnPos = transform.position + transform.forward + Vector3.up * 0.5f;
            GameObject orbInstance = Instantiate(_ammoOrbPrefabs[idx], spawnPos, Quaternion.identity);

            var dropItem = orbInstance.GetComponent<KillRitual.Items.KRDropItem>();
            if (dropItem != null)
            {
                dropItem.ConfigureAmount(remaining);
            }
            else
            {
                Debug.LogWarning(
                    $"[KRJakduSystem] {_ammoOrbPrefabs[idx].name} 프리팹에 KRDropItem 컴포넌트가 없습니다. " +
                    "회수 가능한 오브가 아닙니다.");
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
