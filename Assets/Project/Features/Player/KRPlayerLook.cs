using UnityEngine;

namespace KillRitual
{
    /// <summary>
    /// FPS 플레이어의 마우스 시점 회전을 담당하는 스크립트.
    ///
    /// 구조:
    /// - KRPlayer 오브젝트: 좌우 회전 담당
    /// - CameraRoot 오브젝트: 상하 회전 담당
    /// - Main Camera: 실제 화면 출력
    ///
    /// 이렇게 나누는 이유:
    /// 플레이어 몸체까지 상하로 회전시키면 CharacterController가 기울어진 것처럼 동작할 수 있다.
    /// FPS에서는 몸체는 좌우만 돌리고, 카메라 루트만 상하로 돌리는 구조가 안정적이다.
    /// </summary>
    public class KRPlayerLook : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("상하 회전을 담당할 카메라 부모 오브젝트")]
        [SerializeField]
        private Transform cameraRoot;

        [Header("Look Settings")]
        [Tooltip("마우스 감도")]
        [SerializeField]
        private float mouseSensitivity = 2.5f;

        [Tooltip("아래로 볼 수 있는 최대 각도")]
        [SerializeField]
        private float minPitch = -80f;

        [Tooltip("위로 볼 수 있는 최대 각도")]
        [SerializeField]
        private float maxPitch = 80f;

        [Header("Cursor")]
        [Tooltip("플레이 시작 시 마우스 커서를 잠글지 여부")]
        [SerializeField]
        private bool lockCursorOnStart = true;

        [Tooltip("ESC 키로 커서 잠금/해제를 토글할지 여부. " +
                 "ESC를 처음 누르면 커서가 풀리고(일시정지), 다시 누르면 커서가 잠기며 게임으로 복귀합니다.")]
        [SerializeField]
        private bool allowEscapeToggle = true;

        /// <summary>현재 커서가 잠긴 상태인지 여부.</summary>
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// 현재 상하 회전값.
        /// 마우스 Y 입력을 누적해서 계산한다.
        /// </summary>
        private float pitch;

        private void Start()
        {
            if (lockCursorOnStart)
            {
                LockCursor();
            }
        }

        private void Update()
        {
            HandleLookInput();
            HandleCursorToggle();
        }

        /// <summary>
        /// 마우스 입력을 받아 시점을 회전한다.
        /// 커서가 잠금 해제된 상태(일시정지 등)에서는 시점 회전을 차단한다.
        /// </summary>
        private void HandleLookInput()
        {
            if (!IsCursorLocked) return;
            if (cameraRoot == null) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // 좌우 회전은 플레이어 몸체를 돌린다.
            transform.Rotate(Vector3.up * mouseX);

            // 상하 회전은 카메라 루트만 돌린다.
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// ESC를 누를 때마다 커서 잠금/해제를 토글한다.
        /// 잠겨있으면 해제(일시정지), 해제되어 있으면 다시 잠금(게임 복귀).
        /// </summary>
        private void HandleCursorToggle()
        {
            if (!allowEscapeToggle) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (IsCursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

        /// <summary>
        /// 마우스 커서를 화면 중앙에 고정한다.
        /// FPS 플레이 중 기본 상태다.
        /// </summary>
        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// 마우스 커서를 다시 보이게 한다.
        /// 일시정지 메뉴, 옵션 메뉴에서 사용한다.
        /// </summary>
        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}