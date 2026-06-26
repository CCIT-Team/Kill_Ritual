using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // NavMeshAgent를 쓰기 위해 반드시 필요

// ─────────────────────────────────────────────────────────────
// KR_EnemyAI.cs
// 역할: 적 한 마리의 "두뇌". 보내주신 행동 트리를 그대로 코드로 옮긴 것.
//   매 프레임 위에서부터 조건을 검사해서 상태(State)를 정하고,
//   그 상태에 맞는 행동을 한다.
//
//   ┌ 경직(Stagger) : 체력이 일정 이하로 떨어지면 잠깐 멈춤  (최우선)
//   ├ 공격(Attack)  : 사거리 안 + 시야 확보 → 플레이어 타격
//   └ 추격(Chase)   : 그 외 → NavMesh로 플레이어에게 접근
//
//   "어떻게 거기까지 걸어가는가"는 NavMeshAgent에게 전부 맡긴다.
//   (벽 피하기 · 경로 계산 · 다른 적과 안 겹치기 = 전부 자동)
//
// 부착 위치: Enemy (부모 빈 오브젝트. NavMeshAgent가 붙어 있는 곳)
// ─────────────────────────────────────────────────────────────

// 이 스크립트가 붙은 오브젝트에는 NavMeshAgent가 반드시 있어야 한다.
// 깜빡하고 안 붙였으면 유니티가 자동으로 붙여준다(실수 방지).
[RequireComponent(typeof(NavMeshAgent))]
public class KR_EnemyAI : MonoBehaviour
{
    // 적이 가질 수 있는 상태들. 한 순간에 딱 하나만 가진다.
    public enum State { Chase, Attack, Stagger }

    [Header("현재 상태 (실행 중 확인용 / 직접 건드리지 말 것)")]
    [SerializeField] private State currentState = State.Chase;

    [Header("거리 설정")]
    [Tooltip("이 거리 안으로 들어오면 공격을 시도한다")]
    public float attackRange = 2.5f;
    [Tooltip("이 거리 밖이면 추격조차 하지 않고 대기(아주 멀면 연산 절약)")]
    public float detectRange = 40f;

    [Header("공격 설정")]
    [Tooltip("공격과 공격 사이의 최소 간격(초)")]
    public float attackCooldown = 1.2f;
    [Tooltip("한 번 때릴 때 플레이어에게 주는 피해량")]
    public float attackDamage = 10f;

    [Header("시야(레이캐스트) 설정")]
    [Tooltip("이 레이어에 막히면 시야가 가려진 것으로 본다(보통 벽 레이어)")]
    public LayerMask obstacleMask;

    [Header("경직 설정")]
    [Tooltip("경직이 풀릴 때까지 멈춰 있는 시간(초)")]
    public float staggerDuration = 0.8f;

    // ── 내부에서만 쓰는 변수들 ──
    private NavMeshAgent agent;     // 길찾기 이동 담당
    private Transform target;       // 쫓아갈 대상(플레이어). 매니저가 넣어준다.
    private float attackTimer = 0f; // 다음 공격까지 남은 시간
    private float staggerTimer = 0f;// 경직이 풀릴 때까지 남은 시간

    // 경로를 매 프레임 다시 계산하면 무겁다.
    // 이 간격(초)마다 한 번씩만 목적지를 갱신한다(최적화 핵심).
    private const float REPATH_INTERVAL = 0.15f;
    private float repathTimer = 0f;

    void Awake()
    {
        // 같은 오브젝트에 붙은 NavMeshAgent를 가져온다.
        agent = GetComponent<NavMeshAgent>();
    }

    // 매니저가 "쫓아갈 플레이어는 이 사람이야" 하고 알려줄 때 호출한다.
    // (각 적이 직접 플레이어를 찾지 않게 해서 성능을 아낀다)
    public void SetTarget(Transform player)
    {
        target = player;
    }

