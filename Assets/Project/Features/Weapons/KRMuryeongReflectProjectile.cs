// Assets/Project/Scripts/05_Enemies/Projectiles/KRMuryeongProjectile.cs
using System.Collections.Generic;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using UnityEngine;

namespace KillRitual.Enemies.Projectiles
{
    [DisallowMultipleComponent]
    public sealed class KRMuryeongProjectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _speed = 45f;
        [SerializeField] private float _lifeTime = 3f;

        [Header("Arming")]
        [Tooltip("발사 직후 이 시간 동안은 충돌/폭발 판정을 하지 않아, 발사 위치 주변 콜라이더에 바로 맞아버리는 것을 방지합니다.")]
        [Min(0f)]
        [SerializeField] private float _armDelay = 0.05f;

        [Tooltip("발사 위치에서 이 거리만큼 이동하기 전까지는 충돌/폭발 판정을 하지 않습니다.")]
        [Min(0f)]
        [SerializeField] private float _armDistance = 0.6f;

        [Header("Hit Check")]
        [Tooltip("무령탄이 충돌할 수 있는 레이어로, Enemy/Boss/Ground 등을 포함하고 Player/Projectile/EnemyProjectile은 제외해야 합니다.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Tooltip("얇은 투사체가 작은 틈을 그냥 통과해버리지 않도록 하는 SphereCast 반경입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _hitRadius = 0.35f;

        [Header("Damage")]
        [Tooltip("무령탄의 데미지로, 직격 타격과 폭발 피해 모두 이 값을 그대로 사용합니다.")]
        [Min(0f)]
        [SerializeField] private float _damage = 30f;

        [SerializeField] private KRDamageType _damageType = KRDamageType.Fire;

        [Header("Explosion")]
        [Tooltip("켜면 명중 시 직격 대신 범위 폭발 피해를 입힙니다.")]
        [SerializeField] private bool _explodeOnHit = true;

        [Tooltip("폭발 반경으로, Explode On Hit가 켜져 있을 때만 적용됩니다.")]
        [Min(0f)]
        [SerializeField] private float _explosionRadius = 2.5f;

        [Tooltip("폭발 피해를 받는 대상 레이어로, 보통 Enemy/Boss를 넣고 Ground는 제외합니다.")]
        [SerializeField] private LayerMask _explosionDamageMask = ~0;

        [Tooltip("폭발 시각효과 프리팹으로, 비워두면 생성되지 않습니다.")]
        [SerializeField] private GameObject _explosionVfxPrefab;

        [Tooltip("폭발 VFX 자동 파괴 시간으로, 파티클이 스스로 사라지면 0으로 두어도 됩니다.")]
        [Min(0f)]
        [SerializeField] private float _explosionVfxLifeTime = 2f;

        [Header("Destroy")]
        [SerializeField] private bool _destroyOnHit = true;

        private static readonly RaycastHit[] HitResults = new RaycastHit[8];
        private static readonly Collider[] ExplosionBuffer = new Collider[32];

        private readonly HashSet<IDamageable> _damagedTargets = new HashSet<IDamageable>();

        private Transform _owner;
        private Vector3 _direction;
        private Vector3 _spawnPosition;
        private float _spawnTime;

        private bool _isInitialized;
        private bool _isDestroyed;

        public void Initialize(Vector3 direction, Transform owner)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;

            _direction = direction.normalized;
            _owner = owner;
            _spawnPosition = transform.position;
            _spawnTime = Time.time;

            _isInitialized = true;
            _isDestroyed = false;

            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }

        private void Update()
        {
            if (!_isInitialized || _isDestroyed)
                return;

            float moveDistance = _speed * Time.deltaTime;
            Vector3 startPosition = transform.position;
            Vector3 nextPosition = startPosition + _direction * moveDistance;

            // 착탄 판정이 아직 활성화되지 않은 동안에는 SphereCast 없이 이동만 시킵니다.
            if (!IsArmed())
            {
                transform.position = nextPosition;
                CheckLifeTime();
                return;
            }

            if (CheckHit(startPosition, moveDistance))
                return;

            transform.position = nextPosition;

            CheckLifeTime();
        }

        private bool IsArmed()
        {
            if (_armDelay > 0f && Time.time - _spawnTime < _armDelay)
                return false;

            if (_armDistance > 0f)
            {
                float traveledDistance = Vector3.Distance(_spawnPosition, transform.position);

                if (traveledDistance < _armDistance)
                    return false;
            }

            return true;
        }

        private void CheckLifeTime()
        {
            if (Time.time - _spawnTime >= _lifeTime)
                DestroyProjectile();
        }

        private bool CheckHit(Vector3 origin, float distance)
        {
            Ray ray = new Ray(origin, _direction);

            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                _hitRadius,
                HitResults,
                distance,
                _hitMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
                return false;

            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = HitResults[i];

                if (hit.collider == null)
                    continue;

                if (_owner != null && hit.transform.IsChildOf(_owner))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestIndex = i;
                    bestDistance = hit.distance;
                }
            }

