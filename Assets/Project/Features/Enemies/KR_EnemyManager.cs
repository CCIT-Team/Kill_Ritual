using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 맵의 모든 적을 한곳에서 관리하며, 플레이어를 한 번만 찾아 모든 적에게 나눠주는 총괄 매니저다.

public class KR_EnemyManager : MonoBehaviour
{
    // 어디서든 KR_EnemyManager.Instance 로 접근할 수 있게 하는 통로(싱글턴).
    public static KR_EnemyManager Instance { get; private set; }

    [Header("플레이어 지정")]
    [Tooltip("쫓아갈 플레이어. 비워두면 Player 태그로 자동으로 찾는다.")]
    public Transform player;

    // 현재 살아있는 모든 적의 목록.
    private readonly List<KR_EnemyAI> enemies = new List<KR_EnemyAI>();

    void Awake()
    {
        // 싱글턴 셋업: 매니저가 중복으로 생기지 않게 한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 플레이어를 인스펙터에서 안 넣었으면 Player 태그로 한 번만 찾는다.
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Start()
    {
        // 게임 시작 시 이미 씬에 놓여 있는 적들을 전부 등록한다.
        KR_EnemyAI[] found = FindObjectsOfType<KR_EnemyAI>();
        foreach (var e in found)
            Register(e);
    }

    // 적을 목록에 추가하고, 쫓아갈 플레이어를 알려준다.
    public void Register(KR_EnemyAI enemy)
    {
        if (enemy == null || enemies.Contains(enemy)) return;
        enemies.Add(enemy);
        enemy.SetTarget(player); // 플레이어를 한 번만 전달
    }

    // 적이 죽으면 목록에서 빼준다(KR_EnemyHealth.Die에서 호출).
    public void Unregister(KR_EnemyAI enemy)
    {
        enemies.Remove(enemy);
    }

    // 현재 적 수를 외부에서 확인하고 싶을 때(예: UI에 "남은 적 N마리").
    public int EnemyCount => enemies.Count;
}