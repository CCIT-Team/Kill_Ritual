// Assets/Project/Scripts/03_Weapons/KRPhysicsProjectile.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Managers;

namespace KillRitual.Weapons
{
    [DisallowMultipleComponent]
    public sealed class KRPhysicsProjectile : MonoBehaviour
    {
        [Tooltip("충돌 판정 없이 자동 소멸되기까지의 최대 생존 시간(초). 사거리 소진 전에 안전망 역할을 합니다.")]
        [SerializeField] private float _maxLifetimeSeconds = 6f;

        private void Awake()
        {
            // 충돌 판정은 레이캐스트로 직접 처리하므로, 프리팹에 실수로 남은 Collider가 PhysX 물리 충돌로 플레이어를 밀어내지 않도록 Trigger로 강제 전환합니다.
            if (TryGetComponent(out Collider ownCollider))
            {
                ownCollider.isTrigger = true;
            }

            if (TryGetComponent(out Rigidbody ownRigidbody))
            {
                ownRigidbody.isKinematic = true;
            }
        }

        private static readonly RaycastHit[] _raycastBuffer = new RaycastHit[8];
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        // 중복 제거용 인스턴스 ID 마킹 배열로, O(n²) 이중 루프를 O(n) 단일 패스로 대체하며 _overlapBuffer와 동일한 크기로 인덱스 초과를 막습니다.
        private static readonly int[] _handledInstanceIds = new int[32];

        // 유도 추적탄 전용 NonAlloc 버퍼로, static이면 다른 투사체와 결과가 오염되므로 인스턴스 필드로 선언해 폭발용 버퍼와 분리합니다.
        private readonly Collider[] _tracerOverlapBuffer = new Collider[8];

        private KRDamageType _elementType;
        private float _damage;
        private float _gravityScale;
        private int _pierceRemaining;
        private bool _explodesOnImpact;
        private float _explosionRadius;
        private IDamageable _owner;

        // 사격 판정(벽 포함, Environment 레이어)과 폭발 판정(Damageable 레이어만)을 분리해 브로드페이즈 후보 수를 줄입니다.
        private LayerMask _hitscanLayerMask;
        private LayerMask _explosionLayerMask;

        private Vector3 _velocity;
        private Vector3 _previousPosition;
        private float _remainingRange;
        private float _elapsedLifetime;
        private bool _initialized;

        // BFG 전용 유도 추적탄 옵션 상태로, ConfigureHomingTracers()가 호출되지 않으면 일반 투사체와 동일하게 동작합니다.
        private bool _hasHomingTracers;
        private float _tracerRadius;
        private float _tracerInterval;
        private float _tracerDamage;
        private float _tracerTimer;
        private GameObject _tracerVisualPrefab;
        private Color _tracerVisualColor;

        // 실제 인게임에 보이는 폭발 VFX 프리팹으로, ConfigureExplosionVisual()로 설정하지 않으면 시각효과 없이 데미지 판정만 동작합니다.
        private GameObject _explosionVfxPrefab;

        // 폭발 통계 디버그 구조체로, 릴리즈 빌드에서는 컴파일되지 않아 런타임 비용이 0입니다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public struct KRExplosionStats
        {
            public int RawColliderCount;

            public int ComponentLookupCount;

            public int DuplicateSkipCount;

            public int ActualHitCount;

            public int DeduplicationIterations;

            public Vector3 Center;

            public float Radius;
        }

        public static event System.Action<KRExplosionStats> OnExplosionDebugStats;
#endif

        public void Initialize(
            KRDamageType elementType,
            float damage,
            float speed,
            float gravityScale,
            int pierceCount,
            bool explodesOnImpact,
            float explosionRadius,
            float maxRange,
            IDamageable owner,
            LayerMask hitscanLayerMask,
            LayerMask explosionLayerMask)
        {
            _elementType = elementType;
            _damage = damage;
            _gravityScale = gravityScale;
            _pierceRemaining = pierceCount;
            _explodesOnImpact = explodesOnImpact;
            _explosionRadius = explosionRadius;
            _owner = owner;
            _hitscanLayerMask = hitscanLayerMask;
            _explosionLayerMask = explosionLayerMask;

            _velocity = transform.forward * speed;
            _previousPosition = transform.position;
            _remainingRange = maxRange;
            _elapsedLifetime = 0f;
            _initialized = true;
        }

        public void ConfigureHomingTracers(float radius, float interval, float tracerDamage,
            GameObject tracerVisualPrefab, Color tracerVisualColor)
        {
            _hasHomingTracers = true;
            _tracerRadius = radius;
            _tracerInterval = Mathf.Max(0.01f, interval);
            _tracerDamage = tracerDamage;
            _tracerVisualPrefab = tracerVisualPrefab;
            _tracerVisualColor = tracerVisualColor;
            _tracerTimer = 0f;
        }

        public void ConfigureExplosionVisual(GameObject vfxPrefab)
        {
            _explosionVfxPrefab = vfxPrefab;
        }

