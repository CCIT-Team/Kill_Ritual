// Assets/Project/Scripts/05_Enemies/KRDokkaebiFire.cs
using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 도깨비불 원거리 몬스터입니다.
    ///
    /// 플레이어가 공격 사거리 밖에 있으면 접근하고,
    /// 사거리 안에 들어오면 멈춰서 플레이어를 바라본 뒤 Attack 애니메이션을 재생합니다.
    ///
    /// 실제 투사체 발사는 Attack Trigger가 들어간 순간이 아니라,
    /// Attack 애니메이션 클립 안의 Animation Event가
    /// AnimEvent_FireProjectile()을 호출하는 순간에 발생합니다.
    ///
    /// Animator 파라미터:
    /// - Walk   : Bool    선택. 없으면 무시됨.
    /// - Attack : Trigger 필수 권장. 없으면 애니메이션 없이 즉시 발사 fallback.
    /// - IsDead : Trigger 권장. 없으면 사망 애니메이션 트리거만 무시됨.
    /// </summary>
    public sealed class KRDokkaebiFire : KREnemyBase
    {
        [Header("도깨비불 공격")]
        [Tooltip("이 거리 안에 플레이어가 있으면 공격을 시작합니다.")]
        [Min(1f)]
        [SerializeField] private float _attackRange = 15f;

        [Tooltip("공격 간격(초). 이 시간마다 한 번씩 Attack 애니메이션을 시작합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _fireCooldown = 1.5f;

        [Tooltip("Attack 애니메이션 이벤트가 누락됐을 때 공격 대기 상태를 강제로 해제하는 시간입니다.")]
        [Min(0.1f)]
        [SerializeField] private float _attackEventTimeout = 1.5f;

        [Tooltip("발사체 1발의 데미지.")]
        [Min(0f)]
        [SerializeField] private float _projectileDamage = 6f;

        [Tooltip("발사체의 비행 속도.")]
        [Min(1f)]
        [SerializeField] private float _projectileSpeed = 12f;

        [Header("발사체 프리팹")]
        [Tooltip("비워두면 코드가 자동으로 작은 구를 만들어 발사합니다.")]
        [SerializeField] private GameObject _projectilePrefab;

        [Tooltip("머즐 포인트가 없을 때, transform.position 기준으로 발사 위치를 위로 올리는 값입니다.")]
        [SerializeField] private float _muzzleHeightOffset = 0.8f;

        [Header("발사 위치/각도 보정")]
        [Tooltip("지정하면 이 Transform의 위치를 발사 기준점으로 사용합니다. 도깨비불 중심이나 입/눈/불꽃 중심에 두면 됩니다.")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("발사체 프리팹의 축이 진행 방향과 맞지 않을 때 보정하는 추가 회전값입니다.")]
        [SerializeField] private Vector3 _projectileRotationOffset = Vector3.zero;

        [Tooltip("발사체 스폰 위치를 미세 조정하는 추가 오프셋입니다. 발사 방향 기준 로컬 좌표입니다.")]
        [SerializeField] private Vector3 _projectilePositionOffset = Vector3.zero;

        [Tooltip("커스텀 프리팹에 Collider가 없을 때 자동으로 붙여줄 SphereCollider 반지름입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _projectileColliderRadius = 0.3f;

        [Header("애니메이션")]
        [Tooltip("비워두면 자식 오브젝트에서 자동으로 Animator를 찾습니다.")]
        [SerializeField] private Animator _animator;

        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

        private bool _isWalking;
        private bool _isWaitingForAttackEvent;
        private bool _deathAnimationTriggered;

        private float _nextFireTime;
        private float _attackEventExpireTime;

        private void LateUpdate()
        {
            TryTriggerDeathAnimation();

            if (_isWaitingForAttackEvent && Time.time >= _attackEventExpireTime)
            {
                // Animation Event를 빼먹었을 때 영구 대기 상태에 빠지는 것을 방지합니다.
                // 여기서 강제로 발사하지는 않습니다.
                // 발사 타이밍은 Attack 애니메이션 이벤트가 담당해야 하기 때문입니다.
                _isWaitingForAttackEvent = false;
            }
        }

        private void EnsureAnimatorReady()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
        {
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];

                if (parameter.nameHash == hash && parameter.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetWalking(bool isWalking)
        {
            if (_isWalking == isWalking)
            {
                return;
            }

            _isWalking = isWalking;

            if (!HasAnimatorParameter(WalkHash, AnimatorControllerParameterType.Bool))
            {
                return;
            }

            _animator.SetBool(WalkHash, isWalking);
        }

        /// <summary>
        /// Attack Trigger를 발동합니다.
        /// true를 반환하면 애니메이션 이벤트를 기다리고,
        /// false를 반환하면 애니메이션 없이 즉시 발사 fallback을 사용합니다.
        /// </summary>
        private bool PlayAttackAnimation()
        {
            if (!HasAnimatorParameter(AttackHash, AnimatorControllerParameterType.Trigger))
            {
                return false;
            }

            _animator.ResetTrigger(AttackHash);
            _animator.SetTrigger(AttackHash);
            return true;
        }

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

            if (!ShouldKeepChasing())
            {
                _state = EnemyState.Idle;
                StopMoving();
                SetWalking(false);
                CancelPendingAttackEvent();
                return;
            }

            float distance = DistanceToPlayer();

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

            bool attackAnimationStarted = PlayAttackAnimation();

            if (!attackAnimationStarted)
            {
                // Animator나 Attack Trigger가 없을 때도 기능 자체는 죽지 않게 fallback 처리합니다.
                _isWaitingForAttackEvent = false;
                FireProjectile();
            }
        }

        /// <summary>
        /// Attack 애니메이션 클립의 발사 프레임에 Animation Event로 호출하세요.
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
        /// Animation Event 이름 실수 방지용 호환 함수입니다.
        /// </summary>
        public void AnimationEvent_FireProjectile()
        {
            AnimEvent_FireProjectile();
        }

        /// <summary>
        /// Attack 이벤트 이름을 짧게 쓰고 싶을 때 사용할 수 있는 호환 함수입니다.
        /// </summary>
        public void AnimEvent_Attack()
        {
            AnimEvent_FireProjectile();
        }

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

            Quaternion spawnRotation =
                Quaternion.LookRotation(direction) *
                Quaternion.Euler(_projectileRotationOffset);

            Vector3 spawnPosition =
                muzzlePosition +
                (spawnRotation * _projectilePositionOffset);

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

            IgnoreShooterCollision(projectileObject);

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

        private void IgnoreShooterCollision(GameObject projectileObject)
        {
            if (projectileObject == null)
            {
                return;
            }

            Collider[] shooterColliders = GetComponentsInChildren<Collider>();
            Collider[] projectileColliders = projectileObject.GetComponentsInChildren<Collider>();

            for (int i = 0; i < shooterColliders.Length; i++)
            {
                Collider shooterCollider = shooterColliders[i];

                if (shooterCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < projectileColliders.Length; j++)
                {
                    Collider projectileCollider = projectileColliders[j];

                    if (projectileCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(projectileCollider, shooterCollider);
                }
            }
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

            if (!HasAnimatorParameter(IsDeadHash, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            if (HasAnimatorParameter(AttackHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(AttackHash);
            }

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

    /// <summary>
    /// Animator가 자식 오브젝트에 있고 KRDokkaebiFire가 부모 오브젝트에 있을 때 사용하는 Animation Event 중계기입니다.
    ///
    /// Unity Animation Event는 보통 Animator가 붙은 GameObject의 컴포넌트 함수를 찾습니다.
    /// 따라서 Animator가 Model 자식에 있고 KRDokkaebiFire가 Enemy Root에 있으면 이벤트 함수가 안 잡힐 수 있습니다.
    ///
    /// 그 경우 Animator가 붙은 자식 오브젝트에 이 컴포넌트를 붙이고,
    /// Target에 부모의 KRDokkaebiFire를 연결하세요.
    /// </summary>
    public sealed class KRDokkaebiFireAnimationRelay : MonoBehaviour
    {
        [SerializeField] private KRDokkaebiFire _target;

        private void Awake()
        {
            if (_target == null)
            {
                _target = GetComponentInParent<KRDokkaebiFire>();
            }
        }

        public void AnimEvent_FireProjectile()
        {
            if (_target == null)
            {
                return;
            }

            _target.AnimEvent_FireProjectile();
        }

        public void AnimationEvent_FireProjectile()
        {
            AnimEvent_FireProjectile();
        }

        public void AnimEvent_Attack()
        {
            AnimEvent_FireProjectile();
        }
    }
}