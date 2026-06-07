using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [Header("=== 스폰 설정 ===")]

    [SerializeField] 
    private Transform playerTransform;

    [SerializeField] 
    private List<EnemyBase> enemyPrefabs = new List<EnemyBase>();

    [SerializeField] 
    private Transform[] spawnPoints;

    [SerializeField] 
    private int maxEnemyCount = 20;

    [Header("=== 현재 상태 (읽기 전용) ===")]

    [SerializeField] 
    private List<EnemyBase> activeEnemies = new List<EnemyBase>();

    public Transform PlayerTransform => playerTransform;

    public int ActiveEnemyCount => activeEnemies.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // gameObject = 이 스크립트가 붙어있는 오브젝트
            return;
        }
        Instance = this; // 처음 생성될 때만 Instance에 등록
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("[EnemyManager] 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다!");
            }
        }
    }

    public void RegisterEnemy(EnemyBase enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy); // 목록에 추가
            Debug.Log($"[EnemyManager] 몬스터 등록: {enemy.name} / 총 {activeEnemies.Count}마리");
        }
    }

    public void UnregisterEnemy(EnemyBase enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy); // 목록에서 제거
            Debug.Log($"[EnemyManager] 몬스터 제거: {enemy.name} / 남은 {activeEnemies.Count}마리");
        }
    }

    public EnemyBase SpawnEnemy(EnemyBase prefab, Vector3 position)
    {
        if (activeEnemies.Count >= maxEnemyCount)
        {
            Debug.LogWarning("[EnemyManager] 최대 몬스터 수에 도달했습니다.");
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError("[EnemyManager] 스폰할 프리팹이 null입니다!");
            return null;
        }

        EnemyBase newEnemy = Instantiate(prefab, position, Quaternion.identity);

        return newEnemy;
    }

    public EnemyBase SpawnEnemyAtRandomPoint(EnemyBase prefab)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[EnemyManager] 스폰 포인트가 없습니다. 원점에 스폰합니다.");
            return SpawnEnemy(prefab, Vector3.zero);
            // Vector3.zero = (0, 0, 0) 원점
        }

        int randomIndex = Random.Range(0, spawnPoints.Length);
        return SpawnEnemy(prefab, spawnPoints[randomIndex].position);
    }

    public EnemyBase GetNearestEnemy(Vector3 from)
    {
        EnemyBase nearest = null;
        float minDistance = float.MaxValue; // 가능한 가장 큰 float 값으로 초기화

        foreach (EnemyBase enemy in activeEnemies)
        {
            if (enemy == null) continue; 
            float dist = Vector3.Distance(from, enemy.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    public void KillAllEnemys()
    {
        List<EnemyBase> copy = new List<EnemyBase>(activeEnemies);
        foreach (EnemyBase enemy in copy)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
        activeEnemies.Clear();
    }
}