        private void Update()
        {
            if (!_initialized) return;

            _elapsedLifetime += Time.deltaTime;
            if (_elapsedLifetime >= _maxLifetimeSeconds) { Destroy(gameObject); return; }

            // 유도 추적탄은 투사체가 살아있는 모든 프레임에 주기적으로 동작합니다(이동 여부와 무관).
            if (_hasHomingTracers)
            {
                UpdateHomingTracers();
            }

            _velocity += Physics.gravity * (_gravityScale * Time.deltaTime);

            Vector3 displacement = _velocity * Time.deltaTime;
            float travelDistance = displacement.magnitude;
            if (travelDistance <= 0f) return;

            Vector3 direction = displacement / travelDistance;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 비행 경로를 씬/게임 뷰에서 확인할 수 있도록 매 프레임 궤적 선을 그리며, 색상은 수(水)=파랑, 금(金)=노랑, 나머지=흰색입니다.
            Color trailColor = _elementType == KRDamageType.Water ? Color.cyan :
                               _elementType == KRDamageType.Metal ? Color.yellow : Color.white;
            Debug.DrawLine(_previousPosition, _previousPosition + displacement, trailColor, 0.5f);
#endif

            int hitCount = Physics.RaycastNonAlloc(
                _previousPosition, direction, _raycastBuffer, travelDistance, _hitscanLayerMask);

            if (hitCount > 0)
            {
                int closestIndex = FindClosestHitIndex(hitCount);
                if (closestIndex >= 0)
                {
                    RaycastHit hit = _raycastBuffer[closestIndex];
                    bool destroyed = HandleImpact(hit.point, hit.collider);
                    if (destroyed) return;
                }
            }

            Vector3 nextPosition = _previousPosition + displacement;
            transform.position = nextPosition;
            _previousPosition = nextPosition;
            _remainingRange -= travelDistance;

            if (_remainingRange <= 0f)
            {
                if (_explodesOnImpact) Explode(nextPosition);
                Destroy(gameObject);
            }
        }

        private void UpdateHomingTracers()
        {
            _tracerTimer += Time.deltaTime;
            if (_tracerTimer < _tracerInterval) return;
            _tracerTimer = 0f;

            Vector3 origin = transform.position;

            // 1단계 — 브로드페이즈: Damageable 전용 마스크로 벽을 제외한 주변 후보를 탐지합니다.
            int count = Physics.OverlapSphereNonAlloc(origin, _tracerRadius, _tracerOverlapBuffer, _explosionLayerMask);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = KRManagers.Combat != null
                    ? KRManagers.Combat.Lookup(_tracerOverlapBuffer[i])
                    : _tracerOverlapBuffer[i].GetComponentInParent<IDamageable>();

                if (target == null || ReferenceEquals(target, _owner) || target.IsDead) continue;

                Vector3 toTarget = target.Position - origin;
                float distance = toTarget.magnitude;
                if (distance <= 0.0001f) continue;

                Vector3 direction = toTarget / distance;

                // 2단계 — 시야 확인: 벽 뒤 적은 맞지 않아야 하므로, Hitscan 마스크로 가장 먼저 맞는 대상이 target인지 검증합니다.
                int hitCount = Physics.RaycastNonAlloc(origin, direction, _raycastBuffer, distance, _hitscanLayerMask);
                int closestIndex = FindClosestHitIndex(hitCount);
                if (closestIndex < 0) continue;

                Collider hitCollider = _raycastBuffer[closestIndex].collider;
                IDamageable lineOfSightTarget = KRManagers.Combat != null
                    ? KRManagers.Combat.Lookup(hitCollider)
                    : hitCollider.GetComponentInParent<IDamageable>();

                // 첫 충돌이 의도한 target과 다르면(벽이나 다른 적에게 막힘) 이번 틱은 건너뜁니다.
                if (!ReferenceEquals(lineOfSightTarget, target)) continue;

                var context = new KRDamageContext(_tracerDamage, _elementType, target.Position, direction);
                target.TakeDamage(context);

                SpawnTracerVisual(origin, target.Position);
            }
        }

        private void SpawnTracerVisual(Vector3 origin, Vector3 targetPosition)
        {
            if (_tracerVisualPrefab == null) return;

            GameObject instance = Instantiate(_tracerVisualPrefab, Vector3.zero, Quaternion.identity);

            if (instance.TryGetComponent(out KRHitscanTracer tracer))
            {
                // KRHitscanTracer.Play()가 거리 비례 탄속 방식으로 바뀌어, 유도 추적탄은 빠른 탄속(400m/s)과 짧은 최대 길이(3m)를 직접 지정합니다.
                tracer.Play(origin, targetPosition, _tracerVisualColor, visualSpeedOverride: 400f, maxLengthOverride: 3f);
            }
            else
            {
                Destroy(instance); // 잘못된 프리팹이 할당된 경우 안전하게 정리합니다.
            }
        }

