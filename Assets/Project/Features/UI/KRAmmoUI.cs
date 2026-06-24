// Assets/Project/Scripts/06_UI/KRAmmoUI.cs
using UnityEngine;
using UnityEngine.UI;
using KillRitual.Core.Damage;
using KillRitual.Player.Combat;

namespace KillRitual.UI
{
    /// <summary>
    /// 현재 장착된 오행 속성의 잔탄(자원)량과 최대치를 화면에 표시하는 HUD 컴포넌트입니다.
    ///
    /// [표시 방식] 5속성을 한꺼번에 보여주지 않고, 1~5 숫자키로 무기를 전환할 때마다
    /// 표시 대상이 새로 선택한 속성으로 즉시 갱신됩니다(클래식 FPS 탄환 카운터와 동일한 방식).
    ///
    /// [UI 구성 요구사항 - Unity 에디터에서 직접 배치]
    ///   1. Canvas 하위에 Text(레거시 UI) 오브젝트를 만들고 이 컴포넌트를 부착
    ///   2. _ammoText에 그 Text 컴포넌트를 연결 (예: "73 / 100" 형식으로 표시됨)
    ///   3. (선택) 잔탄 비율을 막대로도 보여주고 싶다면 Image(Filled 타입)를 만들어 _ammoFillImage에 연결
    ///   4. (선택) 속성별 아이콘을 보여주고 싶다면 Image를 만들어 _elementIconImage에 연결하고,
    ///      _elementIcons 배열에 화→수→목→토→금 순서로 스프라이트 5개를 채움
    ///   5. _combatSystem에 플레이어의 KRCombatSystem을 연결
    ///
    /// 텍스트 컴포넌트로 TextMeshPro를 쓰고 싶다면 필드 타입을 UnityEngine.UI.Text에서
    /// TMPro.TextMeshProUGUI로 바꾸기만 하면 나머지 로직은 동일하게 동작합니다.
    /// </summary>
    public sealed class KRAmmoUI : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("잔탄 정보를 가져올 플레이어의 KRCombatSystem")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Header("UI 참조")]
        [Tooltip("\"73 / 100\" 형식으로 잔탄을 표시할 텍스트")]
        [SerializeField] private Text _ammoText;

        [Tooltip("(선택) 잔탄 비율을 막대로 표시할 Image. Image Type을 \"Filled\"로 설정해야 합니다.")]
        [SerializeField] private Image _ammoFillImage;

        [Tooltip("(선택) 현재 속성 아이콘을 표시할 Image")]
        [SerializeField] private Image _elementIconImage;

        [Tooltip("화→수→목→토→금 순서로 5개의 아이콘 스프라이트. _elementIconImage를 쓸 때만 필요합니다.")]
        [SerializeField] private Sprite[] _elementIcons = new Sprite[5];

        [Header("표시 형식")]
        [Tooltip("소수점 없이 정수로 표시할지 여부. 체크 해제하면 \"73.4 / 100.0\"처럼 소수점이 보입니다.")]
        [SerializeField] private bool _roundToInteger = true;

        [Tooltip("잔탄이 이 비율(0~1) 이하로 떨어지면 텍스트/막대 색이 경고색으로 바뀝니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowAmmoWarningRatio = 0.25f;

        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _lowAmmoColor = new Color(1f, 0.3f, 0.3f);

        private void Update()
        {
            if (_combatSystem == null) return;

            KRDamageType element = _combatSystem.CurrentElement;
            float amount = _combatSystem.GetResourceAmount(element);
            float max = _combatSystem.MaxResourcePerElement;
            float ratio = max > 0f ? Mathf.Clamp01(amount / max) : 0f;

            UpdateAmmoText(amount, max, ratio);
            UpdateFillBar(ratio);
            UpdateElementIcon(element);
        }

        private void UpdateAmmoText(float amount, float max, float ratio)
        {
            if (_ammoText == null) return;

            _ammoText.text = _roundToInteger
                ? $"{Mathf.CeilToInt(amount)} / {Mathf.CeilToInt(max)}"
                : $"{amount:F1} / {max:F1}";

            _ammoText.color = ratio <= _lowAmmoWarningRatio ? _lowAmmoColor : _normalColor;
        }

        private void UpdateFillBar(float ratio)
        {
            if (_ammoFillImage == null) return;

            _ammoFillImage.fillAmount = ratio;
            _ammoFillImage.color = ratio <= _lowAmmoWarningRatio ? _lowAmmoColor : _normalColor;
        }

        private void UpdateElementIcon(KRDamageType element)
        {
            if (_elementIconImage == null) return;

            int idx = (int)element;
            if (_elementIcons == null || idx < 0 || idx >= _elementIcons.Length) return;

            Sprite icon = _elementIcons[idx];
            if (icon != null)
            {
                _elementIconImage.sprite = icon;
            }
        }
    }
}
