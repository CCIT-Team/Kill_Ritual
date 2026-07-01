// Assets/Project/Scripts/05_Enemies/KRFodderMelee.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// Fodder(잡몹) 등급의 근거리 몬스터입니다.
    /// 플레이어에게 곧장 다가가서, 닿을 만큼 가까워지면 일정 쿨다운마다 접촉 데미지를 줍니다.
    ///
    /// KREnemyBase를 상속하므로 체력/피격/그로기/사망/색상은 자동으로 처리됩니다.
    /// 이 클래스는 "어떻게 다가가고(UpdateChase), 어떻게 때리는가(UpdateAttack)"만 구현합니다.
    /// </summary>
    public sealed class KRFodderMelee : KREnemyBase
    {
        [Header("근접 공격")]
        [Tooltip("이 거리 안으로 들어오면 공격(Attack) 상태가 됩니다. 큐브 크기에 맞춰 1.5~2.5 정도가 적당합니다.")]
        [Min(0.5f)]
        [SerializeField] private float _attackRange = 2f;

        [Tooltip("한 번 공격한 뒤 다음 공격까지의 대기 시간(초).")]
        [Min(0.1f)]
        [SerializeField] private float _attackCooldown = 1f;

        [Tooltip("접촉 공격 1회당 플레이어에게 주는 데미지.")]
        [Min(0f)]
        [SerializeField] private float _attackDamage = 8f;

        // 다음 공격이 가능한 시각. 쿨다운 관리에 사용합니다.
        private float _nextAttackTime;

        /// <summary>
        /// 추격: 플레이어에게 직접 다가갑니다. 공격 사거리에 들어오면 Attack으로,
        /// 감지 범위 밖으로 멀어지면 다시 Idle로 전환합니다.
        /// </summary>
        protected override void UpdateChase()
        {
            if (_player == null)
            {
                _state = EnemyState.Idle;
                return;
            }

            float distance = DistanceToPlayer();

            if (!ShouldKeepChasing())
            {
                // 플레이어를 놓쳤습니다(끝까지 추격 옵션이 꺼져 있고 감지 범위 밖일 때만 해당).
                _state = EnemyState.Idle;
                StopMoving();
                return;
            }

            if (distance <= _attackRange)
            {
                // 때릴 수 있을 만큼 가까워졌습니다.
                _state = EnemyState.Attack;
                StopMoving();
                return;
            }

            // 아직 멀면 계속 다가갑니다.
            MoveTowards(_player.position);
            FacePlayer();
        }

        /// <summary>
        /// 공격: 사거리 안에 머무는 동안 쿨다운마다 플레이어에게 접촉 데미지를 줍니다.
        /// 플레이어가 멀어지면 다시 추격(Chase) 상태로 돌아갑니다.
        /// </summary>
        protected override void UpdateAttack()
        {
            if (_player == null)
            {
                _state = EnemyState.Idle;
                return;
            }

            float distance = DistanceToPlayer();

            if (distance > _attackRange)
            {
                // 사거리를 벗어났으면 다시 쫓아갑니다.
                _state = EnemyState.Chase;
                return;
            }

            FacePlayer();
            StopMoving();

            if (Time.time >= _nextAttackTime)
            {
                PerformMeleeHit();
                _nextAttackTime = Time.time + _attackCooldown;
            }
        }

        /// <summary>플레이어의 IDamageable을 찾아 데미지를 적용합니다.</summary>
        private void PerformMeleeHit()
        {
            // 게임오버/체력바를 담당하는 KRPlayerDamageFeedback을 우선 찾습니다.
            IDamageable target = FindPlayerDamageable(_player);

            if (target == null || target.IsDead)
            {
                return;
            }

            Vector3 hitDirection = (_player.position - transform.position).normalized;

            // KRDamageContext는 속성(KRDamageType)을 요구하지만, 몬스터 공격에는 속성 개념이 없습니다.
            // 플레이어 체력은 속성과 무관하게 깎이므로, 형식상 아무 값(Fire)이나 넣어 전달합니다.
            var context = new KRDamageContext(_attackDamage, KRDamageType.Fire, _player.position, hitDirection);
            target.TakeDamage(context);
        }

        // 공격 사거리도 씬 뷰에서 빨간 원으로 표시해 디버깅을 돕습니다.
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
    }
}