using KillRitual.Core.Damage;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.Player
{
    /// <summary>
    /// 현재 장착된 오행 속성의 잔탄(자원)량을 HUD에 표시합니다.
    ///   1. 잔탄 비율에 따라 막대 Image의 fillAmount를 조절합니다.
    ///   2. 잔탄 숫자를 Text로 표시합니다 (예: "73 / 100").
    ///   3. 무기를 전환하면 해당 속성의 잔탄으로 자동 갱신됩니다.
    ///   4. 잔탄이 경고 비율 이하로 떨어지면 막대와 텍스트 색이 경고색으로 바뀝니다.
    ///
    /// [잔탄바를 코드로 그리지 않고 UI로 만든 이유]
    /// 유니티에서 Image와 Text 오브젝트를 직접 만들고, 이 스크립트는
    /// 그 오브젝트들을 "잔탄량에 맞춰 갱신"하는 역할만 합니다.
    /// 위치·색·크기는 전부 유니티 화면에서 드래그와 클릭으로 조절할 수 있습니다.
    ///
    /// [연결 방법 요약]
    /// Ammo 오브젝트에 이 스크립트를 붙이고,
    /// _combatSystem에 KRCombatSystem, _ammoBarFill에 Bar Image,
    /// _ammoText에 Text를 끌어다 넣으면 됩니다.
    /// </summary>
    public sealed class KRAmmoUI : MonoBehaviour
    {
        [Header("Combat System 연결")]
        [Tooltip("플레이어의 KRCombatSystem 컴포넌트를 여기에 넣으세요. 비워두면 부모 계층에서 자동으로 찾습니다.")]
        [SerializeField] private KillRitual.Player.Combat.KRCombatSystem _combatSystem;

        [Header("잔탄 막대 UI 연결")]
        [Tooltip("잔탄에 따라 채워질 막대 Image입니다. " +
                 "유니티에서 만든 잔탄바 Image를 여기에 넣으세요. " +
                 "이 Image의 Image Type은 'Filled'여야 fillAmount로 조절됩니다.")]
        [SerializeField] private Image _ammoBarFill;

        [Tooltip("잔탄 숫자를 표시할 Text(선택). 없으면 비워두세요. 예: '73 / 100'")]
        [SerializeField] private TextMeshProUGUI _ammoText;

        [Header("경고 설정")]
        [Tooltip("잔탄 비율이 이 값 이하로 떨어지면 경고색으로 바뀝니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowAmmoWarningRatio = 0.25f;

        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _lowAmmoColor = new Color(1f, 0.3f, 0.3f);

        private void Awake()
        {
            if (_combatSystem == null)
            {
                _combatSystem = GetComponentInParent<KillRitual.Player.Combat.KRCombatSystem>();
            }

            UpdateAmmoUI();
        }

        private void Update()
        {
            UpdateAmmoUI();
        }

        /// <summary>잔탄 막대와 숫자 텍스트를 현재 잔탄량에 맞춰 갱신합니다.</summary>
        private void UpdateAmmoUI()
        {
            if (_combatSystem == null) return;

            KRDamageType element = _combatSystem.CurrentElement;
            float amount = _combatSystem.GetResourceAmount(element);
            float max = _combatSystem.GetMaxResourceAmount(element);
            float ratio = max > 0f ? Mathf.Clamp01(amount / max) : 0f;

            Color color = ratio <= _lowAmmoWarningRatio ? _lowAmmoColor : _normalColor;

            if (_ammoBarFill != null)
            {
                // fillAmount는 0(빈 칸)~1(가득 참) 사이 값입니다. 잔탄 비율을 그대로 넣습니다.
                _ammoBarFill.fillAmount = ratio;
                _ammoBarFill.color = color;
            }

            if (_ammoText != null)
            {
                _ammoText.text = Mathf.CeilToInt(amount) + " / " + Mathf.CeilToInt(max);
                _ammoText.color = color;
            }
        }
    }
}