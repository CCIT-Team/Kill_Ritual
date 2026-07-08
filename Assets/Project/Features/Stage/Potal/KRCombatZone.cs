// Assets/Project/Features/CombatZones/WaveCombatZone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Enemies;

namespace KillRitual.CombatZones
{
    public class WaveCombatZone : MonoBehaviour
    {
        [Header("진입 트리거 (전투구역 진입 감지)")]
        [Tooltip("Activate On Start가 켜져 있으면 사용하지 않습니다. (Arena1처럼 시작부터 닫힌 구역)")]
        [SerializeField] private Collider _entryTrigger;

        [Header("시작 방식")]
        [Tooltip("체크하면 트리거 진입 없이 씬 시작과 동시에 구역이 발동합니다. (Arena1 전용)")]
        [SerializeField] private bool _activateOnStart = false;

        [Header("포탈 차단 콜라이더 (입구 + 출구, 여러 개 등록 가능)")]
        [SerializeField] private List<Collider> _portalBlockers = new List<Collider>();

        [Header("포탈 파티클 (열렸을 때만 재생)")]
        [SerializeField] private ParticleSystem _portalParticle;

        [Header("초기 배치된 적 (부모만 연결하면 자식 자동 인식)")]
        [SerializeField] private Transform _monsterParent;

        [Header("추가 스폰 설정")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private int _enemiesPerWave = 2;
        [SerializeField] private float _spawnInterval = 3f;
        [SerializeField] private int _maxWaveSpawns = 3;

        [Header("소수 잡몹 스킵 설정")]
        [Tooltip("체크하면, KRSkippableEnemyTag가 붙은 몬스터가 일정 마리 이하로 남았을 때 나머지를 무시하고 구역을 클리어 처리합니다.")]
        [SerializeField] private bool _allowSkipRemainingTrash = false;

        [Tooltip("스킵을 허용할 잔여 마리 수. 이 값 이하로 남으면 클리어 처리됩니다.")]
        [Min(1)]
        [SerializeField] private int _skipThreshold = 2;

        private int _importantAliveCount;
        private int _skippableAliveCount;

        private bool _zoneActivated;
        private bool _zoneCleared;
        private bool _allWavesSpawned;

        private void Start()
        {
            if (_portalParticle != null)
                _portalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (_activateOnStart)
            {
                SetBlockers(true);
                ActivateZone();
                return;
            }

            SetBlockers(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryActivateFromTrigger(other);
        }

        /// <summary>ZoneEntryRelay(자식 콜라이더)에서 호출됩니다.</summary>
        public void NotifyEntryTriggered(Collider other)
        {
            TryActivateFromTrigger(other);
        }

        private void TryActivateFromTrigger(Collider other)
        {
            if (_activateOnStart) return;
            if (!other.CompareTag("Player")) return;
            if (_zoneActivated || _zoneCleared) return;

            ActivateZone();
        }

        private void ActivateZone()
        {
            if (_zoneActivated) return;
            _zoneActivated = true;

            SetBlockers(true);

            if (_portalParticle != null)
                _portalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            RegisterInitialEnemies();
            StartCoroutine(SpawnWaveRoutine());
        }

        private void RegisterInitialEnemies()
        {
            if (_monsterParent == null)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: Monster Parent가 연결되어 있지 않습니다.");
                return;
            }

            KREnemyBase[] enemies = _monsterParent.GetComponentsInChildren<KREnemyBase>(true);

            foreach (KREnemyBase enemy in enemies)
            {
                if (enemy == null) continue;

                enemy.gameObject.SetActive(true);
                RegisterEnemy(enemy.gameObject);
            }
        }

        private IEnumerator SpawnWaveRoutine()
        {
            int wavesSpawned = 0;

            while (wavesSpawned < _maxWaveSpawns)
            {
                yield return new WaitForSeconds(_spawnInterval);

                for (int i = 0; i < _enemiesPerWave; i++)
                {
                    if (_spawnPoints.Count == 0 || _enemyPrefab == null) continue;

                    Transform sp = _spawnPoints[Random.Range(0, _spawnPoints.Count)];
                    GameObject newEnemy = Instantiate(_enemyPrefab, sp.position, sp.rotation);
                    RegisterEnemy(newEnemy);
                }

                wavesSpawned++;
            }

            _allWavesSpawned = true;
            CheckClearCondition();
        }

        private void RegisterEnemy(GameObject enemy)
        {
            bool isSkippable = enemy.GetComponent<KRSkippableEnemyTag>() != null;

            if (isSkippable) _skippableAliveCount++;
            else _importantAliveCount++;

            ArenaEnemyLink link = enemy.GetComponent<ArenaEnemyLink>();
            if (link == null)
                link = enemy.AddComponent<ArenaEnemyLink>();

            link.Init(this, isSkippable);
        }

        /// <summary>ArenaEnemyLink.Die()에서 호출됩니다.</summary>
        public void NotifyEnemyDied(bool wasSkippable)
        {
            if (wasSkippable)
                _skippableAliveCount = Mathf.Max(0, _skippableAliveCount - 1);
            else
                _importantAliveCount = Mathf.Max(0, _importantAliveCount - 1);

            CheckClearCondition();
        }

        private void CheckClearCondition()
        {
            if (_zoneCleared) return;
            if (!_allWavesSpawned) return;

            // 스킵 불가(중요) 몬스터가 하나라도 남아있으면 무조건 대기
            if (_importantAliveCount > 0) return;

            // 잡몹까지 전부 잡았으면 즉시 클리어
            if (_skippableAliveCount == 0)
            {
                OpenZone();
                return;
            }

            // 잡몹만 남았고, 스킵이 허용되고, 남은 수가 임계치 이하면 클리어 처리
            if (_allowSkipRemainingTrash && _skippableAliveCount <= _skipThreshold)
            {
                Debug.Log($"[WaveCombatZone] {name}: 잔여 잡몹 {_skippableAliveCount}마리, 스킵 조건 충족 → 클리어 처리");
                OpenZone();
            }
        }

        private void SetBlockers(bool blocked)
        {
            foreach (Collider blocker in _portalBlockers)
            {
                if (blocker != null)
                    blocker.enabled = blocked;
            }
        }

        private void OpenZone()
        {
            _zoneCleared = true; // 재발동 완전 차단

            SetBlockers(false);

            if (_portalParticle != null)
                _portalParticle.Play();

            Debug.Log($"[WaveCombatZone] {name}: 전투 구역 클리어. 포탈 통과 가능.");
        }
    }
}