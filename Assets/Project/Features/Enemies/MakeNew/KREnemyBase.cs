using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual.CombatZones;

namespace KillRitual.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class KREnemyBase : MonoBehaviour, IDamageable
    {
        protected enum EnemyState { Idle, Chase, Attack, Groggy, Dead }

        [Header("적 등급")]
        [Tooltip("흡혼 체력 회복량 계산에 사용됩니다.")]
        [SerializeField]
        private KillRitual.Player.Combat.EnemyGrade _grade
            = KillRitual.Player.Combat.EnemyGrade.Fodder;

        public KillRitual.Player.Combat.EnemyGrade Grade => _grade;

        [Header("체력")]
        [Min(1f)]
        [SerializeField] protected float _maxHealth = 30f;

        [Header("그로기 (경직)")]
        [Range(0.05f, 0.9f)]
        [SerializeField] protected float _groggyHealthRatio = 0.3f;

        [Min(0.1f)]
        [SerializeField] protected float _groggyDuration = 3f;

        [Header("감지 / 이동")]
        [Min(1f)]
        [SerializeField] protected float _detectRange = 20f;

        [SerializeField] protected bool _chaseForever = true;

        [Min(0f)]
        [SerializeField] protected float _moveSpeed = 3.5f;

        [Header("색상 / 피격 피드백")]
        [Tooltip("켜면 모델 머티리얼이 원래 갖고 있던 색을 기본색으로 사용합니다. 끄면 아래 Base Color를 강제로 기본색으로 씁니다.")]
        [SerializeField] private bool _useOriginalMaterialColors = true;

        [Tooltip("_useOriginalMaterialColors가 꺼져 있을 때 사용할 기본 색입니다.")]
        [SerializeField] protected Color _baseColor = Color.gray;

        [Tooltip("맞았을 때 잠깐 곱해질 색입니다.")]
        [SerializeField] protected Color _hitFlashColor = Color.white;

        [Min(0.01f)]
        [SerializeField] protected float _hitFlashDuration = 0.08f;

        [Header("피격 파티클 이펙트")]
        [Tooltip("맞았을 때 생성할 파티클 프리팹입니다. 비워두면 파티클 없이 정상 동작합니다.")]
        [SerializeField] protected ParticleSystem _hitEffectPrefab;

        [Tooltip("데미지 컨텍스트에 피격 위치 정보가 없을 경우 대신 사용할 높이 보정값입니다.")]
        [SerializeField] protected float _hitEffectFallbackHeight = 1f;

        [Header("사망")]
        [Min(0f)]
        [SerializeField] protected float _despawnDelay = 0.5f;

        [Header("모델 참조")]
        [Tooltip("실제 캐릭터 메시가 들어있는 자식 오브젝트입니다. 파티클 등 다른 자식은 여기 안 넣는 것을 권장합니다.")]
        [SerializeField] private Transform _modelRoot;

        //런타임 상태
        protected EnemyState _state = EnemyState.Idle;
        protected float _health;
        protected Transform _player;
        protected NavMeshAgent _agent;
        protected bool _hasSpottedPlayer;

        private Renderer[] _renderers;
        private Color[] _originalColors;
        private MaterialPropertyBlock _mpb;

        private float _hitFlashEndTime;
        private float _groggyEndTime;
        private bool _isGroggy;

        private static readonly int kBaseColorId = Shader.PropertyToID("_BaseColor"); // URP/Lit
        private static readonly int kColorId = Shader.PropertyToID("_Color");         // Built-in/Standard

        private Collider[] _ownColliders;
        private KRGroggyOutline _groggyOutline;

        //IDamageable
        public bool IsDead => _state == EnemyState.Dead;
        public bool IsGroggy => _isGroggy;
        public Vector3 Position => transform.position;

        public KRGroggyOutline GroggyOutline => _groggyOutline;

        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead) return;

            float finalAmount = ModifyIncomingDamage(context);
            ApplyDamageInternal(finalAmount, context);
        }

        public void TakeDamageDirect(KRDamageContext context)
        {
            if (IsDead) return;

            ApplyDamageInternal(context.DamageAmount, context);
        }

        private void ApplyDamageInternal(float amount, KRDamageContext context)
        {
            amount = Mathf.Max(0f, amount);
            amount = ClampFinalDamage(amount);

            _health -= amount;
            _hitFlashEndTime = Time.time + _hitFlashDuration;

            SpawnHitEffect(context);

            OnHealthChanged(Mathf.Clamp01(_health / _maxHealth));

            if (_health <= 0f)
            {
                EnterDead();
                return;
            }

            if (!_isGroggy && _health <= _maxHealth * _groggyHealthRatio)
                EnterGroggy(_groggyDuration);
        }

        protected virtual float ModifyIncomingDamage(KRDamageContext context) => context.DamageAmount;

        protected virtual float ClampFinalDamage(float amount) => amount;

        protected virtual void OnHealthChanged(float ratio) { }

        public void Execute(KillRitual.Core.Interfaces.ExecutionSource source
            = KillRitual.Core.Interfaces.ExecutionSource.Default)
        {
            if (IsDead) return;

            switch (source)
            {
                case KillRitual.Core.Interfaces.ExecutionSource.Absorption:
                    // 흡혼
                    break;

                case KillRitual.Core.Interfaces.ExecutionSource.Jakdu:
                    // 작두
                    var combatSystem = GameObject.FindGameObjectWithTag("Player")
                        ?.GetComponentInParent<KillRitual.Player.Combat.KRCombatSystem>();

                    GetComponent<KillRitual.Items.KRDropSpawner>()
                        ?.SpawnDrops(
                            transform.position,
                            combatSystem?.CurrentElement ?? KRDamageType.Fire
                        );
                    break;

                default:
                    // 테스트 또는 일반 처형
                    break;
            }

            Debug.Log($"[KREnemyBase] {name} 처형됨 ({source})");
            PerformExecution(source);
        }

        protected virtual void PerformExecution(
            KillRitual.Core.Interfaces.ExecutionSource source)
        {
            EnterDead();
        }

        //유니티 생명주기 

        protected virtual void Awake()
        {
            _health = _maxHealth;

            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
                _agent.speed = _moveSpeed;

            _mpb = new MaterialPropertyBlock();

            CacheRenderersAndOriginalColors();
            ApplyCurrentVisualColor();

            CacheOwnColliders();

            CacheGroggyOutline();
        }

        private void OnEnable()
        {
            if (_ownColliders == null) return;
            if (KillRitual.Core.Managers.KRManagers.Combat == null) return;

            foreach (Collider col in _ownColliders)
            {
                if (col == null) continue;
                KillRitual.Core.Managers.KRManagers.Combat.Register(col, this);
            }
        }

        private void OnDisable()
        {
            if (_ownColliders == null) return;
            if (KillRitual.Core.Managers.KRManagers.Combat == null) return;

            foreach (Collider col in _ownColliders)
            {
                if (col == null) continue;
                KillRitual.Core.Managers.KRManagers.Combat.Unregister(col);
            }
        }

        protected virtual void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                _player = playerObject.transform;
            else
                Debug.LogWarning($"[{name}] 'Player' 태그를 가진 오브젝트를 찾지 못했습니다.");
        }

        protected virtual void Update()
        {
            UpdateColorFeedback();

            switch (_state)
            {
                case EnemyState.Idle:
                    UpdateIdle();
                    break;

                case EnemyState.Chase:
                    UpdateChase();
                    break;

                case EnemyState.Attack:
                    UpdateAttack();
                    break;

                case EnemyState.Groggy:
                    UpdateGroggy();
                    break;

                case EnemyState.Dead:
                    break;
            }
        }

        //초기화 보조

        private void CacheRenderersAndOriginalColors()
        {
            Transform searchRoot = _modelRoot != null ? _modelRoot : transform;

            _renderers = searchRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            _originalColors = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];

                if (renderer == null)
                {
                    _originalColors[i] = Color.white;
                    continue;
                }

                Material mat = renderer.sharedMaterial;

                if (mat != null && mat.HasProperty(kBaseColorId))
                    _originalColors[i] = mat.GetColor(kBaseColorId);
                else if (mat != null && mat.HasProperty(kColorId))
                    _originalColors[i] = mat.GetColor(kColorId);
                else
                    _originalColors[i] = Color.white;
            }
        }

        private void CacheOwnColliders()
        {
            var ownColliderList = new List<Collider>();

            foreach (Collider col in GetComponentsInChildren<Collider>(includeInactive: false))
            {
                if (col == null) continue;

                IDamageable colDamageable = col.GetComponent<IDamageable>();

                if (colDamageable != null && !ReferenceEquals(colDamageable, this))
                    continue;

                ownColliderList.Add(col);
            }

            _ownColliders = ownColliderList.ToArray();
        }

        private void CacheGroggyOutline()
        {
            Transform outlineTarget = _modelRoot != null ? _modelRoot : transform;

            _groggyOutline = outlineTarget.GetComponent<KRGroggyOutline>();

            if (_groggyOutline == null)
                _groggyOutline = outlineTarget.gameObject.AddComponent<KRGroggyOutline>();
        }

        //FSM

        protected virtual void UpdateIdle()
        {
            if (_player == null) return;

            if (DistanceToPlayer() <= _detectRange)
            {
                _hasSpottedPlayer = true;
                _state = EnemyState.Chase;
            }
        }

        protected bool ShouldKeepChasing()
        {
            if (_player == null) return false;
            if (_chaseForever && _hasSpottedPlayer) return true;

            return DistanceToPlayer() <= _detectRange;
        }

        protected abstract void UpdateChase();
        protected abstract void UpdateAttack();

        protected virtual void UpdateGroggy()
        {
            StopMoving();

            if (Time.time >= _groggyEndTime)
                ExitGroggy();
        }

        //상태 전환

        private void EnterGroggy(float duration)
        {
            _isGroggy = true;
            _state = EnemyState.Groggy;
            _groggyEndTime = Time.time + duration;

            StopMoving();
            _groggyOutline?.SetOutline(true);
        }

        protected void ForceGroggy(float duration = -1f)
        {
            if (IsDead) return;

            EnterGroggy(duration > 0f ? duration : _groggyDuration);
        }

        private void ExitGroggy()
        {
            _isGroggy = false;
            _state = EnemyState.Chase;

            _groggyOutline?.SetOutline(false);
        }

        private void EnterDead()
        {
            _state = EnemyState.Dead;
            _isGroggy = false;
            _health = 0f;

            _groggyOutline?.SetOutline(false);

            StopMoving();

            if (_agent != null && _agent.enabled)
                _agent.enabled = false;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            RefillJakduResourceOnKill();

            OnDeath();

            GetComponent<ArenaEnemyLink>()?.Die();
            GetComponent<BossSupplyEnemyLink>()?.Die(); 

            Destroy(gameObject, _despawnDelay);
        }

        private void RefillJakduResourceOnKill()
        {
            if (_player == null) return;

            if (KillRitual.Player.Combat.KRJakduSystem.IsSelfExecuting)
                return;

            var jakduSystem = _player.GetComponentInChildren<KillRitual.Player.Combat.KRJakduSystem>(true);
            jakduSystem?.AddResource(1);
        }

        protected virtual void OnDeath() { }

        //피격 파티클 이펙트

        private void SpawnHitEffect(KRDamageContext context)
        {
            if (_hitEffectPrefab == null) return;

            Vector3 hitPoint = context.HitPoint != Vector3.zero
                ? context.HitPoint
                : transform.position + Vector3.up * _hitEffectFallbackHeight;

            Quaternion hitRotation = context.Direction != Vector3.zero
                ? Quaternion.LookRotation(-context.Direction)
                : Quaternion.identity;

            ParticleSystem fx = Instantiate(_hitEffectPrefab, hitPoint, hitRotation);
            fx.Play();

            float lifetime = fx.main.duration + fx.main.startLifetime.constantMax + 0.5f;
            Destroy(fx.gameObject, lifetime);
        }

        //공용 유틸리티

        protected IDamageable FindPlayerDamageable(Transform playerTransform)
        {
            if (playerTransform == null) return null;

            var feedback = playerTransform.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
            if (feedback != null) return feedback;

            return playerTransform.GetComponentInParent<IDamageable>();
        }

        protected float DistanceToPlayer()
        {
            if (_player == null) return float.MaxValue;
            return Vector3.Distance(transform.position, _player.position);
        }

        protected void MoveTowards(Vector3 targetPosition)
        {
            if (_agent == null || !_agent.enabled) return;

            _agent.isStopped = false;
            _agent.SetDestination(targetPosition);
        }

        protected void StopMoving()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        protected void FacePlayer(float maxDegreesPerSecond = -1f)
        {
            if (_player == null) return;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude <= 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer);

            transform.rotation = maxDegreesPerSecond <= 0f
                ? targetRotation
                : Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    maxDegreesPerSecond * Time.deltaTime
                );
        }

        //색상 시각 피드백

        protected Color? OverrideColor { get; set; }

        private void UpdateColorFeedback()
        {
            ApplyCurrentVisualColor();
        }

        private void ApplyCurrentVisualColor()
        {
            if (OverrideColor.HasValue)
            {
                ApplyUniformColor(OverrideColor.Value);
                return;
            }

            bool isFlashing = Time.time < _hitFlashEndTime;

            if (isFlashing)
            {
                ApplyUniformColor(_hitFlashColor);
                return;
            }

            ApplyNormalColor();
        }

        private void ApplyNormalColor()
        {
            if (_renderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                Color color = _useOriginalMaterialColors
                    ? GetOriginalColor(i)
                    : _baseColor;

                renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(kBaseColorId, color);
                _mpb.SetColor(kColorId, color);
                renderer.SetPropertyBlock(_mpb);
            }
        }

        private void ApplyUniformColor(Color color)
        {
            if (_renderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(kBaseColorId, color);
                _mpb.SetColor(kColorId, color);
                renderer.SetPropertyBlock(_mpb);
            }
        }

        private Color GetOriginalColor(int index)
        {
            if (_originalColors == null) return Color.white;
            if (index < 0 || index >= _originalColors.Length) return Color.white;

            return _originalColors[index];
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectRange);
        }
    }
}