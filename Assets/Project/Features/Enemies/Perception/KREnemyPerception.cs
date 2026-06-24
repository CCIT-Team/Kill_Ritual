using UnityEngine;

public class KREnemyPerception : MonoBehaviour
{
    [Header("Forward Sight")]
    [SerializeField] private Transform eyePoint;
    // 시야 판정 시작점. 비워두면 적 transform을 사용한다.

    [SerializeField] private float detectDistance = 18f;
    // 최초 감지 거리.

    [SerializeField, Range(1f, 180f)] private float detectAngle = 70f;
    // 정면 감지 각도. 70이면 정면 기준 좌우 35도다.

    [SerializeField] private bool requireLineOfSight = true;
    // 벽/지형 뒤의 타겟을 감지하지 않을지 여부.

    [SerializeField] private LayerMask sightObstacleMask;
    // 시야를 막는 레이어. Terrain, Wall, Obstacle 등을 넣는다. Player 레이어는 넣지 않는 편이 안전하다.

    [Header("Optimization")]
    [SerializeField] private float checkInterval = 0.15f;
    // 매 프레임 Raycast를 쏘지 않기 위한 감지 체크 주기.

    private KREnemyRoot root;
    private float checkTimer;
    private bool lastCheckResult;

    public void Initialize(KREnemyRoot owner)
    {
        root = owner;

        if (eyePoint == null)
            eyePoint = transform;

        checkTimer = Random.Range(0f, Mathf.Max(0.01f, checkInterval));
    }

    public bool TickCanDetect(float deltaTime)
    {
        if (root == null || root.Target == null || !root.Target.HasValidTarget)
            return false;

        checkTimer -= deltaTime;

        if (checkTimer > 0f)
            return lastCheckResult;

        checkTimer = Mathf.Max(0.01f, checkInterval);
        lastCheckResult = CanSeeTargetNow();
        return lastCheckResult;
    }

    public bool CanSeeTargetNow()
    {
        if (root == null || root.Target == null || !root.Target.HasValidTarget)
            return false;

        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;
        Vector3 targetPosition = root.Target.AimPosition;
        Vector3 toTarget = targetPosition - origin;

        if (toTarget.sqrMagnitude > detectDistance * detectDistance)
            return false;

        Vector3 flatForward = Flatten(transform.forward);
        Vector3 flatToTarget = Flatten(toTarget);

        if (flatForward == Vector3.zero || flatToTarget == Vector3.zero)
            return false;

        float dot = Vector3.Dot(flatForward, flatToTarget);
        float minDot = Mathf.Cos((detectAngle * 0.5f) * Mathf.Deg2Rad);

        if (dot < minDot)
            return false;

        if (requireLineOfSight && sightObstacleMask.value != 0)
        {
            bool blocked = Physics.Raycast(
                origin,
                toTarget.normalized,
                toTarget.magnitude,
                sightObstacleMask,
                QueryTriggerInteraction.Ignore
            );

            if (blocked)
                return false;
        }

        return true;
    }

    private Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;

        if (vector.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return vector.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, detectDistance);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origin, origin + transform.forward * detectDistance);
    }
}
