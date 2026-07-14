// Assets/Project/Scripts/03_Weapons/KRZoomHitscanWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    public sealed class KRZoomHitscanWeapon : KRHitscanWeapon
    {
        [Header("스코프 줌 (휠 버튼 Mouse2)")]
        [Tooltip("줌인했을 때의 카메라 FOV. 기본 FOV보다 작아야 확대되어 보입니다 (예: 기본 60 → 줌 15).")]
        [Range(1f, 89f)]
        [SerializeField] private float _zoomFov = 15f;

        [Tooltip("FOV가 목표값으로 변화하는 속도. 클수록 줌/복귀 전환이 빠릿합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _zoomSmoothSpeed = 12f;

        private float _defaultFov;
        private float _targetFov;
        private bool _defaultFovCached;
        private bool _isZooming;

        protected override void Awake()
        {
            base.Awake();
            CacheDefaultFov();
        }

        private void CacheDefaultFov()
        {
            if (_defaultFovCached) return;
            if (_combatSystem == null || _combatSystem.PlayerCamera == null) return;

            _defaultFov = _combatSystem.PlayerCamera.fieldOfView;
            _targetFov = _defaultFov;
            _defaultFovCached = true;
        }

        private void Update()
        {
            CacheDefaultFov();
            if (!_defaultFovCached || _combatSystem == null || _combatSystem.PlayerCamera == null) return;

            // [버그 수정] 이 컴포넌트는 다른 무기가 장착된 동안에도 GameObject가 활성 상태로
            // 남아있을 수 있어, 장착 여부와 무관하게 매 프레임 Update()가 계속 실행되고
            // Mouse2 입력을 읽어버려 "어떤 무기를 들고 있어도 줌이 걸리는" 버그가 있었습니다.
            // 미장착 상태에서는 입력을 완전히 무시하고, 줌 중이었다면 즉시 기본 FOV로 되돌립니다.
            if (!IsEquipped)
            {
                if (_isZooming)
                {
                    _isZooming = false;
                    _targetFov = _defaultFov;
                }

                Camera idleCam = _combatSystem.PlayerCamera;
                idleCam.fieldOfView = Mathf.Lerp(idleCam.fieldOfView, _defaultFov, _zoomSmoothSpeed * Time.deltaTime);
                return;
            }

            // 휠 버튼 누름 상태에 따라 목표 FOV를 결정합니다.
            _isZooming = Input.GetMouseButton(2);
            _targetFov = _isZooming ? _zoomFov : _defaultFov;

            // 매 프레임 현재 FOV를 목표값으로 부드럽게 보간합니다.
            Camera cam = _combatSystem.PlayerCamera;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, _targetFov, _zoomSmoothSpeed * Time.deltaTime);
        }

        public override void NotifyCancelled()
        {
            _isZooming = false;
            _targetFov = _defaultFovCached ? _defaultFov : 60f;

            if (_defaultFovCached && _combatSystem != null && _combatSystem.PlayerCamera != null)
            {
                _combatSystem.PlayerCamera.fieldOfView = _defaultFov;
            }
        }
    }
}