using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public abstract class EnemyBase : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,      // 대기 (가만히 있음)
        Chase,     // 추격 (플레이어를 따라감)
        Attack,    // 공격
        Hit,       // 피격 (공격 받음)
        Dead       // 사망
    }

    [Header("=== 기본 스탯 ===")]

    [SerializeField] 
    protected float maxHP = 50f;

    [SerializeField] 
    protected float currentHP;

    [SerializeField] 
    protected float attackDamage = 10f;

    [SerializeField] 
    protected float attackCooldown = 1.5f;

    [Header("=== 이동 설정 ===")]

    [SerializeField] 
    protected float moveSpeed = 3.5f;

    [SerializeField] 
    protected float chaseRange = 15f;

    [SerializeField] 
    protected float attackRange = 2f;

    [SerializeField] 
    protected float stopDistance = 1.5f;

    [Header("=== 시각 피드백 ===")]

    [SerializeField] 
    protected Renderer bodyRenderer;

    [SerializeField] 
    protected Color hitFlashColor = Color.red;

    [SerializeField] 
    protected float hitFlashDuration = 0.1f;

    protected NavMeshAgent agent;

    protected Transform playerTransform;

    protected PlayerStats playerStats;

    protected EnemyState currentState = EnemyState.Idle;

    protected float lastAttackTime = -999f;

    private Color originalColor;

    public EnemyState CurrentState => currentState;
    public bool IsDead => currentState == EnemyState.Dead;
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            Debug.LogWarning($"[{name}] NavMeshAgent가 없어서 자동 추가했습니다.");
        }

        currentHP = maxHP;
    }

    protected virtual void Start()
    {
        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;

        if (EnemyManager.Instance != null)
        {
            playerTransform = EnemyManager.Instance.PlayerTransform;

            EnemyManager.Instance.RegisterEnemy(this);
        }
        else
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform != null)
        {
            playerStats = playerTransform.GetComponent<PlayerStats>();
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<Renderer>();
        }

        if (bodyRenderer != null)
        {
            originalColor = bodyRenderer.material.color;
        }

        OnStart();
    }

    protected virtual void OnStart() { }

    protected virtual void Update()
    {
        if (currentState == EnemyState.Dead) return;

        UpdateStateMachine();
    }

    private void UpdateStateMachine()
    {
        if (playerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState(distToPlayer);
                break;

            case EnemyState.Chase:
                HandleChaseState(distToPlayer);
                break;

            case EnemyState.Attack:
                HandleAttackState(distToPlayer);
                break;

            case EnemyState.Hit:
                break;
        }
    }

    private void HandleIdleState(float distToPlayer)
    {
        agent.isStopped = true; // 이동 멈춤

        if (distToPlayer <= chaseRange)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    private void HandleChaseState(float distToPlayer)
    {
        if (distToPlayer <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        if (distToPlayer > chaseRange)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(playerTransform.position);
    }

    private void HandleAttackState(float distToPlayer)
    {
        agent.isStopped = true; // 공격 중엔 이동 멈춤

        if (distToPlayer > attackRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        if (Time.time - lastAttackTime >= attackCooldown)
        {
            PerformAttack();
        }
    }

    protected virtual void PerformAttack()
    {
        lastAttackTime = Time.time; // 공격 시간 기록

        // 플레이어 스탯에 피해 입히기
        if (playerStats != null && !playerStats.IsDead)
        {
            playerStats.TakeDamage(attackDamage);
            Debug.Log($"[{name}] 플레이어에게 {attackDamage} 피해!");
        }
    }

    protected void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return; // 같은 상태면 무시
        currentState = newState;
        // 상태 전환 시 자식 클래스가 추가 처리할 수 있도록 알림
        OnStateChanged(newState);
    }

    protected virtual void OnStateChanged(EnemyState newState) { }

    public virtual void TakeDamage(float damage)
    {
        if (IsDead) return;

        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0f);

        Debug.Log($"[{name}] 피해 받음: {damage} / 남은 HP: {currentHP}");

        // 피격 색상 번쩍임
        StartCoroutine(HitFlash());

        // 사망 체크
        if (currentHP <= 0f)
        {
            Die();
        }
        else
        {
            ChangeState(EnemyState.Hit);
        }
    }

    private IEnumerator HitFlash()
    {
        if (bodyRenderer == null) yield break;

        bodyRenderer.material.color = hitFlashColor;      // 빨간색으로 변경
        yield return new WaitForSeconds(hitFlashDuration); // 잠깐 대기
        bodyRenderer.material.color = originalColor;      // 원래 색으로 복구
    }

    protected virtual void Die()
    {
        ChangeState(EnemyState.Dead);
        agent.isStopped = true;        // 이동 중지
        agent.enabled = false;         // NavMeshAgent 비활성화

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterEnemy(this);
        }

        Debug.Log($"[{name}] 사망!");
        OnDeath();

        Destroy(gameObject, 2f);
    }

    protected virtual void OnDeath() { }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}