            if (bestIndex < 0)
                return false;

            RaycastHit bestHit = HitResults[bestIndex];
            HandleImpact(bestHit);

            return true;
        }

        private void HandleImpact(RaycastHit hit)
        {
            Vector3 impactPoint = hit.point;

            if (impactPoint == Vector3.zero)
                impactPoint = transform.position;

            // 폭발은 직접 타격 데미지가 아닌 별도 범위 처리로, 명중 대상 하나만 보지 않고 범위 안 모든 대상에게 피해를 줍니다.
            if (_explodeOnHit && _explosionRadius > 0f)
            {
                SpawnExplosionVfx(impactPoint, hit.normal);
                ApplyExplosionDamage(impactPoint);
            }
            else
            {
                ApplyDirectDamage(hit.collider, impactPoint);
            }

            if (_destroyOnHit)
                DestroyProjectile();
        }

        private void ApplyDirectDamage(Collider hitCollider, Vector3 hitPoint)
        {
            if (hitCollider == null)
                return;

            if (_owner != null && hitCollider.transform.IsChildOf(_owner))
                return;

            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();

            if (target == null)
                return;

            if (target.IsDead)
                return;

            KRDamageContext context = new KRDamageContext(
                _damage,
                _damageType,
                hitPoint,
                _direction,
                isMuryeongReflected: true);

            target.TakeDamage(context);
        }

        private void ApplyExplosionDamage(Vector3 center)
        {
            _damagedTargets.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                center,
                _explosionRadius,
                ExplosionBuffer,
                _explosionDamageMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = ExplosionBuffer[i];

                if (hit == null)
                    continue;

                if (_owner != null && hit.transform.IsChildOf(_owner))
                    continue;

                IDamageable target = hit.GetComponentInParent<IDamageable>();

                if (target == null)
                    continue;

                if (target.IsDead)
                    continue;

                if (_damagedTargets.Contains(target))
                    continue;

                _damagedTargets.Add(target);

                Vector3 hitPoint = hit.ClosestPoint(center);

                KRDamageContext context = new KRDamageContext(
                    _damage,
                    _damageType,
                    hitPoint,
                    _direction,
                    isMuryeongReflected: true);

                target.TakeDamage(context);
            }
        }

        private void SpawnExplosionVfx(Vector3 position, Vector3 normal)
        {
            if (_explosionVfxPrefab == null)
                return;

            Quaternion rotation = Quaternion.identity;

            if (normal.sqrMagnitude > 0.0001f)
                rotation = Quaternion.LookRotation(normal, Vector3.up);

            GameObject instance = Instantiate(_explosionVfxPrefab, position, rotation);

            if (_explosionVfxLifeTime > 0f)
                Destroy(instance, _explosionVfxLifeTime);
        }

        private void DestroyProjectile()
        {
            if (_isDestroyed)
                return;

            _isDestroyed = true;
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _hitRadius);

            if (_explodeOnHit && _explosionRadius > 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, _explosionRadius);
            }
        }
    }
}