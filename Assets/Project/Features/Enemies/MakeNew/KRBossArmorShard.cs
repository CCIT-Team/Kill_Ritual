// Assets/Project/Features/Enemies/MakeNew/KRBossArmorShard.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Managers;

namespace KillRitual.Enemies
{
    /// <summary>
    /// [2026-07-07 신규] 불가살이 보스의 "철갑 발사"(1페이즈 패턴1) 전용 투사체입니다.
    ///
    /// 플레이어 무기의 KRPhysicsProjectile(Assets/Project/Features/Weapons/KRPhysicsProjectile.cs)과
    /// 같은 이유로 Rigidbody 물리 충돌 대신 레이캐스트로 직접 이동을 계산합니다 — Collider가 있는
    /// 채로 물리 충돌을 켜두면 PhysX가 플레이어를 밀어내는 부작용이 생기기 때문입니다(그 파일
    /// 29~40번째 줄 주석 참고). 다만 동작이 달라서 그 스크립트를 재사용하지 않고 새로 만들었습니다:
    /// KRPhysicsProjectile은 "맞으면 즉시 터지거나 사라짐"인데, 이 철갑 조각은 기획상
    /// "바닥/벽에 꽂혀서 잠시 남아있다가(2페이즈에서는 그 후 폭발)"는 동작이 필요합니다.
    /// </summary>
    public sealed class KRBossArmorShard : MonoBehaviour
    {
        private static readonly RaycastHit[] _raycastBuffer = new RaycastHit[4];

        private Vector3 _velocity;
        private float _damage;
        private bool _willExplode;
        private float _explodeDelay;
        private float _explosionRadius;
        private LayerMask _hitLayerMask;
        private LayerMask _damageableLayerMask;
        private IDamageable _owner;

        private bool _stuck;
        private Vector3 _previousPosition;

        /// <summary>
        /// 발사 직후 보스 컨트롤러가 호출해 이 투사체를 초기화합니다.
        /// </summary>
        /// <param name="velocity">초기 속도(방향 포함).</param>
        /// <param name="damage">플레이어를 직접 맞췄을 때(또는 폭발 시) 주는 피해량.</param>
        /// <param name="hitLayerMask">비행 중 충돌을 감지할 레이어(플레이어+환경 포함).</param>
        /// <param name="damageableLayerMask">폭발 판정에 쓸 레이어(피격 가능 대상만).</param>
        /// <param name="owner">발사한 주체(보스 자기 자신에게는 맞지 않도록 제외).</param>
        /// <param name="willExplode">true면 바닥/벽에 꽂힌 뒤 explodeDelay초 후 폭발합니다(2페이즈용).</param>
        public void Launch(Vector3 velocity, float damage, LayerMask hitLayerMask, LayerMask damageableLayerMask,
            IDamageable owner, bool willExplode = false, float explodeDelay = 1.5f, float explosionRadius = 2.5f)
        {
            _velocity = velocity;
            _damage = damage;
            _hitLayerMask = hitLayerMask;
            _damageableLayerMask = damageableLayerMask;
            _owner = owner;
            _willExplode = willExplode;
            _explodeDelay = explodeDelay;
            _explosionRadius = explosionRadius;
            _previousPosition = transform.position;

            if (velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }

        private void Update()
        {
            if (_stuck) return;

            Vector3 displacement = _velocity * Time.deltaTime;
            float distance = displacement.magnitude;
            if (distance <= 0f) return;

            int hitCount = Physics.RaycastNonAlloc(
                _previousPosition, displacement.normalized, _raycastBuffer, distance, _hitLayerMask);

            if (hitCount > 0)
            {
                RaycastHit closest = _raycastBuffer[0];
                for (int i = 1; i < hitCount; i++)
                {
                    if (_raycastBuffer[i].distance < closest.distance) closest = _raycastBuffer[i];
                }

                HandleHit(closest.point, closest.collider);
                return;
            }

            Vector3 next = _previousPosition + displacement;
            transform.position = next;
            _previousPosition = next;
        }

        private void HandleHit(Vector3 point, Collider hitCollider)
        {
            IDamageable target = KRManagers.Combat != null
                ? KRManagers.Combat.Lookup(hitCollider)
                : hitCollider.GetComponentInParent<IDamageable>();

            if (target != null && !ReferenceEquals(target, _owner) && !target.IsDead)
            {
                // 플레이어(또는 다른 피격 대상)를 직접 맞췄으면 즉시 피해를 주고 사라집니다.
                var context = new KRDamageContext(_damage, KRDamageType.Metal, point, _velocity.normalized);
                target.TakeDamage(context);
                Destroy(gameObject);
                return;
            }

            // 바닥/벽 등 피격 대상이 아닌 것에 맞았으면 그 자리에 박혀서 남습니다.
            transform.position = point;
            _stuck = true;

            if (_willExplode)
                Invoke(nameof(Explode), _explodeDelay);
            else
                Destroy(gameObject, 3f); // 안 터지는 버전(1페이즈)은 잠시 후 조용히 정리
        }

        /// <summary>2페이즈 전용 — 꽂힌 자리에서 지연 폭발해 주변에 광역 피해를 줍니다.</summary>
        private void Explode()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _explosionRadius, _damageableLayerMask);

            foreach (Collider col in hits)
            {
                IDamageable target = KRManagers.Combat != null
                    ? KRManagers.Combat.Lookup(col)
                    : col.GetComponentInParent<IDamageable>();

                if (target == null || ReferenceEquals(target, _owner) || target.IsDead) continue;

                Vector3 direction = (target.Position - transform.position).normalized;
                var context = new KRDamageContext(_damage, KRDamageType.Metal, transform.position, direction);
                target.TakeDamage(context);
            }

            Destroy(gameObject);
        }
    }
}
