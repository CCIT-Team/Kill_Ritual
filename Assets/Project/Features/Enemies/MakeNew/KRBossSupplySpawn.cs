// Assets/Project/Features/Enemies/KRBossSupplySpawner.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Player;
using KillRitual.Player.Combat;

namespace KillRitual.Enemies
{
    public class KRBossSupplySpawner : MonoBehaviour
    {
        [Header("플레이어 참조 (비워두면 자동 탐색)")]
        [SerializeField] private KRPlayerDamageFeedback _playerHealth;
        [SerializeField] private KRCombatSystem _playerCombat;

        [Header("발동 조건")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHealthRatioThreshold = 0.3f;

        [Range(0f, 1f)]
        [SerializeField] private float _lowAmmoRatioThreshold = 0.2f;

        [Min(0.1f)]
        [SerializeField] private float _checkInterval = 1f;

        [Min(0f)]
        [SerializeField] private float _cooldown = 8f;

        [Header("소환 설정")]
        [SerializeField] private GameObject _supplyEnemyPrefab;
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

        [Min(1)]
        [SerializeField] private int _enemiesPerSpawn = 2;

        [Tooltip("보스 전투 한 번 동안 허용할 최대 소환 횟수. -1이면 무제한.")]
        [SerializeField] private int _maxSpawns = 3;

        private Coroutine _monitorRoutine;
        private float _nextAllowedTime;
        private int _spawnsUsed;
        private int _activeSupplyEnemyCount;
        private bool _bossDefeated;

        public void NotifyBossEngaged()
        {
            if (_playerHealth == null)
                _playerHealth = FindObjectOfType<KRPlayerDamageFeedback>();

            if (_playerCombat == null)
                _playerCombat = FindObjectOfType<KRCombatSystem>();

            if (_monitorRoutine == null && !_bossDefeated)
                _monitorRoutine = StartCoroutine(MonitorRoutine());
        }

        public void NotifyBossDefeated()
        {
            _bossDefeated = true;

            if (_monitorRoutine != null)
            {
                StopCoroutine(_monitorRoutine);
                _monitorRoutine = null;
            }
        }

        private IEnumerator MonitorRoutine()
        {
            while (!_bossDefeated)
            {
                yield return new WaitForSeconds(_checkInterval);

                if (_bossDefeated) yield break;

                bool capReached = _maxSpawns >= 0 && _spawnsUsed >= _maxSpawns;
                if (capReached) continue;

                if (Time.time < _nextAllowedTime) continue;
                if (_activeSupplyEnemyCount > 0) continue;

                if (IsHealthLow() || IsAmmoLow())
                {
                    SpawnSupplyEnemies();
                    _nextAllowedTime = Time.time + _cooldown;
                    _spawnsUsed++;
                }
            }

            _monitorRoutine = null;
        }

        private bool IsHealthLow()
        {
            if (_playerHealth == null) return false;
            if (_playerHealth.MaxHealth <= 0f) return false;

            return (_playerHealth.CurrentHealth / _playerHealth.MaxHealth) <= _lowHealthRatioThreshold;
        }

        private bool IsAmmoLow()
        {
            if (_playerCombat == null) return false;

            float ratio = _playerCombat.GetResourceRatioBySlot(_playerCombat.CurrentSlotIndex);
            return ratio <= _lowAmmoRatioThreshold;
        }

        private void SpawnSupplyEnemies()
        {
            if (_supplyEnemyPrefab == null)
            {
                Debug.LogWarning($"[KRBossSupplySpawner] {name}: Supply Enemy Prefab이 비어있습니다.");
                return;
            }

            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                Debug.LogWarning($"[KRBossSupplySpawner] {name}: Spawn Points가 비어있습니다.");
                return;
            }

            int spawnedCount = 0;

            for (int i = 0; i < _enemiesPerSpawn; i++)
            {
                Transform sp = _spawnPoints[Random.Range(0, _spawnPoints.Count)];
                if (sp == null) continue;

                GameObject newEnemy = Instantiate(_supplyEnemyPrefab, sp.position, sp.rotation);
                if (newEnemy == null) continue;

                KREnemyBase enemyBase = newEnemy.GetComponent<KREnemyBase>()
                    ?? newEnemy.GetComponentInChildren<KREnemyBase>(true);

                if (enemyBase == null)
                {
                    Debug.LogWarning($"[KRBossSupplySpawner] {name}: '{newEnemy.name}'에서 KREnemyBase를 찾지 못했습니다.");
                    continue;
                }

                enemyBase.gameObject.SetActive(true);

                BossSupplyEnemyLink link = enemyBase.GetComponent<BossSupplyEnemyLink>();
                if (link == null)
                    link = enemyBase.gameObject.AddComponent<BossSupplyEnemyLink>();

                link.Init(this);

                _activeSupplyEnemyCount++;
                spawnedCount++;
            }

            Debug.Log($"[KRBossSupplySpawner] {name}: 자원 부족 감지 → 보급 몬스터 {spawnedCount}마리 소환 " +
                      $"({_spawnsUsed + 1}/{(_maxSpawns < 0 ? "무제한" : _maxSpawns.ToString())}).");
        }

        public void NotifySupplyEnemyDied()
        {
            _activeSupplyEnemyCount = Mathf.Max(0, _activeSupplyEnemyCount - 1);
        }
    }
}