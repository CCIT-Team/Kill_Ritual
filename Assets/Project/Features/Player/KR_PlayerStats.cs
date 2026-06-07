using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerStats : MonoBehaviour
{
    [Header("=== 체력 설정 ===")]

    [SerializeField] 
    private float maxHP = 100f;

    [SerializeField] 
    private float currentHP;

    [Header("=== 방어도 설정 ===")]

    [SerializeField] 
    private float maxArmor = 50f;

    [SerializeField] 
    private float currentArmor;

    [Header("=== 이벤트 (연결용) ===")]

    public UnityEvent<float, float> OnHPChanged;


    public UnityEvent<float, float> OnArmorChanged;

    public UnityEvent OnArmorBroken;

    public UnityEvent OnPlayerDied;

    public UnityEvent<float> OnDamageTaken;

    public float CurrentHP => currentHP;      // 현재 체력 읽기
    public float MaxHP => maxHP;              // 최대 체력 읽기
    public float CurrentArmor => currentArmor; // 현재 방어도 읽기
    public float MaxArmor => maxArmor;        // 최대 방어도 읽기

    public bool IsDead { get; private set; } = false;

    private void Awake()
    {
        currentHP = maxHP;
        currentArmor = maxArmor;
    }

    // ─────────────────────────────────────────
    // TakeDamage: 몬스터가 플레이어를 공격할 때 호출하는 함수
    // damage = 들어오는 피해량
    // ─────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        // 이미 사망 상태면 피해 무시
        if (IsDead) return;

        // 실제로 받은 총 피해량을 추적 (피드백용)
        float actualDamage = damage;

        // ── 방어도가 남아있을 때: 방어도를 먼저 소모 ──
        if (currentArmor > 0f)
        {
            if (damage <= currentArmor)
            {
                // 피해량이 방어도보다 적으면 → 방어도만 깎임
                currentArmor -= damage;
                damage = 0f; // 체력으로 넘어갈 피해 없음
            }
            else
            {
                // 피해량이 방어도보다 크면 → 방어도를 다 깎고 나머지를 체력으로
                damage -= currentArmor; // 방어도 초과분 계산
                currentArmor = 0f;     // 방어도 소진

                // 방어도 파괴 이벤트 발동 → PlayerFeedback에서 "방어도 파괴!" 텍스트 표시
                OnArmorBroken?.Invoke();
                // ?. = currentArmor가 null이 아닐 때만 실행 (안전한 호출)
            }

            // 방어도 변경 이벤트 발동 → UI 업데이트용
            OnArmorChanged?.Invoke(currentArmor, maxArmor);
        }

        // ── 체력 감소 처리 ──
        if (damage > 0f)
        {
            currentHP -= damage;
            currentHP = Mathf.Max(currentHP, 0f);
            // Mathf.Max: 두 값 중 큰 값 반환 → 체력이 0 미만이 되지 않게 막음

            // 체력 변경 이벤트 발동
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        // 피해를 받았다는 이벤트 발동 (실제 받은 총 피해량 전달)
        OnDamageTaken?.Invoke(actualDamage);

        // 사망 체크
        if (currentHP <= 0f)
        {
            Die();
        }
    }

    // ─────────────────────────────────────────
    // Die: 플레이어 사망 처리
    // ─────────────────────────────────────────
    private void Die()
    {
        IsDead = true;
        OnPlayerDied?.Invoke(); // 사망 이벤트 발동 → 게임오버 화면 등 연결 가능
        Debug.Log("[PlayerStats] 플레이어 사망");
        // Debug.Log: 유니티 Console 창에 메시지 출력 (게임에는 안 보임, 개발용)
    }

    // ─────────────────────────────────────────
    // 체력 회복 함수 (아이템, 힐 스킬 등에서 호출)
    // ─────────────────────────────────────────
    public void HealHP(float amount)
    {
        if (IsDead) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        // Mathf.Min: 두 값 중 작은 값 반환 → 최대 체력 초과 방지
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // ─────────────────────────────────────────
    // 방어도 회복 함수
    // ─────────────────────────────────────────
    public void HealArmor(float amount)
    {
        if (IsDead) return;
        currentArmor = Mathf.Min(currentArmor + amount, maxArmor);
        OnArmorChanged?.Invoke(currentArmor, maxArmor);
    }
}