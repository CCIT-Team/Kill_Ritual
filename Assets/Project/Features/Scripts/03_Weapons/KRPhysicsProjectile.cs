// Assets/Project/Scripts/03_Weapons/KRPhysicsProjectile.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 물리 투사체(Projectile / ExplosiveBurst 계열)의 비행 궤적과 충돌 판정을 전담하는 컴포넌트입니다.
    /// 현재 수(水) 속성 두 모드(등속 플라즈마 소총 / 관통형 플라즈마)와 금(金) 속성 BFG가 이 클래스를
    /// 공유해서 사용하며, GravityScale 파라미터 하나로 "등속 직선" ↔ "포물선" 운동을 모두 표현할 수
    /// 있도록 범용적으로 설계했습니다(필요 시 화(火) 계열에도 동일하게 재사용 가능).
    ///
    /// Rigidbody/PhysX의 내장 Continuous Collision Detection에 의존하지 않고, 매 프레임 직접
    /// 위치를 적분(수동 운동학적 이동)한 뒤 이전 위치→다음 위치 구간을 RaycastNonAlloc으로
    /// 스윕(Sweep)하는 "수동 CCD"를 구현하여, 고속 비행 시 얇은 콜라이더를 관통해버리는
    /// 터널링 현상을 원천 차단합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRPhysicsProjectile : MonoBehaviour
    {
        [Tooltip("충돌 판정 없이 자동 소멸되기까지의 최대 생존 시간(초). 사거리 소진 전에 안전망 역할을 합니다.")]
        [SerializeField] private float _maxLifetimeSeconds = 6f;

        // NonAlloc 공용 버퍼. Unity 메인 스레드는 단일 스레드 순차 실행이므로, 여러 투사체 인스턴스가
        // 동시에(같은 프레임 내에서) Update를 호출받아도 한 인스턴스의 버퍼 사용이 완전히 끝난 뒤
        // 다음 인스턴스의 Update가 실행되기 때문에 static 공유 버퍼가 안전합니다.
        private static readonly RaycastHit[] _raycastBuffer = new RaycastHit[8];
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        private KRDamageType _elementType;
        private float _damage;
        private float _gravityScale;
        private int _pierceRemaining;
        private bool _explodesOnImpact;
        private float _explosionRadius;
        private IDamageable _owner;
        private LayerMask _damageableLayerMask;

        private Vector3 _velocity;
        private Vector3 _previousPosition;
        private float _remainingRange;
        private float _elapsedLifetime;
        private bool _initialized;

        /// <summary>
        /// 발사 직후 KRCombatSystem이 호출하여 투사체의 모든 거동 파라미터를 주입합니다.
        /// </summary>
        /// <param name="elementType">데미지 속성 (오행 중 하나)</param>
        /// <param name="damage">명중/폭발 1회당 기본 데미지 (폭발의 경우 거리 감쇠 적용 전 최대값)</param>
        /// <param name="speed">초기 비행 속도 (미터/초)</param>
        /// <param name="gravityScale">0 = 완전한 등속 직선 운동, 0보다 크면 포물선 운동</param>
        /// <param name="pierceCount">관통 가능 횟수. 0이면 첫 명중에서 즉시 소멸</param>
        /// <param name="explodesOnImpact">true면 충돌(또는 사거리 소진) 시 광역 폭발 데미지를 발생시킴</param>
        /// <param name="explosionRadius">광역 폭발 반경</param>
        /// <param name="maxRange">최대 비행 거리. 이 거리를 모두 소진하면 충돌이 없어도 폭발/소멸합니다.</param>
        /// <param name="owner">발사 주체. 자기 자신에게는 데미지가 들어가지 않도록 비교에 사용됩니다.</param>
        /// <param name="damageableLayerMask">충돌 판정 대상 레이어 마스크</param>
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
            LayerMask damageableLayerMask)
        {
            _elementType = elementType;
            _damage = damage;
            _gravityScale = gravityScale;
            _pierceRemaining = pierceCount;
            _explodesOnImpact = explodesOnImpact;
            _explosionRadius = explosionRadius;
            _owner = owner;
            _damageableLayerMask = damageableLayerMask;

            _velocity = transform.forward * speed;
            _previousPosition = transform.position;
            _remainingRange = maxRange;
            _elapsedLifetime = 0f;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            _elapsedLifetime += Time.deltaTime;

            if (_elapsedLifetime >= _maxLifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            // --- 1) 등가속도(중력) 적용: GravityScale이 0이면 속도 벡터가 전혀 변하지 않아 완전한 등속 직선 운동이 됩니다.
            _velocity += Physics.gravity * (_gravityScale * Time.deltaTime);

            Vector3 displacement = _velocity * Time.deltaTime;
            float travelDistance = displacement.magnitude;

            if (travelDistance <= 0f)
            {
                return;
            }

            Vector3 direction = displacement / travelDistance;

            // --- 2) 수동 CCD Swept Raycast: 이전 위치(P_prev)에서 다음 위치(P_next) 방향으로
            // 변위 거리만큼 레이저 스캔을 수행하여, 한 프레임 안에서 발생할 수 있는 터널링을 방지합니다.
            int hitCount = Physics.RaycastNonAlloc(_previousPosition, direction, _raycastBuffer, travelDistance, _damageableLayerMask);

            if (hitCount > 0)
            {
                int closestIndex = FindClosestHitIndex(hitCount);

                if (closestIndex >= 0)
                {
                    RaycastHit hit = _raycastBuffer[closestIndex];
                    bool destroyed = HandleImpact(hit.point, hit.collider);

                    if (destroyed)
                    {
                        // 이번 프레임의 위치 갱신 없이 즉시 종료 (충돌 지점에서 폭발/소멸 처리가 이미 완료됨).
                        return;
                    }
                }
            }

            Vector3 nextPosition = _previousPosition + displacement;
            transform.position = nextPosition;
            _previousPosition = nextPosition;
            _remainingRange -= travelDistance;

            // --- 3) 사거리 소진: 아무것도 맞추지 못한 채 최대 사거리에 도달하면, 폭발형은 그 자리에서 터지고
            // 비폭발형은 조용히 소멸합니다.
            if (_remainingRange <= 0f)
            {
                if (_explodesOnImpact)
                {
                    Explode(nextPosition);
                }

                Destroy(gameObject);
            }
        }

        /// <summary>NonAlloc 버퍼 안에서 가장 가까운(거리값이 가장 작은) 충돌의 인덱스를 찾습니다.</summary>
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

        /// <summary>
        /// 충돌 지점에서의 처리를 수행합니다.
        /// 반환값이 true이면 이 투사체는 이번 프레임에 파괴(소멸)되었다는 뜻이며,
        /// false이면 관통이 성공하여 비행을 계속한다는 뜻입니다.
        /// </summary>
        private bool HandleImpact(Vector3 hitPoint, Collider hitCollider)
        {
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();

            // 환경(벽 등 IDamageable이 없는 콜라이더)에 부딪힌 경우: 관통 불가, 즉시 폭발/소멸.
            if (target == null)
            {
                if (_explodesOnImpact)
                {
                    Explode(hitPoint);
                }

                Destroy(gameObject);
                return true;
            }

            // 발사 주체 자기 자신이거나 이미 사망한 대상은 충돌을 무시하고 그대로 통과시킵니다
            // (이번 프레임 위치 갱신은 호출부의 Update가 정상적으로 이어서 진행합니다).
            if (ReferenceEquals(target, _owner) || target.IsDead)
            {
                return false;
            }

            var context = new KRDamageContext(_damage, _elementType, hitPoint, _velocity.normalized);
            target.TakeDamage(context);

            // 관통 가능 횟수가 남아있다면 소멸하지 않고 횟수만 차감한 뒤 비행을 계속합니다.
            if (_pierceRemaining > 0)
            {
                _pierceRemaining--;
                return false;
            }

            if (_explodesOnImpact)
            {
                Explode(hitPoint);
            }

            Destroy(gameObject);
            return true;
        }

        /// <summary>
        /// OverlapSphereNonAlloc으로 폭발 반경 내 모든 IDamageable을 찾아, 중심으로부터의 거리에
        /// 비례한 선형 감쇠 데미지(D = Dmax × (1 - d/R))를 적용합니다.
        /// HashSet 등 힙 할당 없이, 작은 고정 크기 버퍼 내에서 단순 선형 중복 검사로 0 GC를 유지합니다.
        /// </summary>
        private void Explode(Vector3 center)
        {
            int count = Physics.OverlapSphereNonAlloc(center, _explosionRadius, _overlapBuffer, _damageableLayerMask);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = _overlapBuffer[i].GetComponentInParent<IDamageable>();

                if (target == null || ReferenceEquals(target, _owner) || target.IsDead)
                {
                    continue;
                }

                // 같은 대상이 여러 콜라이더(예: 신체 부위별 콜라이더)로 버퍼에 중복 등재되어
                // 중복 피해를 입지 않도록, 자신보다 앞선 인덱스들 중 동일 대상이 있었는지 검사합니다.
                if (IsAlreadyHandled(target, i))
                {
                    continue;
                }

                float distance = Vector3.Distance(center, target.Position);
                float clampedRatio = Mathf.Clamp01(distance / Mathf.Max(0.0001f, _explosionRadius));
                float finalDamage = _damage * (1f - clampedRatio);

                if (finalDamage <= 0f)
                {
                    continue;
                }

                Vector3 direction = (target.Position - center).normalized;
                var context = new KRDamageContext(finalDamage, _elementType, center, direction);
                target.TakeDamage(context);
            }
        }

        private bool IsAlreadyHandled(IDamageable target, int currentIndex)
        {
            for (int j = 0; j < currentIndex; j++)
            {
                IDamageable prior = _overlapBuffer[j].GetComponentInParent<IDamageable>();

                if (ReferenceEquals(prior, target))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
