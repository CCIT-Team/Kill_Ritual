// Assets/Project/Scripts/05_UI/HUD/KRWeaponWheelUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillRitual.Core.Damage;
using KillRitual.Player.Combat;

namespace KillRitual.UI.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class KRWeaponWheelUI : MonoBehaviour
    {
        private const int kSlotCount = 5;

        [Serializable]
        private sealed class WeaponWheelSlot
        {
            [Header("Data")]
            [Tooltip("표시용 속성입니다. 런타임에는 CombatSystem의 _elementSlots 기준으로 동기화됩니다.")]
            public KRDamageType element;

            public string displayName;
            public Sprite icon;

            [Header("Existing UI References")]
            [Tooltip("슬롯 루트. 위치 보정용이 아니라 선택 시 스케일 효과만 적용할 수 있습니다.")]
            public RectTransform slotRoot;

            [Tooltip("이미 포토샵에서 만든 5분할 슬라이스 이미지. 이 코드는 회전/FillAmount를 건드리지 않습니다.")]
            public Image wedgeImage;

            public Image iconImage;
            public TMP_Text ammoText;

            [Tooltip("선택됐을 때 켜지는 하이라이트 오브젝트. 없으면 비워둬도 됩니다.")]
            public GameObject selectedRoot;

            [Tooltip("잠긴 슬롯일 때 켜지는 자물쇠/잠금 표시 오브젝트. 없으면 비워둬도 됩니다.")]
            public GameObject lockedRoot;

            [HideInInspector] public Vector3 originalScale;
            [HideInInspector] public Color originalWedgeColor;
            [HideInInspector] public Color originalIconColor;
            [HideInInspector] public Color originalAmmoTextColor;
            [HideInInspector] public bool hasWedgeImage;
            [HideInInspector] public bool hasIconImage;
            [HideInInspector] public bool hasAmmoText;
        }

        [Header("Input")]
        [SerializeField] private KeyCode _openKey = KeyCode.Q;

        [Header("References")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Tooltip("비워두면 이 스크립트가 붙은 WeaponWheel RectTransform을 사용합니다.")]
        [SerializeField] private RectTransform _wheelRoot;

        [Tooltip("비워두면 이 스크립트가 붙은 WeaponWheel의 CanvasGroup을 사용합니다.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("중앙에 현재 선택 무기 이름을 표시하는 텍스트. 스크린샷의 WeaponName을 여기에 연결하면 됩니다.")]
        [SerializeField] private TMP_Text _selectedWeaponNameText;

        [Header("Slots - CombatSystem 슬롯 순서")]
        [Tooltip("길이 5. CombatSystem의 _elementSlots와 같은 슬롯 순서입니다. [0]=1번, [1]=2번, [2]=3번, [3]=4번, [4]=5번.")]
        [SerializeField]
        private WeaponWheelSlot[] _slots = new WeaponWheelSlot[kSlotCount]
        {
            new WeaponWheelSlot { element = KRDamageType.Fire,  displayName = "화" },
            new WeaponWheelSlot { element = KRDamageType.Wood,  displayName = "목" },
            new WeaponWheelSlot { element = KRDamageType.Water, displayName = "수" },
            new WeaponWheelSlot { element = KRDamageType.Earth, displayName = "토" },
            new WeaponWheelSlot { element = KRDamageType.Metal, displayName = "금" }
        };

        [Header("Selection")]
        [Tooltip("화면 중앙 기준 이 거리 안쪽에서는 선택을 바꾸지 않습니다.")]
        [Min(0f)]
        [SerializeField] private float _deadZonePixels = 70f;

        [Tooltip("선택된 슬롯 루트를 약간 키울지 여부. UI가 흔들리면 꺼도 됩니다.")]
        [SerializeField] private bool _scaleSelectedSlot = false;

        [Min(1f)]
        [SerializeField] private float _selectedScale = 1.08f;

        [Tooltip("선택된 wedgeImage 색상을 바꿀지 여부.")]
        [SerializeField] private bool _tintSelectedWedge = true;

        [SerializeField] private Color _selectedWedgeColor = new Color(1f, 1f, 1f, 0.65f);

        [Header("Locked Slot Visual")]
        [SerializeField] private bool _dimLockedSlots = true;

        [Range(0f, 1f)]
        [SerializeField] private float _lockedAlpha = 0.25f;

        [SerializeField] private Color _lockedWedgeColor = new Color(0.15f, 0.15f, 0.15f, 0.55f);

        [SerializeField] private string _lockedAmmoText = "-";

        [SerializeField] private string _lockedCenterText = "잠김";

        [Header("Time Slow / Stop")]
        [Range(0f, 1f)]
        [SerializeField] private float _wheelTimeScale = 0.03f;

        [Min(0.001f)]
        [SerializeField] private float _timeScaleSmoothTime = 0.08f;

        [SerializeField] private bool _snapToFullStopWhenTargetIsZero = true;

        [Header("Fade")]
        [Min(0.001f)]
        [SerializeField] private float _fadeSpeed = 16f;

        [Header("Cursor")]
        [SerializeField] private bool _unlockCursorWhileOpen = true;
        [SerializeField] private bool _showCursorWhileOpen = true;

        private bool _isOpen;
        private bool _hasTimeScaleOwnership;

        private int _selectedIndex = -1;
        private int _lastValidSelectedIndex = -1;

        private float _previousTimeScale = 1f;
        private float _previousFixedDeltaTime = 0.02f;
        private float _timeScaleVelocity;

        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;

        private void Awake()
        {
            CacheReferences();
            NormalizeSlots();
            SyncSlotsFromCombat();
            CacheOriginalSlotVisuals();

            _selectedIndex = ResolveInitialSelectedIndex();
            _lastValidSelectedIndex = _selectedIndex;

            ApplyImmediateHiddenState();
            RefreshAllSlots();
            RefreshSelectionVisuals();
        }

        private void OnDisable()
        {
            ForceCloseAndRestore();
        }

        private void Update()
        {
            if (_combatSystem == null)
                CacheReferences();

            SyncSlotsFromCombat();

            HandleOpenCloseInput();

            if (_isOpen)
            {
                UpdateSelectionFromMouse();
                RefreshAllSlots();
            }

            UpdateCanvasFade();
            UpdateTimeScale();
        }

        private void CacheReferences()
        {
            if (_wheelRoot == null)
                _wheelRoot = transform as RectTransform;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_combatSystem == null)
                _combatSystem = FindObjectOfType<KRCombatSystem>();
        }

        private void NormalizeSlots()
        {
            if (_slots == null || _slots.Length != kSlotCount)
                Array.Resize(ref _slots, kSlotCount);

            for (int i = 0; i < kSlotCount; i++)
            {
                if (_slots[i] == null)
                    _slots[i] = new WeaponWheelSlot();

                if (string.IsNullOrWhiteSpace(_slots[i].displayName))
                    _slots[i].displayName = $"Slot {i + 1}";
            }
        }

        private void SyncSlotsFromCombat()
        {
            if (_combatSystem == null || _slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;

                KRDamageType element = _combatSystem.GetElementBySlotIndex(i);
                _slots[i].element = element;

                if (string.IsNullOrWhiteSpace(_slots[i].displayName) ||
                    _slots[i].displayName.StartsWith("Slot ", StringComparison.Ordinal))
                {
                    _slots[i].displayName = GetDefaultElementDisplayName(element);
                }
            }
        }

        private static string GetDefaultElementDisplayName(KRDamageType element)
        {
            switch (element)
            {
                case KRDamageType.Fire:
                    return "화";
                case KRDamageType.Water:
                    return "수";
                case KRDamageType.Wood:
                    return "목";
                case KRDamageType.Earth:
                    return "토";
                case KRDamageType.Metal:
                    return "금";
                default:
                    return element.ToString();
            }
        }

        private void CacheOriginalSlotVisuals()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                WeaponWheelSlot slot = _slots[i];
                if (slot == null) continue;

                slot.originalScale = slot.slotRoot != null
                    ? slot.slotRoot.localScale
                    : Vector3.one;

                slot.hasWedgeImage = slot.wedgeImage != null;
                slot.hasIconImage = slot.iconImage != null;
                slot.hasAmmoText = slot.ammoText != null;

                if (slot.wedgeImage != null)
                    slot.originalWedgeColor = slot.wedgeImage.color;

                if (slot.iconImage != null)
                    slot.originalIconColor = slot.iconImage.color;

                if (slot.ammoText != null)
                    slot.originalAmmoTextColor = slot.ammoText.color;
            }
        }

        private void HandleOpenCloseInput()
        {
            if (Input.GetKeyDown(_openKey))
                OpenWheel();

            if (Input.GetKeyUp(_openKey))
                CloseWheel(applySelection: true);
        }

        private void OpenWheel()
        {
            if (_isOpen) return;

            _isOpen = true;

            if (_combatSystem != null)
            {
                int currentSlot = _combatSystem.CurrentSlotIndex;

                _selectedIndex = IsSlotSelectable(currentSlot)
                    ? currentSlot
                    : FindFirstSelectableSlot();

                _lastValidSelectedIndex = _selectedIndex;
                _combatSystem.SetWeaponWheelOpen(true);
            }

            BeginTimeControl();
            BeginCursorControl();

            RefreshAllSlots();
            RefreshSelectionVisuals();
        }

        private void CloseWheel(bool applySelection)
        {
            if (!_isOpen) return;

            if (applySelection)
                ApplySelectedWeapon();

            _isOpen = false;

            if (_combatSystem != null)
                _combatSystem.SetWeaponWheelOpen(false);

            EndCursorControl();
            EndTimeControl();
        }

        private void ApplySelectedWeapon()
        {
            if (_combatSystem == null) return;
            if (!IsSlotSelectable(_selectedIndex)) return;

            _combatSystem.TrySwitchSlot(_selectedIndex, ignoreSwitchLock: true);
        }

        private void UpdateSelectionFromMouse()
        {
            int newIndex = ResolveSlotIndexFromMousePosition();

            if (newIndex < 0)
                newIndex = _lastValidSelectedIndex;

            if (!IsSlotSelectable(newIndex))
                newIndex = _lastValidSelectedIndex;

            if (!IsSlotSelectable(newIndex))
                newIndex = FindFirstSelectableSlot();

            if (!IsSlotSelectable(newIndex))
                return;

            if (_selectedIndex == newIndex)
                return;

            _selectedIndex = newIndex;
            _lastValidSelectedIndex = newIndex;

            RefreshSelectionVisuals();
        }

        private int ResolveSlotIndexFromMousePosition()
        {
            Vector2 center = GetWheelScreenCenter();
            Vector2 mouse = Input.mousePosition;
            Vector2 direction = mouse - center;

            if (direction.sqrMagnitude < _deadZonePixels * _deadZonePixels)
                return -1;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float sectorAngle = 360f / kSlotCount;

            // 위쪽을 0번 슬롯으로 보고 시계 방향으로 5분할.
            // 슬롯 의미는 CombatSystem의 _elementSlots 순서를 따릅니다.
            float normalized = 90f - angle + sectorAngle * 0.5f;
            normalized = NormalizeAngle360(normalized);

            int index = Mathf.FloorToInt(normalized / sectorAngle);
            return Mathf.Clamp(index, 0, kSlotCount - 1);
        }

        private Vector2 GetWheelScreenCenter()
        {
            if (_wheelRoot != null)
                return RectTransformUtility.WorldToScreenPoint(null, _wheelRoot.position);

            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static float NormalizeAngle360(float angle)
        {
            angle %= 360f;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        private void RefreshAllSlots()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                WeaponWheelSlot slot = _slots[i];
                if (slot == null) continue;

                bool unlocked = IsSlotSelectable(i);

                if (slot.iconImage != null)
                {
                    slot.iconImage.sprite = slot.icon;
                    slot.iconImage.enabled = slot.icon != null;
                }

                if (slot.ammoText != null)
                {
                    if (unlocked && _combatSystem != null)
                    {
                        float ammo = _combatSystem.GetResourceAmountBySlot(i);
                        slot.ammoText.text = Mathf.FloorToInt(ammo).ToString();
                    }
                    else
                    {
                        slot.ammoText.text = _lockedAmmoText;
                    }
                }

                if (slot.lockedRoot != null)
                    slot.lockedRoot.SetActive(!unlocked);
            }

            RefreshSelectionVisuals();
        }

        private void RefreshSelectionVisuals()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                WeaponWheelSlot slot = _slots[i];
                if (slot == null) continue;

                bool unlocked = IsSlotSelectable(i);
                bool selected = unlocked && i == _selectedIndex;

                if (slot.selectedRoot != null)
                    slot.selectedRoot.SetActive(selected);

                if (_scaleSelectedSlot && slot.slotRoot != null)
                {
                    slot.slotRoot.localScale = selected
                        ? slot.originalScale * _selectedScale
                        : slot.originalScale;
                }

                if (slot.wedgeImage != null)
                {
                    if (!unlocked && _dimLockedSlots)
                    {
                        slot.wedgeImage.color = _lockedWedgeColor;
                    }
                    else if (_tintSelectedWedge && selected)
                    {
                        slot.wedgeImage.color = _selectedWedgeColor;
                    }
                    else
                    {
                        slot.wedgeImage.color = slot.originalWedgeColor;
                    }
                }

                if (slot.iconImage != null)
                {
                    slot.iconImage.color = !unlocked && _dimLockedSlots
                        ? WithAlpha(slot.originalIconColor, slot.originalIconColor.a * _lockedAlpha)
                        : slot.originalIconColor;
                }

                if (slot.ammoText != null)
                {
                    slot.ammoText.color = !unlocked && _dimLockedSlots
                        ? WithAlpha(slot.originalAmmoTextColor, slot.originalAmmoTextColor.a * _lockedAlpha)
                        : slot.originalAmmoTextColor;
                }
            }

            RefreshCenterText();
        }

        private void RefreshCenterText()
        {
            if (_selectedWeaponNameText == null) return;

            if (_selectedIndex < 0 || _selectedIndex >= _slots.Length || _slots[_selectedIndex] == null)
            {
                _selectedWeaponNameText.text = _lockedCenterText;
                return;
            }

            if (!IsSlotSelectable(_selectedIndex))
            {
                _selectedWeaponNameText.text = _lockedCenterText;
                return;
            }

            _selectedWeaponNameText.text = _slots[_selectedIndex].displayName;
        }

        private bool IsSlotSelectable(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= kSlotCount)
                return false;

            if (_combatSystem == null)
                return true;

            return _combatSystem.IsSlotUnlocked(slotIndex);
        }

        private int FindFirstSelectableSlot()
        {
            for (int i = 0; i < kSlotCount; i++)
            {
                if (IsSlotSelectable(i))
                    return i;
            }

            return -1;
        }

        private int ResolveInitialSelectedIndex()
        {
            if (_combatSystem != null)
            {
                int currentSlot = _combatSystem.CurrentSlotIndex;
                if (IsSlotSelectable(currentSlot))
                    return currentSlot;
            }

            return FindFirstSelectableSlot();
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private void BeginTimeControl()
        {
            if (_hasTimeScaleOwnership) return;

            _hasTimeScaleOwnership = true;
            _previousTimeScale = Time.timeScale;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            _timeScaleVelocity = 0f;
        }

        private void EndTimeControl()
        {
            _timeScaleVelocity = 0f;
        }

        private void UpdateTimeScale()
        {
            if (!_hasTimeScaleOwnership) return;

            float target = _isOpen ? _wheelTimeScale : _previousTimeScale;

            float newTimeScale = Mathf.SmoothDamp(
                Time.timeScale,
                target,
                ref _timeScaleVelocity,
                _timeScaleSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (_isOpen &&
                _snapToFullStopWhenTargetIsZero &&
                _wheelTimeScale <= 0.0001f &&
                newTimeScale <= 0.005f)
            {
                newTimeScale = 0f;
            }

            if (!_isOpen && Mathf.Abs(newTimeScale - _previousTimeScale) <= 0.001f)
            {
                Time.timeScale = _previousTimeScale;
                Time.fixedDeltaTime = _previousFixedDeltaTime;
                _hasTimeScaleOwnership = false;
                return;
            }

            Time.timeScale = Mathf.Clamp(newTimeScale, 0f, 1f);

            float fixedScale = Mathf.Max(Time.timeScale, 0.0001f);
            Time.fixedDeltaTime = _previousFixedDeltaTime * fixedScale;
        }

        private void BeginCursorControl()
        {
            if (!_unlockCursorWhileOpen && !_showCursorWhileOpen) return;

            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            if (_unlockCursorWhileOpen)
                Cursor.lockState = CursorLockMode.None;

            if (_showCursorWhileOpen)
                Cursor.visible = true;
        }

        private void EndCursorControl()
        {
            if (!_unlockCursorWhileOpen && !_showCursorWhileOpen) return;

            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;
        }

        private void UpdateCanvasFade()
        {
            if (_canvasGroup == null) return;

            float targetAlpha = _isOpen ? 1f : 0f;
            float step = _fadeSpeed * Time.unscaledDeltaTime;

            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, step);
            _canvasGroup.blocksRaycasts = _isOpen;
            _canvasGroup.interactable = _isOpen;
        }

        private void ApplyImmediateHiddenState()
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void ForceCloseAndRestore()
        {
            if (_combatSystem != null)
                _combatSystem.SetWeaponWheelOpen(false);

            if (_hasTimeScaleOwnership)
            {
                Time.timeScale = _previousTimeScale;
                Time.fixedDeltaTime = _previousFixedDeltaTime;
                _hasTimeScaleOwnership = false;
            }

            EndCursorControl();
        }
    }
}