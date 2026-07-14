using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    public enum KRMuryeongProjectileRank
    {
        Fodder = 0,
        Gapsa = 1,
        Jangryeong = 2,
        Boss = 3
    }

    [DisallowMultipleComponent]
    public sealed class KREnemyProjectile : MonoBehaviour
    {
        [Header("Muryeong Reflection")]
        [SerializeField] private KRMuryeongProjectileRank _sourceRank = KRMuryeongProjectileRank.Fodder;

        [Tooltip("무령으로 반사되는 순간 바꿀 레이어 이름. 반드시 Unity Layer에 Projectile이 있어야 합니다.")]
        [SerializeField] private string _reflectedLayerName = "Projectile";

        [Tooltip("반사 후 데미지 배율. 1이면 원래 투사체 데미지 그대로 적에게 줍니다.")]
        [Min(0f)]
        [SerializeField] private float _reflectedDamageMultiplier = 1f;

        [Tooltip("반사 후 수명을 다시 계산합니다.")]
        [SerializeField] private bool _resetLifeTimeOnReflect = true;

        private float _speed;
        private float _damage;
        private Transform _shooter;
        private Transform _reflectedByPlayer;

        private Vector3 _direction;
        private float _lifeTime = 5f;
        private float _spawnTime;
        public Transform Shooter => _shooter;

        private bool _isLaunched;
        private bool _isReflected;
        private bool _isDestroyed;

        public Transform CachedTransform => transform;
        public KRMuryeongProjectileRank SourceRank => _sourceRank;
        public bool IsReflected => _isReflected;

        public bool CanBeReflectedByMuryeong
        {
            get
            {
                return _isLaunched
                       && !_isReflected
                       && !_isDestroyed
                       && gameObject.activeInHierarchy;
            }
        }

        public void Launch(Vector3 direction, float speed, float damage, Transform shooter)
        {
            _direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;

            _speed = speed;
            _damage = damage;
            _shooter = shooter;
            _spawnTime = Time.time;
            _isLaunched = true;
            _isReflected = false;
            _isDestroyed = false;
            _reflectedByPlayer = null;

            if (_direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }

        public float EstimateArrivalTimeTo(Vector3 worldPoint)
        {
            Vector3 toPoint = worldPoint - transform.position;
            float distance = toPoint.magnitude;

            if (distance <= 0.001f)
                return 0f;

            if (_speed <= 0.001f)
                return float.PositiveInfinity;

            float closingSpeed = Vector3.Dot(_direction.normalized, toPoint.normalized) * _speed;

            if (closingSpeed <= 0.001f)
                return float.PositiveInfinity;

            return distance / closingSpeed;
        }

        public void ReflectByMuryeong(Transform playerRoot, Vector3 reflectDirection)
        {
            if (!CanBeReflectedByMuryeong)
                return;

            if (reflectDirection.sqrMagnitude <= 0.0001f)
                reflectDirection = transform.forward;

            _isReflected = true;
            _reflectedByPlayer = playerRoot;
            _direction = reflectDirection.normalized;

            if (_resetLifeTimeOnReflect)
                _spawnTime = Time.time;

            ApplyReflectedLayer();

            if (_direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }

        private void Update()
        {
            if (!_isLaunched || _isDestroyed)
                return;

            transform.position += _direction * _speed * Time.deltaTime;

            if (Time.time - _spawnTime >= _lifeTime)
            {
                DestroyProjectile();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isDestroyed)
                return;

            if (other == null)
                return;

            if (_isReflected)
                HandleReflectedTrigger(other);
            else
                HandleEnemyProjectileTrigger(other);
        }

        private void HandleEnemyProjectileTrigger(Collider other)
        {
            // 자신을 쏜 몬스터 또는 그 자식과는 충돌 무시.
            if (_shooter != null && other.transform.IsChildOf(_shooter))
                return;

            // 일반 적 투사체 상태에서는 몬스터끼리 맞지 않음.
            if (other.GetComponentInParent<KREnemyBase>() != null)
                return;

            IDamageable target = other.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();

            if (target == null)
                target = other.GetComponentInParent<IDamageable>();

            if (target != null && !target.IsDead)
            {
                var context = new KRDamageContext(
                    _damage,
                    KRDamageType.Fire,
                    transform.position,
                    _direction);

                target.TakeDamage(context);
            }

            DestroyProjectile();
        }

        private void HandleReflectedTrigger(Collider other)
        {
            // 반사된 투사체는 플레이어 자신에게 다시 맞지 않음.
            if (_reflectedByPlayer != null && other.transform.IsChildOf(_reflectedByPlayer))
                return;

            // 반사된 투사체는 플레이어 피해 컴포넌트를 직접 무시.
            // 즉, 반사탄이 다시 플레이어를 때리는 상황 방지.
            if (other.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>() != null)
                return;

            IDamageable target = other.GetComponentInParent<IDamageable>();

            if (target != null && !target.IsDead)
            {
                float reflectedDamage = _damage * _reflectedDamageMultiplier;

                var context = new KRDamageContext(
                    reflectedDamage,
                    KRDamageType.Fire,
                    transform.position,
                    _direction);

                target.TakeDamage(context);
            }

            // 반사 상태에서는 적이든 벽이든 무언가에 닿으면 제거.
            DestroyProjectile();
        }

        private void ApplyReflectedLayer()
        {
            if (string.IsNullOrEmpty(_reflectedLayerName))
                return;

            int layer = LayerMask.NameToLayer(_reflectedLayerName);

            if (layer < 0)
            {
                Debug.LogWarning(
                    $"[{nameof(KREnemyProjectile)}] '{_reflectedLayerName}' 레이어를 찾을 수 없습니다. " +
                    "Unity Project Settings > Tags and Layers에 Projectile 레이어가 있는지 확인하세요.",
                    this);

                return;
            }

            SetLayerRecursively(transform, layer);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private void DestroyProjectile()
        {
            if (_isDestroyed)
                return;

            _isDestroyed = true;
            Destroy(gameObject);
        }
    }
}