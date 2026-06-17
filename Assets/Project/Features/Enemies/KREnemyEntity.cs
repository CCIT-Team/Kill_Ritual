// Assets/Project/Scripts/05_Enemies/KREnemyEntity.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 몬스터의 체력 상태를 관리하고, 피격 시 데미지 처리 및 체력 30% 이하 그로기(처형 대기)
    /// 전이를 테스트할 수 있는 샌드백 엔티티입니다.
    /// 05_Enemies는 적 개체의 체력/상태머신만 다루며, IDamageable 계약을 통해서만 외부(플레이어
    /// 전투 시스템, 투사체)와 상호작용하므로 KRCombatSystem/KRPhysicsProjectile을 전혀 참조하지 않습니다.
    /// 처형(Execute) 성공 시에는 04_Execution의 "Absorption" 개념에 따라, 기존 KREventBus를 통해
    /// KRExecutionSuccessEvent를 발행하여 플레이어의 체력/자원 회복을 트리거합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KREnemyEntity : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [Tooltip("이 개체의 최대 체력")]
        [SerializeField] private float _maxHealth = 200f;

        [Header("그로기(처형 대기) 전이")]
        [Tooltip("현재 체력 비율이 이 값 이하로 떨어지면 그로기 상태로 전이합니다. (예: 0.3 = 30%)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _groggyThresholdRatio = 0.3f;

        [Header("처형 성공 시 플레이어 보상 (Absorption)")]
        [Tooltip("처형 성공 시 플레이어 최대 체력 대비 회복 비율(%). 0~100 범위.")]
        [SerializeField] private float _recoverHealthPercent = 25f;

        [Tooltip("처형 성공 시 플레이어의 오행 5속성 자원에 각각 더해지는 회복량.")]
        [SerializeField] private float _recoverResourceAmount = 25f;

        [Header("디버그/테스트 편의")]
        [Tooltip("true면 사망 즉시 비활성화하지 않고 체력을 가득 채워 자동으로 리셋합니다. 반복 테스트용 샌드백 모드.")]
        [SerializeField] private bool _autoResetForTesting = false;

        [Tooltip("자동 리셋까지 대기하는 시간(초). _autoResetForTesting이 true일 때만 사용됩니다.")]
        [SerializeField] private float _autoResetDelaySeconds = 2f;

        private float _currentHealth;
        private bool _isGroggy;
        private bool _isDead;
        private float _resetTimer;

        // ------------------------------------------------------------------
        // IDamageable 구현부
        // ------------------------------------------------------------------
        public bool IsDead => _isDead;

        public bool IsGroggy => _isGroggy;

        public Vector3 Position => transform.position;

        private void Awake()
        {
            ResetState();
        }

        private void Update()
        {
            // 테스트 편의를 위한 자동 리셋 타이머. 실제 AI/FSM이 붙기 전까지 샌드백 용도로만 사용됩니다.
            if (!_autoResetForTesting || !_isDead)
            {
                return;
            }

            _resetTimer += Time.deltaTime;

            if (_resetTimer >= _autoResetDelaySeconds)
            {
                ResetState();
            }
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (_isDead)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - context.DamageAmount);

            float healthRatio = _currentHealth / _maxHealth;

            // 아직 그로기가 아니었고, 비율이 임계치 이하로 내려갔으며, 그 즉시 죽은 것이 아니라면 그로기로 전이합니다.
            if (!_isGroggy && healthRatio <= _groggyThresholdRatio && _currentHealth > 0f)
            {
                _isGroggy = true;
                Debug.Log($"[KREnemyEntity] {name} 그로기 상태 진입 (체력 {_currentHealth:F1}/{_maxHealth:F1}, {healthRatio * 100f:F0}%) - 처형 대기 중");
            }

            if (_currentHealth <= 0f)
            {
                HandleDeath(wasExecuted: false);
            }
        }

        public void Execute()
        {
            // 처형은 반드시 그로기 상태에서만 유효합니다. 사거리 판정 등 추가 조건은 호출부(KRCombatSystem)의 책임입니다.
            if (_isDead || !_isGroggy)
            {
                return;
            }

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
                Debug.Log($"[KREnemyEntity] {name} 처형됨 - 보상(Absorption) 발행: 체력 +{_recoverHealthPercent}%, 자원 +{_recoverResourceAmount}");

                var rewardEvent = new KRExecutionSuccessEvent(_recoverHealthPercent, _recoverResourceAmount);
                KRManagers.Event.Publish(rewardEvent);
            }
            else
            {
                Debug.Log($"[KREnemyEntity] {name} 일반 사망 (처형 보상 없음)");
            }

            if (!_autoResetForTesting)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>체력/그로기/사망 상태를 모두 초기값으로 되돌립니다. 외부(스포너 등)에서도 호출할 수 있도록 public으로 둡니다.</summary>
        public void ResetState()
        {
            _currentHealth = _maxHealth;
            _isGroggy = false;
            _isDead = false;
            _resetTimer = 0f;
            gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------
        // 디버그 시각화: 현재 상태(정상/그로기/사망)를 색상으로 한눈에 확인할 수 있도록 합니다.
        // ------------------------------------------------------------------
        private void OnDrawGizmos()
        {
            Gizmos.color = _isDead ? Color.gray : (_isGroggy ? Color.yellow : Color.green);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
    }
}
