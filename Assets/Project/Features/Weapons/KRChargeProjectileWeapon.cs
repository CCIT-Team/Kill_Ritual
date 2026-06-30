// Assets/Project/Scripts/03_Weapons/KRChargeProjectileWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 충전 발사형 투사체 무기 전용 클래스입니다. (수(水) 유형II 충전구체, 금(金) 유형II BFG)
    /// KRProjectileWeapon을 그대로 상속해 투사체/폭발 로직은 재사용하고,
    /// "누르고 있는 동안 충전되고, 떼는 순간 그때까지 충전된 만큼의 크기로 발사된다"는
    /// 활시위 당기기(Bow-draw) 방식의 차징을 추가합니다.
    ///
    /// [동작 방식]
    ///   - 버튼을 누르고 있는 동안 _chargeElapsed가 누적됩니다 (최대 _chargeDuration까지).
    ///   - 다 채워진 뒤에도 계속 누르고 있으면, 100% 충전 상태로 그냥 대기합니다(자동 발사 없음).
    ///   - 버튼을 떼는 순간, 그 시점까지의 충전 비율(0~1)로 정확히 1회 발사됩니다.
    ///   - 충전 비율은 데미지·폭발 반경·투사체 시각적 크기에 동일하게 곱연산되어(KRProjectileWeapon.
    ///     GetChargeRatio() 오버라이드), 짧게 끊어 쏘면 작고 약한 샷이, 끝까지 채워 쏘면 최대 크기의
    ///     샷이 나갑니다. 너무 짧게 탭 했을 때 거의 0 크기가 나가지 않도록 _minChargeRatio로
    ///     최소 비율을 보장합니다.
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
        [Tooltip("완전히 충전되기까지 걸리는 시간(초). 버튼을 이 시간만큼 채워서 누르면 100% 크기로 발사됩니다.")]
        [Min(0.01f)]
        [SerializeField] private float _chargeDuration = 1f;

        [Tooltip("아주 짧게 탭 했다 떼어도 보장되는 최소 충전 비율(0~1). " +
                 "0으로 두면 탭만 해도 거의 0 크기의 무의미한 샷이 나갈 수 있어, 기본값은 약간의 하한을 둡니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minChargeRatio = 0.15f;

        [Tooltip("최소 차징 시간(초). 누르고 있던 시간이 이 값보다 짧으면 발사 자체가 일어나지 않고 " +
                 "조용히 취소됩니다(자원/쿨다운 낭비 방지). _minChargeRatio와 다른 개념입니다 — " +
                 "_minChargeRatio는 '발사는 되지만 크기가 작게라도 보장됨'이고, 이 값은 '아예 발사 안 됨'입니다.")]
        [Min(0f)]
        [SerializeField] private float _minHoldTimeBeforeFire = 0.2f;

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

        // 현재 누름 동작에서 누적된 충전 시간(초). 0 ~ _chargeDuration 범위로 클램프됩니다.
        private float _chargeElapsed;

        // NotifyReleased() 시점에 계산되어 DoFire() 직전까지 유지되는 "이번 발사의" 충전 비율.
        // GetChargeRatio()가 이 값을 그대로 반환합니다.
        private float _pendingChargeRatio = 1f;

        public override void NotifyHeld()
        {
            // 충전 도중에는 발사하지 않고 시간만 누적합니다. 100% 도달 후에도 계속 누르고 있으면
            // 그냥 그 상태로 대기합니다(자동 발사 없음) — 실제 발사는 NotifyReleased()에서 일어납니다.
            _chargeElapsed = Mathf.Min(_chargeDuration, _chargeElapsed + Time.deltaTime);
        }

        public override void NotifyReleased()
        {
            // 전혀 충전하지 않은 상태(클릭도 안 한 채 호출되는 매 프레임의 "뗌" 신호)는 무시합니다.
            if (_chargeElapsed <= 0f)
            {
                return;
            }

            // [최소 차징 시간] _minHoldTimeBeforeFire가 _chargeDuration보다 크게 잘못 설정되어
            // 무기가 영원히 발사 불가능해지는 실수를 방지하기 위해 안전하게 클램프합니다.
            float effectiveMinHoldTime = Mathf.Min(_minHoldTimeBeforeFire, _chargeDuration);

            if (_chargeElapsed < effectiveMinHoldTime)
            {
                // 문턱을 넘기지 못한 너무 짧은 탭입니다. 발사하지 않고 조용히 취소합니다
                // (자원도 쿨다운도 전혀 소모되지 않습니다 — TryFireNow() 자체를 호출하지 않으므로).
                _chargeElapsed = 0f;
                return;
            }

            float rawRatio = _chargeDuration > 0f ? _chargeElapsed / _chargeDuration : 1f;
            _pendingChargeRatio = Mathf.Max(_minChargeRatio, Mathf.Clamp01(rawRatio));

            // TryFireNow()는 KRWeaponBase에 protected로 정의된 공용 발사 게이트(쿨다운+자원 확인)이며,
            // 통과 시 내부적으로 DoFire(damage)를 호출합니다. DoFire()는 GetChargeRatio()를 통해
            // 위에서 계산한 _pendingChargeRatio를 데미지/폭발반경/투사체 크기에 적용합니다.
            TryFireNow();

            _chargeElapsed = 0f; // 발사 시도 후(성공/실패 무관) 충전 상태를 초기화해 다음 누름부터 새로 충전합니다.
        }

        /// <summary>
        /// 무기 전환(퀵스왑) 시 호출됩니다. 마우스를 실제로 뗀 것이 아니므로, 충전 중이던 샷을
        /// 발사하지 않고 조용히 취소합니다. (마우스를 뗐을 때 발사하는 NotifyReleased()와
        /// 의도적으로 다르게 동작합니다 — 그렇지 않으면 충전 중 무기를 바꾸기만 해도
        /// 의도치 않게 발사돼버립니다.)
        /// </summary>
        public override void NotifyCancelled()
        {
            _chargeElapsed = 0f;
        }

        /// <summary>이번 발사에 사용할 충전 비율을 부모(KRProjectileWeapon)의 DoFire()에 제공합니다.</summary>
        protected override float GetChargeRatio() => _pendingChargeRatio;

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
