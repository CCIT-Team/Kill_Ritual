// Assets/Project/Scripts/03_Weapons/KRHitscanWeapon.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 레이캐스트(즉발) 방식 무기의 공통 구현입니다.
    /// 펠릿 수(_pelletCount)와 탄퍼짐 각도(_spreadAngleDegrees)만 인스펙터에서 다르게 설정하면
    /// 이 클래스 하나로 다음 5개 무기를 모두 구현할 수 있습니다.
    ///   목(木) 정밀소총/스나이퍼, 토(土) 연사총   → PelletCount=1 (단발)
    ///   화(火) 샷건/슈퍼샷건                      → PelletCount&gt;1, SpreadAngleDegrees&gt;0 (산탄)
    /// </summary>
    public class KRHitscanWeapon : KRWeaponBase
    {
        [Header("산탄/탄퍼짐")]
        [Tooltip("1회 발사당 생성되는 펠릿(레이) 개수. 1이면 일반 단발 무기, 2 이상이면 샷건류입니다.")]
        [Min(1)]
        [SerializeField] private int _pelletCount = 1;

        [Tooltip("탄퍼짐 콘(원뿔)의 전체 각도(도). 0이면 완전한 직사입니다.")]
        [Range(0f, 90f)]
        [SerializeField] private float _spreadAngleDegrees = 0f;

        [Tooltip("탄퍼짐이 있는 무기의 기즈모를 그릴 때 사용하는 박스 높이(시각화 전용, 판정에는 영향 없음)")]
        [Min(0.01f)]
        [SerializeField] private float _boxHeight = 1.5f;

        [Header("총알 꼬리 (트레이서)")]
        [Tooltip("발사 시 생성되는 총알 꼬리 프리팹. LineRenderer + KRHitscanTracer 컴포넌트가 필요합니다. 비워두면 시각효과 없이 발사만 처리됩니다.")]
        [SerializeField] private GameObject _tracerPrefab;

        [Tooltip("트레이서가 완전히 사라지기까지 걸리는 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _tracerDuration = 0.05f;

        [Tooltip("이 무기의 트레이서 색상")]
        [SerializeField] private Color _tracerColor = Color.white;

        // NonAlloc 레이캐스트 공용 버퍼. 클래스(타입) 단위로 공유되므로 같은 클래스를 쓰는
        // 여러 무기 인스턴스(예: 2인 협동의 각 플레이어)가 있어도, Unity가 단일 스레드로
        // 순차 실행되기 때문에 동시성 문제 없이 안전하게 재사용됩니다.
        private static readonly RaycastHit[] _hitscanBuffer = new RaycastHit[16];

        protected override void DoFire(float damagePerPellet)
        {
            Transform fp = ResolveFirePoint();
            int pellets = Mathf.Max(1, _pelletCount);

            for (int p = 0; p < pellets; p++)
            {
                Vector3 direction = ApplySpreadJitter(fp.forward, _spreadAngleDegrees);
                int hitCount = Physics.RaycastNonAlloc(fp.position, direction, _hitscanBuffer, _range, _combatSystem.HitscanLayerMask);

                Vector3 endPoint = ApplyNearestHitDamage(hitCount, damagePerPellet, fp.position, direction);
                SpawnTracer(fp.position, endPoint);
            }
        }

        /// <summary>
        /// NonAlloc 버퍼 안에서 가장 가까운 충돌 1개를 찾아 데미지를 적용하고,
        /// 트레이서가 그려야 할 종료 지점(명중 좌표 또는 빗나갔을 때의 사거리 끝 좌표)을 반환합니다.
        /// </summary>
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
                return origin + (direction * _range); // 빗나감: 트레이서는 사거리 끝까지 그립니다.
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

        /// <summary>원뿔(Cone) 내부의 무작위 방향을 산출합니다. spreadAngleDegrees가 0이면 정확히 forward를 반환합니다.</summary>
        private static Vector3 ApplySpreadJitter(Vector3 forward, float spreadAngleDegrees)
        {
            if (spreadAngleDegrees <= 0f)
            {
                return forward;
            }

            float halfAngle = spreadAngleDegrees * 0.5f;
            float randomYaw = Random.Range(-halfAngle, halfAngle);
            float randomPitch = Random.Range(-halfAngle, halfAngle);

            Quaternion jitterRotation = Quaternion.Euler(randomPitch, randomYaw, 0f);
            return jitterRotation * forward;
        }

        /// <summary>발사 시 총알 꼬리(트레이서) 시각효과를 생성합니다. 프리팹이 비어있으면 생략됩니다.</summary>
        private void SpawnTracer(Vector3 origin, Vector3 endPoint)
        {
            if (_tracerPrefab == null) return;

            GameObject instance = Instantiate(_tracerPrefab, Vector3.zero, Quaternion.identity);

            if (!instance.TryGetComponent(out KRHitscanTracer tracer))
            {
                tracer = instance.AddComponent<KRHitscanTracer>();
            }

            tracer.Play(origin, endPoint, _tracerDuration, _tracerColor);
        }

        // ------------------------------------------------------------------
        // 에디터 기즈모: 탄퍼짐이 있으면 박스(산탄 콘), 없으면 직선 레이로 사거리를 시각화합니다.
        // ------------------------------------------------------------------
        protected virtual void OnDrawGizmosSelected()
        {
            Transform fp = ResolveFirePoint();
            if (fp == null) return;

            Gizmos.color = Color.red;

            if (_spreadAngleDegrees > 0.01f)
            {
                DrawBoxGizmo(fp);
            }
            else
            {
                Gizmos.DrawLine(fp.position, fp.position + (fp.forward * _range));
            }
        }

        /// <summary>
        /// 샷건류의 산탄 콘(원뿔)을 각도와 사거리로부터 폭을 역산한 박스로 표시합니다.
        /// Gizmos.matrix를 firePoint 회전으로 설정해 일그러짐 없이 정렬합니다.
        /// </summary>
        private void DrawBoxGizmo(Transform fp)
        {
            float halfAngleRad = _spreadAngleDegrees * 0.5f * Mathf.Deg2Rad;
            float halfWidth = _range * Mathf.Tan(halfAngleRad);
            float width = Mathf.Max(0.05f, halfWidth * 2f);

            Vector3 boxSize = new Vector3(width, _boxHeight, _range);
            Vector3 boxCenterLocal = new Vector3(0f, 0f, _range * 0.5f);

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(fp.position, fp.rotation, Vector3.one);
            Gizmos.DrawWireCube(boxCenterLocal, boxSize);
            Gizmos.matrix = originalMatrix;
        }
    }
}
