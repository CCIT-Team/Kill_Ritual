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

        public void Execute()
        {
            if (IsDead) return;

            var combatSystem = GameObject.FindGameObjectWithTag("Player")
                ?.GetComponentInParent<KillRitual.Player.Combat.KRCombatSystem>();
            GetComponent<KillRitual.Items.KRDropSpawner>()
                ?.SpawnDrops(transform.position, combatSystem?.CurrentElement
                ?? KRDamageType.Fire);

            Debug.Log($"[KREnemyBase] {name} 처형됨");
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
            _groggyOutline = GetComponent<KRGroggyOutline>();
            // 없으면 자동으로 추가
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

            OnDeath();
            Destroy(gameObject, _despawnDelay);
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