        private void SpawnExplosionVisual(Vector3 center)
        {
            if (_explosionVfxPrefab == null) return;

            GameObject instance = Instantiate(_explosionVfxPrefab, center, Quaternion.identity);

            if (instance.TryGetComponent(out ParticleSystem ps))
            {
                float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(instance, Mathf.Max(0.1f, lifetime));
            }
            else
            {
                Destroy(instance, 3f);
            }
        }

        private int FindClosestHitIndex(int hitCount)
        {
            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (_raycastBuffer[i].distance < closestDistance)
                {
                    closestDistance = _raycastBuffer[i].distance;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }

        private bool HandleImpact(Vector3 hitPoint, Collider hitCollider)
        {
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();

            if (target == null)
            {
                if (_explodesOnImpact) Explode(hitPoint);
                Destroy(gameObject);
                return true;
            }

            if (ReferenceEquals(target, _owner) || target.IsDead) return false;

            var context = new KRDamageContext(_damage, _elementType, hitPoint, _velocity.normalized);
            target.TakeDamage(context);

            if (_pierceRemaining > 0) { _pierceRemaining--; return false; }

            if (_explodesOnImpact) Explode(hitPoint);
            Destroy(gameObject);
            return true;
        }

        private void Explode(Vector3 center)
        {
            // 실제 데미지 판정과 무관하게, 누구를 맞췄는지와 별개로 폭발 시각효과는 항상 재생합니다.
            SpawnExplosionVisual(center);

            // 브로드페이즈: Damageable 전용 마스크로 후보 수 선제 제한
            int count = Physics.OverlapSphereNonAlloc(
                center, _explosionRadius, _overlapBuffer, _explosionLayerMask);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DrawDebugSphere(center, _explosionRadius, Color.red, duration: 3f);
            var stats = new KRExplosionStats
            {
                RawColliderCount = count,
                Center = center,
                Radius = _explosionRadius
            };
#endif

            // 중복 제거용 인스턴스 ID 마킹 배열로, static이라 힙 할당 없이 재사용되며 O(n) 단일 패스로 중복을 식별합니다.
            int handledCount = 0;

            for (int i = 0; i < count; i++)
            {
                // ① O(1) 캐시 조회 — GetComponentInParent 완전 제거
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                stats.ComponentLookupCount++;
#endif
                IDamageable target = KRManagers.Combat != null
                    ? KRManagers.Combat.Lookup(_overlapBuffer[i])
                    : _overlapBuffer[i].GetComponentInParent<IDamageable>(); // 캐시 미등록 폴백

                if (target == null || ReferenceEquals(target, _owner) || target.IsDead) continue;

                // ② O(n) 선형 마킹 중복 검사 — O(n²) 이중 루프 대체
                int instanceId = target.GetHashCode();
                bool isDuplicate = false;
                for (int j = 0; j < handledCount; j++)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    stats.DeduplicationIterations++;
#endif
                    if (_handledInstanceIds[j] == instanceId) { isDuplicate = true; break; }
                }

                if (isDuplicate)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    stats.DuplicateSkipCount++;
                    Debug.DrawLine(center, _overlapBuffer[i].bounds.center, Color.yellow, 3f);
#endif
                    continue;
                }

                // 처리 완료 마킹 — 배열 범위 초과 방지
                if (handledCount < _handledInstanceIds.Length)
                    _handledInstanceIds[handledCount++] = instanceId;

                float distance = Vector3.Distance(center, target.Position);
                float clampedRatio = Mathf.Clamp01(distance / Mathf.Max(0.0001f, _explosionRadius));
                float finalDamage = _damage * (1f - clampedRatio);

                if (finalDamage <= 0f) continue;

                Vector3 dir = (target.Position - center).normalized;
                var context = new KRDamageContext(finalDamage, _elementType, center, dir);
                target.TakeDamage(context);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                stats.ActualHitCount++;
                float alpha = 1f - clampedRatio;
                Debug.DrawLine(center, target.Position,
                    new Color(0f, 1f, 0f, Mathf.Max(0.3f, alpha)), 3f);
#endif
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            OnExplosionDebugStats?.Invoke(stats);
#endif
        }

        // Physics.DrawWireSphere가 없어 Debug.DrawLine 8선으로 구(球)를 근사하는 유틸리티입니다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void DrawDebugSphere(Vector3 center, float radius, Color color, float duration)
        {
            const int segments = 16;
            float step = 360f / segments * Mathf.Deg2Rad;

            for (int i = 0; i < segments; i++)
            {
                float a0 = step * i;
                float a1 = step * (i + 1);

                Debug.DrawLine(
                    center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                    center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius,
                    color, duration);

                Debug.DrawLine(
                    center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius,
                    center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius,
                    color, duration);

                Debug.DrawLine(
                    center + new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                    center + new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                    color, duration);
            }
        }
#endif
    }
}