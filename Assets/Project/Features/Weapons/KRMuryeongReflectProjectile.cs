// Assets/Project/Scripts/05_Enemies/Projectiles/KRMuryeongProjectile.cs
using System.Collections.Generic;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using UnityEngine;

namespace KillRitual.Enemies.Projectiles
{
    /// <summary>
    /// �������� �ݻ� ���� �� ���� �����Ǵ� ���� ����ü.
    ///
    /// Ư¡:
    /// - ���� EnemyProjectile�� �������� ����.
    /// - Rigidbody / Trigger �浹�� �������� �ʰ� SphereCast�� ���� �浹 �˻�.
    /// - ���� ���� �ٷ� �������� �ʵ��� Arm Delay / Arm Distance�� ��.
    /// - ������ ��/��/������ ����� ���� ����.
    /// - �������� �� ��ũ��Ʈ�� �ν����� �� �ϳ��� ���.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRMuryeongProjectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _speed = 45f;
        [SerializeField] private float _lifeTime = 3f;

        [Header("Arming")]
        [Tooltip("���� ���� �� �ð� ������ �浹/���� ������ ���� �ʽ��ϴ�. ���� ��ġ �ֺ� �ݶ��̴��� �ٷ� ������ ���� ������.")]
        [Min(0f)]
        [SerializeField] private float _armDelay = 0.05f;

        [Tooltip("���� ��ġ���� �� �Ÿ���ŭ �̵��ϱ� �������� �浹/���� ������ ���� �ʽ��ϴ�.")]
        [Min(0f)]
        [SerializeField] private float _armDistance = 0.6f;

        [Header("Hit Check")]
        [Tooltip("����ź�� �浹�� �� �ִ� ���̾�. Enemy, Boss, Ground ���� �����ϼ���. Player, Projectile, EnemyProjectile�� ���� �� �����մϴ�.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Tooltip("���� ����ü�� ���� �հ� �������� �ʵ��� �ϴ� SphereCast �ݰ�.")]
        [Min(0.01f)]
        [SerializeField] private float _hitRadius = 0.35f;

        [Header("Damage")]
        [Tooltip("����ź�� ������. ���� Ÿ�ݰ� ���� ��� �� ���� ����մϴ�.")]
        [Min(0f)]
        [SerializeField] private float _damage = 30f;

        [SerializeField] private KRDamageType _damageType = KRDamageType.Fire;

        [Header("Explosion")]
        [Tooltip("���� ������ ��/��/������ ����� �� �����մϴ�.")]
        [SerializeField] private bool _explodeOnHit = true;

        [Tooltip("���� �ݰ�. Explode On Hit�� ���� ���� ���� ���˴ϴ�.")]
        [Min(0f)]
        [SerializeField] private float _explosionRadius = 2.5f;

        [Tooltip("���� �������� ���� ���̾�. ���� Enemy, Boss�� �ְ� Ground�� ������.")]
        [SerializeField] private LayerMask _explosionDamageMask = ~0;

        [Tooltip("���� �ð�ȿ�� ������. ����θ� �������� ����˴ϴ�.")]
        [SerializeField] private GameObject _explosionVfxPrefab;

        [Tooltip("���� VFX �ڵ� ���� �ð�. ��ƼŬ�� ��ü ���ŵǸ� 0���� �ֵ� �˴ϴ�.")]
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

            // ���� ���Ŀ��� ���� �̵��� ��Ŵ.
            // �� �������� SphereCast�� ���� �����Ƿ� �ݻ� ���� �ٷ� �������� ����.
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

            // ������ ������ "���� Ÿ�� ������"�� �ƴ϶� ���� �������� ó��.
            // ��, ������ ��� ���� �����ϰ�, ���� ���� �� ����� ���ظ� ����.
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

            // [2026-07-09 신규] isMuryeongReflected: true — "보스 부위파괴는 무령 반사탄으로만
            // 가능" 규칙 판정용 표식입니다. KRBossBodyPart.TakeDamage()가 이 플래그를 보고
            // 부위 체력 차감(파괴 판정) 여부를 결정합니다.
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

                // [2026-07-09 신규] 위 ApplyDirectDamage()와 동일한 이유로 isMuryeongReflected: true.
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