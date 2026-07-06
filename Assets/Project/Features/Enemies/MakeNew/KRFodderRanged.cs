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

        [Tooltip("Attack 애니메이션이 시작된 후 실제로 발사체가 나가기까지의 지연시간(초). " +
                 "애니메이션의 '준비 동작' 타이밍에 맞춰 조절하세요. 0이면 트리거와 동시에 발사합니다.")]
        [Min(0f)]
        [SerializeField] private float _attackAnimDelay = 0.3f;

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

        [Header("발사 위치/각도 보정")]
        [Tooltip("지정하면 이 Transform의 위치를 발사 기준점(총구)으로 사용합니다. " +
                 "비워두면 몬스터 위치 + _muzzleHeightOffset을 사용합니다. " +
                 "(예: 손이나 무기 끝에 만들어둔 VFXPoint를 연결)")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("발사체 프리팹 자체의 축이 진행 방향과 안 맞을 때 보정하는 추가 회전(오일러 각, degree).")]
        [SerializeField] private Vector3 _projectileRotationOffset = Vector3.zero;

        [Tooltip("발사체 스폰 위치를 미세 조정하는 추가 오프셋. " +
                 "발사 방향 기준 로컬 좌표입니다 (X=좌우, Y=상하, Z=전후).")]
        [SerializeField] private Vector3 _projectilePositionOffset = Vector3.zero;

        [Tooltip("커스텀 프리팹에 Collider가 없을 때 자동으로 붙여줄 SphereCollider의 반지름.")]
        [Min(0.01f)]
        [SerializeField] private float _projectileColliderRadius = 0.3f;

        [Header("애니메이션")]
        [Tooltip("비워두면 자식 오브젝트에서 자동으로 Animator를 찾습니다.")]
        [SerializeField] private Animator _animator;

        // Animator Controller에 미리 만들어 둔 파라미터: Walk(Bool), Attack(Trigger)
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private bool _isWalking;

        private float _nextFireTime;

        /// <summary>Animator가 비어 있으면 자식에서 자동으로 찾습니다.</summary>
        private void EnsureAnimatorReady()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>Walk Bool 파라미터를 갱신합니다(값이 바뀔 때만 SetBool 호출).</summary>
        private void SetWalking(bool isWalking)
        {
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            if (_isWalking == isWalking)
            {
                return;
            }

            _isWalking = isWalking;
            _animator.SetBool(WalkHash, isWalking);

#if UNITY_EDITOR
            Debug.Log($"[{name}] Walk = {isWalking} (Animator: {_animator.name})");
#endif
        }

        /// <summary>Attack Trigger 파라미터를 발동합니다.</summary>
        private void PlayAttackAnimation()
        {
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(AttackHash);
        }

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
                SetWalking(false);
                return;
            }

            FacePlayer();

            // 공격 사거리보다 멀면 다가가고, 사거리 안에 들어오면 멈춰서 발사 상태로 전환합니다.
            // (물러나기 기능은 제거되어, 플레이어가 가까이 와도 도망가지 않습니다.)
            if (distance > _attackRange)
            {
                MoveTowards(_player.position);
                SetWalking(true);
            }
            else
            {
                StopMoving();
                SetWalking(false);
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
                SetWalking(false);
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
            SetWalking(false);

            if (Time.time >= _nextFireTime)
            {
                PlayAttackAnimation();
                _nextFireTime = Time.time + _fireCooldown;

                if (_attackAnimDelay > 0f)
                {
                    CancelInvoke(nameof(FireProjectile));
                    Invoke(nameof(FireProjectile), _attackAnimDelay);
                }
                else
                {
                    FireProjectile();
                }
            }
        }

        /// <summary>플레이어를 향해 발사체 1발을 생성해 날립니다.</summary>
        private void FireProjectile()
        {
            if (_player == null)
            {
                return;
            }

            Vector3 muzzlePosition = _muzzlePoint != null
                ? _muzzlePoint.position
                : transform.position + Vector3.up * _muzzleHeightOffset;

            // 플레이어의 몸통 중앙쯤(약간 위)을 겨냥합니다.
            Vector3 aimPoint = _player.position + Vector3.up * 1f;
            Vector3 direction = (aimPoint - muzzlePosition).normalized;

            // 프리팹 축 보정용 회전과, 발사 방향 기준 위치 미세 조정을 적용합니다.
            Quaternion spawnRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(_projectileRotationOffset);
            Vector3 spawnPosition = muzzlePosition + (spawnRotation * _projectilePositionOffset);

            GameObject projectileObject;

            if (_projectilePrefab != null)
            {
                // 프리팹이 지정돼 있으면 그것을 사용합니다.
                projectileObject = Instantiate(_projectilePrefab, spawnPosition, spawnRotation);

                // VFX 프리팹(파티클 등)에는 보통 Collider가 없으므로, 충돌 판정을 위해 없으면 붙여줍니다.
                Collider prefabCollider = projectileObject.GetComponent<Collider>();
                if (prefabCollider == null)
                {
                    SphereCollider autoCollider = projectileObject.AddComponent<SphereCollider>();
                    autoCollider.isTrigger = true;
                    autoCollider.radius = _projectileColliderRadius;
                }
                else
                {
                    prefabCollider.isTrigger = true;
                }

                // 트리거 이벤트가 정상적으로 발생하려면 최소 한쪽에는 Rigidbody가 필요합니다.
                Rigidbody prefabRigidbody = projectileObject.GetComponent<Rigidbody>();
                if (prefabRigidbody == null)
                {
                    prefabRigidbody = projectileObject.AddComponent<Rigidbody>();
                }

                prefabRigidbody.useGravity = false;
                prefabRigidbody.isKinematic = true;
            }
            else
            {
                // 프리팹이 없으면 코드로 작은 구를 즉석에서 만듭니다.
                projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectileObject.transform.position = spawnPosition;
                projectileObject.transform.rotation = spawnRotation;
                projectileObject.transform.localScale = Vector3.one * 0.3f;

                // 자기 자신(몬스터)과 즉시 충돌하지 않도록 구의 콜라이더는 트리거로 둡니다.
                Collider sphereCollider = projectileObject.GetComponent<Collider>();
                if (sphereCollider != null)
                {
                    sphereCollider.isTrigger = true;
                }

                Rigidbody sphereRigidbody = projectileObject.AddComponent<Rigidbody>();
                sphereRigidbody.useGravity = false;
                sphereRigidbody.isKinematic = true;
            }

            // 발사체가 스폰 위치에서 자기 자신(발사한 몬스터)의 Collider와 겹쳐서
            // 즉시 충돌 판정이 나 사라지는 것을 방지합니다.
            Collider[] shooterColliders = GetComponentsInChildren<Collider>();
            Collider[] projectileColliders = projectileObject.GetComponentsInChildren<Collider>();
            foreach (Collider shooterCollider in shooterColliders)
            {
                foreach (Collider projectileCollider in projectileColliders)
                {
                    Physics.IgnoreCollision(projectileCollider, shooterCollider);
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

            Vector3 muzzlePosition = _muzzlePoint != null
                ? _muzzlePoint.position
                : transform.position + Vector3.up * _muzzleHeightOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(muzzlePosition, 0.08f);
        }
    }
}