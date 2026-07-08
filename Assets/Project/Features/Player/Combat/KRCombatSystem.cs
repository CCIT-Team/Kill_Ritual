// Assets/Project/Scripts/02_Player/Combat/KRCombatSystem.cs
using System;
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
        [Tooltip("체력/공격배율/공격속도배율을 관리하는 KRPlayerStats 컴포넌트. 비워두면 Awake 시 같은 오브젝트 또는 부모 계층에서 자동으로 찾습니다.")]
        [SerializeField] private KRPlayerStats _playerStats;

        [Header("숫자키 슬롯별 속성 매핑")]
        [Tooltip("길이 5. [0]=1번 키, [1]=2번 키, [2]=3번 키, [3]=4번 키, [4]=5번 키에 대응할 속성입니다.")]
        [SerializeField]
        private KRDamageType[] _elementSlots = new KRDamageType[kElementCount]
        {
            KRDamageType.Fire,   // 1번
            KRDamageType.Wood,   // 2번
            KRDamageType.Water,  // 3번
            KRDamageType.Earth,  // 4번
            KRDamageType.Metal   // 5번
        };

        [Header("슬롯 잠금")]
        [Tooltip("길이 5. true면 사용 가능, false면 숫자키/무기휠에서 선택 불가. 현재 4번/5번이 없으면 [3], [4]를 false로 두세요.")]
        [SerializeField]
        private bool[] _slotUnlocked = new bool[kElementCount]
        {
            true,   // 1번
            true,   // 2번
            true,   // 3번
            false,  // 4번
            false   // 5번
        };

        [Tooltip("true면 해당 슬롯의 좌클릭/우클릭 무기가 모두 비어 있을 때 자동으로 잠긴 슬롯처럼 처리합니다.")]
        [SerializeField] private bool _autoLockSlotsWithoutAnyWeapon = true;

        [Header("무기 홀더")]
        [Tooltip("길이 5. _elementSlots와 같은 슬롯 순서입니다. [0]=1번 슬롯, [1]=2번 슬롯, [2]=3번 슬롯, [3]=4번 슬롯, [4]=5번 슬롯.")]
        [SerializeField] private KRWeaponBase[] _typeOneWeapons = new KRWeaponBase[kElementCount];

        [Tooltip("길이 5. _elementSlots와 같은 슬롯 순서입니다. 각 슬롯의 유형II(우클릭) 무기 컴포넌트를 배치합니다.")]
        [SerializeField] private KRWeaponBase[] _typeTwoWeapons = new KRWeaponBase[kElementCount];

        [Header("무기 시각 루트")]
        [Tooltip("길이 5. _elementSlots와 같은 슬롯 순서입니다. 각 슬롯의 손/무기 루트 GameObject를 배치합니다.")]
        [SerializeField] private GameObject[] _weaponVisualRoots = new GameObject[kElementCount];

        [Tooltip("무기 전환 시 새로 켜진 손을 Equip 상태 처음부터 강제로 재생합니다.")]
        [SerializeField] private bool _playEquipOnSwitch = true;

        [Header("무기 전환 잠금")]
        [Tooltip("true면 숫자키 스팸 방지용으로 짧은 시간 동안 추가 전환을 막습니다.")]
        [SerializeField] private bool _lockWeaponSwitchDuringEquip = true;

        [Tooltip("퀵스왑 후 추가 전환을 잠깐 막는 안전 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _weaponSwitchLockFallbackSeconds = 0.1f;

        [Header("퀵스왑 입력 정리")]
        [Tooltip("무기 전환 당시 눌려 있던 마우스 버튼은 손을 뗄 때까지 새 무기에 전달하지 않습니다.")]
        [SerializeField] private bool _suppressHeldFireInputAfterSwitch = true;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("기본 FirePoint입니다. 슬롯/공격 유형별 FirePoint가 비어 있으면 이것을 사용합니다.")]
        [SerializeField] private Transform _firePoint;

        [Header("슬롯 / 공격 유형별 FirePoint")]
        [Tooltip("길이 5. _elementSlots와 같은 슬롯 순서입니다. 각 슬롯의 유형I(좌클릭) 발사 위치입니다.")]
        [SerializeField] private Transform[] _typeOneFirePoints = new Transform[kElementCount];

        [Tooltip("길이 5. _elementSlots와 같은 슬롯 순서입니다. 각 슬롯의 유형II(우클릭) 발사 위치입니다.")]
        [SerializeField] private Transform[] _typeTwoFirePoints = new Transform[kElementCount];

        [SerializeField] private LayerMask _damageableLayerMask = ~0;

        [Tooltip("광역 폭발 판정 전용 마스크. Damageable 레이어만 포함하고 Environment는 제외합니다.")]
        [SerializeField] private LayerMask _explosionLayerMask = ~0;

        [Header("공용 자원 지갑")]
        [Tooltip("길이 5. _elementSlots와 같은 슬롯 순서입니다. [0]=1번 슬롯, [1]=2번 슬롯, [2]=3번 슬롯, [3]=4번 슬롯, [4]=5번 슬롯의 최대 탄약량입니다.")]
        [SerializeField]
        private float[] _maxResourcePerElement = new float[kElementCount]
        {
            100f, 100f, 100f, 100f, 100f
        };

        [Tooltip("처형 보상(Absorption) 연동 여부.")]
        [SerializeField] private bool _subscribeToExecutionRewards = true;

        [Header("디버그")]
        [SerializeField] private bool _debugSlotState;

        private static readonly KeyCode[] _weaponKeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5
        };

        private static readonly RaycastHit[] _aimRaycastBuffer = new RaycastHit[8];

        private KRResourceWallet _resourceWallet;

        private int _currentSlotIndex;
        private KRDamageType _currentElement = KRDamageType.Fire;

        private Transform _activeFirePoint;

        private bool _isWeaponSwitchLocked;
        private float _weaponSwitchUnlockTime;

        private bool _suppressMouse0UntilRelease;
        private bool _suppressMouse1UntilRelease;

        private bool _isWeaponWheelOpen;

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

        public int CurrentSlotIndex => _currentSlotIndex;
        public KRDamageType CurrentElement => _currentElement;

        public float AttackMultiplier => _playerStats != null ? _playerStats.AttackMultiplier : 1f;
        public float AttackSpeedMultiplier => _playerStats != null ? _playerStats.AttackSpeedMultiplier : 1f;
        public bool IsDead => _playerStats != null && _playerStats.IsDead;
        public bool IsGroggy => false;
        public Vector3 Position => transform.position;
        public bool IsWeaponWheelOpen => _isWeaponWheelOpen;

        public bool HasExecutableTargetNearby
        {
            get
            {
                var absorption = GetComponent<KRAbsorptionSystem>();
                return absorption != null && absorption.HasExecutableTarget;
            }
        }

        private void Awake()
        {
            EnsureSlotData();

            if (_playerStats == null)
                _playerStats = GetComponentInParent<KRPlayerStats>();

            _resourceWallet = new KRResourceWallet(_maxResourcePerElement);

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_firePoint == null)
                _firePoint = _playerCamera != null ? _playerCamera.transform : transform;

            int firstUnlockedSlot = FindFirstUnlockedSlotIndex();

            _currentSlotIndex = firstUnlockedSlot >= 0 ? firstUnlockedSlot : 0;
            _currentElement = GetElementBySlotIndex(_currentSlotIndex);
            _activeFirePoint = ResolveFirePoint(mouseButton: 0, slotIndex: _currentSlotIndex);

            _isWeaponSwitchLocked = false;
            _weaponSwitchUnlockTime = 0f;
            _suppressMouse0UntilRelease = false;
            _suppressMouse1UntilRelease = false;
            _isWeaponWheelOpen = false;

            ApplyWeaponVisualRootState(_currentSlotIndex);

            KRWeaponVisual initialVisual = GetWeaponVisualBySlot(_currentSlotIndex);
            if (_playEquipOnSwitch && IsSlotUnlocked(_currentSlotIndex))
                initialVisual?.PlayEquipImmediately();

            DebugCurrentSlotState("Awake");
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

            if (_isWeaponWheelOpen)
            {
                ClearSuppressionIfReleased(0);
                ClearSuppressionIfReleased(1);
                return;
            }

            HandleFireButton(mouseButton: 0, weaponArray: _typeOneWeapons);
            HandleFireButton(mouseButton: 1, weaponArray: _typeTwoWeapons);
            HandleWeaponSelectionInput();
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
                camPos,
                camForward,
                _aimRaycastBuffer,
                maxRange,
                _damageableLayerMask);

            int closestIndex = FindClosestAimHitIndex(hitCount);

            Vector3 aimPoint = closestIndex >= 0
                ? _aimRaycastBuffer[closestIndex].point
                : camPos + camForward * maxRange;

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
            if (_resourceWallet == null) return false;

            int slotIndex = FindSlotIndexByElement(element, requireUnlocked: true);
            if (!IsValidSlotIndex(slotIndex))
            {
                Debug.LogWarning(
                    $"[KRCombatSystem] 자원을 소비하려 했지만 해당 속성이 잠겨 있거나 _elementSlots에 없습니다. Element: {element}",
                    this);

                return false;
            }

            return _resourceWallet.TryConsumeBySlot(slotIndex, amount);
        }

        public bool TryConsumeCurrentResource(float amount)
        {
            if (_resourceWallet == null) return false;
            if (!IsSlotUnlocked(_currentSlotIndex)) return false;

            return _resourceWallet.TryConsumeBySlot(_currentSlotIndex, amount);
        }

        public void RefillResource(KRDamageType element, float amount)
        {
            if (_resourceWallet == null) return;

            int slotIndex = FindSlotIndexByElement(element, requireUnlocked: false);
            if (!IsValidSlotIndex(slotIndex)) return;

            _resourceWallet.RefillBySlot(slotIndex, amount);
        }

        public float GetResourceRatio(KRDamageType element)
        {
            int slotIndex = FindSlotIndexByElement(element, requireUnlocked: false);
            return GetResourceRatioBySlot(slotIndex);
        }

        public float GetResourceAmount(KRDamageType element)
        {
            int slotIndex = FindSlotIndexByElement(element, requireUnlocked: false);
            return GetResourceAmountBySlot(slotIndex);
        }

        public float GetMaxResourceAmount(KRDamageType element)
        {
            int slotIndex = FindSlotIndexByElement(element, requireUnlocked: false);
            return GetMaxResourceAmountBySlot(slotIndex);
        }

        public float GetResourceRatioBySlot(int slotIndex)
        {
            if (_resourceWallet == null) return 0f;
            if (!IsValidSlotIndex(slotIndex)) return 0f;

            float max = _resourceWallet.GetMaxBySlot(slotIndex);
            return max > 0f ? _resourceWallet.GetBySlot(slotIndex) / max : 0f;
        }

        public float GetResourceAmountBySlot(int slotIndex)
        {
            if (_resourceWallet == null) return 0f;
            if (!IsValidSlotIndex(slotIndex)) return 0f;

            return _resourceWallet.GetBySlot(slotIndex);
        }

        public float GetMaxResourceAmountBySlot(int slotIndex)
        {
            if (_resourceWallet == null) return 0f;
            if (!IsValidSlotIndex(slotIndex)) return 0f;

            return _resourceWallet.GetMaxBySlot(slotIndex);
        }

        public bool TrySwitchElement(KRDamageType newElement, bool ignoreSwitchLock = false)
        {
            int slotIndex = FindSlotIndexByElement(newElement, requireUnlocked: true);
            if (!IsValidSlotIndex(slotIndex)) return false;

            return TrySwitchSlot(slotIndex, ignoreSwitchLock);
        }

        public bool TrySwitchSlot(int newSlotIndex, bool ignoreSwitchLock = false)
        {
            if (!IsValidSlotIndex(newSlotIndex)) return false;
            if (!IsSlotUnlocked(newSlotIndex)) return false;
            if (newSlotIndex == _currentSlotIndex) return false;
            if (!ignoreSwitchLock && IsWeaponSwitchLocked()) return false;

            SwitchSlot(newSlotIndex);
            return true;
        }

        public bool IsSlotUnlocked(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex)) return false;

            bool manuallyUnlocked = _slotUnlocked != null &&
                                    slotIndex < _slotUnlocked.Length &&
                                    _slotUnlocked[slotIndex];

            if (!manuallyUnlocked)
                return false;

            if (_autoLockSlotsWithoutAnyWeapon && !HasAnyWeaponAssigned(slotIndex))
                return false;

            return true;
        }

        public bool IsElementUnlocked(KRDamageType element)
        {
            int slotIndex = FindSlotIndexByElement(element, requireUnlocked: true);
            return IsValidSlotIndex(slotIndex);
        }

        public KRDamageType GetElementBySlotIndex(int slotIndex)
        {
            if (_elementSlots == null) return KRDamageType.Fire;
            if (!IsValidSlotIndex(slotIndex)) return KRDamageType.Fire;
            if (slotIndex >= _elementSlots.Length) return KRDamageType.Fire;

            return _elementSlots[slotIndex];
        }

        public int FindSlotIndexOfElement(KRDamageType element)
        {
            return FindSlotIndexByElement(element, requireUnlocked: false);
        }

        public void SetWeaponWheelOpen(bool open)
        {
            if (_isWeaponWheelOpen == open) return;

            _isWeaponWheelOpen = open;

            if (open)
            {
                CancelCurrentWeaponActions();
                return;
            }

            if (_suppressHeldFireInputAfterSwitch)
            {
                _suppressMouse0UntilRelease = Input.GetMouseButton(0);
                _suppressMouse1UntilRelease = Input.GetMouseButton(1);
            }
        }

        private void HandleWeaponSelectionInput()
        {
            for (int i = 0; i < _weaponKeys.Length; i++)
            {
                if (!Input.GetKeyDown(_weaponKeys[i])) continue;

                TrySwitchSlot(i);
            }
        }

        private void SwitchSlot(int newSlotIndex)
        {
            int previousSlotIndex = _currentSlotIndex;

            GetWeaponBySlot(_typeOneWeapons, previousSlotIndex)?.NotifyCancelled();
            GetWeaponBySlot(_typeTwoWeapons, previousSlotIndex)?.NotifyCancelled();

            KRWeaponVisual previousVisual = GetWeaponVisualBySlot(previousSlotIndex);
            previousVisual?.PlayIdleImmediately();

            if (_suppressHeldFireInputAfterSwitch)
            {
                _suppressMouse0UntilRelease = Input.GetMouseButton(0);
                _suppressMouse1UntilRelease = Input.GetMouseButton(1);
            }

            _currentSlotIndex = newSlotIndex;
            _currentElement = GetElementBySlotIndex(_currentSlotIndex);
            _activeFirePoint = ResolveFirePoint(mouseButton: 0, slotIndex: _currentSlotIndex);

            ApplyWeaponVisualRootState(_currentSlotIndex);

            if (_playEquipOnSwitch)
                GetWeaponVisualBySlot(_currentSlotIndex)?.PlayEquipImmediately();
            else
                GetWeaponVisualBySlot(_currentSlotIndex)?.ClearAllTriggers();

            BeginWeaponSwitchLock();

            DebugCurrentSlotState("SwitchSlot");
        }

        private void CancelCurrentWeaponActions()
        {
            GetWeaponBySlot(_typeOneWeapons, _currentSlotIndex)?.NotifyCancelled();
            GetWeaponBySlot(_typeTwoWeapons, _currentSlotIndex)?.NotifyCancelled();
        }

        private void BeginWeaponSwitchLock()
        {
            if (!_lockWeaponSwitchDuringEquip) return;

            _isWeaponSwitchLocked = true;
            _weaponSwitchUnlockTime = Time.unscaledTime + _weaponSwitchLockFallbackSeconds;
        }

        private bool IsWeaponSwitchLocked()
        {
            if (!_lockWeaponSwitchDuringEquip) return false;
            if (!_isWeaponSwitchLocked) return false;

            if (Time.unscaledTime >= _weaponSwitchUnlockTime)
            {
                _isWeaponSwitchLocked = false;
                return false;
            }

            return true;
        }

        private void UpdateWeaponSwitchLockFallback()
        {
            if (_isWeaponSwitchLocked && Time.unscaledTime >= _weaponSwitchUnlockTime)
                _isWeaponSwitchLocked = false;
        }

        public void UnlockWeaponSwitch()
        {
            _isWeaponSwitchLocked = false;
        }

        public void SetCurrentWeaponVisualActive(bool active)
        {
            if (_weaponVisualRoots == null) return;
            if (!IsValidSlotIndex(_currentSlotIndex)) return;
            if (!IsSlotUnlocked(_currentSlotIndex)) return;

            GameObject root = _weaponVisualRoots[_currentSlotIndex];
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }

        private void ApplyWeaponVisualRootState(int activeSlotIndex)
        {
            if (_weaponVisualRoots == null) return;

            for (int i = 0; i < _weaponVisualRoots.Length; i++)
            {
                GameObject root = _weaponVisualRoots[i];
                if (root == null) continue;

                bool shouldBeActive = i == activeSlotIndex && IsSlotUnlocked(i);
                if (root.activeSelf != shouldBeActive)
                    root.SetActive(shouldBeActive);
            }
        }

        private KRWeaponVisual GetWeaponVisualBySlot(int slotIndex)
        {
            if (_weaponVisualRoots == null) return null;
            if (!IsValidSlotIndex(slotIndex)) return null;
            if (slotIndex >= _weaponVisualRoots.Length) return null;

            GameObject root = _weaponVisualRoots[slotIndex];
            if (root == null) return null;

            return root.GetComponentInChildren<KRWeaponVisual>(true);
        }

        private void HandleFireButton(int mouseButton, KRWeaponBase[] weaponArray)
        {
            if (!IsSlotUnlocked(_currentSlotIndex))
            {
                ClearSuppressionIfReleased(mouseButton);
                return;
            }

            KRWeaponBase weapon = GetWeaponBySlot(weaponArray, _currentSlotIndex);

            if (weapon == null)
            {
                ClearSuppressionIfReleased(mouseButton);
                return;
            }

            _activeFirePoint = ResolveFirePoint(mouseButton, _currentSlotIndex);

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

            if (Input.GetMouseButtonDown(mouseButton))
            {
                DebugFireState(mouseButton, weapon);
            }

            if (Input.GetMouseButton(mouseButton))
                weapon.NotifyHeld();
            else
                weapon.NotifyReleased();
        }

        private Transform ResolveFirePoint(int mouseButton, int slotIndex)
        {
            Transform[] firePointArray = mouseButton == 0 ? _typeOneFirePoints : _typeTwoFirePoints;

            if (firePointArray != null && IsValidSlotIndex(slotIndex) && slotIndex < firePointArray.Length)
            {
                Transform specificFirePoint = firePointArray[slotIndex];
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

                if (!Input.GetMouseButton(0))
                {
                    _suppressMouse0UntilRelease = false;
                    return false;
                }

                return true;
            }

            if (mouseButton == 1)
            {
                if (!_suppressMouse1UntilRelease) return false;

                if (!Input.GetMouseButton(1))
                {
                    _suppressMouse1UntilRelease = false;
                    return false;
                }

                return true;
            }

            return false;
        }

        private void ClearSuppressionIfReleased(int mouseButton)
        {
            if (mouseButton == 0 && !Input.GetMouseButton(0))
                _suppressMouse0UntilRelease = false;

            if (mouseButton == 1 && !Input.GetMouseButton(1))
                _suppressMouse1UntilRelease = false;
        }

        private static KRWeaponBase GetWeaponBySlot(KRWeaponBase[] array, int slotIndex)
        {
            if (array == null) return null;
            if (slotIndex < 0 || slotIndex >= array.Length) return null;

            return array[slotIndex];
        }

        private void OnExecutionSuccess(KRExecutionSuccessEvent evt)
        {
            _playerStats?.HealByPercent(evt.RecoverHealthAmount);

            if (_resourceWallet == null) return;

            for (int i = 0; i < kElementCount; i++)
            {
                _resourceWallet.RefillBySlot(i, evt.RecoverAmmoAmount);
            }
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead) return;

            _playerStats?.ApplyDamage(context.DamageAmount);
        }

        public void Execute(KillRitual.Core.Interfaces.ExecutionSource source
            = KillRitual.Core.Interfaces.ExecutionSource.Default)
        {
            _playerStats?.Kill();
        }

        private int FindSlotIndexByElement(KRDamageType element, bool requireUnlocked)
        {
            if (_elementSlots == null) return -1;

            for (int i = 0; i < _elementSlots.Length; i++)
            {
                if (!_elementSlots[i].Equals(element))
                    continue;

                if (requireUnlocked && !IsSlotUnlocked(i))
                    continue;

                return i;
            }

            return -1;
        }

        private int FindFirstUnlockedSlotIndex()
        {
            for (int i = 0; i < kElementCount; i++)
            {
                if (IsSlotUnlocked(i))
                    return i;
            }

            Debug.LogWarning("[KRCombatSystem] 사용 가능한 무기 슬롯이 없습니다. _slotUnlocked 또는 무기 배열을 확인하세요.", this);
            return -1;
        }

        private bool HasAnyWeaponAssigned(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex)) return false;

            KRWeaponBase typeOne = GetWeaponBySlot(_typeOneWeapons, slotIndex);
            KRWeaponBase typeTwo = GetWeaponBySlot(_typeTwoWeapons, slotIndex);

            return typeOne != null || typeTwo != null;
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < kElementCount;
        }

        private static bool IsValidElement(KRDamageType element)
        {
            int idx = (int)element;
            return idx >= 0 && idx < kElementCount;
        }

        private void EnsureSlotData()
        {
            EnsureElementSlots();
            EnsureSlotUnlockedArray();

            EnsureArrayLength(ref _typeOneWeapons, kElementCount);
            EnsureArrayLength(ref _typeTwoWeapons, kElementCount);
            EnsureArrayLength(ref _weaponVisualRoots, kElementCount);
            EnsureArrayLength(ref _typeOneFirePoints, kElementCount);
            EnsureArrayLength(ref _typeTwoFirePoints, kElementCount);
            EnsureFloatArrayLength(ref _maxResourcePerElement, kElementCount, 100f);
        }

        private void EnsureElementSlots()
        {
            if (_elementSlots == null || _elementSlots.Length != kElementCount)
            {
                _elementSlots = new KRDamageType[kElementCount]
                {
                    KRDamageType.Fire,
                    KRDamageType.Wood,
                    KRDamageType.Water,
                    KRDamageType.Earth,
                    KRDamageType.Metal
                };
            }

            for (int i = 0; i < _elementSlots.Length; i++)
            {
                if (!IsValidElement(_elementSlots[i]))
                {
                    _elementSlots[i] = KRDamageType.Fire;
                }
            }
        }

        private void EnsureSlotUnlockedArray()
        {
            if (_slotUnlocked == null || _slotUnlocked.Length != kElementCount)
            {
                _slotUnlocked = new bool[kElementCount]
                {
                    true,
                    true,
                    true,
                    false,
                    false
                };
            }
        }

        private static void EnsureArrayLength<T>(ref T[] array, int length)
        {
            if (array == null)
            {
                array = new T[length];
                return;
            }

            if (array.Length != length)
            {
                Array.Resize(ref array, length);
            }
        }

        private static void EnsureFloatArrayLength(ref float[] array, int length, float defaultValue)
        {
            if (array == null)
            {
                array = new float[length];
                for (int i = 0; i < array.Length; i++)
                    array[i] = defaultValue;

                return;
            }

            int oldLength = array.Length;

            if (oldLength != length)
            {
                Array.Resize(ref array, length);
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] <= 0f)
                    array[i] = defaultValue;
            }
        }

        private void DebugCurrentSlotState(string source)
        {
            if (!_debugSlotState) return;

            Debug.Log(
                $"[KRCombatSystem] {source} | " +
                $"Slot: {_currentSlotIndex + 1}, " +
                $"Unlocked: {IsSlotUnlocked(_currentSlotIndex)}, " +
                $"Element: {_currentElement}, " +
                $"WeaponI: {GetWeaponBySlot(_typeOneWeapons, _currentSlotIndex)?.name}, " +
                $"WeaponII: {GetWeaponBySlot(_typeTwoWeapons, _currentSlotIndex)?.name}, " +
                $"VisualRoot: {(_weaponVisualRoots != null && _currentSlotIndex < _weaponVisualRoots.Length && _weaponVisualRoots[_currentSlotIndex] != null ? _weaponVisualRoots[_currentSlotIndex].name : "None")}",
                this);
        }

        private void DebugFireState(int mouseButton, KRWeaponBase weapon)
        {
            if (!_debugSlotState) return;

            float amount = _resourceWallet != null ? _resourceWallet.GetBySlot(_currentSlotIndex) : 0f;
            float max = _resourceWallet != null ? _resourceWallet.GetMaxBySlot(_currentSlotIndex) : 0f;

            Debug.Log(
                $"[KRCombatSystem] Fire | " +
                $"Button: {mouseButton}, " +
                $"Slot: {_currentSlotIndex + 1}, " +
                $"Unlocked: {IsSlotUnlocked(_currentSlotIndex)}, " +
                $"Element: {_currentElement}, " +
                $"Weapon: {weapon.name}, " +
                $"Ammo: {amount:0.##}/{max:0.##}",
                this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureSlotData();
        }
#endif

        private sealed class KRResourceWallet
        {
            private readonly float[] _pool = new float[kElementCount];
            private readonly float[] _maxBySlot = new float[kElementCount];

            public KRResourceWallet(float[] maxResourceBySlot)
            {
                for (int i = 0; i < kElementCount; i++)
                {
                    float max = maxResourceBySlot != null && i < maxResourceBySlot.Length
                        ? maxResourceBySlot[i]
                        : 100f;

                    if (max <= 0f)
                        max = 100f;

                    _maxBySlot[i] = max;
                    _pool[i] = max;
                }
            }

            public float GetBySlot(int slotIndex)
            {
                if (!IsValidSlotIndex(slotIndex)) return 0f;
                return _pool[slotIndex];
            }

            public float GetMaxBySlot(int slotIndex)
            {
                if (!IsValidSlotIndex(slotIndex)) return 0f;
                return _maxBySlot[slotIndex];
            }

            public bool TryConsumeBySlot(int slotIndex, float amount)
            {
                if (!IsValidSlotIndex(slotIndex)) return false;
                if (amount <= 0f) return true;

                if (_pool[slotIndex] < amount)
                    return false;

                _pool[slotIndex] -= amount;
                return true;
            }

            public void RefillBySlot(int slotIndex, float amount)
            {
                if (!IsValidSlotIndex(slotIndex)) return;
                if (amount <= 0f) return;

                _pool[slotIndex] = Mathf.Min(_maxBySlot[slotIndex], _pool[slotIndex] + amount);
            }
        }
    }
}