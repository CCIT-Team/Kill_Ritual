// Assets/Project/Scripts/03_Weapons/KRChargeProjectileWeapon.cs
using UnityEngine;
using KillRitual.Weapons.Visual;

namespace KillRitual.Weapons
{
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

        [Tooltip("true면 차지 시작/충전 비율/발사/취소 애니메이션 신호를 KRWeaponVisual로 보냅니다.")]
        [SerializeField] private bool _playChargeVisual = true;

        // 현재 누름 동작에서 누적된 충전 시간(초). 0 ~ _chargeDuration 범위로 클램프됩니다.
        private float _chargeElapsed;

        // NotifyReleased() 시점에 계산되어 DoFire() 직전까지 유지되는 "이번 발사의" 충전 비율.
        // GetChargeRatio()가 이 값을 그대로 반환합니다.
        private float _pendingChargeRatio = 1f;

        // 차지 시작 애니메이션을 매 프레임 반복 호출하지 않기 위한 플래그.
        private bool _isCharging;

        protected override void Awake()
        {
            base.Awake();

            if (_visual == null)
            {
                _visual = GetComponentInParent<KRWeaponVisual>();

                if (_visual == null)
                {
                    _visual = GetComponentInChildren<KRWeaponVisual>(true);
                }
            }
        }

        public override void NotifyHeld()
        {
            // 우클릭을 처음 누른 프레임에만 차지 시작 애니메이션을 호출합니다.
            if (!_isCharging)
            {
                _isCharging = true;
                _chargeElapsed = 0f;

                if (_playChargeVisual)
                {
                    _visual?.PlayChargeStart(_visualAttackSlot);
                }
            }

            // 충전 도중에는 발사하지 않고 시간만 누적합니다.
            _chargeElapsed = Mathf.Min(_chargeDuration, _chargeElapsed + Time.deltaTime);

            float ratio = GetCurrentChargeRatio01();

            if (_playChargeVisual)
            {
                _visual?.UpdateCharge(ratio);
            }
        }

        public override void NotifyReleased()
        {
            // 전혀 충전하지 않은 상태(클릭도 안 한 채 호출되는 매 프레임의 "뗌" 신호)는 무시합니다.
            if (!_isCharging && _chargeElapsed <= 0f)
            {
                return;
            }

            // [최소 차징 시간] _minHoldTimeBeforeFire가 _chargeDuration보다 크게 잘못 설정되어
            // 무기가 영원히 발사 불가능해지는 실수를 방지하기 위해 안전하게 클램프합니다.
            float effectiveMinHoldTime = Mathf.Min(_minHoldTimeBeforeFire, _chargeDuration);

            if (_chargeElapsed < effectiveMinHoldTime)
            {
                // 너무 짧게 탭한 경우: 발사하지 않고 취소 애니메이션만 재생합니다.
                if (_playChargeVisual)
                {
                    _visual?.PlayChargeCancel(_visualAttackSlot);
                }

                ResetChargeState();
                return;
            }

            float rawRatio = _chargeDuration > 0f ? _chargeElapsed / _chargeDuration : 1f;
            _pendingChargeRatio = Mathf.Max(_minChargeRatio, Mathf.Clamp01(rawRatio));

            // TryFireNow()는 쿨다운/자원 확인 후 DoFire()를 호출합니다.
            // 성공 여부에 따라 Release 또는 Cancel 애니메이션을 나눕니다.
            bool fired = TryFireNow();

            if (_playChargeVisual)
            {
                if (fired)
                {
                    _visual?.PlayChargeRelease(_visualAttackSlot, _pendingChargeRatio);
                }
                else
                {
                    _visual?.PlayChargeCancel(_visualAttackSlot);
                }
            }

            ResetChargeState();
        }

        public override void NotifyCancelled()
        {
            if (_isCharging || _chargeElapsed > 0f)
            {
                if (_playChargeVisual)
                {
                    _visual?.PlayChargeCancel(_visualAttackSlot);
                }
            }

            ResetChargeState();
        }

        protected override float GetChargeRatio() => _pendingChargeRatio;

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

        private float GetCurrentChargeRatio01()
        {
            if (_chargeDuration <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(_chargeElapsed / _chargeDuration);
        }

        private void ResetChargeState()
        {
            _chargeElapsed = 0f;
            _pendingChargeRatio = 1f;
            _isCharging = false;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!_hasHomingTracers)
            {
                return;
            }

            Transform fp = ResolveFirePoint();

            if (fp == null)
            {
                return;
            }

            Gizmos.color = _homingTracerColor;
            Gizmos.DrawWireSphere(fp.position, _homingTracerRadius);
        }
    }
}