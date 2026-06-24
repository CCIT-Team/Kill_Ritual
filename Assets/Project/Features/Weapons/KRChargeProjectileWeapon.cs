// Assets/Project/Scripts/03_Weapons/KRChargeProjectileWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 충전 발사형 투사체 무기 전용 클래스입니다. (수(水) 유형II 충전구체, 금(金) 유형II BFG)
    /// KRProjectileWeapon을 그대로 상속해 투사체/폭발 로직은 재사용하고,
    /// "버튼을 누르고 있으면 충전 후 1회 발사된다"는 동작과, 선택적으로 "비행 중 유도
    /// 추적탄(주변 적 자동 조준)"을 추가합니다.
    ///
    /// 충전이 완료되기 전 버튼을 떼면 발사가 취소됩니다. 충전 완료 후에는 버튼을 떼고
    /// 다시 눌러야 재충전이 시작되므로, 한 번 누르고 있는 동안 최대 1발만 발사됩니다.
    ///
    /// [유도 추적탄 필드를 공용 부모(KRProjectileWeapon)가 아닌 이 클래스에만 둔 이유]
    /// 플라즈마건/그레네이드런처는 유도 추적탄이 필요 없으므로, 그 둘의 인스펙터에는 이
    /// 기능이 노출되지 않아야 합니다. 가속 연사 필드를 KRHitscanWeapon이 아닌
    /// KRRampingHitscanWeapon에만 둔 것과 동일한 원칙입니다.
    ///
    /// [무기별 설정 예시]
    ///   수(水) 유형II 충전구체 — HasHomingTracers = false (약하고 유도 효과 없음)
    ///   금(金) 유형II BFG     — HasHomingTracers = true  (강하고 비행 중 주변 적을 자동 조준)
    /// </summary>
    public sealed class KRChargeProjectileWeapon : KRProjectileWeapon
    {
        [Header("차징 발사")]
        [Tooltip("충전 완료까지 걸리는 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _chargeDuration = 1f;

        [Header("유도 추적탄 (BFG 전용 옵션)")]
        [Tooltip("true면 비행 중 주기적으로 주변 적을 자동 탐지해 작은 잔탄을 발사합니다. " +
                 "벽 뒤에 있는 적은 레이캐스트 시야 확인을 통해 자동으로 제외됩니다.")]
        [SerializeField] private bool _hasHomingTracers = false;

        [Tooltip("유도 추적탄이 적을 탐지하는 반경")]
        [Min(0.1f)]
        [SerializeField] private float _homingTracerRadius = 15f;

        [Tooltip("유도 추적탄 발사 주기(초). 0.1이면 초당 약 10발의 잔탄이 나갑니다.")]
        [Min(0.01f)]
        [SerializeField] private float _homingTracerInterval = 0.1f;

        [Tooltip("유도 추적탄 1발당 데미지 (메인 충돌/폭발 데미지와는 별개로 누적됩니다)")]
        [Min(0f)]
        [SerializeField] private float _homingTracerDamage = 3f;

        [Tooltip("유도 추적탄이 명중할 때 보여줄 시각효과 프리팹. KRHitscanTracer 컴포넌트가 필요합니다.")]
        [SerializeField] private GameObject _homingTracerVisualPrefab;

        [Tooltip("유도 추적탄 시각효과 색상")]
        [SerializeField] private Color _homingTracerColor = new Color(0.4f, 1f, 0.5f);

        private float _chargeElapsed;
        private bool _hasFiredThisPress;

        public override void NotifyHeld()
        {
            // 이번 누름 동작에서 이미 발사를 시도했다면(성공/실패 무관) 버튼을 뗄 때까지 더 이상 시도하지 않습니다.
            if (_hasFiredThisPress)
            {
                return;
            }

            _chargeElapsed += Time.deltaTime;

            if (_chargeElapsed < _chargeDuration)
            {
                return; // 아직 충전 중
            }

            _hasFiredThisPress = true; // 충전 완료 시점에 1회만 시도하도록 즉시 마킹
            TryFireNow(); // KRWeaponBase에 protected로 정의된 공용 발사 게이트(쿨다운+자원 확인)를 그대로 재사용합니다.
        }

        public override void NotifyReleased()
        {
            _chargeElapsed = 0f;
            _hasFiredThisPress = false;
        }

        /// <summary>
        /// 부모(KRProjectileWeapon)의 발사 로직을 그대로 사용한 뒤, 이 클래스에서만
        /// 유도 추적탄을 추가로 설정합니다. _lastFiredProjectile은 부모가 protected로
        /// 노출해 둔, 방금 생성된 투사체 인스턴스에 대한 참조입니다.
        /// </summary>
        protected override void DoFire(float damage)
        {
            base.DoFire(damage);

            if (_hasHomingTracers && _lastFiredProjectile != null)
            {
                _lastFiredProjectile.ConfigureHomingTracers(
                    _homingTracerRadius,
                    _homingTracerInterval,
                    _homingTracerDamage,
                    _homingTracerVisualPrefab,
                    _homingTracerColor);
            }
        }

        /// <summary>부모의 기본 기즈모(사거리/폭발 반경)에 유도 추적탄 탐지 반경을 추가로 표시합니다.</summary>
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!_hasHomingTracers) return;

            Transform fp = ResolveFirePoint();
            if (fp == null) return;

            Gizmos.color = _homingTracerColor;
            Gizmos.DrawWireSphere(fp.position, _homingTracerRadius);
        }
    }
}
