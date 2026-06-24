using System;
using UnityEngine;

public class KREnemyHealth : MonoBehaviour, KRIDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    // 최대 체력. 적 프리팹마다 인스펙터에서 조절한다.

    [SerializeField] private bool destroyOnDeath = false;
    // 초반 디버깅 단계에서는 false 추천. 죽은 캡슐이 남아야 상태 확인이 쉽다.

    [SerializeField] private float destroyDelay = 3f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<KRDamageInfo, float, float> Damaged;
    // 피해 정보, 현재 체력, 최대 체력을 전달한다. Visual, Sound, UI가 구독할 수 있다.

    public event Action<KRDamageInfo> Died;
    // 사망 이벤트. 스포너, 보상, 문 열림 조건 등이 구독할 수 있다.

    private void Awake()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
    }

    public void SetMaxHealth(float newMaxHealth, bool refill = true)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);

        if (refill)
        {
            CurrentHealth = maxHealth;
            IsDead = false;
        }
    }

    public void ReceiveDamage(KRDamageInfo damageInfo)
    {
        if (IsDead)
            return;

        float finalDamage = Mathf.Max(0f, damageInfo.Amount);

        if (finalDamage <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
        Damaged?.Invoke(damageInfo, CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f)
            Die(damageInfo);
    }

    private void Die(KRDamageInfo damageInfo)
    {
        if (IsDead)
            return;

        IsDead = true;
        Died?.Invoke(damageInfo);

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}
