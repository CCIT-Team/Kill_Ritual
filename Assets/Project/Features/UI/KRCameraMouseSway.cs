using UnityEngine;

namespace KillRitual.UI
{
    public class KRCameraMouseSway : MonoBehaviour
    {
        [Header("Target Camera")]
        [Tooltip("화면 기준 방향을 계산할 카메라. 비워두면 자식 카메라를 자동으로 찾습니다.")]
        [SerializeField] private Transform targetCamera;

        [Header("Input")]
        [SerializeField] private string mouseXInput = "Mouse X";
        [SerializeField] private string mouseYInput = "Mouse Y";

        [Header("Direction")]
        [Tooltip("마우스 이동 방향과 같은 방향으로 카메라가 이동합니다.")]
        [SerializeField] private bool moveSameDirectionAsMouse = true;

        [Header("Move Amount")]
        [Tooltip("마우스 X 이동에 따른 카메라 이동 누적량")]
        [SerializeField] private float horizontalAmount = 0.015f;

        [Tooltip("마우스 Y 이동에 따른 카메라 이동 누적량")]
        [SerializeField] private float verticalAmount = 0.015f;

        [Header("Move Limit")]
        [Tooltip("좌우 최대 이동 거리")]
        [SerializeField] private float maxHorizontalOffset = 0.25f;

        [Tooltip("상하 최대 이동 거리")]
        [SerializeField] private float maxVerticalOffset = 0.15f;

        [Header("Smoothing")]
        [Tooltip("목표 위치까지 따라가는 속도")]
        [SerializeField] private float followSmooth = 8f;

        [Header("Return")]
        [Tooltip("켜면 마우스를 멈췄을 때 원래 위치로 돌아갑니다. 메뉴 카메라라면 보통 꺼두는 것이 좋습니다.")]
        [SerializeField] private bool returnToBaseWhenIdle = false;

        [Tooltip("원래 위치로 돌아가는 속도")]
        [SerializeField] private float returnSmooth = 4f;

        [Header("Advanced")]
        [SerializeField] private bool useUnscaledTime = false;

        private Vector3 baseLocalPosition;

        private Vector2 targetOffset;
        private Vector2 currentOffset;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;

            if (targetCamera == null)
            {
                UnityEngine.Camera childCamera = GetComponentInChildren<UnityEngine.Camera>();

                if (childCamera != null)
                {
                    targetCamera = childCamera.transform;
                }
            }

            if (targetCamera == null)
            {
                targetCamera = transform;
            }
        }

        private void LateUpdate()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float mouseX = Input.GetAxisRaw(mouseXInput);
            float mouseY = Input.GetAxisRaw(mouseYInput);

            if (!moveSameDirectionAsMouse)
            {
                mouseX *= -1f;
                mouseY *= -1f;
            }

            bool hasMouseInput =
                Mathf.Abs(mouseX) > 0.001f ||
                Mathf.Abs(mouseY) > 0.001f;

            if (hasMouseInput)
            {
                targetOffset.x += mouseX * horizontalAmount;
                targetOffset.y += mouseY * verticalAmount;

                targetOffset.x = Mathf.Clamp(
                    targetOffset.x,
                    -maxHorizontalOffset,
                    maxHorizontalOffset
                );

                targetOffset.y = Mathf.Clamp(
                    targetOffset.y,
                    -maxVerticalOffset,
                    maxVerticalOffset
                );
            }
            else if (returnToBaseWhenIdle)
            {
                targetOffset = Vector2.Lerp(
                    targetOffset,
                    Vector2.zero,
                    1f - Mathf.Exp(-returnSmooth * deltaTime)
                );
            }

            float lerpValue = 1f - Mathf.Exp(-followSmooth * deltaTime);

            currentOffset = Vector2.Lerp(
                currentOffset,
                targetOffset,
                lerpValue
            );

            ApplyPositionOffset();
        }

        private void ApplyPositionOffset()
        {
            Vector3 worldOffset =
                targetCamera.right * currentOffset.x +
                targetCamera.up * currentOffset.y;

            Vector3 localOffset;

            if (transform.parent != null)
            {
                localOffset = transform.parent.InverseTransformVector(worldOffset);
            }
            else
            {
                localOffset = worldOffset;
            }

            transform.localPosition = baseLocalPosition + localOffset;
        }

        public void ResetToBasePosition()
        {
            targetOffset = Vector2.zero;
            currentOffset = Vector2.zero;
            transform.localPosition = baseLocalPosition;
        }

        public void SetCurrentAsBasePosition()
        {
            baseLocalPosition = transform.localPosition;
            targetOffset = Vector2.zero;
            currentOffset = Vector2.zero;
        }

        private void OnDisable()
        {
            transform.localPosition = baseLocalPosition;
        }
    }
}