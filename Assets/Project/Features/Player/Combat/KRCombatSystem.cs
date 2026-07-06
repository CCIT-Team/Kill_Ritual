// Assets/Project/Scripts/02_Player/Combat/KRCombatSystem.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Weapons;
using KillRitual.Weapons.Visual;

namespace KillRitual.Player.Combat
{
    [DisallowMultipleComponent]
    public sealed class KRCombatSystem : MonoBehaviour, IDamageable
    {
        private const int kElementCount = 5;

        [Header("스탯 관리자")]
        [Tooltip("체력/공격배율/공격속도배율을 관리하는 KRPlayerStats 컴포넌트. 비워두면 Awake 시 " +
                 "같은 오브젝트(또는 부모 계층)에서 자동으로 찾습니다. 그래도 못 찾으면 안전한 기본값으로 동작합니다.")]
        [SerializeField] private KRPlayerStats _playerStats;

        [Header("무기 홀더 (인스펙터에서 각 속성의 유형I/유형II 무기 GameObject를 드래그하세요)")]
        [Tooltip("길이 5. [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금) 순서로, " +
                 "각 속성의 \"유형I(좌클릭)\" 무기 컴포넌트를 배치합니다.")]
        [SerializeField] private KRWeaponBase[] _typeOneWeapons = new KRWeaponBase[kElementCount];

        [Tooltip("길이 5, 순서는 위와 동일. 각 속성의 \"유형II(우클릭)\" 무기 컴포넌트를 배치합니다. " +
                 "특정 속성에 유형II가 없다면 해당 인덱스를 비워두세요(우클릭이 안전하게 무시됩니다).")]
        [SerializeField] private KRWeaponBase[] _typeTwoWeapons = new KRWeaponBase[kElementCount];

        [Header("무기 시각 루트")]
        [Tooltip("길이 5. [0]=FireHand [1]=WaterHand [2]=WoodHand [3]=EarthHand/DirtHand [4]=MetalHand 순서로 손/무기 루트 GameObject를 배치합니다.")]
        [SerializeField] private GameObject[] _weaponVisualRoots = new GameObject[kElementCount];

        [Tooltip("무기 전환 시 새로 켜진 손을 Equip 상태 처음부터 강제로 재생합니다. 퀵스왑 구조에서는 켜는 편이 안전합니다.")]
        [SerializeField] private bool _playEquipOnSwitch = true;

        [Header("무기 전환 잠금")]
        [Tooltip("true면 숫자키 스팸 방지용으로 짧은 시간 동안 추가 전환을 막습니다. 공격 모션 전체를 막는 용도가 아닙니다.")]
        [SerializeField] private bool _lockWeaponSwitchDuringEquip = true;

        [Tooltip("퀵스왑 후 추가 전환을 잠깐 막는 안전 시간입니다. 길게 잡으면 퀵스왑 감각이 둔해집니다.")]
        [Min(0.01f)]
        [SerializeField] private float _weaponSwitchLockFallbackSeconds = 0.1f;

        [Header("퀵스왑 입력 정리")]
        [Tooltip("무기 전환 당시 눌려 있던 마우스 버튼은 손을 뗄 때까지 새 무기에 전달하지 않습니다. Equip이 공격 모션으로 덮이는 문제를 막습니다.")]
        [SerializeField] private bool _suppressHeldFireInputAfterSwitch = true;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("기본 FirePoint입니다. 속성/공격 유형별 FirePoint가 비어 있으면 이것을 사용합니다.")]
        [SerializeField] private Transform _firePoint;

        [Header("속성 / 공격 유형별 FirePoint")]
        [Tooltip("길이 5. [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금). 각 속성의 유형I(좌클릭) 발사 위치입니다.")]
        [SerializeField] private Transform[] _typeOneFirePoints = new Transform[kElementCount];

        [Tooltip("길이 5. [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금). 각 속성의 유형II(우클릭) 발사 위치입니다.")]
        [SerializeField] private Transform[] _typeTwoFirePoints = new Transform[kElementCount];

        [SerializeField] private LayerMask _damageableLayerMask = ~0;

        [Tooltip("광역 폭발 판정 전용 마스크. Damageable 레이어만 포함하고 Environment(벽/바닥)는 제외합니다. " +
                 "브로드페이즈 후보 수를 줄여 폭발 판정 내로우페이즈 비용을 감소시킵니다.")]
        [SerializeField] private LayerMask _explosionLayerMask = ~0;

        [Header("공용 자원 지갑")]
        [Tooltip("속성별 자원 주머니의 최대치. 길이 5, [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금) 순서. " +
                 "속성마다 다른 최대 탄약량을 줄 수 있습니다(예: 금(金) BFG는 적게, 목(木) 정밀소총은 많게).")]
        [SerializeField] private float[] _maxResourcePerElement = new float[] { 100f, 100f, 100f, 100f, 100f };

