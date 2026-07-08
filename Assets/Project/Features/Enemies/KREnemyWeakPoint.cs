// Assets/Project/Scripts/05_Enemies/KREnemyWeakPoint.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 적 약점 오브젝트입니다.
    ///
    /// 동작:
    /// - 약점 오브젝트가 피해를 받으면 본체에도 피해를 전달합니다.
    /// - 본체 전달 피해는 기본 1.5배입니다.
    /// - 약점 체력은 따로 감소합니다.
    /// - 약점 체력이 0이 되면 약점이 파괴되고 본체가 그로기에 빠집니다.
    ///
    /// 주의:
    /// - 이 스크립트는 약점 오브젝트에 붙이세요.
    /// - 적 루트에 붙이면 적 전체 Collider/Renderer를 끌 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KREnemyWeakPoint : MonoBehaviour, IDamageable
    {
        [Header("공유 대상")]
        [Tooltip("약점이 붙어 있는 적 본체입니다. 비워두면 부모에서 KREnemyBase를 자동 탐색합니다.")]
        [SerializeField] private KREnemyBase _ownerEnemy;

        [Tooltip("실제 피해를 전달할 대상입니다. 비워두면 Owner Enemy를 IDamageable로 사용합니다.")]
        [SerializeField] private MonoBehaviour _sharedDamageTargetBehaviour;

        [Header("약점 체력")]
        [Min(1f)]
        [SerializeField] private float _maxWeakPointHealth = 60f;

        [Tooltip("약점에 들어온 피해를 본체에 전달할 때 곱하는 배율입니다.")]
        [Min(0f)]
        [SerializeField] private float _ownerDamageMultiplier = 1.5f;

        [Tooltip("본체로 전달할 때 사용할 피해 속성입니다.")]
        [SerializeField] private KRDamageType _forwardedDamageType = KRDamageType.Fire;

        [Header("본체 전달 피해 제한")]
        [Tooltip("샷건/폭발 중복 판정으로 본체가 한 프레임에 삭제되는 것을 막습니다.")]
        [SerializeField] private bool _limitOwnerDamagePerFrame = true;

        [Tooltip("약점이 본체에 한 프레임 동안 전달할 수 있는 최대 피해량입니다.")]
        [Min(1f)]
        [SerializeField] private float _maxOwnerDamagePerFrame = 300f;

        [Header("파괴/그로기")]
        [Tooltip("약점 체력이 0이 됐을 때 적이 그로기에 빠지는 시간입니다.")]
        [Min(0.1f)]
        [SerializeField] private float _groggyDurationOnBreak = 2.5f;

        [Tooltip("약점 파괴 시 이 오브젝트의 Renderer들을 끕니다.")]
        [SerializeField] private bool _disableRenderersOnBreak = true;

        [Tooltip("약점 파괴 시 이 오브젝트의 Collider들을 끕니다.")]
        [SerializeField] private bool _disableCollidersOnBreak = true;

        [Tooltip("true면 약점 파괴 후 이 GameObject를 Destroy합니다.")]
        [SerializeField] private bool _destroyGameObjectOnBreak = false;

        [Min(0f)]
        [SerializeField] private float _destroyDelay = 0.05f;

        [SerializeField] private GameObject _breakVfxPrefab;

        [Header("자동 수집")]
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Collider[] _colliders;

        private IDamageable _sharedDamageTarget;

        private float _currentWeakPointHealth;
        private bool _isBroken;

        private int _lastOwnerDamageFrame = -1;
        private float _ownerDamageThisFrame;

        public bool IsDead => _isBroken;
        public bool IsGroggy => false;
        public Vector3 Position => transform.position;

        public float CurrentWeakPointHealth => _currentWeakPointHealth;
        public float MaxWeakPointHealth => _maxWeakPointHealth;

        public float HealthRatio
        {
            get
            {
                if (_maxWeakPointHealth <= 0f)
                {
                    return 0f;
                }

                return _currentWeakPointHealth / _maxWeakPointHealth;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            _currentWeakPointHealth = _maxWeakPointHealth;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_maxWeakPointHealth < 1f)
            {
                _maxWeakPointHealth = 1f;
            }

            if (_maxOwnerDamagePerFrame < 1f)
            {
                _maxOwnerDamagePerFrame = 1f;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_colliders == null || _colliders.Length == 0)
            {
                _colliders = GetComponentsInChildren<Collider>(true);
            }
        }
#endif

        private void ResolveReferences()
        {
            if (_ownerEnemy == null)
            {
                _ownerEnemy = GetComponentInParent<KREnemyBase>();
            }

            if (_sharedDamageTargetBehaviour != null)
            {
                _sharedDamageTarget = _sharedDamageTargetBehaviour as IDamageable;
            }

            if (_sharedDamageTarget == null && _ownerEnemy != null)
            {
                _sharedDamageTarget = _ownerEnemy as IDamageable;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_colliders == null || _colliders.Length == 0)
            {
                _colliders = GetComponentsInChildren<Collider>(true);
            }
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (_isBroken)
            {
                return;
            }

            ResolveReferences();

            float rawDamage = Mathf.Max(0f, context.DamageAmount);

            if (rawDamage <= 0f)
            {
                return;
            }

            ApplyDamageToOwner(rawDamage);
            ApplyDamageToWeakPoint(rawDamage);
        }

        private void ApplyDamageToOwner(float rawDamage)
        {
            if (_sharedDamageTarget == null)
            {
                return;
            }

            if (ReferenceEquals(_sharedDamageTarget, this))
            {
                return;
            }

            if (_sharedDamageTarget.IsDead)
            {
                return;
            }

            float finalDamage = rawDamage * _ownerDamageMultiplier;

            if (_limitOwnerDamagePerFrame)
            {
                if (_lastOwnerDamageFrame != Time.frameCount)
                {
                    _lastOwnerDamageFrame = Time.frameCount;
                    _ownerDamageThisFrame = 0f;
                }

                float remainingFrameDamage = _maxOwnerDamagePerFrame - _ownerDamageThisFrame;

                if (remainingFrameDamage <= 0f)
                {
                    return;
                }

                finalDamage = Mathf.Min(finalDamage, remainingFrameDamage);
                _ownerDamageThisFrame += finalDamage;
            }

            Vector3 hitDirection = _ownerEnemy != null
                ? (transform.position - _ownerEnemy.transform.position).normalized
                : transform.forward;

            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = transform.forward;
            }

            var forwardedContext = new KRDamageContext(
                finalDamage,
                _forwardedDamageType,
                transform.position,
                hitDirection);

            _sharedDamageTarget.TakeDamage(forwardedContext);
        }

        private void ApplyDamageToWeakPoint(float rawDamage)
        {
            _currentWeakPointHealth -= rawDamage;

            if (_currentWeakPointHealth > 0f)
            {
                return;
            }

            _currentWeakPointHealth = 0f;
            BreakWeakPoint();
        }

        private void BreakWeakPoint()
        {
            if (_isBroken)
            {
                return;
            }

            _isBroken = true;

            SpawnBreakVfx();
            RequestOwnerGroggy();
            DisableWeakPointVisualAndCollision();

            if (_destroyGameObjectOnBreak)
            {
                Destroy(gameObject, _destroyDelay);
            }
        }

        private void SpawnBreakVfx()
        {
            if (_breakVfxPrefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(
                _breakVfxPrefab,
                transform.position,
                transform.rotation);

            ParticleSystem particle = instance.GetComponentInChildren<ParticleSystem>();

            if (particle != null)
            {
                ParticleSystem.MainModule main = particle.main;
                float lifeTime = main.duration + main.startLifetime.constantMax;
                Destroy(instance, Mathf.Max(0.1f, lifeTime));
            }
            else
            {
                Destroy(instance, 3f);
            }
        }

        private void RequestOwnerGroggy()
        {
            if (_ownerEnemy == null)
            {
                return;
            }

            KRHybridMeleeRangedEnemy hybridEnemy = _ownerEnemy as KRHybridMeleeRangedEnemy;

            if (hybridEnemy != null)
            {
                hybridEnemy.EnterWeakPointGroggy(_groggyDurationOnBreak);
            }

            _ownerEnemy.SendMessage("EnterGroggy", _groggyDurationOnBreak, SendMessageOptions.DontRequireReceiver);
            _ownerEnemy.SendMessage("EnterStagger", _groggyDurationOnBreak, SendMessageOptions.DontRequireReceiver);
            _ownerEnemy.SendMessage("SetGroggy", _groggyDurationOnBreak, SendMessageOptions.DontRequireReceiver);
            _ownerEnemy.SendMessage("ApplyGroggy", _groggyDurationOnBreak, SendMessageOptions.DontRequireReceiver);
        }

        private void DisableWeakPointVisualAndCollision()
        {
            if (_disableCollidersOnBreak && _colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    Collider col = _colliders[i];

                    if (col == null)
                    {
                        continue;
                    }

                    col.enabled = false;
                }
            }

            if (_disableRenderersOnBreak && _renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];

                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.enabled = false;
                }
            }
        }

        public void Execute()
        {
            if (_isBroken)
            {
                return;
            }

            BreakWeakPoint();
        }

        public void Execute(ExecutionSource source = ExecutionSource.Default)
        {
            Execute();
        }

        public void ResetWeakPoint()
        {
            _isBroken = false;
            _currentWeakPointHealth = _maxWeakPointHealth;

            _lastOwnerDamageFrame = -1;
            _ownerDamageThisFrame = 0f;

            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null)
                    {
                        _colliders[i].enabled = true;
                    }
                }
            }

            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].enabled = true;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
}