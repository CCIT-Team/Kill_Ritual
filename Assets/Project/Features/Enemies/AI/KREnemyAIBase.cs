using UnityEngine;

public abstract class KREnemyAIBase : MonoBehaviour
{
    [Header("Aggro")]
    [SerializeField] private KRAggroPolicy aggroPolicy = KRAggroPolicy.LockOnFirstDetection;
    // 살굿 기본값은 LockOnFirstDetection이다. 한 번 감지하면 추적을 멈추지 않는다.

    [SerializeField] private float forgetAfterLostSightTime = 3f;
    // ForgetAfterLostSight 정책을 쓸 때만 사용한다.

    public KREnemyState CurrentState { get; protected set; } = KREnemyState.Idle;
    public bool HasAggro { get; protected set; }
    public bool IsDead => CurrentState == KREnemyState.Dead;
    public bool IsAttacking => CurrentState == KREnemyState.Attacking;

    protected KREnemyRoot Root { get; private set; }

    private bool initialized;
    private float lostSightTimer;

    public virtual void Initialize(KREnemyRoot root)
    {
        Root = root;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            TryLazyInitialize();

        if (!initialized || Root == null)
            return;

        if (IsDead)
            return;

        float deltaTime = Time.deltaTime;

        Root.Target?.Tick(deltaTime);
        TickPerception(deltaTime);
        TickAI(deltaTime);
    }

    private void TryLazyInitialize()
    {
        KREnemyRoot root = GetComponent<KREnemyRoot>();

        if (root != null && root.IsInitialized)
            Initialize(root);
    }

    protected virtual void TickPerception(float deltaTime)
    {
        if (Root.Target == null || !Root.Target.HasValidTarget)
            return;

        if (HasAggro && aggroPolicy == KRAggroPolicy.LockOnFirstDetection)
            return;

        bool canSee = Root.Perception != null && Root.Perception.TickCanDetect(deltaTime);

        if (canSee)
        {
            BecomeAggro();
            lostSightTimer = 0f;
            return;
        }

        if (!HasAggro || aggroPolicy != KRAggroPolicy.ForgetAfterLostSight)
            return;

        lostSightTimer += deltaTime;

        if (lostSightTimer >= forgetAfterLostSightTime)
            LoseAggro();
    }

    protected abstract void TickAI(float deltaTime);
    // 몬스터마다 다른 행동 판단은 자식 AI 클래스에서 구현한다.

    public virtual void OnDamaged(KRDamageInfo damageInfo)
    {
        if (IsDead)
            return;

        BecomeAggro();
    }

    public virtual void OnDeath(KRDamageInfo damageInfo)
    {
        CurrentState = KREnemyState.Dead;
        HasAggro = false;
        Root?.Motor?.Stop();
        Root?.Visual?.SetMoving(false);
    }

    protected void BecomeAggro()
    {
        if (IsDead)
            return;

        bool wasAggro = HasAggro;
        HasAggro = true;

        if (CurrentState == KREnemyState.Idle)
            CurrentState = KREnemyState.Chasing;

        if (!wasAggro)
            Root?.Visual?.PlayAggro();
    }

    protected void LoseAggro()
    {
        if (IsDead)
            return;

        HasAggro = false;
        CurrentState = KREnemyState.Idle;
        Root?.Motor?.Stop();
        Root?.Visual?.SetMoving(false);
    }

    protected void SetState(KREnemyState newState)
    {
        if (IsDead)
            return;

        CurrentState = newState;
    }

    protected bool HasUsableTarget()
    {
        return Root != null && Root.Target != null && Root.Target.HasValidTarget;
    }

    protected void ChaseTarget(float deltaTime)
    {
        if (!HasUsableTarget() || Root.Motor == null)
        {
            Root?.Visual?.SetMoving(false);
            return;
        }

        CurrentState = KREnemyState.Chasing;
        Root.Motor.MoveTowards(Root.Target.AimPosition, deltaTime);
        Root.Visual?.SetMoving(true);
    }

    protected void StopMovement(float deltaTime)
    {
        Root?.Motor?.TickIdle(deltaTime);
        Root?.Visual?.SetMoving(false);
    }
}