    void Update()
    {
        // 아직 추격 대상이 없으면 아무것도 하지 않는다.
        if (target == null) return;

        // 쿨다운 타이머는 매 프레임 줄여준다.
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        // ── 행동 트리: 위에서부터 조건 검사 ──
        // 1순위) 경직 중이면 다른 건 무시하고 경직만 처리
        if (currentState == State.Stagger)
        {
            TickStagger();
            return;
        }

        // 플레이어와의 거리(제곱)를 구한다.
        // sqrMagnitude는 제곱근 계산을 생략해 magnitude보다 빠르다(최적화).
        float sqrDistance = (target.position - transform.position).sqrMagnitude;

        // 너무 멀면(detectRange 밖) 그냥 멈춰서 대기 → 연산 절약
        if (sqrDistance > detectRange * detectRange)
        {
            EnterChaseIdle();
            return;
        }

        // 2순위) 사거리 안 + 시야 확보 → 공격
        bool inAttackRange = sqrDistance <= attackRange * attackRange;
        if (inAttackRange && HasLineOfSight())
        {
            currentState = State.Attack;
            TickAttack();
        }
        // 3순위) 그 외 → 추격
        else
        {
            currentState = State.Chase;
            TickChase();
        }
    }

    // ── 추격: NavMesh로 플레이어에게 접근 ──
    void TickChase()
    {
        if (agent.isStopped) agent.isStopped = false;

        // 경로 갱신은 매 프레임이 아니라 일정 간격마다 한 번만(최적화)
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = REPATH_INTERVAL;
            // SetDestination 한 줄이면 NavMesh가 알아서 길을 찾아 걸어간다.
            agent.SetDestination(target.position);
        }
    }

    // 너무 멀 때: 멈춰서 대기(추격 상태이긴 하나 이동만 정지)
    void EnterChaseIdle()
    {
        currentState = State.Chase;
        if (!agent.isStopped) agent.isStopped = true;
    }

    // ── 공격: 멈춰서 플레이어를 바라보고, 쿨다운마다 타격 ──
    void TickAttack()
    {
        // 사거리 안에 들어왔으니 이동은 멈춘다.
        if (!agent.isStopped) agent.isStopped = true;

        // 플레이어 쪽으로 몸을 부드럽게 돌린다(Y축만).
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
        }

        // 쿨다운이 다 됐으면 한 대 때린다.
        if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            DealDamage();
        }
    }

    // 실제 피해를 주는 부분.
    // 플레이어 체력 스크립트가 생기면 여기서 호출하면 된다(아래 주석 참고).
    void DealDamage()
    {
        // 예시) 플레이어에 KR_PlayerHealth 같은 스크립트가 있다면:
        // var hp = target.GetComponent<KR_PlayerHealth>();
        // if (hp != null) hp.TakeDamage(attackDamage);

        Debug.Log($"{name}가 플레이어를 공격! 피해 {attackDamage}");
    }

    // ── 경직: 잠깐 멈췄다가 시간이 지나면 추격으로 복귀 ──
    void TickStagger()
    {
        if (!agent.isStopped) agent.isStopped = true;

        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0f)
        {
            // 경직 종료 → 다시 추격부터 시작
            currentState = State.Chase;
            agent.isStopped = false;
        }
    }

    // 체력 스크립트(KR_EnemyHealth)가 "경직 들어가!"라고 부를 함수.
    public void EnterStagger()
    {
        currentState = State.Stagger;
        staggerTimer = staggerDuration;
        if (agent != null) agent.isStopped = true;
    }

    // ── 시야 판정: 적과 플레이어 사이에 벽이 있는지 확인 ──
    // 벽에 막히면 false(못 봄), 뻥 뚫려 있으면 true(봄).
    bool HasLineOfSight()
    {
        Vector3 origin = transform.position + Vector3.up * 1f; // 눈높이쯤
        Vector3 toTarget = (target.position + Vector3.up * 1f) - origin;

        // origin에서 플레이어 방향으로 광선을 쏴서 벽(obstacleMask)에 맞는지 본다.
        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude, obstacleMask))
        {
            return false; // 벽에 먼저 맞았다 = 플레이어를 못 본다
        }
        return true; // 막힌 게 없다 = 플레이어가 보인다
    }

    // Scene 뷰에서 사거리를 눈으로 확인하기 위한 보조선(게임 실행과 무관).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}