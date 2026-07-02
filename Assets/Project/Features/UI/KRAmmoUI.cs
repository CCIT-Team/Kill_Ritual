using KillRitual.Core.Damage;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.Player
{
    /// <summary>
    /// 현재 장착된 오행 속성의 잔탄(자원)량과 속성 UI를 HUD에 표시합니다.
    ///
    /// 1. 잔탄 비율에 따라 막대 Image의 fillAmount를 조절합니다.
    /// 2. 잔탄 숫자를 현재 탄약만 표시합니다. 예: "73"
    /// 3. 무기를 전환하면 해당 속성의 잔탄, 로고, 밑줄 색, 텍스트 색이 자동 갱신됩니다.
    /// 4. 잔탄이 경고 비율 이하로 떨어지면 잔탄 막대만 경고색으로 바뀝니다.
    ///
    /// [연결 방법 요약]
    /// Ammo 오브젝트에 이 스크립트를 붙이고,
    /// _combatSystem에 KRCombatSystem,
    /// _ammoBarFill에 잔탄 Bar Image,
    /// _ammoText에 탄약 Text,
    /// _elementLogoImage에 속성 로고 Image,
    /// _elementUnderlineImage에 밑줄 Image를 넣으면 됩니다.
    ///
    /// _elementVisualSettings에는 속성별 로고와 색을 등록합니다.
    /// </summary>
    public sealed class KRAmmoUI : MonoBehaviour
    {
        [System.Serializable]
        private struct ElementVisualSetting
        {
            [Tooltip("이 설정이 적용될 오행 속성입니다.")]
            public KRDamageType Element;

            [Tooltip("현재 속성이 이 값일 때 표시할 로고 Sprite입니다.")]
            public Sprite LogoSprite;

            [Tooltip("현재 속성이 이 값일 때 사용할 UI 색입니다. 밑줄과 탄약 텍스트에 적용됩니다.")]
            public Color ElementColor;
        }

        [Header("Combat System 연결")]
        [Tooltip("플레이어의 KRCombatSystem 컴포넌트를 여기에 넣으세요. 비워두면 부모 계층에서 자동으로 찾습니다.")]
        [SerializeField] private KillRitual.Player.Combat.KRCombatSystem _combatSystem;

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
        [Tooltip("속성별 로고와 UI 색을 등록합니다.")]
        [SerializeField] private List<ElementVisualSetting> _elementVisualSettings = new List<ElementVisualSetting>();

        [Header("경고 설정")]
        [Tooltip("잔탄 비율이 이 값 이하로 떨어지면 잔탄 막대가 경고색으로 바뀝니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowAmmoWarningRatio = 0.25f;

        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _lowAmmoColor = new Color(1f, 0.3f, 0.3f);

        private KRDamageType _lastElement;
        private bool _hasLastElement;

        private Color _currentElementColor;

        private void Awake()
        {
            _currentElementColor = _normalColor;

            if (_combatSystem == null)
            {
                _combatSystem = GetComponentInParent<KillRitual.Player.Combat.KRCombatSystem>();
            }

            UpdateAmmoUI(forceVisualRefresh: true);
        }

        private void Update()
        {
            UpdateAmmoUI(forceVisualRefresh: false);
        }

        /// <summary>
        /// 잔탄 막대, 숫자 텍스트, 속성 로고, 밑줄 색을 현재 상태에 맞춰 갱신합니다.
        /// </summary>
        private void UpdateAmmoUI(bool forceVisualRefresh)
        {
            if (_combatSystem == null) return;

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
            Color ammoBarColor = isLowAmmo ? _lowAmmoColor : _normalColor;

            if (_ammoBarFill != null)
            {
                _ammoBarFill.fillAmount = ratio;
                _ammoBarFill.color = ammoBarColor;
            }

            if (_ammoText != null)
            {
                // 기존 "73 / 100" 표시 제거.
                // 현재 탄약만 표시.
                _ammoText.text = Mathf.CeilToInt(amount).ToString();

                // 탄약 텍스트 색은 언더라인과 동일하게 현재 속성 색을 따라갑니다.
                _ammoText.color = _currentElementColor;
            }
        }

        /// <summary>
        /// 현재 속성에 맞춰 로고, 밑줄 색, 텍스트 기준 색을 갱신합니다.
        /// </summary>
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

            // 해당 속성 설정이 없을 때의 기본 처리.
            _currentElementColor = _normalColor;

            if (_elementLogoImage != null)
            {
                _elementLogoImage.enabled = false;
            }

            if (_elementUnderlineImage != null)
            {
                _elementUnderlineImage.color = _normalColor;
            }

            if (_ammoText != null)
            {
                _ammoText.color = _normalColor;
            }
        }

        private bool TryGetElementVisualSetting(KRDamageType element, out ElementVisualSetting setting)
        {
            for (int i = 0; i < _elementVisualSettings.Count; i++)
            {
                if (_elementVisualSettings[i].Element.Equals(element))
                {
                    setting = _elementVisualSettings[i];
                    return true;
                }
            }

            setting = default;
            return false;
        }
    }
}