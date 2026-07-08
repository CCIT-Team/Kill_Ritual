// Assets/Project/Features/Enemies/KREnemyBase.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

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

        /// <summary>적 등급. KRAbsorptionSystem이 회복량 계산 시 참조합니다.</summary>
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

        // ── 런타임 상태 ────────────────────────────────────────────────
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

        // ── IDamageable ────────────────────────────────────────────────
        public bool IsDead => _state == EnemyState.Dead;
        public bool IsGroggy => _isGroggy;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 이 적의 그로기 테두리 컴포넌트.
        /// 외부 스크립트는 가능하면 GetComponent로 직접 찾지 말고 이 프로퍼티를 참조하세요.
        /// </summary>
        public KRGroggyOutline GroggyOutline => _groggyOutline;

        /// <summary>
        /// 일반 피해 진입점.
        /// 하위 클래스가 ModifyIncomingDamage()를 오버라이드하면 몸통 방어, 보스 기본 피해 감소 등을 적용할 수 있습니다.
        /// </summary>
        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead) return;

            float finalAmount = ModifyIncomingDamage(context);
            ApplyDamageInternal(finalAmount, context);
        }

        /// <summary>
        /// 보스의 부위별 약점 전용 직접 피해 진입점.
        /// KRBossBodyPart가 이미 최종 피해량을 계산해서 넘겼다고 보고,
        /// ModifyIncomingDamage()를 다시 거치지 않습니다.
        /// </summary>
        public void TakeDamageDirect(KRDamageContext context)
        {
            if (IsDead) return;

            ApplyDamageInternal(context.DamageAmount, context);
        }

        private void ApplyDamageInternal(float amount, KRDamageContext context)
        {
            amount = Mathf.Max(0f, amount);
            // [2026-07-08 신규] 페이즈 전환 문턱 등에서 초과피해를 자르기 위한 훅. 기본 구현은
            // 그대로 반환합니다 — 보스가 필요할 때만 오버라이드합니다.
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

        /// <summary>
        /// 들어오는 피해량을 실제로 적용하기 직전에 가공할 수 있는 훅입니다.
        /// 기본 구현은 가공 없이 그대로 반환합니다.
        /// 보스처럼 몸통 직접 피격 피해를 줄여야 하는 클래스에서 오버라이드하세요.
        /// </summary>
        protected virtual float ModifyIncomingDamage(KRDamageContext context) => context.DamageAmount;

        /// <summary>
        /// [2026-07-08 신규] 체력에 실제로 반영되기 직전, 최종 피해량을 한 번 더 자를 수 있는
        /// 훅입니다. ModifyIncomingDamage()와 달리 TakeDamage()/TakeDamageDirect() 양쪽 경로를
        /// 전부 거치므로(부위 피격 포함), 보스 페이즈 전환 문턱에서 초과피해를 자르는 등
        /// "체력 자체"를 기준으로 한 규칙에 씁니다. 기본 구현은 가공 없이 그대로 반환합니다.
        /// </summary>
        protected virtual float ClampFinalDamage(float amount) => amount;

        /// <summary>
        /// 피해 적용 직후 현재 체력 비율을 알려주는 훅입니다.
        /// 보스 페이즈 전환 등에 사용합니다.
        /// </summary>
        protected virtual void OnHealthChanged(float ratio) { }

        public void Execute(KillRitual.Core.Interfaces.ExecutionSource source
            = KillRitual.Core.Interfaces.ExecutionSource.Default)
        {
            if (IsDead) return;

            switch (source)
            {
                case KillRitual.Core.Interfaces.ExecutionSource.Absorption:
                    // 흡혼 — 체력 회복은 KRAbsorptionSystem이 이미 처리합니다.
                    // 탄약 드롭 없음.
                    break;

                case KillRitual.Core.Interfaces.ExecutionSource.Jakdu:
                    // 작두 — 탄약 오브 드롭.
                    var combatSystem = GameObject.FindGameObjectWithTag("Player")
                        ?.GetComponentInParent<KillRitual.Player.Combat.KRCombatSystem>();

                    GetComponent<KillRitual.Items.KRDropSpawner>()
                        ?.SpawnDrops(
                            transform.position,
                            combatSystem?.CurrentElement ?? KRDamageType.Fire
                        );
                    break;

                default:
                    // 기타 — 테스트 또는 일반 처형.
                    break;
            }

            Debug.Log($"[KREnemyBase] {name} 처형됨 ({source})");
            PerformExecution(source);
        }

        /// <summary>
        /// [2026-07-08 신규] 처형이 실제로 대상에게 어떤 결과를 남길지 결정하는 훅입니다.
        /// 기본 구현은 그대로 즉사(EnterDead)시킵니다 — 일반 잡몹은 이 기본 동작 그대로 씁니다.
        /// 보스처럼 "처형당해도 안 죽고 큰 피해만 입어야" 하는 경우 오버라이드하세요.
        /// </summary>
        protected virtual void PerformExecution(
            KillRitual.Core.Interfaces.ExecutionSource source)
        {
            EnterDead();
        }

        // ── 유니티 생명주기 ────────────────────────────────────────────

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

        // ── 초기화 보조 ────────────────────────────────────────────────

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

                // 보스 부위처럼 콜라이더 GameObject 자체에 다른 IDamageable이 붙어 있으면
                // KREnemyBase가 해당 콜라이더를 가로채지 않도록 제외합니다.
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

        // ── FSM ────────────────────────────────────────────────────────

        protected virtual void UpdateIdle()
        {
            if (_player == null) return;

            if (DistanceToPlayer() <= _detectRange)
            {
                _hasSpottedPlayer = true;
                _state = EnemyState.Chase;

                Debug.Log($"[KREnemyBase] {name}: 플레이어 감지(거리 {DistanceToPlayer():F1}) — Idle → Chase 전환");
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

        // ── 상태 전환 ──────────────────────────────────────────────────

        private void EnterGroggy(float duration)
        {
            _isGroggy = true;
            _state = EnemyState.Groggy;
            _groggyEndTime = Time.time + duration;

            StopMoving();
            _groggyOutline?.SetOutline(true);
        }

        /// <summary>
        /// 외부에서 강제로 그로기 상태에 진입시킬 때 사용합니다.
        /// duration을 생략하거나 0 이하로 넘기면 인스펙터의 기본 _groggyDuration을 사용합니다.
        /// </summary>
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

            Destroy(gameObject, _despawnDelay);
        }

        /// <summary>
        /// 적 처치 시 플레이어의 작두 자원을 1 회복시킵니다.
        /// 단, 작두가 자기 자신의 판정으로 처치한 경우에는 자기환급을 막기 위해 회복하지 않습니다.
        /// </summary>
        private void RefillJakduResourceOnKill()
        {
            if (_player == null) return;

            if (KillRitual.Player.Combat.KRJakduSystem.IsSelfExecuting)
                return;

            var jakduSystem = _player.GetComponentInChildren<KillRitual.Player.Combat.KRJakduSystem>(true);
            jakduSystem?.AddResource(1);
        }

        protected virtual void OnDeath() { }

        // ── 피격 파티클 이펙트 ─────────────────────────────────────────

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

        // ── 공용 유틸리티 ──────────────────────────────────────────────

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

            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning(
                    $"[KREnemyBase] {name}: NavMeshAgent가 NavMesh 위에 있지 않아 이동이 무시됩니다. " +
                    "씬에 NavMesh가 베이크되어 있는지, 스폰 위치가 NavMesh 범위 안인지 확인하세요."
                );
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(targetPosition);
        }

        protected void StopMoving()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        /// <summary>
        /// maxDegreesPerSecond를 생략하면 기존처럼 즉시 플레이어를 바라봅니다.
        /// 양수로 넘기면 해당 초당 각도만큼 천천히 회전합니다.
        /// 보스의 등/측면 약점 공략을 허용할 때 사용합니다.
        /// </summary>
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

        // ── 색상 시각 피드백 ───────────────────────────────────────────

        /// <summary>
        /// 하위 클래스가 특정 구간 동안 강제로 몸 색을 바꾸고 싶을 때 사용합니다.
        /// null이면 기존 피격 플래시 / 기본색 로직을 따릅니다.
        /// 값이 있으면 히트 플래시보다 우선됩니다.
        /// </summary>
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