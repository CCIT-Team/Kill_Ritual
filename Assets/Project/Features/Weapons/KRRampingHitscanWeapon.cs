// Assets/Project/Scripts/03_Weapons/KRRampingHitscanWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    public sealed class KRRampingHitscanWeapon : KRHitscanWeapon
    {
        [Header("연사 가속")]
        [Tooltip("완전히 가속되었을 때의 최소 쿨다운(가장 빠른 연사 속도)")]
        [Min(0.01f)]
        [SerializeField] private float _minCooldown = 0.05f;

        [Tooltip("기본 Cooldown(느림)에서 MinCooldown(빠름)까지 가속되는 데 걸리는 연속 사격 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _rampUpDuration = 2.5f;

        [Header("펠릿 수 증가 (부채꼴 확장)")]
        [Tooltip("완전히 가속되었을 때 1회 발사당 나가는 최대 펠릿 수. " +
                 "가속 시작 시점의 펠릿 수는 부모(KRHitscanWeapon)의 _pelletCount(기본 1)를 그대로 사용합니다.")]
        [Min(1)]
        [SerializeField] private int _maxPelletCount = 7;

        [Header("탄퍼짐 확대")]
        [Tooltip("완전히 가속되었을 때의 최대 탄퍼짐 각도(도). 펠릿들이 이 각도 범위 안에서 " +
                 "균등 간격으로 부채꼴을 그리며 펼쳐집니다. " +
                 "가속 시작 시점의 탄퍼짐은 부모(KRHitscanWeapon)의 _spreadAngleDegrees(기본 0)를 그대로 사용합니다.")]
        [Range(0f, 90f)]
        [SerializeField] private float _maxSpreadAngleDegrees = 35f;

        [Header("펠릿 발사 시차 (레이저 빔 느낌 방지)")]
        [Tooltip("한 번에 여러 펠릿이 나갈 때, 펠릿 사이의 발사 간격(초). 0이면 모든 펠릿이 " +
                 "완전히 동시에 나가고, 0.01~0.03 정도면 \"투투투투\" 부채꼴이 빠르게 펼쳐지는 " +
                 "느낌을 줍니다.")]
        [Range(0f, 0.1f)]
        [SerializeField] private float _pelletStaggerInterval = 0.02f;

        [Header("화염방사기 비주얼")]
        [Tooltip("true면 펠릿마다 가는 트레이서 선 대신, 짧은 시간 동안 실제로 날아가는 작은 불덩이를 " +
                 "표시합니다. false면 부모(KRHitscanWeapon)의 기본 트레이서를 그대로 사용합니다.")]
        [SerializeField] private bool _useFlameVisual = true;

        [Tooltip("불덩이 시각효과 프리팹. KRFlameGlobVisual 컴포넌트가 필요합니다. " +
                 "비워두면 UseFlameVisual이 true여도 시각효과 없이 발사만 처리됩니다.")]
        [SerializeField] private GameObject _flameGlobPrefab;

        [Tooltip("총구에서 명중 지점까지 불덩이가 날아가는 데 걸리는 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _flameTravelDuration = 0.12f;

        [Tooltip("불덩이 색상 (보통 주황~빨강 계열)")]
        [SerializeField] private Color _flameColor = new Color(1f, 0.5f, 0.1f);

        private float _rampLevel;
        private Coroutine _burstCoroutine;

        private float RampRatio => _rampUpDuration > 0f ? _rampLevel / _rampUpDuration : 1f;

        public override void NotifyHeld()
        {
            _rampLevel = Mathf.Min(_rampUpDuration, _rampLevel + Time.deltaTime);
            base.NotifyHeld();
        }

        public override void NotifyReleased()
        {
            _rampLevel = 0f;
        }

        protected override float GetEffectiveCooldown()
        {
            return Mathf.Lerp(_cooldown, _minCooldown, RampRatio);
        }

        protected override int GetCurrentPelletCount()
        {
            return Mathf.RoundToInt(Mathf.Lerp(_pelletCount, _maxPelletCount, RampRatio));
        }

        protected override float GetCurrentSpreadAngle()
        {
            return Mathf.Lerp(_spreadAngleDegrees, _maxSpreadAngleDegrees, RampRatio);
        }

        protected override Vector3 ComputePelletDirection(Vector3 baseDirection, float spreadAngleDegrees,
            int pelletIndex, int totalPellets)
        {
            if (spreadAngleDegrees <= 0f || totalPellets <= 1)
            {
                return baseDirection;
            }

            // totalPellets개를 -halfAngle ~ +halfAngle 범위에 균등 간격으로 배치합니다.
            // 예: 5발이면 t = 0, 0.25, 0.5, 0.75, 1 → 각도 = -half, -half/2, 0, +half/2, +half
            float halfAngle = spreadAngleDegrees * 0.5f;
            float t = (float)pelletIndex / (totalPellets - 1); // 0~1
            float yawAngle = Mathf.Lerp(-halfAngle, halfAngle, t);

            Quaternion fanRotation = Quaternion.Euler(0f, yawAngle, 0f);
            return fanRotation * baseDirection;
        }

        protected override void DoFire(float damagePerPellet)
        {
            if (_burstCoroutine != null)
            {
                StopCoroutine(_burstCoroutine);
            }

            _burstCoroutine = StartCoroutine(FireBurstRoutine(damagePerPellet));
        }

        private System.Collections.IEnumerator FireBurstRoutine(float damagePerPellet)
        {
            int pellets = Mathf.Max(1, GetCurrentPelletCount());
            float spread = GetCurrentSpreadAngle();

            for (int p = 0; p < pellets; p++)
            {
                // FirePoint는 매 펠릿마다 다시 조회합니다 — 발사 도중 카메라/조준 방향이
                // 바뀔 수 있으므로, 시차를 둔 만큼 각 펠릿이 그 순간의 정확한 조준 방향을 따릅니다.
                Transform fp = ResolveFirePoint();
                FireSinglePellet(fp, damagePerPellet, spread, pelletIndex: p, totalPellets: pellets);

                if (_pelletStaggerInterval > 0f && p < pellets - 1)
                {
                    yield return new WaitForSeconds(_pelletStaggerInterval);
                }
            }

            _burstCoroutine = null;
        }

        private void OnDisable()
        {
            // 무기가 비활성화(전환 등)될 때 진행 중이던 버스트를 안전하게 정리합니다.
            if (_burstCoroutine != null)
            {
                StopCoroutine(_burstCoroutine);
                _burstCoroutine = null;
            }
        }

        protected override void SpawnPelletVisual(Vector3 origin, Vector3 endPoint)
        {
            if (!_useFlameVisual)
            {
                base.SpawnPelletVisual(origin, endPoint);
                return;
            }

            if (_flameGlobPrefab == null) return; // 프리팹 미할당 시 조용히 시각효과 생략

            GameObject instance = Instantiate(_flameGlobPrefab, origin, Quaternion.identity);

            if (instance.TryGetComponent(out KRFlameGlobVisual flame))
            {
                flame.Play(origin, endPoint, _flameColor, _flameTravelDuration);
            }
            else
            {
                Destroy(instance); // 잘못된 프리팹이 할당된 경우 안전하게 정리
            }
        }
    }
}