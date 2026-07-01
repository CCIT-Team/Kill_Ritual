// Assets/Project/Scripts/05_Enemies/KRFodderRanged.cs
using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// Fodder(잡몹) 등급의 원거리 몬스터입니다.
    /// 플레이어와 일정 거리를 유지하면서(너무 가까우면 물러나고, 너무 멀면 다가감)
    /// 사거리 안에 있으면 쿨다운마다 플레이어를 향해 발사체를 쏩니다.
    ///
    /// 발사체는 별도의 프리팹 없이도 동작하도록, 발사 순간 코드로 작은 구(Sphere)를 만들어
    /// KREnemyProjectile 컴포넌트를 붙여 날립니다. 나중에 멋진 발사체 프리팹이 생기면
    /// 인스펙터의 _projectilePrefab 슬롯에 연결만 하면 그걸 대신 사용합니다.
    /// </summary>
    public sealed class KRFodderRanged : KREnemyBase
    {
        [Header("원거리 공격")]
        [Tooltip("이 거리 안에 플레이어가 있으면 발사를 시작합니다(공격 사거리).")]
        [Min(1f)]
        [SerializeField] private float _attackRange = 15f;

        [Tooltip("발사 간격(초).")]
        [Min(0.1f)]
        [SerializeField] private float _fireCooldown = 1.5f;

        [Tooltip("발사체 1발의 데미지.")]
        [Min(0f)]
        [SerializeField] private float _projectileDamage = 6f;

        [Tooltip("발사체의 비행 속도(미터/초).")]
        [Min(1f)]
        [SerializeField] private float _projectileSpeed = 12f;

        [Header("발사체 프리팹 (선택)")]
        [Tooltip("비워두면 코드가 자동으로 작은 구를 만들어 발사합니다. " +
                 "직접 만든 발사체 프리팹이 있으면 여기에 연결하세요.")]
        [SerializeField] private GameObject _projectilePrefab;

        [Tooltip("발사체가 나가는 높이 보정. 큐브 중심보다 살짝 위에서 쏘면 자연스럽습니다.")]
        [SerializeField] private float _muzzleHeightOffset = 0.5f;

        private float _nextFireTime;

        /// <summary>
        /// 추격: 공격 사거리 밖이면 플레이어에게 다가가고, 사거리 안에 들어오면 멈춰서 Attack으로 전환합니다.
        /// (물러나기 기능은 제거되어, 플레이어가 가까이 와도 도망가지 않습니다.)
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
                _state = EnemyState.Idle;
                StopMoving();
                return;
            }

            FacePlayer();

            // 공격 사거리보다 멀면 다가가고, 사거리 안에 들어오면 멈춰서 발사 상태로 전환합니다.
            // (물러나기 기능은 제거되어, 플레이어가 가까이 와도 도망가지 않습니다.)
            if (distance > _attackRange)
            {
                MoveTowards(_player.position);
            }
            else
            {
                StopMoving();
                _state = EnemyState.Attack;
            }
        }

        /// <summary>
        /// 공격: 사거리 안에서는 멈춰서 플레이어를 바라보며 쿨다운마다 발사체를 쏩니다.
        /// 플레이어가 사거리 밖으로 멀어지면 다시 추격(Chase) 상태로 돌아갑니다.
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
                _state = EnemyState.Chase;
                return;
            }

            // 사거리 안에서는 멈춰서 플레이어를 바라보며 발사합니다(물러나기 없음).
            FacePlayer();
            StopMoving();

            if (Time.time >= _nextFireTime)
            {
                FireProjectile();
                _nextFireTime = Time.time + _fireCooldown;
            }
        }

        /// <summary>플레이어를 향해 발사체 1발을 생성해 날립니다.</summary>
        private void FireProjectile()
        {
            if (_player == null)
            {
                return;
            }

            Vector3 muzzlePosition = transform.position + Vector3.up * _muzzleHeightOffset;

            // 플레이어의 몸통 중앙쯤(약간 위)을 겨냥합니다.
            Vector3 aimPoint = _player.position + Vector3.up * 1f;
            Vector3 direction = (aimPoint - muzzlePosition).normalized;

            GameObject projectileObject;

            if (_projectilePrefab != null)
            {
                // 프리팹이 지정돼 있으면 그것을 사용합니다.
                projectileObject = Instantiate(_projectilePrefab, muzzlePosition, Quaternion.LookRotation(direction));
            }
            else
            {
                // 프리팹이 없으면 코드로 작은 구를 즉석에서 만듭니다.
                projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectileObject.transform.position = muzzlePosition;
                projectileObject.transform.localScale = Vector3.one * 0.3f;

                // 자기 자신(몬스터)과 즉시 충돌하지 않도록 구의 콜라이더는 트리거로 둡니다.
                Collider sphereCollider = projectileObject.GetComponent<Collider>();
                if (sphereCollider != null)
                {
                    sphereCollider.isTrigger = true;
                }
            }

            // 발사체 컴포넌트를 붙이고 초기화합니다.
            KREnemyProjectile projectile = projectileObject.GetComponent<KREnemyProjectile>();
            if (projectile == null)
            {
                projectile = projectileObject.AddComponent<KREnemyProjectile>();
            }

            projectile.Launch(
                direction: direction,
                speed: _projectileSpeed,
                damage: _projectileDamage,
                shooter: transform);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
    }
}