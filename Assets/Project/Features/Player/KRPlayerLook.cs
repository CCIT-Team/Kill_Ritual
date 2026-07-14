using UnityEngine;

namespace KillRitual
{
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

        [Tooltip("ESC 키로 커서 잠금/해제를 토글할지 여부입니다(처음 누르면 풀리고, 다시 누르면 잠깁니다).")]
        [SerializeField]
        private bool allowEscapeToggle = true;

        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

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

        private void HandleCursorToggle()
        {
            if (!allowEscapeToggle) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (IsCursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}