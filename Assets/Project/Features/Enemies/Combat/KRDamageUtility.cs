using UnityEngine;

public static class KRDamageUtility
{
    public static bool ApplyDamage(KRIDamageable damageable, float amount, Vector3 hitPoint, Vector3 hitDirection, Transform attacker)
    {
        if (damageable == null || damageable.IsDead)
            return false;

        KRDamageInfo damageInfo = new KRDamageInfo(
            Mathf.Max(0f, amount),
            hitPoint,
            hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector3.zero,
            attacker
        );

        damageable.ReceiveDamage(damageInfo);
        return true;
    }
}
