using UnityEngine;

public class KRAITarget : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform aimPoint;
    // 적이 바라보거나 공격 판정을 계산할 기준점이다. 비워두면 transform을 사용한다.

    [SerializeField] private int priority = 0;
    // 추후 미끼 부적, 소환물, 동료 NPC를 만들 때 타겟 우선순위로 사용할 수 있다.

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public int Priority => priority;

    public KRIDamageable Damageable { get; private set; }
    // 공격할 때마다 GetComponent를 호출하지 않기 위해 Awake에서 캐싱한다.

    private void Awake()
    {
        Damageable = GetComponentInParent<KRIDamageable>();
    }

    public bool IsValidTarget()
    {
        if (Damageable == null)
            return true;

        return !Damageable.IsDead;
    }
}
