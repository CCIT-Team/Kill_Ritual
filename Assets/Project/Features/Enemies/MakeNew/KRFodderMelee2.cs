// Assets/Project/Scripts/05_Enemies/KRFodderMelee2.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// Fodder(잡몹) 등급의 근접 몬스터입니다.
    /// NavMeshAgent를 이용해 플레이어를 끝까지 추격하다가, 공격 사거리 안에 들어오면 멈춰서
    /// 쿨다운마다 플레이어에게 직접 데미지를 줍니다(총알이나 발사체 없이 "닿으면 때리는" 방식).
    ///
    /// 이동/추격/그로기/사망 등 공통 로직은 전부 부모 클래스인 KREnemyBase가 처리합니다.
    /// 이 클래스는 딱 두 가지, "추격 중일 때 무엇을 할지"(UpdateChase)와
    /// "공격 사거리 안에서 무엇을 할지"(UpdateAttack)만 정의합니다.
    /// </summary>
    public sealed class KRFodderMelee2 : KREnemyBase
    {
        [Header("근접 공격")]
        [Tooltip("이 거리 안에 플레이어가 있으면 공격을 시작합니다(공격 사거리). " +
                 "감지 범위(_detectRange)보다 훨씬 작아야 합니다. 예: 1.5~2.5.")]
        [Min(0.1f)]
        [SerializeField] private float _attackRange = 2f;

        [Tooltip("공격 간격(초). 이 시간마다 한 번씩 때립니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackCooldown = 1.2f;

        [Tooltip("한 번 때릴 때 주는 데미지.")]
        [Min(0f)]
        [SerializeField] private float _attackDamage = 10f;

        [Tooltip("공격 사거리보다 플레이어가 살짝 더 안쪽에 있어도 때릴 수 있도록 주는 여유값(오차 보정). " +
                 "0으로 두어도 무방합니다.")]
        [Min(0f)]
        [SerializeField] private float _attackRangeBuffer = 0.2f;

        // 다음 공격이 가능한 시각. Time.time과 비교해서 쿨다운을 판단합니다.
        private float _nextAttackTime;

        /// <summary>
        /// 추격 상태: 공격 사거리 밖이면 플레이어에게 계속 다가갑니다.
        /// 사거리 안에 들어오면 멈춰서 Attack 상태로 전환합니다.
        /// </summary>
        protected override void UpdateChase()
        {
            if (_player == null)
            {
                _state = EnemyState.Idle;
                return;
            }

            // 더 이상 추격할 필요가 없으면(범위를 벗어났고 chaseForever도 아니면) Idle로 복귀합니다.
            if (!ShouldKeepChasing())
            {
                _state = EnemyState.Idle;
                StopMoving();
                return;
            }

            float distance = DistanceToPlayer();

            if (distance > _attackRange)
            {
                // 아직 멀리 있으면 플레이어를 향해 계속 걸어갑니다.
                MoveTowards(_player.position);
            }
            else
            {
                // 사거리 안에 들어왔으면 멈추고 공격 상태로 전환합니다.
                StopMoving();
                _state = EnemyState.Attack;
            }
        }

        /// <summary>
        /// 공격 상태: 사거리 안에서는 플레이어를 바라보며 멈춰 서서 쿨다운마다 때립니다.
        /// 플레이어가 사거리 밖으로 벗어나면 다시 추격(Chase) 상태로 돌아갑니다.
        /// </summary>
        protected override void UpdateAttack()
        {
            if (_player == null)
            {
                _state = EnemyState.Idle;
                return;
            }

            float distance = DistanceToPlayer();

            // 플레이어가 멀어졌으면 다시 쫓아갑니다.
            if (distance > _attackRange + _attackRangeBuffer)
            {
                _state = EnemyState.Chase;
                return;
            }

            // 사거리 안에서는 플레이어 쪽을 바라보며 제자리에 멈춥니다.
            FacePlayer();
            StopMoving();

            // 쿨다운이 다 됐으면 때립니다.
            if (Time.time >= _nextAttackTime)
            {
                PerformMeleeAttack();
                _nextAttackTime = Time.time + _attackCooldown;
            }
        }

        /// <summary>
        /// 실제로 플레이어에게 데미지를 주는 부분입니다.
        /// 발사체 없이, "지금 사거리 안에 플레이어가 있다"는 사실만으로 직접 데미지를 적용합니다.
        /// (때리는 애니메이션/이펙트는 나중에 이 메서드 안에 추가하면 됩니다.)
        /// </summary>
        private void PerformMeleeAttack()
        {
            if (_player == null)
            {
                return;
            }

            // 혹시 그 사이 사거리를 벗어났다면(예: 쿨다운 중 플레이어가 순간이동 등) 공격을 취소합니다.
            if (DistanceToPlayer() > _attackRange + _attackRangeBuffer)
            {
                return;
            }

            // 부모 클래스(KREnemyBase)가 제공하는 헬퍼로 플레이어의 IDamageable을 찾습니다.
            IDamageable target = FindPlayerDamageable(_player);

            if (target == null || target.IsDead)
            {
                return;
            }

            // KRDamageContext는 속성을 요구하지만 근접 몬스터에는 속성 개념이 없습니다.
            // KRFodderProjectile과 동일하게, 형식상 아무 값(Fire)이나 넣어 전달하며
            // 플레이어 체력은 속성과 무관하게 깎입니다.
            // [중요] 만약 팀의 KRDamageType enum에 Physical, None, Blunt 같은 값이 따로 있다면
            // 그 값으로 바꿔서 사용하세요. (KRDamageType.cs 파일에서 실제 목록을 확인하세요.)
            var context = new KRDamageContext(
                _attackDamage,
                KRDamageType.Fire,
                transform.position,
                (_player.position - transform.position).normalized);

            target.TakeDamage(context);
        }

        /// <summary>
        /// 씬 뷰에서 몬스터를 선택했을 때, 공격 사거리를 빨간 원으로 표시합니다.
        /// (부모의 노란 원 = 감지 범위, 이 빨간 원 = 공격 사거리)
        /// </summary>
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
    }
}