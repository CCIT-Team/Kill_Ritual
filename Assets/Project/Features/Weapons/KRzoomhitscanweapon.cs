// Assets/Project/Scripts/03_Weapons/KRZoomHitscanWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 목(木) 유형II "스나이퍼" 전용 무기 클래스입니다.
    /// KRHitscanWeapon을 그대로 상속해 레이캐스트/트레이서/쿨다운 로직은 재사용하고,
    /// "휠 버튼(Mouse2)을 누르고 있는 동안 카메라가 줌인, 떼면 복귀"하는 스코프 기능만 추가합니다.
    ///
    /// [동작 방식]
    ///   - 이 무기가 장착된 동안, 가운데 휠 버튼(Mouse2)을 누르고 있으면 FOV가 _zoomFov로 줄어들고(줌인),
    ///     떼는 순간 원래 FOV로 부드럽게 복귀합니다.
    ///   - 줌 중에도 좌클릭/우클릭 발사는 정상 동작합니다 (줌은 순수 시각효과).
    ///   - 무기를 전환하면 줌 상태가 즉시 초기화되고 FOV가 복귀합니다.
    ///
    /// [Mouse2를 KRCombatSystem을 거치지 않고 직접 읽는 이유]
    ///   KRCombatSystem은 좌클릭(유형I)과 우클릭(유형II) 두 채널만 관리합니다.
    ///   휠 버튼은 발사와 무관한 순수 카메라 효과이므로, 무기 스크립트가 직접 Update()에서
    ///   Input.GetMouseButton(2)를 읽어 처리하는 것이 가장 간단하고 명확합니다.
    /// </summary>
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

            // 휠 버튼 누름 상태에 따라 목표 FOV를 결정합니다.
            _isZooming = Input.GetMouseButton(2);
            _targetFov = _isZooming ? _zoomFov : _defaultFov;

            // 매 프레임 현재 FOV를 목표값으로 부드럽게 보간합니다.
            Camera cam = _combatSystem.PlayerCamera;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, _targetFov, _zoomSmoothSpeed * Time.deltaTime);
        }

        /// <summary>무기 전환 시 줌 상태를 초기화하고 FOV를 즉시 복귀시킵니다.</summary>
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