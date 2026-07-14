// Assets/Project/Scripts/05_Enemies/KRHybridMeleeRangedEnemy.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    public sealed class KRHybridMeleeRangedEnemy : KREnemyBase
    {
        [Header("공격 거리")]
        [Tooltip("이 거리 안에 플레이어가 있으면 원거리 공격 상태로 들어갑니다.")]
        [Min(1f)]
        [SerializeField] private float _rangedAttackRange = 15f;

        [Tooltip("이 거리 안에 플레이어가 있으면 원거리 대신 근접 공격을 사용합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _meleeAttackRange = 2.2f;

        [Tooltip("근접 사거리 판정 여유값입니다.")]
        [Min(0f)]
        [SerializeField] private float _meleeAttackRangeBuffer = 0.2f;

        [Tooltip("원거리 사거리 판정 여유값입니다.")]
        [Min(0f)]
        [SerializeField] private float _rangedAttackRangeBuffer = 0.75f;

        [Header("근접 공격 - KRFodderMelee2 방식")]
        [Tooltip("공격 간격(초). 이 시간마다 한 번씩 근접 공격 애니메이션을 시작합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _meleeAttackCooldown = 1.2f;

        [Tooltip("한 번 때릴 때 주는 데미지.")]
        [Min(0f)]
        [SerializeField] private float _meleeAttackDamage = 10f;

        [Tooltip("공격 애니메이션 이벤트가 누락됐을 때 공격 대기 상태를 강제로 해제하는 시간입니다. 데미지는 넣지 않습니다.")]
        [Min(0.1f)]
        [SerializeField] private float _meleeAttackEventTimeout = 1.5f;

        [Tooltip("공격 판정 구(Gizmo)와 데미지가 발생하는 지점의 높이를 위로 올리는 보정값입니다.")]
        [SerializeField] private float _meleeAttackHeightOffset = 1f;

        private Vector3 MeleeAttackOrigin => transform.position + Vector3.up * _meleeAttackHeightOffset;

        [Header("원거리 공격")]
        [Min(0.1f)]
        [SerializeField] private float _rangedCooldown = 1.6f;

        [Min(0f)]
        [SerializeField] private float _projectileDamage = 6f;

        [Min(1f)]
        [SerializeField] private float _projectileSpeed = 12f;

        [Tooltip("원거리 공격 시작 후 첫 투사체가 나가는 시간입니다. 이벤트가 없을 때만 fallback으로 사용됩니다.")]
        [Min(0f)]
        [SerializeField] private float _rangedFireDelay = 0.35f;

        [Tooltip("원거리 공격 중 새 공격/이동 전환을 막는 시간입니다. 연사 전체 시간보다 길어야 합니다.")]
        [Min(0.05f)]
        [SerializeField] private float _rangedAttackLockDuration = 1.0f;

        [Tooltip("한 번 원거리 공격할 때 연사할 투사체 수입니다.")]
        [Min(1)]
        [SerializeField] private int _projectileCount = 5;

        [Tooltip("연사할 때 발사 간격입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _projectileBurstInterval = 0.08f;

        [Tooltip("투사체들이 좌우로 퍼지는 전체 각도입니다.")]
        [Range(0f, 90f)]
        [SerializeField] private float _projectileSpreadAngle = 18f;

        [Header("발사체 프리팹")]
        [Tooltip("비워두면 코드가 자동으로 작은 구를 만들어 발사합니다.")]
        [SerializeField] private GameObject _projectilePrefab;

        [Tooltip("머즐 포인트가 없을 때 transform.position 기준으로 발사 위치를 위로 올리는 값입니다.")]
        [SerializeField] private float _muzzleHeightOffset = 1.1f;

        [Tooltip("지정하면 이 Transform의 위치를 발사 기준점으로 사용합니다.")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("발사체가 몬스터/머즐 Collider 안에서 생성되지 않도록 발사 방향으로 밀어내는 거리입니다.")]
        [Min(0f)]
        [SerializeField] private float _projectileSpawnForwardOffset = 0.65f;

        [Tooltip("발사체 프리팹의 축이 진행 방향과 맞지 않을 때 보정하는 추가 회전값입니다.")]
        [SerializeField] private Vector3 _projectileRotationOffset = Vector3.zero;

        [Tooltip("발사체 스폰 위치를 미세 조정하는 추가 오프셋입니다. 발사 방향 기준 로컬 좌표입니다.")]
        [SerializeField] private Vector3 _projectilePositionOffset = Vector3.zero;

        [Tooltip("커스텀 프리팹에 Collider가 없을 때 자동으로 붙일 SphereCollider 반지름입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _projectileColliderRadius = 0.3f;

        [Header("애니메이션")]
        [Tooltip("비워두면 자식 오브젝트에서 자동으로 Animator를 찾습니다.")]
        [SerializeField] private Animator _animator;

        [Tooltip("걷기 Bool 파라미터입니다.")]
        [SerializeField] private string _isWalkingBoolName = "IsWalking";

        [Tooltip("근접 공격 Trigger입니다. 기존 Fodder처럼 쓰려면 Attack으로 바꾸세요.")]
        [SerializeField] private string _meleeAttackTriggerName = "MeleeAttack";

        [Tooltip("원거리 공격 Trigger입니다. 기존 도깨비불 구조와 맞추기 위해 기본값은 Attack입니다.")]
        [SerializeField] private string _rangedAttackTriggerName = "Attack";

        [SerializeField] private string _groggyTriggerName = "Groggy";
        [SerializeField] private string _deadTriggerName = "IsDead";

        [Header("약점 파괴 그로기")]
        [Min(0.1f)]
        [SerializeField] private float _defaultWeakPointGroggyDuration = 2.5f;

        [Header("소멸/디졸브 Animation Event 전달")]
        [SerializeField] private bool _forwardDisintegrateAnimationEvents = true;

        [Tooltip("비워두면 자기 자신과 Animator 오브젝트에 메시지를 보냅니다.")]
        [SerializeField] private GameObject[] _disintegrateMessageTargets;

        [SerializeField]
        private string[] _disintegrateMessageNames =
        {
            "StartDisintegrate",
            "BeginDisintegrate",
            "PlayDisintegrate",
            "StartDissolve",
            "BeginDissolve",
            "PlayDissolve",
            "StartDeathDissolve",
            "BeginDeathDissolve"
        };

        [Header("디버그")]
        [SerializeField] private bool _debugLog = false;

        private int _isWalkingBoolHash;
        private int _meleeAttackTriggerHash;
        private int _rangedAttackTriggerHash;
        private int _groggyTriggerHash;
        private int _deadTriggerHash;

        private bool _hashReady;
        private bool _isWalking;
        private bool _deathAnimationTriggered;

        private float _weakPointGroggyEndTime;

        // ─────────────────────────────────────────────
        // Melee: KRFodderMelee2와 같은 대기/이벤트 구조
        // ─────────────────────────────────────────────

        private float _nextMeleeAttackTime;

        // 공격 애니메이션은 시작됐지만, 아직 데미지 이벤트가 들어오지 않은 상태.
        private bool _isWaitingForMeleeAttackEvent;

        // Animation Event 누락 방지용.
        private float _meleeAttackEventExpireTime;

        // ─────────────────────────────────────────────
        // Ranged
        // ─────────────────────────────────────────────

        private bool _isRangedAttackActive;
        private bool _rangedProjectilesFired;

        private float _rangedFireTime;
        private float _rangedAttackEndTime;
        private float _nextRangedTime;

        private Coroutine _rangedBurstRoutine;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_rangedAttackRange < _meleeAttackRange)
            {
                _rangedAttackRange = _meleeAttackRange + 0.1f;
            }

            _projectileCount = Mathf.Max(1, _projectileCount);

            if (_projectileBurstInterval < 0.01f)
            {
                _projectileBurstInterval = 0.01f;
            }

            float minimumRangedLockDuration =
                _rangedFireDelay +
                Mathf.Max(0, _projectileCount - 1) * _projectileBurstInterval +
                0.05f;

            if (_rangedAttackLockDuration < minimumRangedLockDuration)
            {
                _rangedAttackLockDuration = minimumRangedLockDuration;
            }
        }
#endif

        private void LateUpdate()
        {
            TryTriggerDeathAnimation();

            if (_isWaitingForMeleeAttackEvent && Time.time >= _meleeAttackEventExpireTime)
            {
                // KRFodderMelee2와 동일:
                // 이벤트가 누락된 경우, 다음 공격이 다시 가능하도록 대기 상태만 해제합니다.
                // 여기서 데미지를 넣지 않습니다.
                _isWaitingForMeleeAttackEvent = false;
            }

            UpdateRangedTimedExecution();
        }

        private void UpdateRangedTimedExecution()
        {
            if (!_isRangedAttackActive)
            {
                return;
            }

            if (!_rangedProjectilesFired && Time.time >= _rangedFireTime)
            {
                FireProjectileBurstOnce("Timed ranged rapid fire");
            }

            if (Time.time >= _rangedAttackEndTime && _rangedBurstRoutine == null)
            {
                _isRangedAttackActive = false;
            }
        }

        protected override void UpdateChase()
        {
            if (IsDead)
            {
                StopMoving();
                SetWalking(false);
                CancelAllAttacks();
                TryTriggerDeathAnimation();
                return;
            }

            if (_player == null)
            {
                _state = EnemyState.Idle;
                SetWalking(false);
                CancelAllAttacks();
                return;
            }

            if (Time.time < _weakPointGroggyEndTime)
            {
                StopMoving();
                SetWalking(false);
                CancelAllAttacks();
                return;
            }

            if (!ShouldKeepChasing())
            {
                _state = EnemyState.Idle;
                StopMoving();
                SetWalking(false);
                CancelAllAttacks();
                return;
            }

            float distance = DistanceToPlayer();

            FacePlayer();

            if (distance > _rangedAttackRange)
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
                CancelAllAttacks();
                TryTriggerDeathAnimation();
                return;
            }

            if (_player == null)
            {
                _state = EnemyState.Idle;
                SetWalking(false);
                CancelAllAttacks();
                return;
            }

            if (Time.time < _weakPointGroggyEndTime)
            {
                StopMoving();
                SetWalking(false);
                CancelAllAttacks();
                return;
            }

            float distance = DistanceToPlayer();

            if (!_isWaitingForMeleeAttackEvent &&
                !_isRangedAttackActive &&
                _rangedBurstRoutine == null &&
                distance > _rangedAttackRange + _rangedAttackRangeBuffer)
            {
                _state = EnemyState.Chase;
                SetWalking(false);
                return;
            }

            FacePlayer();
            StopMoving();
            SetWalking(false);

            // 근접 공격 이벤트 대기 중이면 KRFodderMelee2와 동일하게 새 공격을 시작하지 않습니다.
            if (_isWaitingForMeleeAttackEvent)
            {
                return;
            }

            // 원거리 연사 중이면 새 공격을 시작하지 않습니다.
            if (_isRangedAttackActive || _rangedBurstRoutine != null)
            {
                return;
            }

            if (distance <= _meleeAttackRange + _meleeAttackRangeBuffer)
            {
                if (Time.time >= _nextMeleeAttackTime)
                {
                    BeginMeleeAttack();
                }

                return;
            }

            if (Time.time >= _nextRangedTime)
            {
                BeginRangedAttack();
            }
        }

        public void EnterWeakPointGroggy(float duration)
        {
            if (IsDead)
            {
                return;
            }

            float finalDuration = duration > 0f ? duration : _defaultWeakPointGroggyDuration;

            _weakPointGroggyEndTime = Mathf.Max(
                _weakPointGroggyEndTime,
                Time.time + finalDuration);

            CancelAllAttacks();

            StopMoving();
            SetWalking(false);

            PlayTriggerIfExists(_groggyTriggerHash);
        }

        // ─────────────────────────────────────────────
        // Melee: KRFodderMelee2와 같은 구조
        // ─────────────────────────────────────────────

        private void BeginMeleeAttack()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (DistanceToPlayer() > _meleeAttackRange + _meleeAttackRangeBuffer)
            {
                return;
            }

            _isWaitingForMeleeAttackEvent = true;
            _meleeAttackEventExpireTime = Time.time + _meleeAttackEventTimeout;
            _nextMeleeAttackTime = Time.time + _meleeAttackCooldown;

            // 근접으로 전환되는 순간 원거리 대기/연사를 끊습니다.
            CancelRangedAttackOnly();

            SetAnimatorMeleeAttackTrigger();
        }

        public void AnimEvent_DealMeleeDamage()
        {
            if (!_isWaitingForMeleeAttackEvent)
            {
                return;
            }

            _isWaitingForMeleeAttackEvent = false;
            TryApplyMeleeDamage();
        }

        public void AnimationEvent_DealMeleeDamage()
        {
            AnimEvent_DealMeleeDamage();
        }

        public void AnimEvent_MeleeAttack()
        {
            AnimEvent_DealMeleeDamage();
        }

        public void AnimEvent_Attack()
        {
            if (_isWaitingForMeleeAttackEvent)
            {
                AnimEvent_DealMeleeDamage();
                return;
            }

            if (_isRangedAttackActive)
            {
                AnimEvent_FireProjectile();
            }
        }

        private void TryApplyMeleeDamage()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (DistanceToPlayer() > _meleeAttackRange + _meleeAttackRangeBuffer)
            {
                return;
            }

            IDamageable target = FindPlayerDamageable(_player);

            if (target == null || target.IsDead)
            {
                return;
            }

            Vector3 direction = (_player.position - MeleeAttackOrigin).normalized;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }

            var context = new KRDamageContext(
                _meleeAttackDamage,
                KRDamageType.Fire,
                MeleeAttackOrigin,
                direction);

            target.TakeDamage(context);
        }

        // ─────────────────────────────────────────────
        // Ranged
        // ─────────────────────────────────────────────

        private void BeginRangedAttack()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (DistanceToPlayer() > _rangedAttackRange + _rangedAttackRangeBuffer)
            {
                return;
            }

            _isRangedAttackActive = true;
            _rangedProjectilesFired = false;

            _rangedFireTime = Time.time + _rangedFireDelay;
            _rangedAttackEndTime = Time.time + _rangedAttackLockDuration;
            _nextRangedTime = Time.time + _rangedCooldown;

            SetAnimatorRangedAttackTrigger();
        }

        public void AnimEvent_FireProjectile()
        {
            AnimEvent_FireProjectiles();
        }

        public void AnimationEvent_FireProjectile()
        {
            AnimEvent_FireProjectile();
        }

        public void AnimEvent_FireProjectiles()
        {
            if (!_isRangedAttackActive)
            {
                return;
            }

            FireProjectileBurstOnce("Ranged animation event");
        }

        public void AnimationEvent_FireProjectiles()
        {
            AnimEvent_FireProjectiles();
        }

        public void AnimEvent_RangedAttack()
        {
            AnimEvent_FireProjectiles();
        }

        private void FireProjectileBurstOnce(string reason)
        {
            if (_rangedProjectilesFired)
            {
                return;
            }

            _rangedProjectilesFired = true;

            DebugLog(reason);
            FireProjectileBurst();
        }

        private void FireProjectileBurst()
        {
            if (_player == null || IsDead)
            {
                return;
            }

            if (_rangedBurstRoutine != null)
            {
                StopCoroutine(_rangedBurstRoutine);
                _rangedBurstRoutine = null;
            }

            _rangedBurstRoutine = StartCoroutine(FireProjectileBurstRoutine());
        }

        private IEnumerator FireProjectileBurstRoutine()
        {
            int count = Mathf.Max(1, _projectileCount);

            for (int i = 0; i < count; i++)
            {
                if (_player == null || IsDead)
                {
                    break;
                }

                Vector3 muzzlePosition = _muzzlePoint != null
                    ? _muzzlePoint.position
                    : transform.position + Vector3.up * _muzzleHeightOffset;

                Vector3 aimPoint = _player.position + Vector3.up * 1f;
                Vector3 baseDirection = aimPoint - muzzlePosition;

                if (baseDirection.sqrMagnitude <= 0.0001f)
                {
                    baseDirection = transform.forward;
                }
                else
                {
                    baseDirection.Normalize();
                }

                Vector3 direction = ComputeRapidFireDirection(baseDirection, i, count);
                FireSingleProjectile(muzzlePosition, direction, i);

                if (i < count - 1)
                {
                    yield return new WaitForSeconds(_projectileBurstInterval);
                }
            }

            _rangedBurstRoutine = null;

            if (Time.time >= _rangedAttackEndTime)
            {
                _isRangedAttackActive = false;
            }
        }

        private Vector3 ComputeRapidFireDirection(Vector3 baseDirection, int index, int count)
        {
            if (count <= 1 || _projectileSpreadAngle <= 0.01f)
            {
                return baseDirection;
            }

            float normalized = (float)index / (count - 1);
            float yawOffset = Mathf.Lerp(
                -_projectileSpreadAngle * 0.5f,
                _projectileSpreadAngle * 0.5f,
                normalized);

            Vector3 direction = Quaternion.AngleAxis(yawOffset, Vector3.up) * baseDirection;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return baseDirection;
            }

            return direction.normalized;
        }

        private void FireSingleProjectile(Vector3 muzzlePosition, Vector3 direction, int shotIndex)
        {
            Quaternion spawnRotation =
                Quaternion.LookRotation(direction) *
                Quaternion.Euler(_projectileRotationOffset);

            Vector3 spawnPosition =
                muzzlePosition +
                direction * _projectileSpawnForwardOffset +
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

            DebugLog("Projectile fired [" + shotIndex + "]: " + projectileObject.name);
        }

        private void IgnoreShooterCollision(GameObject projectileObject)
        {
            if (projectileObject == null)
            {
                return;
            }

            Collider[] shooterColliders = GetComponentsInChildren<Collider>(true);
            Collider[] projectileColliders = projectileObject.GetComponentsInChildren<Collider>(true);

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

        // ─────────────────────────────────────────────
        // Animator
        // ─────────────────────────────────────────────

        private void EnsureHashes()
        {
            if (_hashReady)
            {
                return;
            }

            _hashReady = true;

            _isWalkingBoolHash = Animator.StringToHash(_isWalkingBoolName);
            _meleeAttackTriggerHash = Animator.StringToHash(_meleeAttackTriggerName);
            _rangedAttackTriggerHash = Animator.StringToHash(_rangedAttackTriggerName);
            _groggyTriggerHash = Animator.StringToHash(_groggyTriggerName);
            _deadTriggerHash = Animator.StringToHash(_deadTriggerName);
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
            EnsureHashes();
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
            EnsureHashes();
            EnsureAnimatorReady();

            if (_isWalking == isWalking)
            {
                return;
            }

            _isWalking = isWalking;

            if (!HasAnimatorParameter(_isWalkingBoolHash, AnimatorControllerParameterType.Bool))
            {
                return;
            }

            _animator.SetBool(_isWalkingBoolHash, isWalking);
        }

        private void SetAnimatorMeleeAttackTrigger()
        {
            EnsureHashes();
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            if (!HasAnimatorParameter(_meleeAttackTriggerHash, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            _animator.ResetTrigger(_meleeAttackTriggerHash);
            _animator.SetTrigger(_meleeAttackTriggerHash);
        }

        private void SetAnimatorRangedAttackTrigger()
        {
            EnsureHashes();
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            if (!HasAnimatorParameter(_rangedAttackTriggerHash, AnimatorControllerParameterType.Trigger))
            {
                _isRangedAttackActive = false;
                return;
            }

            _animator.ResetTrigger(_rangedAttackTriggerHash);
            _animator.SetTrigger(_rangedAttackTriggerHash);
        }

        private void PlayTriggerIfExists(int triggerHash)
        {
            EnsureHashes();
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            if (!HasAnimatorParameter(triggerHash, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            _animator.ResetTrigger(triggerHash);
            _animator.SetTrigger(triggerHash);
        }

        private void CancelAllAttacks()
        {
            _isWaitingForMeleeAttackEvent = false;
            CancelRangedAttackOnly();
        }

        private void CancelRangedAttackOnly()
        {
            _isRangedAttackActive = false;
            _rangedProjectilesFired = false;

            if (_rangedBurstRoutine != null)
            {
                StopCoroutine(_rangedBurstRoutine);
                _rangedBurstRoutine = null;
            }
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
            CancelAllAttacks();

            StopMoving();
            SetWalking(false);

            EnsureHashes();
            EnsureAnimatorReady();

            if (_animator == null)
            {
                return;
            }

            if (HasAnimatorParameter(_meleeAttackTriggerHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(_meleeAttackTriggerHash);
            }

            if (HasAnimatorParameter(_rangedAttackTriggerHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(_rangedAttackTriggerHash);
            }

            PlayTriggerIfExists(_deadTriggerHash);
        }

        // ─────────────────────────────────────────────
        // Death / Disintegrate Animation Event
        // ─────────────────────────────────────────────

        public void AnimEvent_StartDisintegrate()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimationEvent_StartDisintegrate()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimEvent_BeginDisintegrate()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimationEvent_BeginDisintegrate()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimEvent_Disintegrate()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimationEvent_Disintegrate()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimEvent_StartDissolve()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimationEvent_StartDissolve()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimEvent_BeginDissolve()
        {
            ForwardDisintegrateAnimationEvent();
        }

        public void AnimationEvent_BeginDissolve()
        {
            ForwardDisintegrateAnimationEvent();
        }

        private void ForwardDisintegrateAnimationEvent()
        {
            if (!_forwardDisintegrateAnimationEvents)
            {
                return;
            }

            if (_disintegrateMessageNames == null || _disintegrateMessageNames.Length == 0)
            {
                return;
            }

            if (_disintegrateMessageTargets != null && _disintegrateMessageTargets.Length > 0)
            {
                for (int i = 0; i < _disintegrateMessageTargets.Length; i++)
                {
                    SendDisintegrateMessagesTo(_disintegrateMessageTargets[i]);
                }

                return;
            }

            SendDisintegrateMessagesTo(gameObject);

            EnsureAnimatorReady();

            if (_animator != null && _animator.gameObject != gameObject)
            {
                SendDisintegrateMessagesTo(_animator.gameObject);
            }
        }

        private void SendDisintegrateMessagesTo(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            for (int i = 0; i < _disintegrateMessageNames.Length; i++)
            {
                string messageName = _disintegrateMessageNames[i];

                if (string.IsNullOrEmpty(messageName))
                {
                    continue;
                }

                targetObject.SendMessage(
                    messageName,
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        private void DebugLog(string message)
        {
            if (!_debugLog)
            {
                return;
            }

            Debug.Log("[KRHybridMeleeRangedEnemy] " + message, this);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(MeleeAttackOrigin, _meleeAttackRange);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _rangedAttackRange);

            Vector3 muzzlePosition = _muzzlePoint != null
                ? _muzzlePoint.position
                : transform.position + Vector3.up * _muzzleHeightOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(muzzlePosition, 0.08f);
        }
    }

    public sealed class KRHybridMeleeRangedEnemyAnimationRelay : MonoBehaviour
    {
        [SerializeField] private KRHybridMeleeRangedEnemy _target;

        private void Awake()
        {
            if (_target == null)
            {
                _target = GetComponentInParent<KRHybridMeleeRangedEnemy>();
            }
        }

        public void AnimEvent_DealMeleeDamage()
        {
            if (_target == null)
            {
                return;
            }

            _target.AnimEvent_DealMeleeDamage();
        }

        public void AnimationEvent_DealMeleeDamage()
        {
            AnimEvent_DealMeleeDamage();
        }

        public void AnimEvent_MeleeAttack()
        {
            AnimEvent_DealMeleeDamage();
        }

        public void AnimEvent_Attack()
        {
            if (_target == null)
            {
                return;
            }

            _target.AnimEvent_Attack();
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

        public void AnimEvent_FireProjectiles()
        {
            if (_target == null)
            {
                return;
            }

            _target.AnimEvent_FireProjectiles();
        }

        public void AnimationEvent_FireProjectiles()
        {
            AnimEvent_FireProjectiles();
        }

        public void AnimEvent_RangedAttack()
        {
            AnimEvent_FireProjectiles();
        }

        public void AnimEvent_StartDisintegrate()
        {
            if (_target == null)
            {
                return;
            }

            _target.AnimEvent_StartDisintegrate();
        }

        public void AnimationEvent_StartDisintegrate()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimEvent_BeginDisintegrate()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimationEvent_BeginDisintegrate()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimEvent_Disintegrate()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimationEvent_Disintegrate()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimEvent_StartDissolve()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimationEvent_StartDissolve()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimEvent_BeginDissolve()
        {
            AnimEvent_StartDisintegrate();
        }

        public void AnimationEvent_BeginDissolve()
        {
            AnimEvent_StartDisintegrate();
        }
    }
}