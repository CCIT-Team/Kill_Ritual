// Assets/Project/Scripts/03_Weapons/KRRampingHitscanWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 토(土) 유형II "스컬크러셔" 전용 무기 클래스입니다.
    /// KRHitscanWeapon을 그대로 상속해 레이캐스트/산탄/트레이서 로직은 재사용하고,
    /// "발사 버튼을 계속 누르고 있을수록 연사 속도가 가속된다"는 동작만 추가합니다.
    ///
    /// 버튼을 누르고 있는 동안 매 프레임 가속도(_rampLevel)가 누적되어,
    /// Cooldown(느림) → MinCooldown(빠름)으로 RampUpDuration초에 걸쳐 선형 보간됩니다.
    /// 버튼을 떼는 즉시 가속도가 0으로 초기화됩니다(미니건 RPM 가속과 동일한 개념).
    /// </summary>
    public sealed class KRRampingHitscanWeapon : KRHitscanWeapon
    {
        [Header("연사 가속 (스컬크러셔)")]
        [Tooltip("완전히 가속되었을 때의 최소 쿨다운(가장 빠른 연사 속도)")]
        [Min(0.01f)]
        [SerializeField] private float _minCooldown = 0.05f;

        [Tooltip("기본 Cooldown(느림)에서 MinCooldown(빠름)까지 가속되는 데 걸리는 연속 사격 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _rampUpDuration = 2.5f;

        private float _rampLevel;

        public override void NotifyHeld()
        {
            _rampLevel = Mathf.Min(_rampUpDuration, _rampLevel + Time.deltaTime);
            base.NotifyHeld(); // 내부적으로 TryFireNow() → GetEffectiveCooldown() 오버라이드를 사용합니다.
        }

        public override void NotifyReleased()
        {
            _rampLevel = 0f;
        }

        protected override float GetEffectiveCooldown()
        {
            float t = _rampUpDuration > 0f ? _rampLevel / _rampUpDuration : 1f;
            // _cooldown은 KRWeaponBase에 protected로 선언되어 있어 상속 체인을 통해 직접 접근 가능합니다.
            return Mathf.Lerp(_cooldown, _minCooldown, t);
        }
    }
}
