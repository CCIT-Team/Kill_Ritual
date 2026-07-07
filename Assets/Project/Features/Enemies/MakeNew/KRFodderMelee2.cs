// Assets/Project/Scripts/05_Enemies/KRFodderMelee2.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// Fodder(잡몹) 등급의 근접 몬스터입니다.
    /// NavMeshAgent를 이용해 플레이어를 끝까지 추격하다가, 공격 사거리 안에 들어오면 멈춰서
    /// 쿨다운마다 공격 애니메이션을 재생합니다.
    ///
    /// 실제 데미지는 공격 시작 순간이 아니라, 공격 애니메이션 클립 안의 Animation Event가
    /// AnimEvent_DealMeleeDamage()를 호출하는 순간에 적용됩니다.
    ///
    /// 이동/추격/그로기/사망 등 공통 로직은 부모 클래스인 KREnemyBase가 처리합니다.
    /// 이 클래스는 "추격 중일 때 무엇을 할지"(UpdateChase),
    /// "공격 사거리 안에서 무엇을 할지"(UpdateAttack),
    /// "근접 공격 데미지 타이밍"만 정의합니다.
    /// </summary>
    public sealed class KRFodderMelee2 : KREnemyBase
    {
        [Header("근접 공격")]
        [Tooltip("이 거리 안에 플레이어가 있으면 공격을 시작합니다(공격 사거리). " +
                 "감지 범위(_detectRange)보다 훨씬 작아야 합니다. 예: 1.5~2.5.")]
        [Min(0.1f)]
        [SerializeField] private float _attackRange = 2f;

        [Tooltip("공격 간격(초). 이 시간마다 한 번씩 공격 애니메이션을 시작합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackCooldown = 1.2f;

        [Tooltip("한 번 때릴 때 주는 데미지.")]
        [Min(0f)]
        [SerializeField] private float _attackDamage = 10f;

        [Tooltip("공격 사거리보다 플레이어가 살짝 더 안쪽에 있어도 때릴 수 있도록 주는 여유값(오차 보정). " +
                 "0으로 두어도 무방합니다.")]
        [Min(0f)]
        [SerializeField] private float _attackRangeBuffer = 0.2f;

        [Header("애니메이션")]
        [Tooltip("이 몬스터의 애니메이션을 재생하는 Animator 컴포넌트입니다. " +
                 "보통 자식 오브젝트(모델)에 붙어있는 Animator를 여기로 드래그해서 연결하세요. " +
                 "비워두면 애니메이션 없이도 정상 동작합니다(에러 안 남).")]
        [SerializeField] private Animator _animator;

        [Tooltip("공격 애니메이션 이벤트가 누락됐을 때 공격 대기 상태를 강제로 해제하는 시간입니다. " +
                 "이 값이 없으면 Animation Event를 빼먹었을 때 몬스터가 공격 대기 상태에 갇힐 수 있습니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackEventTimeout = 1.5f;

        private static readonly int AnimParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int AnimParamAttack = Animator.StringToHash("Attack");
        private static readonly int AnimParamIsDead = Animator.StringToHash("IsDead");

        [Header("공격 판정 위치 보정")]
        [Tooltip("공격 판정 구(Gizmo)와 데미지가 발생하는 지점의 높이를 위로 올리는 보정값입니다. " +
                 "몬스터 발밑(원점) 기준이 아니라 몸통/가슴 높이쯤에서 공격이 나가는 것처럼 보이게 하고 싶을 때 조절하세요. " +
                 "0으로 두면 기존과 동일하게 발밑 기준입니다.")]
        [SerializeField] private float _attackHeightOffset = 1f;

        private Vector3 AttackOrigin => transform.position + Vector3.up * _attackHeightOffset;

        private float _nextAttackTime;

        // 공격 애니메이션은 시작됐지만, 아직 데미지 이벤트가 들어오지 않은 상태.
        private bool _isWaitingForAttackEvent;

        // Animation Event 누락 방지용.
        private float _attackEventExpireTime;

        // 죽음 애니메이션 트리거 중복 방지.
        private bool _deathAnimationTriggered;

        /// <summary>
        /// Update는 부모 KREnemyBase가 사용할 가능성이 있으므로 건드리지 않습니다.
        /// LateUpdate에서 사망 애니메이션 트리거와 공격 이벤트 누락 방지만 처리합니다.
        /// </summary>
        private void LateUpdate()
        {
            TryTriggerDeathAnimation();

            if (_isWaitingForAttackEvent && Time.time >= _attackEventExpireTime)
            {
                // 이벤트가 누락된 경우, 다음 공격이 다시 가능하도록 대기 상태만 해제합니다.
                // 여기서 데미지를 넣지 않는 이유:
                // 데미지 타이밍을 Animation Event로 통제하려는 목적과 충돌하기 때문입니다.
                _isWaitingForAttackEvent = false;
            }
        }

        /// <summary>
        /// 추격 상태: 공격 사거리 밖이면 플레이어에게 계속 다가갑니다.
        /// 사거리 안에 들어오면 멈춰서 Attack 상태로 전환합니다.
        /// </summary>
        protected override void UpdateChase()
        {
            if (IsDead)
            {
                StopMoving();
                SetAnimatorMoving(false);
                TryTriggerDeathAnimation();
                return;
            }

            if (_player == null)
            {
                _state = EnemyState.Idle;
                SetAnimatorMoving(false);
                return;
            }

            if (!ShouldKeepChasing())
            {
                _state = EnemyState.Idle;
                StopMoving();
                SetAnimatorMoving(false);
                return;
            }

            float distance = DistanceToPlayer();

            if (distance > _attackRange)
            {
                MoveTowards(_player.position);
                SetAnimatorMoving(true);
            }
            else
            {
                StopMoving();
                SetAnimatorMoving(false);
                _state = EnemyState.Attack;
            }
        }

        /// <summary>
        /// 공격 상태: 사거리 안에서는 플레이어를 바라보며 멈춰 서서 쿨다운마다 공격 애니메이션을 재생합니다.
        /// 실제 데미지는 이 함수가 아니라 Animation Event에서 들어갑니다.
        /// </summary>
        protected override void UpdateAttack()
        {
            if (IsDead)
            {
                StopMoving();
                SetAnimatorMoving(false);
                _isWaitingForAttackEvent = false;
                TryTriggerDeathAnimation();
                return;
            }

            if (_player == null)
            {
                _state = EnemyState.Idle;
                SetAnimatorMoving(false);
                _isWaitingForAttackEvent = false;
                return;
            }

            float distance = DistanceToPlayer();

            if (distance > _attackRange + _attackRangeBuffer)
            {
                _state = EnemyState.Chase;
                _isWaitingForAttackEvent = false;
                return;
            }

            FacePlayer();
            StopMoving();
            SetAnimatorMoving(false);

            // 이미 공격 애니메이션 이벤트를 기다리는 중이면 새 공격을 시작하지 않습니다.
            // 이걸 막지 않으면 Attack Trigger가 겹치고, 데미지 이벤트도 꼬일 수 있습니다.
            if (_isWaitingForAttackEvent)
            {
                return;
            }

            if (Time.time >= _nextAttackTime)
            {
                BeginMeleeAttack();
            }
        }

        /// <summary>
        /// 공격 시작.
        /// 여기서는 데미지를 주지 않고 Attack 트리거만 보냅니다.
        /// 실제 데미지는 애니메이션 클립의 이벤트가 AnimEvent_DealMeleeDamage()를 호출할 때 적용됩니다.
        /// </summary>
        private void BeginMeleeAttack()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (DistanceToPlayer() > _attackRange + _attackRangeBuffer)
            {
                return;
            }

            _isWaitingForAttackEvent = true;
            _attackEventExpireTime = Time.time + _attackEventTimeout;
            _nextAttackTime = Time.time + _attackCooldown;

            SetAnimatorAttackTrigger();
        }

        /// <summary>
        /// Animation Event에서 호출할 함수입니다.
        ///
        /// 사용법:
        /// Attack 애니메이션 클립을 열고, 실제 손/무기가 닿는 프레임에 Animation Event를 추가한 뒤
        /// Function에 AnimEvent_DealMeleeDamage를 선택하세요.
        /// </summary>
        public void AnimEvent_DealMeleeDamage()
        {
            if (!_isWaitingForAttackEvent)
            {
                return;
            }

            _isWaitingForAttackEvent = false;
            TryApplyMeleeDamage();
        }

        /// <summary>
        /// 함수 이름을 다르게 기억해도 쓸 수 있도록 둔 호환용 래퍼입니다.
        /// Animation Event에서 이 이름을 선택해도 동일하게 작동합니다.
        /// </summary>
        public void AnimationEvent_DealMeleeDamage()
        {
            AnimEvent_DealMeleeDamage();
        }

        /// <summary>
        /// 실제 데미지 적용.
        /// 반드시 Animation Event를 통해 호출되는 구조로 사용합니다.
        /// </summary>
        private void TryApplyMeleeDamage()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (DistanceToPlayer() > _attackRange + _attackRangeBuffer)
            {
                return;
            }

            IDamageable target = FindPlayerDamageable(_player);

            if (target == null || target.IsDead)
            {
                return;
            }

            var context = new KRDamageContext(
                _attackDamage,
                KRDamageType.Fire,
                AttackOrigin,
                (_player.position - AttackOrigin).normalized);

            target.TakeDamage(context);
        }

        private void SetAnimatorMoving(bool isMoving)
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetBool(AnimParamIsMoving, isMoving);
        }

        private void SetAnimatorAttackTrigger()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(AnimParamAttack);
            _animator.SetTrigger(AnimParamAttack);
        }

        private void TryTriggerDeathAnimation()
        {
            if (_deathAnimationTriggered)
            {
                return;
            }

            if (!IsDead)
            {
                return;
            }

            _deathAnimationTriggered = true;
            _isWaitingForAttackEvent = false;

            StopMoving();
            SetAnimatorMoving(false);

            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(AnimParamAttack);
            _animator.SetTrigger(AnimParamIsDead);
        }

        /// <summary>
        /// 씬 뷰에서 몬스터를 선택했을 때, 공격 사거리를 빨간 원으로 표시합니다.
        /// 부모의 노란 원 = 감지 범위, 이 빨간 원 = 공격 사거리입니다.
        /// </summary>
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AttackOrigin, _attackRange);
        }
    }
}