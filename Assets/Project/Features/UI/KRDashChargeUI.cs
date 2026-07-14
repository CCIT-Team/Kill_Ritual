using UnityEngine;
using UnityEngine.UI;

namespace KillRitual
{
    public sealed class KRDashChargeUI : MonoBehaviour
    {
        [Header("Slot Fill Images")]
        [Tooltip("대시 슬롯 Fill Image들입니다. 최대 대시 수만큼 넣으세요.")]
        [SerializeField] private Image[] slotFills;

        [Header("Slot Size")]
        [Tooltip("슬롯 하나가 가득 찼을 때의 너비입니다. 0이면 시작 시 각 Fill의 현재 너비를 사용합니다.")]
        [Min(0f)]
        [SerializeField] private float slotMaxWidth = 0f;

        private RectTransform[] slotFillRects;

        private float[] slotBaseWidths;
        private Vector2[] slotBasePositions;

        private void Awake()
        {
            CacheRects();
        }

        private void CacheRects()
        {
            if (slotFills == null)
            {
                slotFillRects = new RectTransform[0];
                slotBaseWidths = new float[0];
                slotBasePositions = new Vector2[0];
                return;
            }

            slotFillRects = new RectTransform[slotFills.Length];
            slotBaseWidths = new float[slotFills.Length];
            slotBasePositions = new Vector2[slotFills.Length];

            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < slotFills.Length; i++)
            {
                if (slotFills[i] == null)
                {
                    continue;
                }

                RectTransform rect = slotFills[i].rectTransform;

                slotFillRects[i] = rect;
                slotBasePositions[i] = rect.anchoredPosition;

                float width = slotMaxWidth > 0f
                    ? slotMaxWidth
                    : Mathf.Abs(rect.rect.width);

                if (width <= 0.01f)
                {
                    width = Mathf.Abs(rect.sizeDelta.x);
                }

                if (width <= 0.01f)
                {
                    width = 32f;
                    Debug.LogWarning("[KRDashChargeUI] 슬롯 너비를 가져오지 못했습니다. slotMaxWidth를 직접 입력하세요.");
                }

                slotBaseWidths[i] = width;

                // 시작 시에는 완충 상태로 맞춰둠.
                SetSlotWidthRightToLeft(i, width);
            }
        }

        public void SetDashState(int currentCharges, int maxCharges, float recharge01)
        {
            if (slotFillRects == null || slotFillRects.Length == 0)
            {
                return;
            }

            maxCharges = Mathf.Clamp(maxCharges, 0, slotFillRects.Length);
            currentCharges = Mathf.Clamp(currentCharges, 0, maxCharges);
            recharge01 = Mathf.Clamp01(recharge01);

            for (int i = 0; i < slotFillRects.Length; i++)
            {
                float ratio = 0f;

                if (i < currentCharges)
                {
                    ratio = 1f;
                }
                else if (i == currentCharges && currentCharges < maxCharges)
                {
                    ratio = recharge01;
                }

                float width = slotBaseWidths[i] * ratio;
                SetSlotWidthRightToLeft(i, width);
            }
        }

        private void SetSlotWidthRightToLeft(int index, float width)
        {
            RectTransform rect = slotFillRects[index];

            if (rect == null)
            {
                return;
            }

            float maxWidth = slotBaseWidths[index];
            width = Mathf.Clamp(width, 0f, maxWidth);

            float pivotX = rect.pivot.x;
            float baseWidth = slotBaseWidths[index];

            Vector2 basePos = slotBasePositions[index];
            Vector2 pos = basePos;

            // 오른쪽 끝 고정 공식:
            // right = positionX + (1 - pivotX) * width
            // 시작 시 오른쪽 끝을 유지하기 위해 positionX를 보정함.
            pos.x = basePos.x + (1f - pivotX) * (baseWidth - width);

            rect.anchoredPosition = pos;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }
}