// Assets/Project/Features/UI/KRJakduChargeUI.cs
using TMPro;
using UnityEngine;

namespace KillRitual
{
    public sealed class KRJakduChargeUI : MonoBehaviour
    {
        [Header("텍스트 UI 참조")]
        [Tooltip("작두 자원을 표시할 TextMeshProUGUI입니다.")]
        [SerializeField] private TextMeshProUGUI _jakduText;

        public void SetJakduState(int currentCharges, int maxCharges)
        {
            if (_jakduText == null) return;

            _jakduText.text = currentCharges.ToString();
        }
    }
}
