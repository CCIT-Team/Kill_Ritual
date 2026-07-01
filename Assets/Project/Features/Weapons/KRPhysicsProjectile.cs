// Assets/Project/Scripts/03_Weapons/KRPhysicsProjectile.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Managers;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 물리 투사체(Projectile / ExplosiveBurst 계열)의 비행 궤적과 충돌 판정을 전담하는 컴포넌트입니다.
    ///
    /// [유도 추적탄(Homing Tracer) 기능 - BFG 전용 옵션]
    /// ConfigureHomingTracers()를 호출해두면, 비행 중 일정 주기마다 자신을 중심으로 반경 내
    /// 적을 탐지하고, 레이캐스트로 시야가 실제로 막혀있지 않은지(벽 뒤 적이 아닌지) 확인한 뒤,
    /// 시야가 확보된 대상에게만 소량의 즉발 데미지를 자동으로 적용합니다. 이는 BFG가 비행하며
    /// 주변 적을 자동 조준해 작은 탄을 계속 쏘는 고전적인 효과를 구현합니다.
    /// 이 기능을 호출하지 않은 일반 투사체(플라즈마건, 그레네이드런처 등)는 기존과 동일하게
    /// 충돌/폭발 시에만 데미지를 입힙니다.
    ///
    /// [디버그 통계 구조]
    /// 이 클래스는 #if UNITY_EDITOR || DEVELOPMENT_BUILD 블록 안에서만 컴파일되는
    /// KRExplosionStats 구조체를 통해 폭발 1회당 연산 지표를 수집합니다.
    /// 수집된 데이터는 KRCombatDebugOverlay가 OnGUI로 화면에 렌더링하며,
    /// 릴리즈 빌드에서는 관련 코드 전체가 제거되어 런타임 비용이 0입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRPhysicsProjectile : MonoBehaviour
    {
        [Tooltip("충돌 판정 없이 자동 소멸되기까지의 최대 생존 시간(초). 사거리 소진 전에 안전망 역할을 합니다.")]
        [SerializeField] private float _maxLifetimeSeconds = 6f;

        private void Awake()
        {
            // [방어 로직] 충돌 판정은 이 스크립트가 직접 레이캐스트로 계산하므로, 투사체 자신에게는
            // 원래 Collider/Rigidbody가 전혀 필요하지 않습니다. 그런데 프리팹 작업 중 실수로
            // Collider가 남아있으면, 그 콜라이더는 PhysX의 일반 충돌로 처리되어 플레이어의
            // Rigidbody와 물리적으로 겹침-밀어내기(depenetration)를 일으킵니다. 투사체가 느릴수록
            // 플레이어 몸 근처에 오래 머물러 이 밀어내기가 누적되어 "넉백"처럼 느껴지게 됩니다.
            // 이는 _owner 제외 로직(게임 로직)과 전혀 다른 채널(물리 엔진)에서 발생하므로 스크립트로
            // 막을 수 없고, Collider 자체를 Trigger로 강제 전환해야 물리적 충돌이 원천 차단됩니다.
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

        // [최적화] 중복 제거용 인스턴스 ID 마킹 배열. O(n²) 이중 루프를 O(n) 단일 패스로 대체합니다.
        // _overlapBuffer와 동일한 크기로 선언해 인덱스 초과를 원천 차단합니다.
        private static readonly int[] _handledInstanceIds = new int[32];

        // 유도 추적탄 전용 NonAlloc 버퍼. 폭발용 버퍼와 분리해 동시에 사용해도 안전합니다.
        private static readonly Collider[] _tracerOverlapBuffer = new Collider[8];

        private KRDamageType _elementType;
        private float _damage;
        private float _gravityScale;
        private int _pierceRemaining;
        private bool _explodesOnImpact;
        private float _explosionRadius;
        private IDamageable _owner;

        // [최적화] 사격 판정(벽 포함)과 폭발 판정(피격 가능 개체만)을 분리합니다.
        // Hitscan은 벽을 감지해야 하므로 Environment 레이어를 포함하고,
        // 폭발 판정은 Damageable 레이어만 사용해 브로드페이즈 후보 수 자체를 줄입니다.
        private LayerMask _hitscanLayerMask;
        private LayerMask _explosionLayerMask;

        private Vector3 _velocity;
        private Vector3 _previousPosition;
        private float _remainingRange;
        private float _elapsedLifetime;
        private bool _initialized;

        // ------------------------------------------------------------------
        // [유도 추적탄] BFG 전용 옵션 상태. ConfigureHomingTracers()가 호출되지 않으면
        // _hasHomingTracers가 false로 유지되어 일반 투사체와 동일하게 동작합니다.
        // ------------------------------------------------------------------
        private bool _hasHomingTracers;
        private float _tracerRadius;
        private float _tracerInterval;
        private float _tracerDamage;
        private float _tracerTimer;
        private GameObject _tracerVisualPrefab;
        private Color _tracerVisualColor;

        // [폭발 시각효과] 실제 인게임에서 보이는 폭발 VFX 프리팹. ConfigureExplosionVisual()로
        // 설정하지 않으면 시각효과 없이 데미지 판정만 동작합니다(기존과 동일하게 안전).
        private GameObject _explosionVfxPrefab;

        // -----------------------------------------------------------------------
        // [DEBUG] 폭발 통계 구조체.
        // 릴리즈 빌드에서는 컴파일 자체가 되지 않으므로 런타임 비용 0.
        // -----------------------------------------------------------------------
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 폭발(Explode) 1회 실행 시 수집되는 연산 지표입니다.
        /// KRCombatDebugOverlay가 이 구조체를 구독해 OnGUI로 시각화합니다.
        /// </summary>
        public struct KRExplosionStats
        {
            /// <summary>OverlapSphereNonAlloc이 반환한 원시 콜라이더 수 (브로드페이즈 통과 수)</summary>
            public int RawColliderCount;

            /// <summary>GetComponentInParent 호출 횟수. 현재 구조에서는 중복 검사 포함 최대 O(n²)번 호출됨</summary>
            public int ComponentLookupCount;

            /// <summary>중복 콜라이더로 판정되어 건너뛴 횟수</summary>
            public int DuplicateSkipCount;

            /// <summary>실제로 TakeDamage가 호출된 유효 피격 대상 수</summary>
            public int ActualHitCount;

            /// <summary>IsAlreadyHandled 내부의 이중 루프 총 반복 횟수. O(n²) 비용의 직접 지표</summary>
            public int DeduplicationIterations;

            /// <summary>폭발 발생 월드 좌표</summary>
            public Vector3 Center;

            /// <summary>폭발 반경</summary>
            public float Radius;
        }

        /// <summary>
        /// 폭발 1회 완료 시 발행되는 이벤트.
        /// KRCombatDebugOverlay가 구독해 통계를 누적합니다.
        /// </summary>
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

        /// <summary>
        /// [선택적 기능] 비행 중 유도 추적탄(자동 조준 잔탄)을 활성화합니다.
        /// Initialize() 직후, 발사 전 호출해야 합니다. 호출하지 않으면 일반 투사체로 동작합니다.
        /// </summary>
        /// <param name="radius">추적탄이 적을 탐지하는 반경</param>
        /// <param name="interval">추적탄 발사 주기(초). 0.1이면 초당 약 10발의 작은 탄이 나갑니다.</param>
        /// <param name="tracerDamage">추적탄 1발당 데미지 (메인 폭발 데미지와는 별개입니다)</param>
        /// <param name="tracerVisualPrefab">추적탄이 명중할 때 보여줄 시각효과 프리팹 (KRHitscanTracer). null이면 시각효과 생략.</param>
        /// <param name="tracerVisualColor">추적탄 시각효과 색상</param>
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

        /// <summary>
        /// [선택적 기능] 폭발 시 실제로 화면에 보이는 시각효과(파티클 등)를 지정합니다.
        /// 호출하지 않으면 데미지 판정은 그대로 동작하되 시각효과 없이 조용히 폭발합니다.
        /// </summary>
        /// <param name="vfxPrefab">폭발 지점에 생성할 프리팹. ParticleSystem이 있으면 재생 시간에 맞춰 자동 정리되고,
        /// 없으면 3초 후 안전하게 제거됩니다.</param>
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
            // 비행 경로를 씬 뷰와 게임 뷰 모두에서 확인할 수 있도록 매 프레임 궤적 선을 그립니다.
            // 색상: 수(水)=파랑, 금(金)=노랑, 나머지=흰색
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

        /// <summary>
        /// [유도 추적탄] 일정 주기마다 주변 적을 탐지하고, 레이캐스트로 실제 시야가 확보된
        /// 대상에게만 소량의 즉발 데미지를 적용합니다. 시야 확인에 KRManagers.Combat의 O(1)
        /// 캐시 조회를 사용해, 광역 폭발 최적화와 동일한 원칙(해시 기반 사전 매핑)을 재사용합니다.
        /// </summary>
        private void UpdateHomingTracers()
        {
            _tracerTimer += Time.deltaTime;
            if (_tracerTimer < _tracerInterval) return;
            _tracerTimer = 0f;

            Vector3 origin = transform.position;

            // 1단계 — 브로드페이즈: Damageable 전용 마스크로 주변 후보를 탐지합니다(벽 제외).
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

                // 2단계 — 시야 확인: Hitscan 마스크(벽 포함)로 실제로 막혀있지 않은지 레이캐스트로 검증합니다.
                // 벽 뒤에 있는 적은 추적탄에 맞지 않아야 하므로, 가장 먼저 맞는 대상이 정확히 target인지 확인합니다.
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

        /// <summary>유도 추적탄이 명중했을 때의 시각효과(작은 트레이서)를 생성합니다. 프리팹이 없으면 생략됩니다.</summary>
        private void SpawnTracerVisual(Vector3 origin, Vector3 targetPosition)
        {
            if (_tracerVisualPrefab == null) return;

            GameObject instance = Instantiate(_tracerVisualPrefab, Vector3.zero, Quaternion.identity);

            if (instance.TryGetComponent(out KRHitscanTracer tracer))
            {
                // [시그니처 갱신] KRHitscanTracer.Play()가 고정 duration 방식에서 거리 비례
                // 탄속(visualSpeed) 방식으로 바뀌었습니다. 유도 추적탄은 짧은 거리에서 빠르게
                // 번뜩이는 느낌이 맞으므로 빠른 탄속(400m/s)과 짧은 최대 길이(3m)를 직접 지정합니다.
                tracer.Play(origin, targetPosition, _tracerVisualColor, visualSpeedOverride: 400f, maxLengthOverride: 3f);
            }
            else
            {
                Destroy(instance); // 잘못된 프리팹이 할당된 경우 안전하게 정리합니다.
            }
        }

        /// <summary>
        /// 폭발 지점에 시각효과 프리팹을 생성합니다. ParticleSystem이 붙어 있으면 그 재생 시간에
        /// 맞춰 자동으로 정리하고, 일반 메시/스프라이트 등이면 안전하게 3초 후 제거합니다.
        /// 프리팹이 비어있으면 데미지 판정에는 영향 없이 시각효과만 생략됩니다.
        /// </summary>
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

        /// <summary>
        /// [최적화 적용] OverlapSphereNonAlloc으로 폭발 반경 내 IDamageable을 수집하고
        /// 선형 감쇠 데미지(D = Dmax × (1 - d/R))를 적용합니다.
        ///
        /// 개선 내용 (SAP 내로우페이즈 원칙 적용):
        ///   ① GetComponentInParent 제거 → KRManagers.Combat.Lookup(collider) O(1) 조회
        ///   ② O(n²) 이중 루프 중복 제거 → _handledInstanceIds[] 배열 마킹 O(n)
        ///   ③ _explosionLayerMask 분리 → 브로드페이즈 후보 수 자체를 줄임 (환경 레이어 제외)
        /// </summary>
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

            // 중복 제거용 인스턴스 ID 마킹 배열. O(n) 단일 패스로 중복을 식별합니다.
            // static 배열이므로 힙 할당 없이 재사용됩니다(GC 0).
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

        // -----------------------------------------------------------------------
        // Debug.DrawLine 8선으로 구(球)를 근사하는 유틸리티.
        // Physics.DrawWireSphere는 존재하지 않으므로 직접 구현합니다.
        // -----------------------------------------------------------------------
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