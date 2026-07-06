// Assets/Project/Scripts/05_Enemies/KRFodderRanged.cs
using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// Fodder(잡몹) 등급의 원거리 몬스터입니다.
    /// 플레이어와 일정 거리를 유지하면서 사거리 안에 있으면 Attack 애니메이션을 재생하고,
    /// 실제 투사체 발사는 Attack 애니메이션 클립 안의 Animation Event가 호출하는 시점에 발생합니다.
    ///
    /// 기존 방식:
    /// - Attack Trigger 발생
    /// - Invoke 지연 후 FireProjectile()
    ///
    /// 수정 방식:
    /// - Attack Trigger 발생
    /// - 애니메이션의 발사 프레임에서 AnimEvent_FireProjectile() 호출
    /// - 그 순간 투사체 발사
    /// </summary>
    public sealed class KRFodderRanged : KREnemyBase
    {
        [Header("원거리 공격")]
        [Tooltip("이 거리 안에 플레이어가 있으면 발사를 시작합니다(공격 사거리).")]
        [Min(1f)]
        [SerializeField] private float _attackRange = 15f;

        [Tooltip("발사 간격(초). 이 시간마다 한 번씩 Attack 애니메이션을 시작합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _fireCooldown = 1.5f;

        [Tooltip("Attack 애니메이션 이벤트가 누락됐을 때 공격 대기 상태를 강제로 해제하는 시간입니다. " +
                 "이 값이 없으면 Animation Event를 빼먹었을 때 몬스터가 공격 대기 상태에 갇힐 수 있습니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackEventTimeout = 1.5f;

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

        // Animator Controller에 미리 만들어 둘 파라미터.
        // 기존 코드의 Walk 이름은 유지합니다.
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

        private bool _isWalking;
        private bool _isWaitingForAttackEvent;
        private bool _deathAnimationTriggered;

        private float _nextFireTime;
        private float _attackEventExpireTime;

        /// <summary>
        /// 부모 KREnemyBase의 Update 흐름을 건드리지 않기 위해 LateUpdate에서
        /// 사망 애니메이션 트리거와 Animation Event 누락 방지만 처리합니다.
        /// </summary>
        private void LateUpdate()
        {
            TryTriggerDeathAnimation();

            if (_isWaitingForAttackEvent && Time.time >= _attackEventExpireTime)
            {
                // Animation Event가 빠졌을 때 다음 행동이 막히지 않도록 대기 상태만 해제합니다.
                // 여기서 강제로 발사하지 않는 이유:
                // 발사 타이밍을 애니메이션 이벤트로 통제하려는 목적과 충돌하기 때문입니다.
                _isWaitingForAttackEvent = false;
            }
        }

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
        }

        /// <summary>Attack Trigger 파라미터를 발동합니다.</summary>
        private void PlayAttackAnimation()
        {
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(AttackHash);
            _animator.SetTrigger(AttackHash);
        }

        /// <summary>
        /// 추격: 공격 사거리 밖이면 플레이어에게 다가가고,
        /// 사거리 안에 들어오면 멈춰서 Attack 상태로 전환합니다.
        /// </summary>
        protected override void UpdateChase()
        {
            if (IsDead)
            {
                StopMoving();
                SetWalking(false);
                CancelPendingAttackEvent();
                TryTriggerDeathAnimation();
                return;
            }

            if (_player == null)
            {
                _state = EnemyState.Idle;
                StopMoving();
                SetWalking(false);
                CancelPendingAttackEvent();
                return;
            }

            float distance = DistanceToPlayer();

            if (!ShouldKeepChasing())
            {
                _state = EnemyState.Idle;
                StopMoving();
                SetWalking(false);
                CancelPendingAttackEvent();
                return;
            }

            FacePlayer();

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
        /// 공격: 사거리 안에서는 멈춰서 플레이어를 바라보며 쿨다운마다 Attack 애니메이션을 재생합니다.
        /// 실제 투사체 발사는 이 함수가 아니라 Animation Event에서 발생합니다.
        /// </summary>
        protected override void UpdateAttack()
        {
            if (IsDead)
            {
                StopMoving();
                SetWalking(false);
                CancelPendingAttackEvent();
                TryTriggerDeathAnimation();
                return;
            }

            if (_player == null)
            {
                _state = EnemyState.Idle;
                StopMoving();
                SetWalking(false);
                CancelPendingAttackEvent();
                return;
            }

            float distance = DistanceToPlayer();

            // 이미 공격 애니메이션이 시작되어 발사 이벤트를 기다리는 중이면,
            // 플레이어가 살짝 사거리 밖으로 나가도 바로 Chase로 끊지 않습니다.
            // 원거리 공격은 '발사 준비 동작'이 끝나면 실제 발사가 나가는 쪽이 자연스럽습니다.
            if (!_isWaitingForAttackEvent && distance > _attackRange)
            {
                _state = EnemyState.Chase;
                SetWalking(false);
                return;
            }

            FacePlayer();
            StopMoving();
            SetWalking(false);

            if (_isWaitingForAttackEvent)
            {
                return;
            }

            if (Time.time >= _nextFireTime)
            {
                BeginRangedAttack();
            }
        }

        /// <summary>
        /// 원거리 공격 시작.
        /// 여기서는 투사체를 발사하지 않고 Attack 애니메이션만 재생합니다.
        /// 실제 투사체는 Attack 애니메이션 이벤트에서 발사됩니다.
        /// </summary>
        private void BeginRangedAttack()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (DistanceToPlayer() > _attackRange)
            {
                return;
            }

            _isWaitingForAttackEvent = true;
            _attackEventExpireTime = Time.time + _attackEventTimeout;
            _nextFireTime = Time.time + _fireCooldown;

            PlayAttackAnimation();
        }

        /// <summary>
        /// Attack 애니메이션 클립에 넣을 Animation Event 함수입니다.
        /// 발사체가 실제로 손/입/무기에서 나가야 하는 프레임에 이 함수를 호출하세요.
        /// </summary>
        public void AnimEvent_FireProjectile()
        {
            if (!_isWaitingForAttackEvent)
            {
                return;
            }

            _isWaitingForAttackEvent = false;
            FireProjectile();
        }

        /// <summary>
        /// Animation Event 이름을 다르게 기억했을 때를 위한 호환용 래퍼입니다.
        /// </summary>
        public void AnimationEvent_FireProjectile()
        {
            AnimEvent_FireProjectile();
        }

        /// <summary>
        /// 더 짧은 이름을 쓰고 싶을 때를 위한 호환용 래퍼입니다.
        /// </summary>
        public void AnimEvent_Attack()
        {
            AnimEvent_FireProjectile();
        }

        /// <summary>플레이어를 향해 발사체 1발을 생성해 날립니다.</summary>
        private void FireProjectile()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            Vector3 muzzlePosition = _muzzlePoint != null
                ? _muzzlePoint.position
                : transform.position + Vector3.up * _muzzleHeightOffset;

            Vector3 aimPoint = _player.position + Vector3.up * 1f;
            Vector3 direction = (aimPoint - muzzlePosition).normalized;

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            Quaternion spawnRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(_projectileRotationOffset);
            Vector3 spawnPosition = muzzlePosition + (spawnRotation * _projectilePositionOffset);

            GameObject projectileObject;

            if (_projectilePrefab != null)
            {
                projectileObject = Instantiate(_projectilePrefab, spawnPosition, spawnRotation);

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
                projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectileObject.transform.position = spawnPosition;
                projectileObject.transform.rotation = spawnRotation;
                projectileObject.transform.localScale = Vector3.one * 0.3f;

                Collider sphereCollider = projectileObject.GetComponent<Collider>();
                if (sphereCollider != null)
                {
                    sphereCollider.isTrigger = true;
                }

                Rigidbody sphereRigidbody = projectileObject.AddComponent<Rigidbody>();
                sphereRigidbody.useGravity = false;
                sphereRigidbody.isKinematic = true;
            }

            Collider[] shooterColliders = GetComponentsInChildren<Collider>();
            Collider[] projectileColliders = projectileObject.GetComponentsInChildren<Collider>();

            foreach (Collider shooterCollider in shooterColliders)
            {
                if (shooterCollider == null)
                {
                    continue;
                }

                foreach (Collider projectileCollider in projectileColliders)
                {
                    if (projectileCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(projectileCollider, shooterCollider);
                }
            }

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

        private void CancelPendingAttackEvent()
        {
            _isWaitingForAttackEvent = false;
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
            CancelPendingAttackEvent();

            StopMoving();
            SetWalking(false);

            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(AttackHash);
            _animator.SetTrigger(IsDeadHash);
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