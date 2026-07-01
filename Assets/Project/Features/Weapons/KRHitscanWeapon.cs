// Assets/Project/Scripts/03_Weapons/KRHitscanWeapon.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 레이캐스트(즉발) 방식 무기의 공통 구현입니다.
    /// 펠릿 수(_pelletCount)와 탄퍼짐 각도(_spreadAngleDegrees)만 인스펙터에서 다르게 설정하면
    /// 이 클래스 하나로 다음 무기들을 구현할 수 있습니다.
    ///   목(木) 정밀소총/스나이퍼, 토(土) 연사총   → PelletCount=1 (단발)
    ///   화(火) 샷건/슈퍼샷건                      → PelletCount&gt;1, SpreadAngleDegrees&gt;0 (산탄)
    ///
    /// [KRRampingHitscanWeapon 확장 포인트]
    /// GetCurrentPelletCount(), GetCurrentSpreadAngle(), ComputePelletDirection()을 virtual로
    /// 두어, 자식 클래스가 동적으로 오버라이드할 수 있습니다. 스컬크러셔(KRRampingHitscanWeapon)는
    /// 이를 이용해 가속 비율에 따라 펠릿 수/탄퍼짐을 늘리고, 부채꼴 형태로 균등 배치합니다.
    /// </summary>
    public class KRHitscanWeapon : KRWeaponBase
    {
        [Header("산탄/탄퍼짐")]
        [Tooltip("1회 발사당 생성되는 펠릿(레이) 개수. 1이면 일반 단발 무기, 2 이상이면 샷건류입니다. " +
                 "KRRampingHitscanWeapon에서는 가속 시작 시점(최저 가속)의 펠릿 수로 사용됩니다.")]
        [Min(1)]
        [SerializeField] protected int _pelletCount = 1;

        [Tooltip("탄퍼짐 콘(원뿔)의 전체 각도(도). 0이면 완전한 직사입니다. " +
                 "KRRampingHitscanWeapon에서는 가속 시작 시점(최저 가속)의 탄퍼짐 각도로 사용됩니다.")]
        [Range(0f, 90f)]
        [SerializeField] protected float _spreadAngleDegrees = 0f;

        [Tooltip("탄퍼짐이 있는 무기의 기즈모를 그릴 때 사용하는 박스 높이(시각화 전용, 판정에는 영향 없음)")]
        [Min(0.01f)]
        [SerializeField] private float _boxHeight = 1.5f;

        [Header("총알 꼬리 (트레이서)")]
        [Tooltip("발사 시 생성되는 총알 꼬리 프리팹. LineRenderer + KRHitscanTracer 컴포넌트가 필요합니다. 비워두면 시각효과 없이 발사만 처리됩니다.")]
        [SerializeField] private GameObject _tracerPrefab;

        [Tooltip("트레이서가 화면에서 이동하는 속도(미터/초). 클수록 빨리 지나가서 짧게 보입니다. " +
                 "충돌 지점에 도달하는 즉시 잔상 없이 사라집니다.")]
        [Min(1f)]
        [SerializeField] private float _tracerVisualSpeed = 250f;

        [Tooltip("트레이서 선 자체의 최대 시각적 길이(미터). 0이면 제한 없이 전체 사거리를 다 그립니다.")]
        [Min(0f)]
        [SerializeField] private float _tracerMaxVisualLength = 8f;

        [Tooltip("이 무기의 트레이서 색상")]
        [SerializeField] private Color _tracerColor = Color.white;

        // 인스턴스 버퍼로 선언합니다. KRRampingHitscanWeapon처럼 코루틴으로 펠릿을 순차 발사할 때,
        // yield 사이 프레임에 다른 무기 인스턴스가 static 버퍼를 덮어써서 결과가 오염되는 문제를
        // 방지합니다. 단일 무기는 한 번에 하나의 레이캐스트만 수행하므로 인스턴스 버퍼로도 충분합니다.
        private readonly RaycastHit[] _hitscanBuffer = new RaycastHit[16];

        protected override void DoFire(float damagePerPellet)
        {
            Transform fp = ResolveFirePoint();
            int pellets = Mathf.Max(1, GetCurrentPelletCount());
            float spread = GetCurrentSpreadAngle();

            for (int p = 0; p < pellets; p++)
            {
                FireSinglePellet(fp, damagePerPellet, spread, p, pellets);
            }
        }

        /// <summary>
        /// 펠릿 1발을 즉시 발사합니다(레이캐스트 + 데미지 적용 + 트레이서). 기본 구현(DoFire)은
        /// 이 메서드를 같은 프레임 안에서 N번 호출해 모든 펠릿을 동시에 쏩니다.
        /// KRRampingHitscanWeapon처럼 펠릿 사이에 시차를 두고 싶은 자식 클래스는, DoFire()를
        /// 직접 오버라이드하지 않고 이 메서드를 코루틴 안에서 한 번씩 호출하는 방식으로
        /// "여러 발의 단발"처럼 보이게 만들 수 있습니다 (한 덩어리로 보이는 레이저 느낌 방지).
        /// </summary>
        /// <param name="pelletIndex">이번 발사 사이클 안에서 이 펠릿의 순서(0부터 시작). 부채꼴 패턴 등
        /// 펠릿 순서에 따라 달라지는 방향 계산(ComputePelletDirection)에 사용됩니다.</param>
        /// <param name="totalPellets">이번 발사 사이클의 전체 펠릿 수.</param>
        protected void FireSinglePellet(Transform fp, float damage, float spreadAngleDegrees,
            int pelletIndex, int totalPellets)
        {
            Vector3 baseDirection = _combatSystem.GetAimDirection(fp.position, _range);
            Vector3 direction = ComputePelletDirection(baseDirection, spreadAngleDegrees, pelletIndex, totalPellets);

            int hitCount = Physics.RaycastNonAlloc(
                fp.position, direction, _hitscanBuffer, _range, _combatSystem.HitscanLayerMask);

            Vector3 endPoint = ApplyNearestHitDamage(hitCount, damage, fp.position, direction);
            SpawnPelletVisual(fp.position, endPoint);
        }

        /// <summary>
        /// 펠릿 1발의 최종 발사 방향을 계산합니다. 기본 구현은 콘(원뿔) 안에서 완전히 무작위인
        /// 산탄 패턴(ApplySpreadJitter)을 사용합니다. KRRampingHitscanWeapon(스컬크러셔)처럼
        /// 균등 간격의 부채꼴 패턴이 필요한 자식 클래스는 이 메서드를 오버라이드해
        /// pelletIndex/totalPellets를 이용한 결정론적 패턴으로 완전히 교체할 수 있습니다.
        /// </summary>
        protected virtual Vector3 ComputePelletDirection(Vector3 baseDirection, float spreadAngleDegrees,
            int pelletIndex, int totalPellets)
        {
            return ApplySpreadJitter(baseDirection, spreadAngleDegrees);
        }

        /// <summary>
        /// 펠릿 1발의 시각효과를 생성합니다. 기본 구현은 즉발 트레이서(선)를 그리지만,
        /// KRRampingHitscanWeapon(스컬크러셔)처럼 "불덩이가 날아가는" 느낌이 필요한 자식 클래스는
        /// 이 메서드를 오버라이드해 다른 시각효과(예: KRFlameGlobVisual)로 완전히 교체할 수 있습니다.
        /// 데미지는 이미 ApplyNearestHitDamage에서 즉시 적용되었으므로, 이 메서드는 순수하게
        /// "어떻게 보여줄 것인가"만 담당하고 판정에는 전혀 영향을 주지 않습니다.
        /// </summary>
        protected virtual void SpawnPelletVisual(Vector3 origin, Vector3 endPoint)
        {
            SpawnTracer(origin, endPoint);
        }

        /// <summary>
        /// 이번 발사에 사용할 펠릿(레이) 수를 반환합니다.
        /// KRRampingHitscanWeapon이 오버라이드해서 가속 비율에 따라 동적으로 늘립니다.
        /// </summary>
        protected virtual int GetCurrentPelletCount() => _pelletCount;

        /// <summary>
        /// 이번 발사에 사용할 탄퍼짐 각도를 반환합니다.
        /// KRRampingHitscanWeapon이 오버라이드해서 가속 비율에 따라 동적으로 늘립니다.
        /// </summary>
        protected virtual float GetCurrentSpreadAngle() => _spreadAngleDegrees;

        private Vector3 ApplyNearestHitDamage(int hitCount, float damage, Vector3 origin, Vector3 direction)
        {
            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (_hitscanBuffer[i].distance < closestDistance)
                {
                    closestDistance = _hitscanBuffer[i].distance;
                    closestIndex = i;
                }
            }

            if (closestIndex < 0)
            {
                return origin + (direction * _range);
            }

            RaycastHit hit = _hitscanBuffer[closestIndex];
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            if (target != null && !target.IsDead && !ReferenceEquals(target, _combatSystem.Owner))
            {
                var context = new KRDamageContext(damage, _element, hit.point, direction);
                target.TakeDamage(context);
            }

            return hit.point;
        }

        /// <summary>콘(원뿔) 내부의 무작위 산탄 방향을 계산합니다. 자식 클래스가 결정론적 패턴(부채꼴 등)의
        /// 폴백이나 보조 계산용으로 재사용할 수 있도록 protected로 둡니다.</summary>
        protected static Vector3 ApplySpreadJitter(Vector3 forward, float spreadAngleDegrees)
        {
            if (spreadAngleDegrees <= 0f) return forward;

            float halfAngle = spreadAngleDegrees * 0.5f;
            Quaternion jitter = Quaternion.Euler(
                Random.Range(-halfAngle, halfAngle),
                Random.Range(-halfAngle, halfAngle),
                0f);
            return jitter * forward;
        }

        private void SpawnTracer(Vector3 origin, Vector3 endPoint)
        {
            if (_tracerPrefab == null) return;

            GameObject instance = Instantiate(_tracerPrefab, Vector3.zero, Quaternion.identity);

            if (!instance.TryGetComponent(out KRHitscanTracer tracer))
            {
                tracer = instance.AddComponent<KRHitscanTracer>();
            }

            tracer.Play(origin, endPoint, _tracerColor, _tracerVisualSpeed, _tracerMaxVisualLength);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Transform fp = ResolveFirePoint();
            if (fp == null) return;

            Gizmos.color = Color.red;

            if (GetCurrentSpreadAngle() > 0.01f)
                DrawBoxGizmo(fp, GetCurrentSpreadAngle());
            else
                Gizmos.DrawLine(fp.position, fp.position + (fp.forward * _range));
        }

        private void DrawBoxGizmo(Transform fp, float spreadAngle)
        {
            float halfAngleRad = spreadAngle * 0.5f * Mathf.Deg2Rad;
            float width = Mathf.Max(0.05f, _range * Mathf.Tan(halfAngleRad) * 2f);

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(fp.position, fp.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0f, _range * 0.5f), new Vector3(width, _boxHeight, _range));
            Gizmos.matrix = originalMatrix;
        }
    }
}