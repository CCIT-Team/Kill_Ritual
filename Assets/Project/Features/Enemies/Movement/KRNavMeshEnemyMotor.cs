using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class KRNavMeshEnemyMotor : KREnemyMotor
{
    [Header("NavMesh")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float angularSpeed = 720f;
    [SerializeField] private float stoppingDistance = 1.6f;
    // 공격 시작 거리와 비슷하게 맞추면 근접 적이 목표에 과하게 붙지 않는다.

    [SerializeField] private float destinationUpdateInterval = 0.12f;
    // 매 프레임 SetDestination을 호출하지 않기 위한 갱신 주기.

    [SerializeField] private float destinationUpdateThreshold = 0.5f;
    // 목표 위치가 이 거리 이상 바뀌었을 때만 새 경로를 요청한다.

    [SerializeField] private bool manualRotation = true;
    // true면 NavMeshAgent 회전을 끄고 코드로 직접 회전한다. FPS 적의 공격 방향 제어에 유리하다.

    [SerializeField] private bool warpToNearestNavMeshOnStart = true;
    // 적이 NavMesh 밖에서 시작했을 때 가까운 NavMesh로 보정한다.

    [SerializeField] private float navMeshSampleRadius = 2f;

    private NavMeshAgent agent;
    private Vector3 lastDestination;
    private float destinationTimer;
    private bool hasDestination;

    public NavMeshAgent Agent => agent;
    public bool IsAgentUsable => agent != null && agent.enabled && agent.isOnNavMesh;

    public override void Initialize(KREnemyRoot owner)
    {
        base.Initialize(owner);

        agent = GetComponent<NavMeshAgent>();
        ConfigureAgent();

        if (warpToNearestNavMeshOnStart)
            TryWarpToNearestNavMesh();
    }

    private void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = !manualRotation;
        agent.autoBraking = true;
        agent.autoRepath = true;
    }

    private void TryWarpToNearestNavMesh()
    {
        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    public override void TickIdle(float deltaTime)
    {
        // 경로가 남아 있으면 공격/대기 중에도 미끄러질 수 있으므로 정지시킨다.
        if (!IsAgentUsable)
            return;

        agent.isStopped = true;
    }

    public override void MoveTowards(Vector3 worldPosition, float deltaTime)
    {
        if (!IsAgentUsable)
            return;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;

        destinationTimer -= deltaTime;

        bool shouldUpdateDestination =
            !hasDestination ||
            destinationTimer <= 0f ||
            (worldPosition - lastDestination).sqrMagnitude >= destinationUpdateThreshold * destinationUpdateThreshold;

        if (shouldUpdateDestination)
        {
            agent.SetDestination(worldPosition);
            lastDestination = worldPosition;
            hasDestination = true;
            destinationTimer = Mathf.Max(0.02f, destinationUpdateInterval);
        }

        if (manualRotation)
        {
            Vector3 rotateDirection = agent.desiredVelocity.sqrMagnitude > 0.01f
                ? agent.desiredVelocity
                : GetFlatDirectionTo(worldPosition, out float _);

            RotateTowards(rotateDirection, deltaTime);
        }
    }

    public override void MoveDirection(Vector3 direction, float speed, float deltaTime)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.y = 0f;
        direction.Normalize();

        StopPathOnly();
        RotateTowards(direction, deltaTime);

        Vector3 offset = direction * speed * deltaTime;

        if (IsAgentUsable)
            agent.Move(offset);
        else
            transform.position += offset;
    }

    public override void RotateTowards(Vector3 direction, float deltaTime)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, angularSpeed * deltaTime);
    }

    public override void Stop()
    {
        if (!IsAgentUsable)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        hasDestination = false;
    }

    private void StopPathOnly()
    {
        if (!IsAgentUsable)
            return;

        agent.isStopped = true;

        if (agent.hasPath)
            agent.ResetPath();

        hasDestination = false;
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0f, newSpeed);

        if (agent != null)
            agent.speed = moveSpeed;
    }

    public void SetStoppingDistance(float newStoppingDistance)
    {
        stoppingDistance = Mathf.Max(0f, newStoppingDistance);

        if (agent != null)
            agent.stoppingDistance = stoppingDistance;
    }
}
