using UnityEngine;

public static class KRAttackHitUtility
{
    private const int MaxOverlapCount = 32;
    private static readonly Collider[] OverlapBuffer = new Collider[MaxOverlapCount];
    // GC를 줄이기 위해 OverlapSphereNonAlloc용 버퍼를 재사용한다.

    public static bool IsPointInCone(Transform origin, Vector3 point, float radius, float angle)
    {
        if (origin == null)
            return false;

        Vector3 toPoint = point - origin.position;
        toPoint.y = 0f;

        if (toPoint.sqrMagnitude > radius * radius)
            return false;

        Vector3 forward = origin.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f || toPoint.sqrMagnitude <= 0.0001f)
            return false;

        forward.Normalize();
        Vector3 direction = toPoint.normalized;

        float dot = Vector3.Dot(forward, direction);
        float minDot = Mathf.Cos((angle * 0.5f) * Mathf.Deg2Rad);

        return dot >= minDot;
    }

    public static int DamageOverlapSphere(Vector3 center, float radius, LayerMask targetMask, float damage, Transform attacker)
    {
        int count = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            OverlapBuffer,
            targetMask,
            QueryTriggerInteraction.Ignore
        );

        int damagedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = OverlapBuffer[i];

            if (hitCollider == null)
                continue;

            KRIDamageable damageable = hitCollider.GetComponentInParent<KRIDamageable>();

            if (damageable == null || damageable.IsDead)
                continue;

            Vector3 closestPoint = hitCollider.ClosestPoint(center);
            Vector3 hitDirection = closestPoint - center;

            if (KRDamageUtility.ApplyDamage(damageable, damage, closestPoint, hitDirection, attacker))
                damagedCount++;
        }

        return damagedCount;
    }

    public static bool TryRayDamage(Vector3 origin, Vector3 direction, float distance, LayerMask targetMask, float damage, Transform attacker)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, targetMask, QueryTriggerInteraction.Ignore))
            return false;

        KRIDamageable damageable = hit.collider.GetComponentInParent<KRIDamageable>();

        if (damageable == null || damageable.IsDead)
            return false;

        return KRDamageUtility.ApplyDamage(damageable, damage, hit.point, direction, attacker);
    }
}
