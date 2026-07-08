using UnityEngine;

/// <summary>
/// CombatArena가 등록한 적에게 자동으로 붙는 연결 컴포넌트.
/// 적이 죽었을 때 이를 CombatArena에 알려서 생존 카운트를 갱신시킵니다.
///
/// 사용법:
/// 1) 적이 Destroy()로 사라지는 방식이라면 아무것도 안 해도 됩니다.
///    (OnDestroy에서 자동으로 사망 처리)
/// 2) 오브젝트 풀링을 쓰거나, 사망 시 Destroy 대신 SetActive(false) 등을
///    사용한다면, 기존 Health/사망 스크립트의 사망 시점에서
///    GetComponent<ArenaEnemyLink>()?.Die() 를 한 줄 호출해주세요.
/// </summary>
public class ArenaEnemyLink : MonoBehaviour
{
    private CombatArena arena;
    private bool reported = false;

    public void Bind(CombatArena owner)
    {
        arena = owner;
        reported = false;
    }

    /// <summary>
    /// 기존 사망 처리 로직에서 명시적으로 호출해주는 함수.
    /// 예: healthComponent.OnDeath += () => GetComponent<ArenaEnemyLink>().Die();
    /// </summary>
    public void Die()
    {
        ReportDeath();
    }

    private void OnDestroy()
    {
        ReportDeath();
    }

    private void ReportDeath()
    {
        if (reported) return;
        reported = true;

        if (arena != null)
        {
            arena.NotifyEnemyDeath(gameObject);
        }
    }
}