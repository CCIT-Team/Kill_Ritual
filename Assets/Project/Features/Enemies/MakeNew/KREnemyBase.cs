// Assets/Project/Features/Enemies/KREnemyBase.cs
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

        [Header("색상 (등급/상태 시각화)")]
        [SerializeField] protected Color _baseColor = Color.gray;
        [SerializeField] protected Color _hitFlashColor = Color.white;

        [Min(0.01f)]
        [SerializeField] protected float _hitFlashDuration = 0.08f;

        [Header("사망")]
        [Min(0f)]
        [SerializeField] protected float _despawnDelay = 0.5f;

        // ── 런타임 상태 ────────────────────────────────────────────────
        protected EnemyState _state = EnemyState.Idle;
        protected float _health;
        protected Transform _player;
        protected NavMeshAgent _agent;
        protected bool _hasSpottedPlayer;

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _hitFlashEndTime;
        private float _groggyEndTime;
        private bool _isGroggy;

        private static readonly int kBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int kColorId = Shader.PropertyToID("_Color");

        private Collider[] _ownColliders;
        private KRGroggyOutline _groggyOutline;

        // ── IDamageable ────────────────────────────────────────────────
        public bool IsDead => _state == EnemyState.Dead;
        public bool IsGroggy => _isGroggy;
        public Vector3 Position => transform.position;

        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead) return;

            _health -= context.DamageAmount;
            _hitFlashEndTime = Time.time + _hitFlashDuration;

            if (_health <= 0f) { EnterDead(); return; }

            if (!_isGroggy && _health <= _maxHealth * _groggyHealthRatio)
                EnterGroggy();
        }

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
                        ?.SpawnDrops(transform.position, combatSystem?.CurrentElement
                        ?? KRDamageType.Fire);
                    break;

                default:
                    // 기타 — 기존 방식 그대로 (테스트 등)
                    break;
            }

            Debug.Log($"[KREnemyBase] {name} 처형됨 ({source})");
            EnterDead();
        }

        // ── 유니티 생명주기 ────────────────────────────────────────────

        protected virtual void Awake()
        {
            _health = _maxHealth;
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = _moveSpeed;

            _renderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            ApplyColor(_baseColor);

            _ownColliders = GetComponentsInChildren<Collider>(includeInactive: false);

            // KRGroggyOutline이 없으면 자동으로 추가합니다.
            // 적마다 수동으로 컴포넌트를 붙일 필요가 없습니다.
            _groggyOutline = GetComponent<KRGroggyOutline>();
            if (_groggyOutline == null)
                _groggyOutline = gameObject.AddComponent<KRGroggyOutline>();
        }

        private void OnEnable()
        {
            if (_ownColliders == null) return;
            if (KillRitual.Core.Managers.KRManagers.Combat == null) return;
            foreach (Collider col in _ownColliders)
                KillRitual.Core.Managers.KRManagers.Combat.Register(col, this);
        }

        private void OnDisable()
        {
            if (_ownColliders == null) return;
            if (KillRitual.Core.Managers.KRManagers.Combat == null) return;
            foreach (Collider col in _ownColliders)
                KillRitual.Core.Managers.KRManagers.Combat.Unregister(col);
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
                case EnemyState.Idle: UpdateIdle(); break;
                case EnemyState.Chase: UpdateChase(); break;
                case EnemyState.Attack: UpdateAttack(); break;
                case EnemyState.Groggy: UpdateGroggy(); break;
                case EnemyState.Dead: break;
            }
        }

        // ── FSM ────────────────────────────────────────────────────────

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

        // ── 상태 전환 ──────────────────────────────────────────────────

        private void EnterGroggy()
        {
            _isGroggy = true;
            _state = EnemyState.Groggy;
            _groggyEndTime = Time.time + _groggyDuration;
            StopMoving();
            _groggyOutline?.SetOutline(true);
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
                colliders[i].enabled = false;

            // [2026-07-06 추가] 작두 자원 보충 — 처치 수단과 무관하게(총격/작두/기타) 적이 죽을 때마다
            // 작두 충전량을 1 회복시킵니다. KRExecutionSuccessEvent를 쓰지 않는 이유는, 그 이벤트가
            // 현재 프로젝트 어디에서도 Publish(발행)되지 않는 미사용 이벤트이기 때문입니다
            // (KRExecutionSuccessEvent.cs 주석엔 "KREnemyEntity가 발행"이라 적혀 있으나 그 클래스는
            // 더 이상 존재하지 않습니다). 그래서 여기서 KRJakduSystem을 직접 찾아 호출합니다.
            RefillJakduResourceOnKill();

            OnDeath();
            Destroy(gameObject, _despawnDelay);
        }

        /// <summary>
        /// 적 처치 시 플레이어의 작두(Jakdu) 자원을 1 회복시킵니다.
        /// _player는 Start()에서 "Player" 태그로 이미 캐싱해 둔 참조를 그대로 재사용합니다
        /// (FindGameObjectWithTag를 매 처치마다 반복 호출하지 않기 위함).
        /// </summary>
        private void RefillJakduResourceOnKill()
        {
            if (_player == null) return;

            // [2026-07-06 추가] 작두가 방금 자기 자신의 판정으로 처치한 대상이면 재충전하지 않습니다.
            // 안 그러면 "작두 자원 소모 → 작두로 처치 → 같은 프레임에 자원 재충전"이 반복되어
            // 작두가 사실상 자원을 소모하지 않는 것처럼 느껴지는 자기환급 버그가 생깁니다.
            // (다른 무기/시스템으로 처치했을 때는 그대로 작두 자원이 재충전됩니다.)
            if (KillRitual.Player.Combat.KRJakduSystem.IsSelfExecuting) return;

            var jakduSystem = _player.GetComponentInChildren<KillRitual.Player.Combat.KRJakduSystem>(true);
            jakduSystem?.AddResource(1);
        }

        protected virtual void OnDeath() { }

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
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            _agent.SetDestination(targetPosition);
        }

        protected void StopMoving()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        protected void FacePlayer()
        {
            if (_player == null) return;
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toPlayer);
        }

        // ── 색상 시각 피드백 ───────────────────────────────────────────

        private void UpdateColorFeedback()
        {
            // 그로기 상태의 시각 피드백은 색상 변경이 아닌 KRGroggyOutline(셰이더 테두리)으로 처리합니다.
            Color targetColor = Time.time < _hitFlashEndTime ? _hitFlashColor : _baseColor;
            ApplyColor(targetColor);
        }

        private void ApplyColor(Color color)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(kBaseColorId, color);
            _mpb.SetColor(kColorId, color);
            _renderer.SetPropertyBlock(_mpb);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectRange);
        }
    }
}