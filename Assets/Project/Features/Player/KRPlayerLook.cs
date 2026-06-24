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

        [Tooltip("테스트 중 ESC로 커서를 해제할지 여부")]
        [SerializeField]
        private bool allowEscapeUnlock = true;

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
            HandleCursorUnlockForTest();
        }

        /// <summary>
        /// 마우스 입력을 받아 시점을 회전한다.
        /// </summary>
        private void HandleLookInput()
        {
            if (cameraRoot == null)
                return;

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
        /// 테스트 중 ESC를 누르면 마우스 커서를 다시 보이게 한다.
        /// 에디터에서 Play 모드를 빠져나오거나 UI를 조작할 때 필요하다.
        /// </summary>
        private void HandleCursorUnlockForTest()
        {
            if (!allowEscapeUnlock)
                return;

            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            UnlockCursor();
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
        /// 테스트, 일시정지 메뉴, 옵션 메뉴에서 사용한다.
        /// </summary>
        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}