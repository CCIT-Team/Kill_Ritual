using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 그로기 상태일 때 항상 테두리를 표시하고,
    /// 처형 가능 거리 안에 들어오면 색상이 주황색으로 바뀝니다.
    /// </summary>
    public sealed class KRGroggyOutline : MonoBehaviour
    {
        [Tooltip("그로기 상태 기본 테두리 색상.")]
        [SerializeField] private Color _groggyColor = new Color(0.2f, 0.6f, 1f);  // 파랑

        [Tooltip("처형 가능 거리 안에 들어왔을 때 테두리 색상.")]
        [SerializeField] private Color _executableColor = new Color(1f, 0.5f, 0f); // 주황

        [Tooltip("테두리 두께.")]
        [Min(0f)]
        [SerializeField] private float _outlineWidth = 20f;

        private Outline _outline;
        private bool _isGroggy;
        private bool _isInRange;

        private void Awake()
        {
            _outline = GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();

            _outline.OutlineWidth = _outlineWidth;
            _outline.enabled = false;
        }

        /// <summary>그로기 상태 변경. KREnemyBase가 호출합니다.</summary>
        public void SetOutline(bool groggy)
        {
            _isGroggy = groggy;
            Refresh();
        }

        /// <summary>처형 가능 범위 진입/이탈. KRAbsorptionZone이 호출합니다.</summary>
        public void SetInRange(bool inRange)
        {
            Debug.Log($"[{name}] SetInRange({inRange}) 호출됨");
            _isInRange = inRange;
            Refresh();
        }

        private void Refresh()
        {
            if (_outline == null) return;

            if (!_isGroggy)
            {
                // 그로기 아니면 무조건 끔
                _outline.enabled = false;
                return;
            }

            // 그로기 상태 → 항상 켜고 거리에 따라 색상 변경
            _outline.enabled = true;
            _outline.OutlineColor = _isInRange ? _executableColor : _groggyColor;
        }
    }
}