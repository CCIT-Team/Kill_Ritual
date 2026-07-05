// Assets/Project/Features/Player/KRJakduSystem.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

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

        /// <summary>이동 속도 배율. 플레이어 이동 스크립트에서 이 값을 곱해 속도를 조절합니다.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        public int CurrentResource => _currentResource;
        public int MaxResource => _maxResource;

        private void Awake()
        {
            if (_jakduZone == null)
                _jakduZone = GetComponentInChildren<KRJakduZone>(includeInactive: true);
            if (_combatSystem == null)
                _combatSystem = GetComponentInParent<KRCombatSystem>();
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            _currentResource = _maxResource;

            // 시작 시 판정 존 비활성화
            if (_jakduZone != null)
                _jakduZone.gameObject.SetActive(false);
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
        }

        // ── 작두 시퀀스 ────────────────────────────────────────────────

        private IEnumerator JakduSequence()
        {
            _isActing = true;
            _currentResource--;

            // TODO: 작두 애니메이션 트리거

            // ① 감속
            SpeedMultiplier = _slowRatio;
            yield return new WaitForSeconds(_slowDuration);

            // ② 판정 — 존을 1프레임 활성화해 적을 수집
            KRDamageType dropElement = GetLowestRatioElement();

            if (_jakduZone != null)
            {
                _jakduZone.gameObject.SetActive(true);
                yield return null; // OnTriggerStay 수집 대기

                ApplyHits(_jakduZone.GetHits(), dropElement);

                _jakduZone.gameObject.SetActive(false);
            }

            // ③ 반발 가속
            SpeedMultiplier = _reboundRatio;
            yield return new WaitForSeconds(_reboundDuration);

            SpeedMultiplier = 1f;
            _isActing = false;
        }

        // ── 피해 적용 ──────────────────────────────────────────────────

        private void ApplyHits(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets,
            KRDamageType dropElement)
        {
            float totalDropRatio = 0f;

            foreach (IDamageable target in targets)
            {
                if (target == null || target.IsDead) continue;

                EnemyGrade grade = GetGrade(target);
                float damage = _baseDamage * GetDamageMultiplier(grade);
                bool killed = ApplyDamage(target, damage);

                totalDropRatio += CalculateDropRatio(grade, killed);
            }

            // 드롭 상한 70% 적용
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

        private void SpawnAmmoOrb(KRDamageType element, float ratio)
        {
            if (_combatSystem == null) return;

            int idx = (int)element;
            if (_ammoOrbPrefabs == null || idx >= _ammoOrbPrefabs.Length) return;
            if (_ammoOrbPrefabs[idx] == null) return;

            float maxAmount = _combatSystem.GetMaxResourceAmount(element);
            float dropAmount = maxAmount * ratio;
            float current = _combatSystem.GetResourceAmount(element);
            float canReceive = maxAmount - current;

            // 즉시 회수 가능한 분량은 바로 채웁니다.
            if (canReceive > 0f)
                _combatSystem.RefillResource(element, Mathf.Min(dropAmount, canReceive));

            // 잔여분은 바닥 오브로 남깁니다.
            float remaining = dropAmount - canReceive;
            if (remaining > 0f)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
                Instantiate(_ammoOrbPrefabs[idx], spawnPos, Quaternion.identity);
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
