using UnityEngine;

public abstract class KREnemyMotor : MonoBehaviour
{
    protected KREnemyRoot Root { get; private set; }

    public virtual void Initialize(KREnemyRoot owner)
    {
        Root = owner;
    }

    public abstract void TickIdle(float deltaTime);
    // 대기 중 처리. NavMeshAgent 정지, 중력/위치 보정 등에 사용한다.

    public abstract void MoveTowards(Vector3 worldPosition, float deltaTime);
    // 경로 기반 추적 이동.

    public abstract void MoveDirection(Vector3 direction, float speed, float deltaTime);
    // 돌진/넉백처럼 특정 방향으로 강제 이동.

    public abstract void RotateTowards(Vector3 direction, float deltaTime);
    // 특정 방향을 향해 회전.

    public abstract void Stop();
    // 이동 중지.

    public Vector3 GetFlatDirectionTo(Vector3 worldPosition, out float distance)
    {
        Vector3 toTarget = worldPosition - transform.position;
        toTarget.y = 0f;

        distance = toTarget.magnitude;

        if (distance <= 0.001f)
            return Vector3.zero;

        return toTarget / distance;
    }
}