        [Tooltip("처형 보상(Absorption) 연동 여부.")]
        [SerializeField] private bool _subscribeToExecutionRewards = true;

        private static readonly KeyCode[] _weaponKeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5
        };

        private static readonly KRDamageType[] _allElements =
        {
            KRDamageType.Fire,
            KRDamageType.Water,
            KRDamageType.Wood,
            KRDamageType.Earth,
            KRDamageType.Metal
        };

        private static readonly RaycastHit[] _aimRaycastBuffer = new RaycastHit[8];

        private KRResourceWallet _resourceWallet;
        private KRDamageType _currentElement = KRDamageType.Fire;

        private Transform _activeFirePoint;

        private bool _isWeaponSwitchLocked;
        private float _weaponSwitchUnlockTime;

        private bool _suppressMouse0UntilRelease;
        private bool _suppressMouse1UntilRelease;

        public Transform FirePoint
        {
            get
            {
                if (_activeFirePoint != null) return _activeFirePoint;
                if (_firePoint != null) return _firePoint;
                if (_playerCamera != null) return _playerCamera.transform;
                return transform;
            }
        }

        public Camera PlayerCamera => _playerCamera;
        public LayerMask HitscanLayerMask => _damageableLayerMask;
        public LayerMask ExplosionLayerMask => _explosionLayerMask;
        public IDamageable Owner => this;
        public KRDamageType CurrentElement => _currentElement;
        public float AttackMultiplier => _playerStats != null ? _playerStats.AttackMultiplier : 1f;
        public float AttackSpeedMultiplier => _playerStats != null ? _playerStats.AttackSpeedMultiplier : 1f;
        public bool IsDead => _playerStats != null && _playerStats.IsDead;
        public bool IsGroggy => false;
        public Vector3 Position => transform.position;
        /// <summary>처형 가능한 대상이 근처에 있는지 여부. KRAbsorptionSystem에 위임합니다.</summary>
        public bool HasExecutableTargetNearby
        {
            get
            {
                var absorption = GetComponent<KRAbsorptionSystem>();
                return absorption != null && absorption.HasExecutableTarget;
            }
        }

        public Vector3 GetAimDirection(Vector3 muzzleOrigin, float maxRange)
        {
            if (_playerCamera == null)
            {
                Transform firePoint = FirePoint;
                return firePoint != null ? firePoint.forward : Vector3.forward;
            }

            Vector3 camPos = _playerCamera.transform.position;
            Vector3 camForward = _playerCamera.transform.forward;

            int hitCount = Physics.RaycastNonAlloc(
                camPos, camForward, _aimRaycastBuffer, maxRange, _damageableLayerMask);

            int closestIndex = FindClosestAimHitIndex(hitCount);

            Vector3 aimPoint = closestIndex >= 0
                ? _aimRaycastBuffer[closestIndex].point
                : camPos + (camForward * maxRange);

            Vector3 direction = aimPoint - muzzleOrigin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : camForward;
        }

        private static int FindClosestAimHitIndex(int hitCount)
        {
            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (_aimRaycastBuffer[i].distance < closestDistance)
                {
                    closestDistance = _aimRaycastBuffer[i].distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        public bool TryConsumeResource(KRDamageType element, float amount)
        {
            return _resourceWallet != null && _resourceWallet.TryConsume(element, amount);
        }

        public void RefillResource(KRDamageType element, float amount)
        {
            _resourceWallet?.Refill(element, amount);
        }

        public float GetResourceRatio(KRDamageType element)
        {
            if (_resourceWallet == null) return 0f;
            float max = _resourceWallet.GetMax(element);
            return max > 0f ? _resourceWallet.Get(element) / max : 0f;
        }

        public float GetResourceAmount(KRDamageType element)
        {
            return _resourceWallet != null ? _resourceWallet.Get(element) : 0f;
        }

        public float GetMaxResourceAmount(KRDamageType element)
        {
            return _resourceWallet != null ? _resourceWallet.GetMax(element) : 0f;
        }

        private void Awake()
        {
            if (_playerStats == null)
                _playerStats = GetComponentInParent<KRPlayerStats>();

            _resourceWallet = new KRResourceWallet(_maxResourcePerElement);

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_firePoint == null)
                _firePoint = _playerCamera != null ? _playerCamera.transform : transform;

            _currentElement = KRDamageType.Fire;
            _activeFirePoint = ResolveFirePoint(mouseButton: 0, _currentElement);

            _isWeaponSwitchLocked = false;
            _weaponSwitchUnlockTime = 0f;
            _suppressMouse0UntilRelease = false;
            _suppressMouse1UntilRelease = false;

            ApplyWeaponVisualRootState(_currentElement);

            KRWeaponVisual initialVisual = GetWeaponVisual(_currentElement);
            if (_playEquipOnSwitch)
                initialVisual?.PlayEquipImmediately();
        }

        private void OnEnable()
        {
            if (_subscribeToExecutionRewards)
                KRManagers.Event.Subscribe<KRExecutionSuccessEvent>(OnExecutionSuccess);
        }

        private void OnDisable()
        {
            if (_subscribeToExecutionRewards)
                KRManagers.Event.Unsubscribe<KRExecutionSuccessEvent>(OnExecutionSuccess);
        }

        private void Update()
        {
            UpdateWeaponSwitchLockFallback();
            HandleFireButton(mouseButton: 0, weaponArray: _typeOneWeapons);
            HandleFireButton(mouseButton: 1, weaponArray: _typeTwoWeapons);
            HandleWeaponSelectionInput();
        }

        private void HandleWeaponSelectionInput()
        {
            for (int i = 0; i < _weaponKeys.Length; i++)
            {
                if (!Input.GetKeyDown(_weaponKeys[i])) continue;

                var newElement = (KRDamageType)i;
                if (newElement == _currentElement) continue;
                if (IsWeaponSwitchLocked()) continue;

                SwitchElement(newElement);
            }
        }

        private void SwitchElement(KRDamageType newElement)
        {
            KRDamageType previousElement = _currentElement;

            GetWeapon(_typeOneWeapons, previousElement)?.NotifyCancelled();
            GetWeapon(_typeTwoWeapons, previousElement)?.NotifyCancelled();

            KRWeaponVisual previousVisual = GetWeaponVisual(previousElement);
            previousVisual?.PlayIdleImmediately();

            if (_suppressHeldFireInputAfterSwitch)
            {
                _suppressMouse0UntilRelease = Input.GetMouseButton(0);
                _suppressMouse1UntilRelease = Input.GetMouseButton(1);
            }

            _currentElement = newElement;
            _activeFirePoint = ResolveFirePoint(mouseButton: 0, _currentElement);

            ApplyWeaponVisualRootState(_currentElement);

            if (_playEquipOnSwitch)
                GetWeaponVisual(_currentElement)?.PlayEquipImmediately();
            else
                GetWeaponVisual(_currentElement)?.ClearAllTriggers();

            BeginWeaponSwitchLock();
        }

        private void BeginWeaponSwitchLock()
        {
            if (!_lockWeaponSwitchDuringEquip) return;
            _isWeaponSwitchLocked = true;
            _weaponSwitchUnlockTime = Time.time + _weaponSwitchLockFallbackSeconds;
        }

        private bool IsWeaponSwitchLocked()
        {
            if (!_lockWeaponSwitchDuringEquip) return false;
            if (!_isWeaponSwitchLocked) return false;
            if (Time.time >= _weaponSwitchUnlockTime)
            {
                _isWeaponSwitchLocked = false;
                return false;
            }
            return true;
        }

        private void UpdateWeaponSwitchLockFallback()
        {
            if (_isWeaponSwitchLocked && Time.time >= _weaponSwitchUnlockTime)
                _isWeaponSwitchLocked = false;
        }

        public void UnlockWeaponSwitch() => _isWeaponSwitchLocked = false;

        /// <summary>
        /// [2026-07-06 추가] 현재 장착 중인 원소 무기의 시각 오브젝트(_weaponVisualRoots)만 강제로
        /// 켜고 끕니다. 흡혼(KRAbsorptionSystem)처럼 맨손 처형 애니메이션을 쓰는 처형기가 실행되는
        /// 동안, 손에 들고 있던 무기 모델이 화면에 그대로 겹쳐 보이지 않도록 하기 위한 용도입니다.
        /// 무기 전환(SwitchElement) 로직과는 무관하며, _currentElement 값 자체는 바뀌지 않습니다.
        /// </summary>
        public void SetCurrentWeaponVisualActive(bool active)
        {
            if (_weaponVisualRoots == null) return;

            int idx = (int)_currentElement;
            if (idx < 0 || idx >= _weaponVisualRoots.Length) return;

            GameObject root = _weaponVisualRoots[idx];
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }

        private void ApplyWeaponVisualRootState(KRDamageType activeElement)
        {
            if (_weaponVisualRoots == null) return;
            int activeIndex = (int)activeElement;

            for (int i = 0; i < _weaponVisualRoots.Length; i++)
            {
                GameObject root = _weaponVisualRoots[i];
                if (root == null) continue;
                bool shouldBeActive = i == activeIndex;
                if (root.activeSelf != shouldBeActive)
                    root.SetActive(shouldBeActive);
            }
        }

        private KRWeaponVisual GetWeaponVisual(KRDamageType element)
        {
            int idx = (int)element;
            if (_weaponVisualRoots == null || idx < 0 || idx >= _weaponVisualRoots.Length) return null;
            GameObject root = _weaponVisualRoots[idx];
            if (root == null) return null;
            return root.GetComponentInChildren<KRWeaponVisual>(true);
        }

        private void HandleFireButton(int mouseButton, KRWeaponBase[] weaponArray)
        {
            KRWeaponBase weapon = GetWeapon(weaponArray, _currentElement);

            if (weapon == null)
            {
                ClearSuppressionIfReleased(mouseButton);
                return;
            }

            _activeFirePoint = ResolveFirePoint(mouseButton, _currentElement);

            if (IsFireInputSuppressed(mouseButton))
            {
                weapon.NotifyReleased();
                return;
            }

            int otherButton = mouseButton == 0 ? 1 : 0;
            if (Input.GetMouseButton(otherButton))
            {
                weapon.NotifyReleased();
                return;
            }

            if (Input.GetMouseButton(mouseButton))
                weapon.NotifyHeld();
            else
                weapon.NotifyReleased();
        }

        private Transform ResolveFirePoint(int mouseButton, KRDamageType element)
        {
            Transform[] firePointArray = mouseButton == 0 ? _typeOneFirePoints : _typeTwoFirePoints;
            int idx = (int)element;

            if (firePointArray != null && idx >= 0 && idx < firePointArray.Length)
            {
                Transform specificFirePoint = firePointArray[idx];
                if (specificFirePoint != null) return specificFirePoint;
            }

            if (_firePoint != null) return _firePoint;
            if (_playerCamera != null) return _playerCamera.transform;
            return transform;
        }

        private bool IsFireInputSuppressed(int mouseButton)
        {
            if (!_suppressHeldFireInputAfterSwitch) return false;

            if (mouseButton == 0)
            {
                if (!_suppressMouse0UntilRelease) return false;
                if (!Input.GetMouseButton(0)) { _suppressMouse0UntilRelease = false; return false; }
                return true;
            }

            if (mouseButton == 1)
            {
                if (!_suppressMouse1UntilRelease) return false;
                if (!Input.GetMouseButton(1)) { _suppressMouse1UntilRelease = false; return false; }
                return true;
            }

            return false;
        }

        private void ClearSuppressionIfReleased(int mouseButton)
        {
            if (mouseButton == 0 && !Input.GetMouseButton(0)) _suppressMouse0UntilRelease = false;
            if (mouseButton == 1 && !Input.GetMouseButton(1)) _suppressMouse1UntilRelease = false;
        }

        private static KRWeaponBase GetWeapon(KRWeaponBase[] array, KRDamageType element)
        {
            int idx = (int)element;
            return array != null && idx >= 0 && idx < array.Length ? array[idx] : null;
        }

        private void OnExecutionSuccess(KRExecutionSuccessEvent evt)
        {
            _playerStats?.HealByPercent(evt.RecoverHealthAmount);
            if (_resourceWallet == null) return;
            for (int i = 0; i < _allElements.Length; i++)
                _resourceWallet.Refill(_allElements[i], evt.RecoverAmmoAmount);
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead) return;
            _playerStats?.ApplyDamage(context.DamageAmount);
        }

        public void Execute(KillRitual.Core.Interfaces.ExecutionSource source
            = KillRitual.Core.Interfaces.ExecutionSource.Default)
            => _playerStats?.Kill();

        private sealed class KRResourceWallet
        {
            private readonly float[] _pool = new float[kElementCount];
            private readonly float[] _maxPerElement = new float[kElementCount];

            public KRResourceWallet(float[] maxPerElement)
            {
                for (int i = 0; i < kElementCount; i++)
                {
                    float max = (maxPerElement != null && i < maxPerElement.Length)
                        ? maxPerElement[i] : 100f;
                    _maxPerElement[i] = max;
                    _pool[i] = max;
                }
            }

            public float Get(KRDamageType element) => _pool[(int)element];
            public float GetMax(KRDamageType element) => _maxPerElement[(int)element];

            public bool TryConsume(KRDamageType element, float amount)
            {
                int idx = (int)element;
                if (_pool[idx] < amount) return false;
                _pool[idx] -= amount;
                return true;
            }

            public void Refill(KRDamageType element, float amount)
            {
                int idx = (int)element;
                _pool[idx] = Mathf.Min(_maxPerElement[idx], _pool[idx] + amount);
            }
        }
    }
}