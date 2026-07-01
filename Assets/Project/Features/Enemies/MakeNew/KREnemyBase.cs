// Assets/Project/Scripts/05_Enemies/KREnemyBase.cs
using UnityEngine;
using UnityEngine.AI;                 // NavMeshAgent를 사용하기 위해 필요합니다.
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 모든 몬스터(Fodder / Heavy / SuperHeavy / Boss)의 공통 기반 클래스입니다.
    ///
    /// 이 클래스 하나가 다음을 모두 책임집니다:
    ///   1. 체력 관리와 피격 처리 (플레이어 무기 코드가 호출하는 IDamageable.TakeDamage 구현)
    ///   2. 그로기(Groggy) 상태 — 체력이 일정 비율 이하로 떨어지면 경직되며 주황색으로 빛납니다.
    ///   3. 사망 처리 — 체력이 0 이하가 되면 콜라이더를 끄고 잠시 후 오브젝트를 제거합니다.
    ///   4. FSM(유한 상태 머신)의 뼈대 — Idle / Chase / Attack / Groggy / Dead 5개 상태.
    ///   5. 색상 기반 시각 피드백 — 등급별 기본색, 피격 시 흰색 번쩍임, 그로기 시 주황 발광.
    ///
    /// [이동 방식: NavMesh]
    /// 이 클래스는 NavMeshAgent를 사용해 플레이어를 추격합니다. 즉 유니티가 미리 구워둔(Bake)
    /// NavMesh(걸어다닐 수 있는 바닥) 위에서, 벽이나 장애물을 알아서 우회하는 최단 경로로 따라옵니다.
    /// 우리가 직접 속도를 계산하지 않고, 에이전트에게 "목적지"만 알려주면(SetDestination) 됩니다.
    ///
    /// [중요] 이 클래스는 abstract(추상)입니다. 직접 GameObject에 붙일 수 없고,
    /// 반드시 이 클래스를 상속한 KRFodderMelee / KRFodderRanged 같은 "구체 몬스터"를 붙입니다.
    /// 상속받은 자식은 UpdateChase()와 UpdateAttack() 두 메서드만 자기 방식대로 구현하면 됩니다.
    ///
    /// [필수 컴포넌트] NavMeshAgent가 반드시 필요합니다. RequireComponent로 자동 추가되지만,
    /// 씬에 NavMesh가 구워져 있지 않으면 에이전트가 "NavMesh 위에 있지 않다"는 경고를 냅니다.
    ///
    /// [플레이어 무기와의 연결]
    /// 플레이어의 무기 스크립트(KRHitscanWeapon 등)는 GetComponentInParent<IDamageable>()로
    /// 이 컴포넌트를 찾아 TakeDamage()를 호출합니다. 따라서 이 컴포넌트가 붙은 GameObject(또는 그 부모)에는
    /// 반드시 Collider가 있어야 하고, "Damageable" 레이어에 속해 있어야 총에 맞습니다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class KREnemyBase : MonoBehaviour, IDamageable
    {
        /// <summary>몬스터가 가질 수 있는 5개의 행동 상태입니다.</summary>
        protected enum EnemyState
        {
            Idle,    // 대기: 플레이어를 아직 발견하지 못함
            Chase,   // 추격: 플레이어 쪽으로 이동
            Attack,  // 공격: 사거리 안에 들어와 공격 중
            Groggy,  // 그로기: 경직 상태(주황색 발광), 처형 대기
            Dead     // 사망: 종착 상태
        }

        // ==================================================================
        // 인스펙터 설정값 — 유니티 에디터의 Inspector 창에서 조절합니다.
        // ==================================================================

        [Header("체력")]
        [Tooltip("이 몬스터의 최대 체력입니다.")]
        [Min(1f)]
        [SerializeField] protected float _maxHealth = 30f;

        [Header("그로기 (경직)")]
        [Tooltip("체력이 '최대체력 × 이 비율' 이하로 떨어지면 그로기(경직) 상태가 됩니다. 0.3이면 30% 이하일 때입니다.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] protected float _groggyHealthRatio = 0.3f;

        [Tooltip("그로기(경직)가 유지되는 시간(초). 이 시간이 지나면 다시 움직입니다(처형당하지 않은 경우).")]
        [Min(0.1f)]
        [SerializeField] protected float _groggyDuration = 3f;

        [Header("감지 / 이동")]
        [Tooltip("플레이어를 발견하는 최대 거리. 이 안에 플레이어가 들어오면 추격을 시작합니다.")]
        [Min(1f)]
        [SerializeField] protected float _detectRange = 20f;

        [Tooltip("체크하면, 한 번이라도 플레이어를 발견한 뒤에는 거리와 상관없이 끝까지 추격합니다. " +
                 "끄면 감지 범위를 벗어났을 때 다시 대기(Idle) 상태로 돌아갑니다.")]
        [SerializeField] protected bool _chaseForever = true;

        [Tooltip("이동 속도(미터/초). NavMeshAgent의 speed에 적용됩니다.")]
        [Min(0f)]
        [SerializeField] protected float _moveSpeed = 3.5f;

        [Header("색상 (등급/상태 시각화)")]
        [Tooltip("이 몬스터의 평상시 기본 색상입니다. 등급(Fodder/Heavy 등)을 색으로 구분합니다.")]
        [SerializeField] protected Color _baseColor = Color.gray;

        [Tooltip("피격당한 순간 잠깐 번쩍이는 색(보통 흰색).")]
        [SerializeField] protected Color _hitFlashColor = Color.white;

        [Tooltip("그로기(경직) 상태일 때 빛나는 색(주황색).")]
        [SerializeField] protected Color _groggyColor = new Color(1f, 0.5f, 0f);

        [Tooltip("피격 번쩍임이 지속되는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] protected float _hitFlashDuration = 0.08f;

        [Header("사망")]
        [Tooltip("죽은 뒤 오브젝트가 화면에서 사라지기까지의 시간(초).")]
        [Min(0f)]
        [SerializeField] protected float _despawnDelay = 0.5f;

        // ==================================================================
        // 내부 런타임 상태 — 인스펙터에 노출되지 않는 작동용 변수들
        // ==================================================================

        protected EnemyState _state = EnemyState.Idle;   // 현재 FSM 상태
        protected float _health;                          // 현재 체력
        protected Transform _player;                      // 추격 대상(플레이어) Transform
        protected NavMeshAgent _agent;                    // NavMesh 기반 이동을 담당하는 에이전트

        // 한 번이라도 플레이어를 발견한 적이 있는지. _chaseForever가 켜져 있으면
        // 이 값이 true가 된 뒤부터는 거리와 상관없이 계속 추격합니다.
        protected bool _hasSpottedPlayer;

        private Renderer _renderer;                        // 색상을 바꿀 렌더러(큐브의 표면)
        private MaterialPropertyBlock _mpb;                // 색상을 효율적으로 바꾸기 위한 블록
        private float _hitFlashEndTime;                    // 피격 번쩍임이 끝나는 시각
        private float _groggyEndTime;                      // 그로기가 끝나는 시각
        private bool _isGroggy;                            // 그로기 여부

        // 색상 셰이더 프로퍼티 이름. URP/Standard 양쪽에서 흔히 쓰는 두 가지를 모두 시도합니다.
        private static readonly int kBaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
        private static readonly int kColorId = Shader.PropertyToID("_Color");         // Standard

        // ==================================================================
        // IDamageable 구현 — 플레이어 무기 코드가 이 4개를 통해 몬스터와 상호작용합니다.
        // ==================================================================

        /// <summary>이미 죽었는지 여부. 무기 코드가 중복 데미지를 막기 위해 확인합니다.</summary>
        public bool IsDead => _state == EnemyState.Dead;

        /// <summary>그로기(처형 대기) 상태인지 여부. 플레이어의 처형(E키) 판정에 사용됩니다.</summary>
        public bool IsGroggy => _isGroggy;

        /// <summary>현재 월드 위치. 처형 사거리/AoE 거리 계산 등에 사용됩니다.</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// 플레이어 무기가 데미지를 줄 때 호출됩니다. 체력을 깎고, 피격 번쩍임을 켜고,
        /// 임계치 이하면 그로기로 전환하고, 0 이하면 사망 처리합니다.
        /// </summary>
        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead)
            {
                return; // 이미 죽은 대상에게는 데미지가 들어가지 않습니다.
            }

            _health -= context.DamageAmount;

            // 피격 순간 흰색으로 잠깐 번쩍이게 만듭니다(시각 피드백).
            _hitFlashEndTime = Time.time + _hitFlashDuration;

            if (_health <= 0f)
            {
                EnterDead();
                return;
            }

            // 그로기 상태가 아직 아니면서 체력이 임계치 이하로 떨어졌다면 그로기로 진입합니다.
            if (!_isGroggy && _health <= _maxHealth * _groggyHealthRatio)
            {
                EnterGroggy();
            }
        }

        /// <summary>
        /// 그로기 상태의 몬스터를 플레이어가 처형(E키)할 때 호출됩니다. 즉시 사망 처리합니다.
        /// (처형 사거리/시야 판정은 호출부인 KRCombatSystem이 이미 끝낸 상태로 들어옵니다.)
        /// </summary>
        public void Execute()
        {
            if (IsDead)
            {
                return;
            }

            // [선택] 처형 보상 이벤트(KRExecutionSuccessEvent)를 발행하고 싶다면 여기에 추가합니다.
            // 지금은 외부 매니저 의존 없이 안전하게 돌아가도록 비워둡니다.
            // 나중에 KRManagers가 준비되면 이 자리에서 이벤트를 발행하면 플레이어가 체력/자원을 회복합니다.

            EnterDead();
        }

        // ==================================================================
        // 유니티 생명주기 메서드
        // ==================================================================

        protected virtual void Awake()
        {
            _health = _maxHealth;

            // NavMeshAgent를 가져와 이동 속도를 설정합니다.
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = _moveSpeed;

            // 색상을 바꿀 렌더러를 찾습니다. 큐브 자신 또는 자식에서 찾습니다.
            _renderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();

            ApplyColor(_baseColor);
        }

        protected virtual void Start()
        {
            // 씬에서 플레이어를 찾습니다. 플레이어 오브젝트에 "Player" 태그가 지정돼 있어야 합니다.
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _player = playerObject.transform;
            }
            else
            {
                Debug.LogWarning($"[{name}] 'Player' 태그를 가진 오브젝트를 찾지 못했습니다. " +
                                 "플레이어 오브젝트의 Tag를 Player로 설정하세요.");
            }
        }

        protected virtual void Update()
        {
            UpdateColorFeedback();

            // FSM: 현재 상태에 따라 매 프레임 적절한 동작을 실행합니다.
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
                    // 사망 상태에서는 아무 행동도 하지 않습니다.
                    break;
            }
        }

        // ==================================================================
        // FSM 상태별 동작
        // ==================================================================

        /// <summary>대기 상태: 플레이어가 감지 범위 안에 들어오면 추격을 시작합니다.</summary>
        protected virtual void UpdateIdle()
        {
            if (_player == null)
            {
                return;
            }

            if (DistanceToPlayer() <= _detectRange)
            {
                _hasSpottedPlayer = true; // 한 번 발견했음을 기록합니다.
                _state = EnemyState.Chase;
            }
        }

        /// <summary>
        /// 지금 플레이어를 계속 추격해야 하는지 판단합니다.
        /// - _chaseForever가 켜져 있고 한 번이라도 발견한 적이 있으면(_hasSpottedPlayer) → 거리 무관하게 항상 true.
        /// - 그렇지 않으면 → 감지 범위(_detectRange) 안에 있을 때만 true.
        /// 자식 클래스(근거리/원거리)가 "Idle로 돌아갈지" 결정할 때 이 메서드를 사용합니다.
        /// </summary>
        protected bool ShouldKeepChasing()
        {
            if (_player == null)
            {
                return false;
            }

            if (_chaseForever && _hasSpottedPlayer)
            {
                return true; // 한 번 봤으면 끝까지 쫓습니다.
            }

            return DistanceToPlayer() <= _detectRange;
        }

        /// <summary>
        /// 추격 상태: 자식 클래스(근거리/원거리)가 각자 구현합니다.
        /// 근거리는 플레이어에게 직접 다가가고, 원거리는 일정 거리를 유지합니다.
        /// </summary>
        protected abstract void UpdateChase();

        /// <summary>
        /// 공격 상태: 자식 클래스가 각자 구현합니다.
        /// 근거리는 접촉 데미지, 원거리는 발사체를 쏩니다.
        /// </summary>
        protected abstract void UpdateAttack();

        /// <summary>그로기 상태: 모든 행동을 멈추고, 시간이 지나면 다시 추격 상태로 복귀합니다.</summary>
        protected virtual void UpdateGroggy()
        {
            // 그로기 동안에는 이동을 멈춥니다.
            StopMoving();

            if (Time.time >= _groggyEndTime)
            {
                ExitGroggy();
            }
        }

        // ==================================================================
        // 상태 전환 헬퍼
        // ==================================================================

        private void EnterGroggy()
        {
            _isGroggy = true;
            _state = EnemyState.Groggy;
            _groggyEndTime = Time.time + _groggyDuration;
            StopMoving();
        }

        private void ExitGroggy()
        {
            _isGroggy = false;
            // 그로기에서 빠져나오면 다시 플레이어를 쫓습니다.
            _state = EnemyState.Chase;
        }

        private void EnterDead()
        {
            _state = EnemyState.Dead;
            _isGroggy = false;
            _health = 0f;

            StopMoving();

            // 에이전트를 꺼서 더 이상 이동/경로 계산을 하지 않게 합니다.
            if (_agent != null && _agent.enabled)
            {
                _agent.enabled = false;
            }

            // 죽은 뒤에는 총알이 통과하고 플레이어와 부딪히지 않도록 콜라이더를 모두 끕니다.
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            OnDeath(); // 자식이 추가 처리(예: 사망 이펙트)를 넣을 수 있는 훅.

            Destroy(gameObject, _despawnDelay);
        }

        /// <summary>사망 순간 자식 클래스가 추가 동작을 넣고 싶을 때 오버라이드합니다(기본은 비어 있음).</summary>
        protected virtual void OnDeath() { }

        // ==================================================================
        // 공용 유틸리티 — 자식 클래스가 사용합니다.
        // ==================================================================

        /// <summary>
        /// 플레이어에게 데미지를 줄 대상(IDamageable)을 찾습니다.
        /// 플레이어에는 IDamageable이 여러 개 붙어 있을 수 있으므로(KRCombatSystem과
        /// KRPlayerDamageFeedback), 게임오버·화면효과·체력바를 담당하는 KRPlayerDamageFeedback을
        /// 우선적으로 찾습니다. 없으면 아무 IDamageable이나 사용합니다.
        /// </summary>
        protected IDamageable FindPlayerDamageable(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return null;
            }

            // 1순위: 게임오버/체력바를 담당하는 KRPlayerDamageFeedback.
            var feedback = playerTransform.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
            if (feedback != null)
            {
                return feedback;
            }

            // 2순위: 그 외 아무 IDamageable(예: KRCombatSystem).
            return playerTransform.GetComponentInParent<IDamageable>();
        }

        /// <summary>플레이어까지의 거리. 플레이어가 없으면 매우 큰 값을 반환합니다.</summary>
        protected float DistanceToPlayer()
        {
            if (_player == null)
            {
                return float.MaxValue;
            }
            return Vector3.Distance(transform.position, _player.position);
        }

        /// <summary>
        /// 지정한 목표 지점으로 이동하도록 NavMeshAgent에 목적지를 설정합니다.
        /// 에이전트가 NavMesh 위에서 벽을 우회하는 경로를 알아서 찾아 걸어갑니다.
        /// </summary>
        protected void MoveTowards(Vector3 targetPosition)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(targetPosition);
        }

        /// <summary>이동을 멈춥니다(에이전트 정지).</summary>
        protected void StopMoving()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        /// <summary>플레이어 쪽을 바라보도록 수평으로 회전합니다(원거리 몬스터 조준 등에 사용).</summary>
        protected void FacePlayer()
        {
            if (_player == null)
            {
                return;
            }

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer);
            }
        }

        // ==================================================================
        // 색상 시각 피드백
        // ==================================================================

        /// <summary>매 프레임 현재 상태에 맞는 색을 결정해 큐브 표면에 적용합니다.</summary>
        private void UpdateColorFeedback()
        {
            Color targetColor;

            if (Time.time < _hitFlashEndTime)
            {
                // 피격 직후: 흰색 번쩍임이 그로기 발광보다 우선합니다(맞은 게 확실히 보이도록).
                targetColor = _hitFlashColor;
            }
            else if (_isGroggy)
            {
                // 그로기 중: 주황색 발광.
                targetColor = _groggyColor;
            }
            else
            {
                targetColor = _baseColor;
            }

            ApplyColor(targetColor);
        }

        /// <summary>
        /// MaterialPropertyBlock으로 색을 적용합니다. 머티리얼을 복제하지 않으므로
        /// 메모리 낭비 없이 개별 몬스터마다 다른 색을 줄 수 있습니다.
        /// URP(_BaseColor)와 Standard(_Color) 셰이더 양쪽을 모두 지원합니다.
        /// </summary>
        private void ApplyColor(Color color)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(kBaseColorId, color);
            _mpb.SetColor(kColorId, color);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ==================================================================
        // 에디터 기즈모 — 씬 뷰에서 감지 범위를 노란 원으로 표시합니다(디버그용).
        // ==================================================================
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectRange);
        }
    }
}