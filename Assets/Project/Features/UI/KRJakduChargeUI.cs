// Assets/Project/Features/UI/KRJakduChargeUI.cs
using TMPro;
using UnityEngine;

namespace KillRitual
{
    /// <summary>
    /// 작두(처형) 자원 보유 상태를 TextMeshProUGUI 텍스트로 표시합니다.
    ///
    /// [동작 방식]
    /// - 최대치는 표시하지 않고 "현재 보유 개수" 숫자 하나만 텍스트로 표시합니다(예: 3).
    /// - KRJakduSystem이 자원이 변할 때마다(소모/충전) SetJakduState()를 호출해 텍스트를 갱신합니다.
    ///
    /// [연결 방법]
    /// 1. Canvas 하위에 TextMeshProUGUI 오브젝트를 만듭니다.
    /// 2. 이 컴포넌트를 붙이고 _jakduText 필드에 위 TextMeshProUGUI를 연결합니다.
    /// 3. KRJakduSystem 컴포넌트의 _jakduChargeUI 필드에 이 컴포넌트를 연결합니다.
    /// </summary>
    public sealed class KRJakduChargeUI : MonoBehaviour
    {
        [Header("텍스트 UI 참조")]
        [Tooltip("작두 자원을 표시할 TextMeshProUGUI입니다.")]
        [SerializeField] private TextMeshProUGUI _jakduText;

        /// <summary>
        /// currentCharges: 현재 보유한 작두 자원 개수 (maxCharges는 화면에 표시하지 않으므로 미사용)
        /// </summary>
        public void SetJakduState(int currentCharges, int maxCharges)
        {
            if (_jakduText == null) return;

            _jakduText.text = currentCharges.ToString();
        }
    }
}
