using UnityEngine;

public class KRDebugPlayerHealth : MonoBehaviour, KRIDamageable
{
    [Header("Debug Health")]
    [SerializeField] private float maxHealth = 100f;

    public bool IsDead { get; private set; }

    private float currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        IsDead = false;
    }

    public void ReceiveDamage(KRDamageInfo damageInfo)
    {
        if (IsDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damageInfo.Amount);
        Debug.Log($"[Player] Damaged: {damageInfo.Amount} / HP: {currentHealth}", this);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            Debug.Log("[Player] Dead", this);
        }
    }
}
