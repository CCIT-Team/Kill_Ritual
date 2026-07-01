// Assets/Project/Scripts/05_Enemies/KREnemyEntity.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 몬스터의 체력 상태를 관리하고, 피격·그로기 전이·처형 보상을 처리하는 샌드백 엔티티입니다.
    ///
    /// [최적화 연동] KRCombatRegistry 자동 등록/해제
    ///   OnEnable()  → 자신의 모든 Collider를 KRManagers.Combat에 등록합니다.
    ///   OnDisable() → 등록한 Collider를 KRManagers.Combat에서 해제합니다.
    ///   이 라이프사이클 패턴 덕분에 KRPhysicsProjectile은 GetComponentInParent 없이
    ///   KRManagers.Combat.Lookup(collider)만으로 O(1)에 IDamageable을 얻습니다.
    ///   오브젝트 풀링 사용 시: Instantiate 대신 SetActive(true/false)로 풀링해도
    ///   OnEnable/OnDisable이 호출되므로 캐시 등록/해제가 자동으로 유지됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KREnemyEntity : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [Min(1f)]
        [SerializeField] private float _maxHealth = 200f;

        [Header("그로기 전이")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _groggyThresholdRatio = 0.3f;

        [Header("처형 보상 (Absorption)")]
        [Tooltip("처형 성공 시 플레이어 최대 체력 대비 회복 비율(%)")]
        [SerializeField] private float _recoverHealthPercent = 25f;

        [Tooltip("처형 성공 시 오행 5속성 자원에 각각 더해지는 회복량")]
        [SerializeField] private float _recoverResourceAmount = 25f;

        [Header("디버그/테스트")]
        [Tooltip("사망 후 자동 리셋. 반복 테스트 샌드백 용도.")]
        [SerializeField] private bool _autoResetForTesting = false;

        [SerializeField] private float _autoResetDelaySeconds = 2f;

        private float _currentHealth;
        private bool _isGroggy;
        private bool _isDead;
        private float _resetTimer;

        // 등록한 콜라이더 목록. OnDisable에서 정확히 해제하기 위해 캐싱합니다.
        private Collider[] _ownColliders;

        // ------------------------------------------------------------------
        // IDamageable
        // ------------------------------------------------------------------
        public bool IsDead => _isDead;
        public bool IsGroggy => _isGroggy;
        public Vector3 Position => transform.position;

        // ------------------------------------------------------------------
        // 라이프사이클 — 캐시 등록/해제
        // ------------------------------------------------------------------
        private void Awake()
        {
            _ownColliders = GetComponentsInChildren<Collider>(includeInactive: false);
            ResetState();
        }

        private void OnEnable()
        {
            // 씬 등장(또는 풀에서 꺼낼 때) 모든 콜라이더를 캐시에 등록합니다.
            // KRManagers가 아직 초기화되지 않은 극단적 엣지케이스를 null 체크로 보호합니다.
            if (KRManagers.Combat == null) return;

            foreach (Collider col in _ownColliders)
            {
                KRManagers.Combat.Register(col, this);
            }
        }

        private void OnDisable()
        {
            // 씬 퇴장(또는 풀에 반환할 때) 캐시에서 정확히 해제합니다.
            // 이 해제가 누락되면 파괴된 콜라이더로 향하는 Lookup이 null ref를 유발합니다.
            if (KRManagers.Combat == null) return;

            foreach (Collider col in _ownColliders)
            {
                KRManagers.Combat.Unregister(col);
            }
        }

        private void Update()
        {
            if (!_autoResetForTesting || !_isDead) return;

            _resetTimer += Time.deltaTime;
            if (_resetTimer >= _autoResetDelaySeconds) ResetState();
        }

        // ------------------------------------------------------------------
        // IDamageable 구현
        // ------------------------------------------------------------------
        public void TakeDamage(KRDamageContext context)
        {
            if (_isDead) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - context.DamageAmount);
            float ratio = _currentHealth / _maxHealth;

            if (!_isGroggy && ratio <= _groggyThresholdRatio && _currentHealth > 0f)
            {
                _isGroggy = true;
                Debug.Log($"[KREnemyEntity] {name} 그로기 진입 ({_currentHealth:F1}/{_maxHealth:F1})");
            }

            if (_currentHealth <= 0f) HandleDeath(wasExecuted: false);
        }

        public void Execute()
        {
            if (_isDead || !_isGroggy) return;

            _currentHealth = 0f;
            HandleDeath(wasExecuted: true);
        }

        private void HandleDeath(bool wasExecuted)
        {
            _isDead = true;
            _isGroggy = false;
            _resetTimer = 0f;

            if (wasExecuted)
            {
                Debug.Log($"[KREnemyEntity] {name} 처형됨 → 보상 발행");
                KRManagers.Event.Publish(
                    new KRExecutionSuccessEvent(_recoverHealthPercent, _recoverResourceAmount));
            }

            if (!_autoResetForTesting) gameObject.SetActive(false);
        }

        public void ResetState()
        {
            _currentHealth = _maxHealth;
            _isGroggy = false;
            _isDead = false;
            _resetTimer = 0f;
            gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------
        // 에디터 기즈모
        // ------------------------------------------------------------------
        private void OnDrawGizmos()
        {
            Gizmos.color = _isDead ? Color.gray : (_isGroggy ? Color.yellow : Color.green);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
    }
}