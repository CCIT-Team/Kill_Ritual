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

            // [2026-07-07 추가] ModifyIncomingDamage() 훅을 거쳐 최종 피해량을 결정합니다.
            // 기본 구현은 context.DamageAmount를 그대로 반환하므로 기존 적들은 동작 변화가 없습니다.
            ApplyDamageInternal(ModifyIncomingDamage(context));
        }

        /// <summary>
        /// [2026-07-07 추가] 불가살이 보스의 부위별 약점(KRBossBodyPart) 전용 진입점입니다.
        /// KRBossBodyPart가 "이 부위가 지금 철갑인지 노출인지"에 따라 이미 최종 피해량을 계산해서
        /// 넘기기 때문에, 여기서는 ModifyIncomingDamage()(몸통 전체 기본 방어용 훅)를 다시 거치지
        /// 않고 곧바로 체력에 반영합니다 — 그렇지 않으면 부위 배율과 몸통 방어 배율이 이중으로
        /// 곱해져서 의도한 것보다 훨씬 적은 피해가 들어가는 문제가 생깁니다.
        /// 일반 적들은 이 메서드를 쓸 일이 없습니다(부위별 약점이 없으므로).
        /// </summary>
        public void TakeDamageDirect(KRDamageContext context)
        {
            if (IsDead) return;
            ApplyDamageInternal(context.DamageAmount);
        }

        private void ApplyDamageInternal(float amount)
        {
            _health -= amount;
            _hitFlashEndTime = Time.time + _hitFlashDuration;

            // [2026-07-07 추가] 현재 체력 비율을 알려주는 훅 — 보스의 페이즈 전환 감지 등에 사용합니다.
            OnHealthChanged(Mathf.Clamp01(_health / _maxHealth));

            if (_health <= 0f) { EnterDead(); return; }

            if (!_isGroggy && _health <= _maxHealth * _groggyHealthRatio)
                EnterGroggy(_groggyDuration);
        }

        /// <summary>
        /// [2026-07-07 추가] 들어오는 피해량을 실제로 적용하기 직전에 가공할 수 있는 훅입니다.
        /// 기본 구현은 가공 없이 그대로 반환합니다(기존 적 전부 영향 없음).
        /// 보스처럼 "몸통에 직접 맞으면 거의 무적" 같은 방어 게이트가 필요한 하위 클래스에서
        /// 오버라이드하세요(예: KRBossJakdu01 — TakeDamage() 경로, 즉 부위가 아닌 몸통 콜라이더에
        /// 직접 맞았을 때만 적용됩니다. 부위별 피해는 TakeDamageDirect()로 별도 처리됩니다).
        /// </summary>
        protected virtual float ModifyIncomingDamage(KRDamageContext context) => context.DamageAmount;

        /// <summary>
        /// [2026-07-07 추가] 피해 적용 직후(TakeDamage 안에서) 현재 체력 비율(0~1)을 알려주는 훅입니다.
        /// 기본 구현은 아무 것도 하지 않습니다. 보스의 체력 임계값 기반 페이즈 전환 감지에 사용하세요.
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

            // [2026-07-07 변경] 예전엔 계층 안의 모든 콜라이더를 무조건 "이 적 자신"에게 등록했습니다.
            // 불가살이 보스처럼 부위마다 자기만의 IDamageable(KRBossBodyPart)을 갖는 경우, 그 부위의
            // 콜라이더까지 여기서 보스 자신에게 등록해버리면 두 등록이 같은 콜라이더를 두고 경쟁하게
            // 되고, 어느 쪽이 나중에 실행되느냐(초기화 순서)에 따라 부위별 약점이 무시될 수 있습니다.
            // 그래서 "이 콜라이더의 GameObject에 이미 다른 IDamageable이 있는 경우"는 제외하고,
            // 그 부위 컴포넌트가 스스로 자신을 등록하도록 넘깁니다(KRBossBodyPart.OnEnable() 참고).
            var ownColliderList = new List<Collider>();
            foreach (Collider col in GetComponentsInChildren<Collider>(includeInactive: false))
            {
                IDamageable colDamageable = col.GetComponent<IDamageable>();
                if (colDamageable != null && !ReferenceEquals(colDamageable, this)) continue;
                ownColliderList.Add(col);
            }
            _ownColliders = ownColliderList.ToArray();

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
                // [2026-07-07 추가] "이동을 안 한다"는 문제의 첫 번째 용의자 — 애초에 Idle에서
                // Chase로 전환이 안 되면 UpdateChase()/UpdateAttack() 자체가 절대 호출되지 않아서
                // MoveTowards()도 패턴도 아무것도 실행되지 않습니다. 이 로그가 안 뜨면 원인은
                // 여기(플레이어 미감지)입니다 — 100% 확실하게 구분하기 위한 로그.
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
        /// [2026-07-07 추가] 외부(주로 보스 자신의 페이즈 시퀀스)에서 강제로 그로기 상태에
        /// 진입시킬 때 씁니다. 예: 무령 패링 성공 시 평소보다 훨씬 긴 처형 창을 열어주는 용도.
        /// duration을 생략하거나 0 이하로 넘기면 인스펙터의 기본 _groggyDuration을 그대로 씁니다.
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
            if (_agent == null || !_agent.enabled) return;

            // [2026-07-07 추가] "이동을 안 한다"는 문제의 대부분은 NavMeshAgent가 NavMesh 위에 없어서
            // SetDestination이 조용히 무시되는 경우입니다. 원인을 바로 알 수 있게 경고를 남깁니다.
            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning($"[KREnemyBase] {name}: NavMeshAgent가 NavMesh 위에 있지 않아 이동이 무시됩니다. " +
                                  "씬에 NavMesh가 베이크되어 있는지, 스폰 위치가 NavMesh 범위 안인지 확인하세요.");
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
        /// [2026-07-07 변경] maxDegreesPerSecond를 생략(기본값 -1)하면 기존과 동일하게 즉시 정면으로
        /// 스냅합니다(일반 몹은 그대로 이 동작을 씀). 값을 양수로 주면 그 속도로만 천천히 회전합니다 —
        /// 거대한 보스가 매 프레임 즉시 플레이어를 조준해버리면 플레이어가 등/옆으로 돌아갈 방법이
        /// 전혀 없어지므로(항상 정면이 플레이어를 향하게 즉시 보정됨), 불가살이처럼 "등 약점"을
        /// 실제로 노려서 때릴 수 있게 하려면 회전 속도 제한이 필요합니다.
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
                : Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesPerSecond * Time.deltaTime);
        }

        // ── 색상 시각 피드백 ───────────────────────────────────────────

        /// <summary>
        /// [2026-07-07 추가] 하위 클래스가 특정 구간 동안 강제로 몸 색을 바꾸고 싶을 때 씁니다.
        /// null이면(기본) 기존처럼 히트 플래시/기본색 로직을 그대로 따릅니다.
        /// 값을 설정한 동안은 히트 플래시도 이 색에 덮어써집니다(우선순위가 더 높음).
        /// 예: KRBossJakdu01이 패링 예고/판정 구간 동안 몸 색을 다르게 표시해 "지금이 그 타이밍"임을
        /// 시각적으로 알리는 데 사용합니다(그동안은 아무 시각 피드백이 없어서 기준을 알 수 없었음).
        /// 다 쓴 뒤에는 반드시 null로 되돌려 원래 색 로직이 다시 동작하게 하세요.
        /// </summary>
        protected Color? OverrideColor { get; set; }

        private void UpdateColorFeedback()
        {
            if (OverrideColor.HasValue)
            {
                ApplyColor(OverrideColor.Value);
                return;
            }

            // 그로기 상태의 시각 피드백은 색상 변경이 아닌 KRGroggyOutline(셰이더 테두리)으로 처리합니다.
            Color targetColor = Time.time < _hitFlashEndTime ? _hitFlashColor : _baseColor;
            ApplyColor(targetColor);
        }

        private void ApplyColor(Color color)
        {
            if (_renderer == null) return;

            // [2026-07-07 추가] _mpb는 원래 Awake()에서 한 번만 생성됩니다. 근데 플레이 모드
            // 도중 스크립트를 수정하면(도메인 리로드) 유니티가 Awake()를 다시 불러주지 않으면서도
            // 이런 직렬화 안 되는 필드는 null로 리셋해버릴 수 있어서, 드물게 Update() 도중
            // _mpb가 null인 채로 GetPropertyBlock(null)이 호출돼 ArgumentNullException이 났습니다.
            // 여기서 안전하게 다시 만들어주면 어떤 이유로 null이 되든 확실히 막힙니다.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

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