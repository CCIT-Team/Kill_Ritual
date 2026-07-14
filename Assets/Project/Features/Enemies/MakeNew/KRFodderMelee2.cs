// Assets/Project/Scripts/05_Enemies/KRFodderMelee2.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    public sealed class KRFodderMelee2 : KREnemyBase
    {
        [Header("근접 공격")]
        [Tooltip("이 거리 안에 플레이어가 있으면 공격을 시작하며, 감지 범위보다 훨씬 작아야 합니다(예: 1.5~2.5).")]
        [Min(0.1f)]
        [SerializeField] private float _attackRange = 2f;

        [Tooltip("공격 간격(초). 이 시간마다 한 번씩 공격 애니메이션을 시작합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackCooldown = 1.2f;

        [Tooltip("한 번 때릴 때 주는 데미지.")]
        [Min(0f)]
        [SerializeField] private float _attackDamage = 10f;

        [Tooltip("공격 사거리 오차를 보정하는 여유값으로, 0으로 두어도 무방합니다.")]
        [Min(0f)]
        [SerializeField] private float _attackRangeBuffer = 0.2f;

        [Header("애니메이션")]
        [Tooltip("이 몬스터의 애니메이션을 재생하는 Animator로, 비워두면 애니메이션 없이도 정상 동작합니다.")]
        [SerializeField] private Animator _animator;

        [Tooltip("공격 애니메이션 이벤트가 누락됐을 때 공격 대기 상태를 강제로 해제하는 시간입니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackEventTimeout = 1.5f;

        private static readonly int AnimParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int AnimParamAttack = Animator.StringToHash("Attack");
        private static readonly int AnimParamIsDead = Animator.StringToHash("IsDead");

        [Header("공격 판정 위치 보정")]
        [Tooltip("공격 판정과 데미지 발생 지점의 높이를 발밑 기준에서 위로 올리는 보정값입니다(0이면 발밑 기준).")]
        [SerializeField] private float _attackHeightOffset = 1f;

        private Vector3 AttackOrigin => transform.position + Vector3.up * _attackHeightOffset;

        private float _nextAttackTime;

        // 공격 애니메이션은 시작됐지만, 아직 데미지 이벤트가 들어오지 않은 상태.
        private bool _isWaitingForAttackEvent;

        // Animation Event 누락 방지용.
        private float _attackEventExpireTime;

        // 죽음 애니메이션 트리거 중복 방지.
        private bool _deathAnimationTriggered;

        private void LateUpdate()
        {
            TryTriggerDeathAnimation();

            if (_isWaitingForAttackEvent && Time.time >= _attackEventExpireTime)
            {
                // 데미지 타이밍은 Animation Event가 통제하므로, 이벤트 누락 시 여기서는 대기 상태만 해제합니다.
                _isWaitingForAttackEvent = false;
            }
        }

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

            // 이미 공격 애니메이션 이벤트를 기다리는 중이면 Trigger가 겹치지 않도록 새 공격을 시작하지 않습니다.
            if (_isWaitingForAttackEvent)
            {
                return;
            }

            if (Time.time >= _nextAttackTime)
            {
                BeginMeleeAttack();
            }
        }

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

        public void AnimEvent_DealMeleeDamage()
        {
            if (!_isWaitingForAttackEvent)
            {
                return;
            }

            _isWaitingForAttackEvent = false;
            TryApplyMeleeDamage();
        }

        public void AnimationEvent_DealMeleeDamage()
        {
            AnimEvent_DealMeleeDamage();
        }

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

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AttackOrigin, _attackRange);
        }
    }
}