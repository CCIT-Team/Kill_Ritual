using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// KR_EnemyManager.cs
// 역할: 맵에 있는 모든 적을 한곳에서 관리하는 "총괄 매니저".
//   최적화의 핵심이 여기 있다.
//
//   1) 플레이어를 단 한 번만 찾아서 모든 적에게 나눠준다.
//      (각 적이 매 프레임 FindObjectOfType로 플레이어를 찾으면
//       적이 많아질수록 급격히 느려지기 때문)
//
//   2) (선택) 적이 많을 때 "멀리 있는 적은 가끔만 생각하게" 만드는
//      틀을 제공한다. 지금은 각 적이 스스로 판단하지만,
//      나중에 수백 마리로 늘리면 이 매니저에서 일괄 제어하면 된다.
//
// 부착 위치: 씬에 빈 오브젝트 하나(예: "_EnemyManager")를 만들고 붙인다.
//   적 오브젝트가 아니라, 관리 전용 오브젝트에 붙이는 점에 주의.
// ─────────────────────────────────────────────────────────────

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

        // 플레이어를 인스펙터에서 안 넣었으면 "Player" 태그로 한 번만 찾는다.
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Start()
    {
        // 게임 시작 시 이미 씬에 놓여 있는 적들을 전부 등록한다.
        // (스크립트로 적을 생성하는 경우엔 생성 직후 Register를 부르면 된다)
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