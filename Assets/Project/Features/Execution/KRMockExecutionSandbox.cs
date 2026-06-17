using System.Collections;
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;

namespace KillRitual.Execution
{
    /// <summary>
    /// 처형(Execution) 시퀀스를 Developer A가 별도 협업 없이 즉시 단독으로 테스트할 수 있도록 만든
    /// 목업(Mock) 테스트용 더미입니다. 항상 IsGroggy = true를 반환하므로, 그로기 연출 시스템이
    /// 아직 완성되지 않았더라도 처형 흐름(프롬프트 표시 → E키 → Execute → 보상 지급) 전체를
    /// 곧바로 검증할 수 있습니다.
    /// </summary>
    public sealed class KRMockExecutionSandbox : MonoBehaviour, IDamageable
    {
        [Header("Reset / Reward Settings")]
        [SerializeField] private float _resetDelaySeconds = 3f;
        [SerializeField] private float _recoverHealthAmount = 40f;
        [SerializeField] private float _recoverAmmoAmount = 30f;

        private MeshRenderer _meshRenderer;
        private bool _isDead;

        public bool IsDead => _isDead;

        // 테스트 목적상 항상 그로기 상태를 유지하여 언제든 처형 테스트가 가능하도록 합니다.
        public bool IsGroggy => true;

        public Vector3 Position => transform.position;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshRenderer == null)
            {
                Debug.LogWarning("[KRMockExecutionSandbox] MeshRenderer가 없습니다. 처형 시 시각적 피드백을 위해 MeshRenderer를 추가해주세요.");
            }
        }

        public void TakeDamage(KRDamageContext context)
        {
            // 테스트 더미는 일반 피격으로는 죽지 않고 오직 Execute()를 통해서만 처리되도록 설계합니다.
            // 필요 시 여기서 히트 리액션(예: 색상 점멸) 연출을 추가할 수 있습니다.
        }

        public void Execute()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }

            // 처형 성공 보상 이벤트를 발행하여 KRCombatSystem이 체력 40%, 전체 탄약 +30을 회복하도록 합니다.
            KRManagers.Event.Publish(new KRExecutionSuccessEvent(_recoverHealthAmount, _recoverAmmoAmount));

            StartCoroutine(ResetAfterDelay());
        }

        /// <summary>
        /// 3초 후 더미를 다시 활성화하여 반복 테스트가 가능하도록 합니다.
        /// </summary>
        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSeconds(_resetDelaySeconds);

            _isDead = false;

            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = true;
            }
        }
    }
}
