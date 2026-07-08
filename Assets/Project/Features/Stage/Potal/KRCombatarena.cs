using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 둠 이터널 스타일의 전투 구역(Arena) 시스템.
/// 맵에 배치된 포탈(ArenaPortal)을 플레이어가 통과하면 구역을 잠그고,
/// 초기 배치된 적 + 시간차로 스폰되는 웨이브를 모두 처치해야
/// 구역이 열립니다. 한 번 클리어된 구역은 다시 발동되지 않습니다.
/// </summary>
public class CombatArena : MonoBehaviour
{
    [Header("포탈 (구역 입구/출구)")]
    [Tooltip("이 구역을 잠글 때 함께 닫히는 포탈들. 플레이어가 이 중 하나를 통과하면 구역이 발동됩니다. (여러 입구가 있다면 전부 등록)")]
    [SerializeField] private List<ArenaPortal> portals = new List<ArenaPortal>();

    [Header("초기 배치된 적 (이미 씬에 존재)")]
    [Tooltip("맵에 미리 배치되어 있는 적 오브젝트들을 여기에 드래그")]
    [SerializeField] private List<GameObject> preplacedEnemies = new List<GameObject>();

    [Header("웨이브 스폰 설정")]
    [Tooltip("적을 생성할 스폰 위치들 (등록된 순서대로 소모됨)")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [Tooltip("스폰될 적 프리팹 후보 목록 (랜덤 선택)")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();
    [Tooltip("한 번에 스폰되는 적 수")]
    [SerializeField] private int enemiesPerWave = 2;
    [Tooltip("웨이브 사이 대기 시간(초)")]
    [SerializeField] private float waveInterval = 5f;
    [Tooltip("구역 진입 후 첫 웨이브가 스폰되기까지 대기 시간(초)")]
    [SerializeField] private float firstWaveDelay = 3f;

    [Header("추가 배리어 (포탈 외의 단순 벽 등, 선택사항)")]
    [SerializeField] private List<GameObject> extraBarrierObjects = new List<GameObject>();

    [Header("이벤트 (사운드, UI, 카메라 연출 등과 연결)")]
    public UnityEvent onArenaLocked;   // 구역 진입 -> 잠금 시작
    public UnityEvent onWaveSpawned;   // 웨이브 스폰될 때마다
    public UnityEvent onArenaCleared;  // 모든 적 처치 완료 시점
    public UnityEvent onArenaOpened;   // 배리어가 실제로 열린 시점

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private int spawnPointCursor = 0;
    private bool isActive = false;
    private bool hasCompleted = false;
    private Coroutine spawnRoutine;

    private void Awake()
    {
        // 각 포탈에게 자기 자신을 등록해서, 포탈이 진입을 감지하면 콜백을 받도록 연결
        foreach (var portal in portals)
        {
            if (portal != null) portal.Bind(this);
        }
    }

    /// <summary>
    /// ArenaPortal이 플레이어 진입을 감지했을 때 호출하는 콜백.
    /// </summary>
    public void OnPortalEntered(ArenaPortal portal)
    {
        if (hasCompleted || isActive) return;
        StartArena();
    }

    private void StartArena()
    {
        isActive = true;
        SetLocked(true);
        onArenaLocked?.Invoke();

        // 초기 배치된 적들을 생존 목록에 등록
        aliveEnemies.Clear();
        foreach (var enemy in preplacedEnemies)
        {
            if (enemy == null || !enemy.activeInHierarchy) continue;
            RegisterEnemyInternal(enemy);
        }

        spawnPointCursor = 0;

        // 초기 적도 없고 스폰포인트도 없으면 즉시 클리어 처리 (안전장치)
        if (aliveEnemies.Count == 0 && spawnPoints.Count == 0)
        {
            CompleteArena();
            return;
        }

        if (spawnPoints.Count > 0)
        {
            spawnRoutine = StartCoroutine(SpawnWaveRoutine());
        }
    }

    private IEnumerator SpawnWaveRoutine()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        while (spawnPointCursor < spawnPoints.Count)
        {
            int spawnedThisWave = 0;
            while (spawnedThisWave < enemiesPerWave && spawnPointCursor < spawnPoints.Count)
            {
                SpawnEnemyAt(spawnPoints[spawnPointCursor]);
                spawnPointCursor++;
                spawnedThisWave++;
            }
            onWaveSpawned?.Invoke();

            if (spawnPointCursor < spawnPoints.Count)
                yield return new WaitForSeconds(waveInterval);
        }

        spawnRoutine = null;
        // 스폰이 모두 끝난 시점에 이미 전멸해 있었을 수 있으니 재확인
        CheckClearCondition();
    }

    private void SpawnEnemyAt(Transform point)
    {
        if (enemyPrefabs.Count == 0 || point == null) return;
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        GameObject enemy = Instantiate(prefab, point.position, point.rotation);
        RegisterEnemyInternal(enemy);
    }

    private void RegisterEnemyInternal(GameObject enemy)
    {
        aliveEnemies.Add(enemy);

        var link = enemy.GetComponent<ArenaEnemyLink>();
        if (link == null) link = enemy.AddComponent<ArenaEnemyLink>();
        link.Bind(this);
    }

    /// <summary>
    /// 적이 죽었을 때 ArenaEnemyLink가 호출해주는 콜백.
    /// </summary>
    public void NotifyEnemyDeath(GameObject enemy)
    {
        aliveEnemies.Remove(enemy);
        CheckClearCondition();
    }

    private void CheckClearCondition()
    {
        if (!isActive || hasCompleted) return;

        if (aliveEnemies.Count == 0 && spawnPointCursor >= spawnPoints.Count)
        {
            CompleteArena();
        }
    }

    private void CompleteArena()
    {
        hasCompleted = true;
        isActive = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        onArenaCleared?.Invoke();
        SetLocked(false);
        onArenaOpened?.Invoke();

        // 모든 포탈의 감지 트리거를 꺼서 이 구역이 다시는 발동되지 않도록 함
        foreach (var portal in portals)
        {
            if (portal != null) portal.DisableTrigger();
        }
    }

    private void SetLocked(bool locked)
    {
        foreach (var portal in portals)
        {
            if (portal != null) portal.SetLocked(locked);
        }
        foreach (var barrier in extraBarrierObjects)
        {
            if (barrier != null) barrier.SetActive(locked);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireSphere(sp.position, 0.5f);
        }

        Gizmos.color = Color.cyan;
        foreach (var portal in portals)
        {
            if (portal == null) continue;
            Gizmos.DrawWireCube(portal.transform.position, Vector3.one * 1.5f);
        }
    }
}