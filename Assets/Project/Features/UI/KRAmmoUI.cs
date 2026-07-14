using KillRitual.Core.Damage;
using KillRitual.Player.Combat;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.Player
{
    public sealed class KRAmmoUI : MonoBehaviour
    {
        [System.Serializable]
        private struct ElementVisualSetting
        {
            [Tooltip("이 설정이 적용될 오행 속성입니다. 숫자키 슬롯 번호가 아니라 실제 속성값입니다.")]
            public KRDamageType Element;

            [Tooltip("현재 속성이 이 값일 때 표시할 로고 Sprite입니다.")]
            public Sprite LogoSprite;

            [Tooltip("현재 속성이 이 값일 때 사용할 UI 색입니다. 밑줄과 탄약 텍스트에 적용됩니다.")]
            public Color ElementColor;
        }

        [Header("Combat System 연결")]
        [Tooltip("플레이어의 KRCombatSystem 컴포넌트를 여기에 넣으세요. 비워두면 부모 계층에서 자동으로 찾습니다.")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Header("잔탄 막대 UI 연결")]
        [Tooltip("잔탄에 따라 채워질 막대 Image입니다. Image Type은 'Filled'여야 fillAmount로 조절됩니다.")]
        [SerializeField] private Image _ammoBarFill;

        [Tooltip("현재 잔탄 숫자를 표시할 Text입니다. 예: '73'")]
        [SerializeField] private TextMeshProUGUI _ammoText;

        [Header("속성 UI 연결")]
        [Tooltip("현재 속성에 따라 바뀔 로고 Image입니다.")]
        [SerializeField] private Image _elementLogoImage;

        [Tooltip("현재 속성에 따라 색이 바뀔 밑줄 Image입니다.")]
        [SerializeField] private Image _elementUnderlineImage;

        [Header("속성별 시각 설정")]
        [Tooltip("속성별 로고와 UI 색을 등록합니다. 리스트 순서는 슬롯 순서가 아닙니다. Element 값이 기준입니다.")]
        [SerializeField] private List<ElementVisualSetting> _elementVisualSettings = new List<ElementVisualSetting>();

        [Header("경고 설정")]
        [Tooltip("잔탄 비율이 이 값 이하로 떨어지면 잔탄 막대가 경고색으로 바뀝니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowAmmoWarningRatio = 0.25f;

        [Tooltip("잔탄이 충분할 때 잔탄 막대에 적용할 기본 색입니다. 속성 색이 아니라 막대 기본색입니다.")]
        [SerializeField] private Color _normalAmmoBarColor = Color.white;

        [Tooltip("잔탄이 부족할 때 잔탄 막대에 적용할 경고 색입니다.")]
        [SerializeField] private Color _lowAmmoColor = new Color(1f, 0.3f, 0.3f);

        [Header("디버그")]
        [Tooltip("켜면 현재 UI가 읽고 있는 CurrentElement와 탄약량을 콘솔에 출력합니다.")]
        [SerializeField] private bool _debugCurrentElement;

        private readonly Dictionary<KRDamageType, ElementVisualSetting> _visualSettingMap
            = new Dictionary<KRDamageType, ElementVisualSetting>();

        private KRDamageType _lastElement;
        private bool _hasLastElement;

        private Color _currentElementColor = Color.white;

        private void Awake()
        {
            CacheCombatSystem();
            RebuildVisualSettingMap();

            _currentElementColor = _normalAmmoBarColor;

            UpdateAmmoUI(forceVisualRefresh: true);
        }

        private void OnEnable()
        {
            CacheCombatSystem();
            RebuildVisualSettingMap();

            _hasLastElement = false;
            UpdateAmmoUI(forceVisualRefresh: true);
        }

        private void Update()
        {
            UpdateAmmoUI(forceVisualRefresh: false);
        }

        private void CacheCombatSystem()
        {
            if (_combatSystem != null) return;

            _combatSystem = GetComponentInParent<KRCombatSystem>();
        }

        private void RebuildVisualSettingMap()
        {
            _visualSettingMap.Clear();

            if (_elementVisualSettings == null) return;

            for (int i = 0; i < _elementVisualSettings.Count; i++)
            {
                ElementVisualSetting setting = _elementVisualSettings[i];

                if (_visualSettingMap.ContainsKey(setting.Element))
                {
                    Debug.LogWarning(
                        $"[KRAmmoUI] ElementVisualSettings에 중복 속성이 있습니다. " +
                        $"Element: {setting.Element}, Index: {i}. 첫 번째 설정만 사용합니다.",
                        this);

                    continue;
                }

                _visualSettingMap.Add(setting.Element, setting);
            }
        }

        private void UpdateAmmoUI(bool forceVisualRefresh)
        {
            if (_combatSystem == null)
            {
                CacheCombatSystem();
                if (_combatSystem == null) return;
            }

            KRDamageType element = _combatSystem.CurrentElement;

            bool elementChanged = !_hasLastElement || !_lastElement.Equals(element);

            if (forceVisualRefresh || elementChanged)
            {
                UpdateElementVisual(element);

                _lastElement = element;
                _hasLastElement = true;
            }

            float amount = _combatSystem.GetResourceAmount(element);
            float max = _combatSystem.GetMaxResourceAmount(element);
            float ratio = max > 0f ? Mathf.Clamp01(amount / max) : 0f;

            bool isLowAmmo = ratio <= _lowAmmoWarningRatio;
            Color ammoBarColor = isLowAmmo ? _lowAmmoColor : _normalAmmoBarColor;

            if (_ammoBarFill != null)
            {
                _ammoBarFill.fillAmount = ratio;
                _ammoBarFill.color = ammoBarColor;
            }

            if (_ammoText != null)
            {
                _ammoText.text = Mathf.CeilToInt(amount).ToString();
                _ammoText.color = _currentElementColor;
            }

            if (_debugCurrentElement)
            {
                Debug.Log(
                    $"[KRAmmoUI] CurrentElement: {element}, Ammo: {amount:0.##}/{max:0.##}, Ratio: {ratio:0.##}",
                    this);
            }
        }

        private void UpdateElementVisual(KRDamageType element)
        {
            if (TryGetElementVisualSetting(element, out ElementVisualSetting setting))
            {
                _currentElementColor = setting.ElementColor;

                if (_elementLogoImage != null)
                {
                    _elementLogoImage.sprite = setting.LogoSprite;
                    _elementLogoImage.enabled = setting.LogoSprite != null;
                }

                if (_elementUnderlineImage != null)
                {
                    _elementUnderlineImage.color = setting.ElementColor;
                }

                if (_ammoText != null)
                {
                    _ammoText.color = setting.ElementColor;
                }

                return;
            }

            ApplyFallbackVisual(element);
        }

        private void ApplyFallbackVisual(KRDamageType element)
        {
            _currentElementColor = _normalAmmoBarColor;

            if (_elementLogoImage != null)
            {
                _elementLogoImage.sprite = null;
                _elementLogoImage.enabled = false;
            }

            if (_elementUnderlineImage != null)
            {
                _elementUnderlineImage.color = _normalAmmoBarColor;
            }

            if (_ammoText != null)
            {
                _ammoText.color = _normalAmmoBarColor;
            }

            Debug.LogWarning(
                $"[KRAmmoUI] 현재 속성에 해당하는 UI 설정을 찾지 못했습니다. Element: {element}. " +
                $"_elementVisualSettings에 해당 속성의 로고와 색을 등록하세요.",
                this);
        }

        private bool TryGetElementVisualSetting(KRDamageType element, out ElementVisualSetting setting)
        {
            if (_visualSettingMap.Count <= 0)
            {
                RebuildVisualSettingMap();
            }

            return _visualSettingMap.TryGetValue(element, out setting);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildVisualSettingMap();

            if (!Application.isPlaying)
                return;

            _hasLastElement = false;
            UpdateAmmoUI(forceVisualRefresh: true);
        }
#endif
    }
}