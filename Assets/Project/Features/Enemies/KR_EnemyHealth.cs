using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 적의 체력을 관리하며, 임계치 이하로 떨어지면 쿨다운을 두고 KR_EnemyAI에 경직을 알리고 0이 되면 죽는다.

[RequireComponent(typeof(KR_EnemyAI))]
public class KR_EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth; // 현재 체력(확인용)

    [Header("경직")]
    [Tooltip("현재 체력이 (최대체력 × 이 비율) 이하가 되면 경직된다. 0.5 = 절반")]
    [Range(0f, 1f)] public float staggerThreshold = 0.5f;
    [Tooltip("경직이 한 번 발동한 뒤 다시 발동 가능해질 때까지의 시간(초)")]
    public float staggerCooldown = 3f;

    // ── 내부 변수 ──
    private KR_EnemyAI ai;
    private float staggerCooldownTimer = 0f; // 다음 경직까지 남은 시간
    private bool isDead = false;

    void Awake()
    {
        ai = GetComponent<KR_EnemyAI>();
        currentHealth = maxHealth; // 시작 시 체력 가득
    }

    void Update()
    {
        // 경직 쿨다운만 시간에 따라 줄여준다.
        if (staggerCooldownTimer > 0f)
            staggerCooldownTimer -= Time.deltaTime;
    }

    // 플레이어의 총알/무기가 적을 맞혔을 때 이 함수를 호출한다.
    public void TakeDamage(float amount)
    {
        if (isDead) return; // 이미 죽었으면 무시

        currentHealth -= amount;

        // 체력이 0 이하 → 사망 처리
        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // 체력이 임계치 이하로 내려갔고, 경직 쿨다운이 끝났으면 경직 발동
        float threshold = maxHealth * staggerThreshold;
        if (currentHealth <= threshold && staggerCooldownTimer <= 0f)
        {
            ai.EnterStagger();                 // AI에게 경직 명령
            staggerCooldownTimer = staggerCooldown; // 쿨다운 시작
        }
    }

    void Die()
    {
        isDead = true;

        // 매니저에 죽음을 알려 목록에서 빼게 한다(있을 때만).
        if (KR_EnemyManager.Instance != null)
            KR_EnemyManager.Instance.Unregister(ai);

        // 가장 단순한 처리로 오브젝트를 바로 제거한다.
        Destroy(gameObject);
    }

    // 외부에서 현재 체력을 읽고 싶을 때 쓰는 통로(읽기 전용).
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
}