using UnityEngine;

namespace KillRitual.Player.Visual
{
    /// <summary>
    /// 1인칭 손/무기 루트를 카메라 앞 고정 위치에 붙이는 스크립트.
    /// 손이 지형 아래로 들어가거나 플레이어 몸 위치에 끌려가는 문제를 막기 위해 사용.
    ///
    /// 부착 위치:
    /// - HandRoot
    /// - WeaponVisualRoot
    /// - FirstPersonHandsRoot
    ///
    /// 주의:
    /// - 애니메이션이 직접 움직이는 Mesh에 붙이지 말고,
    ///   그 상위의 빈 오브젝트에 붙이는 것이 안정적임.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class KRCameraLockedHands : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Camera Local Position")]
        [Tooltip("카메라 기준 손 위치. z는 앞쪽, y는 위아래, x는 좌우.")]
        [SerializeField] private Vector3 _localPosition = new Vector3(0.25f, -0.35f, 0.65f);

        [Header("Camera Local Rotation")]
        [Tooltip("카메라 기준 손 회전값.")]
        [SerializeField] private Vector3 _localEulerAngles = new Vector3(0f, 0f, 0f);

        [Header("Options")]
        [SerializeField] private bool _followRotation = true;
        [SerializeField] private bool _forceEveryFrame = true;

        private Quaternion _localRotation;

        private void Awake()
        {
            if (_cameraTransform == null)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                    _cameraTransform = mainCamera.transform;
            }

            _localRotation = Quaternion.Euler(_localEulerAngles);
        }

        private void OnValidate()
        {
            _localRotation = Quaternion.Euler(_localEulerAngles);
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
                return;

            ApplyCameraLock();

            if (!_forceEveryFrame)
                enabled = false;
        }

        private void ApplyCameraLock()
        {
            transform.position = _cameraTransform.TransformPoint(_localPosition);

            if (_followRotation)
            {
                transform.rotation = _cameraTransform.rotation * _localRotation;
            }
        }
    }
}