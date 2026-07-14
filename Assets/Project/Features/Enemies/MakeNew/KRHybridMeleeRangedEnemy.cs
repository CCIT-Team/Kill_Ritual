// Assets/Project/Scripts/05_Enemies/KRHybridMeleeRangedEnemy.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    public sealed class KRHybridMeleeRangedEnemy : KREnemyBase
    {
        [Header("奢問 剪葬")]
        [Tooltip("檜 剪葬 寰縑 Ы溯檜橫陛 氈戲賊 錳剪葬 奢問 鼻鷓煎 菟橫骨棲棻.")]
        [Min(1f)]
        [SerializeField] private float _rangedAttackRange = 15f;

        [Tooltip("檜 剪葬 寰縑 Ы溯檜橫陛 氈戲賊 錳剪葬 渠褐 斬蕾 奢問擊 餌辨м棲棻.")]
        [Min(0.1f)]
        [SerializeField] private float _meleeAttackRange = 2.2f;

        [Tooltip("斬蕾 餌剪葬 っ薑 罹嶸高殮棲棻.")]
        [Min(0f)]
        [SerializeField] private float _meleeAttackRangeBuffer = 0.2f;

        [Tooltip("錳剪葬 餌剪葬 っ薑 罹嶸高殮棲棻.")]
        [Min(0f)]
        [SerializeField] private float _rangedAttackRangeBuffer = 0.75f;

        [Header("斬蕾 奢問 - KRFodderMelee2 寞衝")]
        [Tooltip("奢問 除問(蟾). 檜 衛除葆棻 и 廓噶 斬蕾 奢問 擁棲詭檜暮擊 衛濛м棲棻.")]
        [Min(0.1f)]
        [SerializeField] private float _meleeAttackCooldown = 1.2f;

        [Tooltip("и 廓 陽萵 陽 輿朝 等嘐雖.")]
        [Min(0f)]
        [SerializeField] private float _meleeAttackDamage = 10f;

        [Tooltip("奢問 擁棲詭檜暮 檜漸お陛 援塊腑擊 陽 奢問 渠晦 鼻鷓蒂 鬼薯煎 п薯ж朝 衛除殮棲棻. 等嘐雖朝 厥雖 彊蝗棲棻.")]
        [Min(0.1f)]
        [SerializeField] private float _meleeAttackEventTimeout = 1.5f;

        [Tooltip("奢問 っ薑 掘(Gizmo)諦 等嘐雖陛 嫦儅ж朝 雖薄曖 堪檜蒂 嬪煎 螢葬朝 爾薑高殮棲棻.")]
        [SerializeField] private float _meleeAttackHeightOffset = 1f;

        private Vector3 MeleeAttackOrigin => transform.position + Vector3.up * _meleeAttackHeightOffset;

        [Header("錳剪葬 奢問")]
        [Min(0.1f)]
        [SerializeField] private float _rangedCooldown = 1.6f;

        [Min(0f)]
        [SerializeField] private float _projectileDamage = 6f;

        [Min(1f)]
        [SerializeField] private float _projectileSpeed = 12f;

        [Tooltip("錳剪葬 奢問 衛濛 �� 羅 癱餌羹陛 釭陛朝 衛除殮棲棻. 檜漸お陛 橈擊 陽虜 fallback戲煎 餌辨腌棲棻.")]
        [Min(0f)]
        [SerializeField] private float _rangedFireDelay = 0.35f;

        [Tooltip("錳剪葬 奢問 醞 億 奢問/檜翕 瞪�素� 虞朝 衛除殮棲棻. 翱餌 瞪羹 衛除爾棻 望橫撿 м棲棻.")]
        [Min(0.05f)]
        [SerializeField] private float _rangedAttackLockDuration = 1.0f;

        [Tooltip("и 廓 錳剪葬 奢問й 陽 翱餌й 癱餌羹 熱殮棲棻.")]
        [Min(1)]
        [SerializeField] private int _projectileCount = 5;

        [Tooltip("翱餌й 陽 嫦餌 除問殮棲棻.")]
        [Min(0.01f)]
        [SerializeField] private float _projectileBurstInterval = 0.08f;

        [Tooltip("癱餌羹菟檜 謝辦煎 ぷ雖朝 瞪羹 陝紫殮棲棻.")]
        [Range(0f, 90f)]
        [SerializeField] private float _projectileSpreadAngle = 18f;

        [Header("嫦餌羹 Щ葬ぱ")]
        [Tooltip("綠錶舒賊 囀萄陛 濠翕戲煎 濛擎 掘蒂 虜菟橫 嫦餌м棲棻.")]
        [SerializeField] private GameObject _projectilePrefab;

        [Tooltip("該闌 ん檣お陛 橈擊 陽 transform.position 晦遽戲煎 嫦餌 嬪纂蒂 嬪煎 螢葬朝 高殮棲棻.")]
        [SerializeField] private float _muzzleHeightOffset = 1.1f;

        [Tooltip("雖薑ж賊 檜 Transform曖 嬪纂蒂 嫦餌 晦遽薄戲煎 餌辨м棲棻.")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("嫦餌羹陛 跨蝶攪/該闌 Collider 寰縑憮 儅撩腎雖 彊紫煙 嫦餌 寞щ戲煎 塵橫頂朝 剪葬殮棲棻.")]
        [Min(0f)]
        [SerializeField] private float _projectileSpawnForwardOffset = 0.65f;

        [Tooltip("嫦餌羹 Щ葬ぱ曖 蹴檜 霞ч 寞щ婁 蜃雖 彊擊 陽 爾薑ж朝 蹺陛 �蛻�高殮棲棻.")]
        [SerializeField] private Vector3 _projectileRotationOffset = Vector3.zero;

        [Tooltip("嫦餌羹 蝶ア 嬪纂蒂 嘐撮 褻薑ж朝 蹺陛 螃Щ撢殮棲棻. 嫦餌 寞щ 晦遽 煎鏽 謝ル殮棲棻.")]
        [SerializeField] private Vector3 _projectilePositionOffset = Vector3.zero;

        [Tooltip("醴蝶籤 Щ葬ぱ縑 Collider陛 橈擊 陽 濠翕戲煎 稱橾 SphereCollider 奩雖葷殮棲棻.")]
        [Min(0.01f)]
        [SerializeField] private float _projectileColliderRadius = 0.3f;

        [Header("擁棲詭檜暮")]
        [Tooltip("綠錶舒賊 濠衝 螃粽薛お縑憮 濠翕戲煎 Animator蒂 瓊蝗棲棻.")]
        [SerializeField] private Animator _animator;

        [Tooltip("務晦 Bool だ塭嘐攪殮棲棻.")]
        [SerializeField] private string _isWalkingBoolName = "IsWalking";

        [Tooltip("斬蕾 奢問 Trigger殮棲棻. 晦襄 Fodder籀歲 噙溥賊 Attack戲煎 夥紱撮蹂.")]
        [SerializeField] private string _meleeAttackTriggerName = "MeleeAttack";

        [Tooltip("錳剪葬 奢問 Trigger殮棲棻. 晦襄 紫梟綠碳 掘褻諦 蜃蹺晦 嬪п 晦獄高擎 Attack殮棲棻.")]
        [SerializeField] private string _rangedAttackTriggerName = "Attack";

        [SerializeField] private string _groggyTriggerName = "Groggy";
        [SerializeField] private string _deadTriggerName = "IsDead";

        [Header("擒薄 だ惚 斜煎晦")]
        [Min(0.1f)]
        [SerializeField] private float _defaultWeakPointGroggyDuration = 2.5f;

        [Header("模資/蛤褸粽 Animation Event 瞪殖")]
        [SerializeField] private bool _forwardDisintegrateAnimationEvents = true;

        [Tooltip("綠錶舒賊 濠晦 濠褐婁 Animator 螃粽薛お縑 詭衛雖蒂 爾鹿棲棻.")]
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

        [Header("蛤幗斜")]
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

        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
        // Melee: KRFodderMelee2諦 偽擎 渠晦/檜漸お 掘褻
        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

        private float _nextMeleeAttackTime;

        // 奢問 擁棲詭檜暮擎 衛濛腑雖虜, 嬴霜 等嘐雖 檜漸お陛 菟橫螃雖 彊擎 鼻鷓.
        private bool _isWaitingForMeleeAttackEvent;

        // Animation Event 援塊 寞雖辨.
        private float _meleeAttackEventExpireTime;

        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
        // Ranged
        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

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
                // KRFodderMelee2諦 翕橾:
                // 檜漸お陛 援塊脹 唳辦, 棻擠 奢問檜 棻衛 陛棟ж紫煙 渠晦 鼻鷓虜 п薯м棲棻.
                // 罹晦憮 等嘐雖蒂 厥雖 彊蝗棲棻.
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

            // 斬蕾 奢問 檜漸お 渠晦 醞檜賊 KRFodderMelee2諦 翕橾ж啪 億 奢問擊 衛濛ж雖 彊蝗棲棻.
            if (_isWaitingForMeleeAttackEvent)
            {
                return;
            }

            // 錳剪葬 翱餌 醞檜賊 億 奢問擊 衛濛ж雖 彊蝗棲棻.
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

        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
        // Melee: KRFodderMelee2諦 偽擎 掘褻
        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

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

            // 斬蕾戲煎 瞪�秘Ж� 牖除 錳剪葬 渠晦/翱餌蒂 莒蝗棲棻.
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

        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
        // Ranged
        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

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

        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
        // Animator
        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

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

        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
        // Death / Disintegrate Animation Event
        // 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

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