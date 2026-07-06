using UnityEngine;

namespace KillRitual.Player.Visual
{
    /// <summary>
    /// FPS 손/무기를 월드 지형보다 항상 앞에 보이도록 렌더링하는 카메라 세팅 스크립트.
    ///
    /// 원리:
    /// - Main Camera는 FirstPersonHands 레이어를 렌더링하지 않음.
    /// - Hand Camera는 FirstPersonHands 레이어만 렌더링함.
    /// - Hand Camera는 Main Camera보다 나중에 렌더링하고, Depth만 지운 뒤 손을 다시 그림.
    ///
    /// 결과:
    /// - 손이 바닥/벽 안으로 들어가도 월드 지형에 가려지지 않음.
    /// - 손의 실제 위치를 억지로 고정하지 않아도 됨.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class KRFirstPersonHandsCameraSetup : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] private string _handsLayerName = "FirstPersonHands";

        [Header("Hand Camera")]
        [SerializeField] private Camera _handCamera;

        [Tooltip("손 카메라의 FOV. 0 이하이면 메인 카메라 FOV를 그대로 사용합니다.")]
        [SerializeField] private float _handFov = 0f;

        [Tooltip("손 카메라의 Near Clip. 너무 크면 손이 카메라 근처에서 잘립니다.")]
        [SerializeField] private float _nearClip = 0.01f;

        [Tooltip("손 카메라의 Far Clip. 손/무기만 그리므로 짧게 둡니다.")]
        [SerializeField] private float _farClip = 5f;

        [Tooltip("메인 카메라보다 얼마나 나중에 렌더링할지 정합니다.")]
        [SerializeField] private float _depthOffset = 1f;

        [Header("Runtime Sync")]
        [SerializeField] private bool _syncFovEveryFrame = true;

        private Camera _mainCamera;
        private int _handsLayer;
        private int _handsLayerMask;

        private void Awake()
        {
            _mainCamera = GetComponent<Camera>();

            _handsLayer = LayerMask.NameToLayer(_handsLayerName);

            if (_handsLayer < 0)
            {
                Debug.LogError(
                    $"[KRFirstPersonHandsCameraSetup] Layer '{_handsLayerName}' not found. " +
                    $"Unity의 Edit Layer에서 먼저 레이어를 만들어야 합니다.",
                    this
                );
                enabled = false;
                return;
            }

            _handsLayerMask = 1 << _handsLayer;

            SetupMainCamera();
            SetupHandCamera();
        }

        private void LateUpdate()
        {
            if (_mainCamera == null || _handCamera == null)
                return;

            // Hand Camera가 Main Camera의 위치/회전을 정확히 따라가게 한다.
            _handCamera.transform.SetPositionAndRotation(
                _mainCamera.transform.position,
                _mainCamera.transform.rotation
            );

            if (_syncFovEveryFrame)
            {
                _handCamera.fieldOfView = _handFov > 0f
                    ? _handFov
                    : _mainCamera.fieldOfView;
            }
        }

        private void SetupMainCamera()
        {
            // 메인 카메라는 손 레이어를 렌더링하지 않는다.
            _mainCamera.cullingMask &= ~_handsLayerMask;
        }

        private void SetupHandCamera()
        {
            if (_handCamera == null)
            {
                GameObject handCameraObject = new GameObject("Hand Camera");
                handCameraObject.transform.SetParent(_mainCamera.transform, false);

                _handCamera = handCameraObject.AddComponent<Camera>();
            }

            _handCamera.transform.SetPositionAndRotation(
                _mainCamera.transform.position,
                _mainCamera.transform.rotation
            );

            // 손 카메라는 손 레이어만 렌더링한다.
            _handCamera.cullingMask = _handsLayerMask;

            // 중요:
            // Depth만 지우면 메인 카메라가 그린 화면 색은 유지되고,
            // 깊이값만 초기화되어 손이 월드보다 앞에 그려진다.
            _handCamera.clearFlags = CameraClearFlags.Depth;

            _handCamera.depth = _mainCamera.depth + _depthOffset;

            _handCamera.nearClipPlane = _nearClip;
            _handCamera.farClipPlane = _farClip;

            _handCamera.fieldOfView = _handFov > 0f
                ? _handFov
                : _mainCamera.fieldOfView;

            _handCamera.orthographic = false;
            _handCamera.enabled = true;

            // 보조 카메라에는 AudioListener가 있으면 안 됨.
            AudioListener audioListener = _handCamera.GetComponent<AudioListener>();

            if (audioListener != null)
                Destroy(audioListener);
        }
    }
}