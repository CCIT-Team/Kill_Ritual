// Assets/Project/Features/CombatZones/WaveCombatZone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Enemies;
using KillRitual.Player;          // KRPlayerDamageFeedback
using KillRitual.Player.Combat;   // KRCombatSystem

namespace KillRitual.CombatZones
{
    /// <summary>
    /// 전투 구역 관리 스크립트.
    ///
    /// 핵심 구조:
    /// - _monsterParent 하위의 KREnemyBase를 이 구역 몬스터로 등록
    /// - KRSkippableEnemyTag가 붙은 적은 잡몹
    /// - KRSkippableEnemyTag가 없는 적은 핵심 몬스터
    /// - 핵심 몬스터가 살아 있는데 잡몹이 0마리면 잡몹 보충 소환
    /// - 소환된 잡몹도 _monsterParent 하위로 생성
    /// - 구역 클리어 시 _monsterParent 하위 몬스터를 정리
    ///
    /// 포탈 파티클:
    /// - 닫힘 / 차단 상태: 재생
    /// - 열림 / 통과 가능 상태: 정지
    /// </summary>
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

        [Header("포탈 연기 파티클 (닫혀 있을 때 재생, 열리면 정지)")]
        [SerializeField] private ParticleSystem _portalParticle;

        [Header("이 구역 몬스터 루트")]
        [Tooltip("이 구역에 속한 몬스터들의 부모입니다. 초기 배치 몬스터와 소환 몬스터가 모두 이 하위에 들어갑니다.")]
        [SerializeField] private Transform _monsterParent;

        [Header("잡몹 보충 소환 설정")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

        [Tooltip("보충 소환할 잡몹 프리팹입니다. KRSkippableEnemyTag가 붙어 있어야 합니다. 없으면 런타임에 자동 추가합니다.")]
        [SerializeField] private GameObject _enemyPrefab;

        [Tooltip("한 번 보충 소환할 때 생성할 잡몹 수입니다.")]
        [Min(1)]
        [SerializeField] private int _enemiesPerWave = 2;

        [Tooltip("보충 소환 조건이 충족된 뒤 실제 생성되기까지의 지연 시간입니다.")]
        [Min(0f)]
        [SerializeField] private float _spawnInterval = 0.5f;

        [Tooltip("보충 소환 가능한 최대 횟수입니다. 기존 웨이브 수가 아니라 보충 소환 횟수입니다.")]
        [Min(0)]
        [SerializeField] private int _maxWaveSpawns = 3;

        [Tooltip("구역 시작 직후 잡몹이 0마리이고 핵심 몬스터만 있으면 바로 보충 소환을 검사합니다.")]
        [SerializeField] private bool _checkSummonOnZoneStart = true;

        [Header("소수 잡몹 스킵 설정")]
        [Tooltip("체크하면, 핵심 몬스터가 모두 죽은 뒤 잡몹이 일정 마리 이하로 남았을 때 구역을 클리어 처리합니다.")]
        [SerializeField] private bool _allowSkipRemainingTrash = false;

        [Tooltip("스킵을 허용할 잔여 잡몹 수. 이 값 이하로 남으면 클리어 처리됩니다.")]
        [Min(1)]
        [SerializeField] private int _skipThreshold = 2;

        [Header("클리어 시 몬스터 루트 정리")]
        [Tooltip("체크하면 구역 클리어 시 Monster Parent 하위의 모든 KREnemyBase를 정리합니다.")]
        [SerializeField] private bool _cleanupMonsterParentOnClear = true;

        [Tooltip("체크하면 Destroy 대신 SetActive(false)를 사용합니다. 풀링/안전성 기준으로 true 권장.")]
        [SerializeField] private bool _deactivateMonstersInsteadOfDestroy = true;

        [Header("자원 부족 시 긴급 소환 (아레나 단위, 포탈 열리면 자동 중단)")]
        [Tooltip("체크하면 이 아레나 전투 중에는 체력/탄약이 부족해질 때 보급용 몬스터를 소환합니다.")]
        [SerializeField] private bool _enableSupplySpawn = false;

        [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private KRPlayerDamageFeedback _playerHealth;

        [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private KRCombatSystem _playerCombat;

        [Range(0f, 1f)]
        [Tooltip("체력 비율이 이 값 이하로 떨어지면 소환 조건 충족.")]
        [SerializeField] private float _lowHealthRatioThreshold = 0.3f;

        [Range(0f, 1f)]
        [Tooltip("현재 장착 무기의 탄약 비율이 이 값 이하로 떨어지면 소환 조건 충족.")]
        [SerializeField] private float _lowAmmoRatioThreshold = 0.2f;

        [Min(0.1f)]
        [Tooltip("몇 초마다 체력/탄약 상태를 검사할지.")]
        [SerializeField] private float _supplyCheckInterval = 1f;

        [Min(0f)]
        [Tooltip("한 번 소환한 뒤 다시 소환 가능해지기까지의 최소 대기 시간.")]
        [SerializeField] private float _supplyCooldown = 8f;

        [Tooltip("보급용으로 소환할 몬스터 프리팹. KRSkippableEnemyTag 여부는 상관없습니다(클리어 판정과 무관).")]
        [SerializeField] private GameObject _supplyEnemyPrefab;

        [Min(1)]
        [SerializeField] private int _supplyEnemiesPerSpawn = 2;

        [Tooltip("이 아레나에서 보급 소환을 허용할 최대 횟수. -1이면 무제한.")]
        [SerializeField] private int _maxSupplySpawns = 2;

        private Coroutine _supplyMonitorRoutine;
        private float _nextSupplyAllowedTime;
        private int _supplySpawnsUsed;
        private int _activeSupplyEnemyCount;

        private int _importantAliveCount;
        private int _skippableAliveCount;
        private int _summonUsedCount;

        private bool _zoneActivated;
        private bool _zoneCleared;
        private bool _summonInProgress;

        private Coroutine _summonRoutine;

        private readonly HashSet<int> _registeredEnemyIds = new HashSet<int>();

        private void Start()
        {
            if (_activateOnStart)
            {
                SetGateClosed(true);
                ActivateZone();
                return;
            }

            // 일반 구역은 진입 전에는 열려 있음.
            SetGateClosed(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[WaveCombatZone] {name} OnTriggerEnter: {other.name}");
            TryActivateFromTrigger(other);
        }

        /// <summary>
        /// ZoneEntryRelay 같은 자식 콜라이더에서 호출할 수 있습니다.
        /// </summary>
        public void NotifyEntryTriggered(Collider other)
        {
            Debug.Log($"[WaveCombatZone] {name} NotifyEntryTriggered (relay): {other.name}");
            TryActivateFromTrigger(other);
        }

        private void TryActivateFromTrigger(Collider other)
        {
            if (_activateOnStart) return;
            if (_zoneActivated || _zoneCleared) return;
            if (other == null) return;
            if (!other.CompareTag("Player")) return;

            ActivateZone();
        }

        private void ActivateZone()
        {
            Debug.Log($"[WaveCombatZone] {name} ActivateZone 호출됨");
            if (_zoneActivated || _zoneCleared) return;

            _zoneActivated = true;

            _importantAliveCount = 0;
            _skippableAliveCount = 0;
            _summonUsedCount = 0;
            _summonInProgress = false;

            _registeredEnemyIds.Clear();

            SetGateClosed(true);

            RegisterInitialEnemies();

            CheckClearCondition();

            if (_checkSummonOnZoneStart)
                TryRequestTrashSummon();
            // 여기 추가
            if (_enableSupplySpawn)
                StartSupplyMonitor();
        }

        private void RegisterInitialEnemies()
        {
            if (_monsterParent == null)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: Monster Parent가 연결되어 있지 않습니다.");
                return;
            }

            if (!_monsterParent.gameObject.activeSelf)
                _monsterParent.gameObject.SetActive(true);

            KREnemyBase[] enemies = _monsterParent.GetComponentsInChildren<KREnemyBase>(true);

            foreach (KREnemyBase enemy in enemies)
            {
                if (enemy == null) continue;

                enemy.gameObject.SetActive(true);
                RegisterEnemy(enemy.gameObject);
            }
        }

        private void RegisterEnemy(GameObject enemy)
        {
            if (enemy == null) return;

            int id = enemy.GetInstanceID();
            if (_registeredEnemyIds.Contains(id))
                return;

            _registeredEnemyIds.Add(id);

            bool isSkippable = IsSkippableEnemy(enemy);

            if (isSkippable)
                _skippableAliveCount++;
            else
                _importantAliveCount++;

            ArenaEnemyLink link = enemy.GetComponent<ArenaEnemyLink>();
            if (link == null)
                link = enemy.AddComponent<ArenaEnemyLink>();

            link.Init(this, isSkippable);
        }

        private void RegisterSpawnedEnemy(GameObject spawnedRoot)
        {
            if (spawnedRoot == null) return;

            EnsureSkippableTag(spawnedRoot);

            KREnemyBase enemyBase = spawnedRoot.GetComponent<KREnemyBase>();

            if (enemyBase == null)
                enemyBase = spawnedRoot.GetComponentInChildren<KREnemyBase>(true);

            if (enemyBase == null)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: 소환된 프리팹 '{spawnedRoot.name}'에서 KREnemyBase를 찾지 못했습니다.");
                return;
            }

            enemyBase.gameObject.SetActive(true);
            RegisterEnemy(enemyBase.gameObject);
        }

        private bool IsSkippableEnemy(GameObject enemy)
        {
            if (enemy == null) return false;

            if (enemy.GetComponent<KRSkippableEnemyTag>() != null)
                return true;

            if (enemy.GetComponentInChildren<KRSkippableEnemyTag>(true) != null)
                return true;

            if (enemy.GetComponentInParent<KRSkippableEnemyTag>() != null)
                return true;

            return false;
        }

        private void EnsureSkippableTag(GameObject enemy)
        {
            if (enemy == null) return;

            if (IsSkippableEnemy(enemy))
                return;

            enemy.AddComponent<KRSkippableEnemyTag>();

            Debug.LogWarning(
                $"[WaveCombatZone] {name}: 보충 소환 프리팹 '{enemy.name}'에 KRSkippableEnemyTag가 없어 런타임에 자동 추가했습니다. " +
                "프리팹에 직접 붙이는 것을 권장합니다."
            );
        }

        private bool CanSummonTrash()
        {
            if (!_zoneActivated) return false;
            if (_zoneCleared) return false;
            if (_summonInProgress) return false;

            // 핵심 몬스터가 살아 있는데 잡몹이 하나도 없으면 보충 소환.
            if (_importantAliveCount <= 0) return false;
            if (_skippableAliveCount > 0) return false;

            if (_summonUsedCount >= _maxWaveSpawns) return false;
            if (_enemyPrefab == null) return false;
            if (_enemiesPerWave <= 0) return false;
            if (_spawnPoints == null || _spawnPoints.Count == 0) return false;

            return true;
        }

        private void TryRequestTrashSummon()
        {
            if (!CanSummonTrash())
                return;

            _summonRoutine = StartCoroutine(TrashSummonRoutine());
        }

        private IEnumerator TrashSummonRoutine()
        {
            _summonInProgress = true;

            if (_spawnInterval > 0f)
                yield return new WaitForSeconds(_spawnInterval);

            if (!CanSummonTrashAfterDelay())
            {
                _summonInProgress = false;
                _summonRoutine = null;
                yield break;
            }

            int spawnedCount = 0;

            for (int i = 0; i < _enemiesPerWave; i++)
            {
                if (_zoneCleared)
                    break;

                Transform spawnPoint = GetRandomValidSpawnPoint();
                if (spawnPoint == null)
                    continue;

                GameObject newEnemy = Instantiate(
                    _enemyPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    _monsterParent
                );

                if (newEnemy == null)
                    continue;

                RegisterSpawnedEnemy(newEnemy);
                spawnedCount++;
            }

            _summonUsedCount++;
            _summonInProgress = false;
            _summonRoutine = null;

            Debug.Log($"[WaveCombatZone] {name}: 잡몹 보충 소환 {spawnedCount}마리. 사용 횟수 {_summonUsedCount}/{_maxWaveSpawns}");

            CheckClearCondition();

            // 프리팹/스폰포인트 문제로 실제 등록된 잡몹이 없을 수 있으므로 재검사.
            TryRequestTrashSummon();
        }

        private bool CanSummonTrashAfterDelay()
        {
            if (!_zoneActivated) return false;
            if (_zoneCleared) return false;

            if (_importantAliveCount <= 0) return false;
            if (_skippableAliveCount > 0) return false;

            if (_summonUsedCount >= _maxWaveSpawns) return false;
            if (_enemyPrefab == null) return false;
            if (_enemiesPerWave <= 0) return false;
            if (_spawnPoints == null || _spawnPoints.Count == 0) return false;

            return true;
        }

        private Transform GetRandomValidSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Count == 0)
                return null;

            for (int attempt = 0; attempt < _spawnPoints.Count; attempt++)
            {
                Transform candidate = _spawnPoints[Random.Range(0, _spawnPoints.Count)];
                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// ArenaEnemyLink.Die() 또는 NotifyDead()에서 호출됩니다.
        /// </summary>
        public void NotifyEnemyDied(bool wasSkippable)
        {
            if (!_zoneActivated || _zoneCleared)
                return;

            if (wasSkippable)
                _skippableAliveCount = Mathf.Max(0, _skippableAliveCount - 1);
            else
                _importantAliveCount = Mathf.Max(0, _importantAliveCount - 1);

            CheckClearCondition();

            // 적 사망으로 잡몹이 0마리가 되었을 수 있으므로 보충 소환 조건 재검사.
            TryRequestTrashSummon();
        }

        private void CheckClearCondition()
        {
            if (_zoneCleared) return;
            if (!_zoneActivated) return;

            // 핵심 몬스터가 하나라도 남아 있으면 구역 클리어 불가.
            if (_importantAliveCount > 0)
                return;

            // 핵심 몬스터가 전부 죽었고, 잡몹도 없으면 클리어.
            if (_skippableAliveCount == 0)
            {
                OpenZone();
                return;
            }

            // 핵심 몬스터가 전부 죽었고, 잡몹만 소수 남았으면 클리어.
            // 이때 OpenZone()에서 Monster Parent 하위를 전부 정리한다.
            if (_allowSkipRemainingTrash && _skippableAliveCount <= _skipThreshold)
            {
                Debug.Log($"[WaveCombatZone] {name}: 잔여 잡몹 {_skippableAliveCount}마리, 스킵 조건 충족 → Monster Parent 정리 후 클리어");
                OpenZone();
            }
        }

        private void OpenZone()
        {
            Debug.Log($"[WaveCombatZone] {name} OpenZone 호출됨");
            if (_zoneCleared)
                return;

            // 먼저 true로 바꿔야 함.
            // 그래야 정리 중 SetActive(false) / Destroy 때문에 ArenaEnemyLink가 호출돼도 카운트가 다시 안 흔들림.
            _zoneCleared = true;

            if (_summonRoutine != null)
            {
                StopCoroutine(_summonRoutine);
                _summonRoutine = null;
            }

            _summonInProgress = false;

            // 여기 추가 — 포탈이 열리면 보급 소환도 즉시 중단
            StopSupplyMonitor();


            if (_cleanupMonsterParentOnClear)
                CleanupMonsterParent();

            SetGateClosed(false);

            Debug.Log($"[WaveCombatZone] {name}: 전투 구역 클리어. 포탈 통과 가능.");
        }

        private void CleanupMonsterParent()
        {
            if (_monsterParent == null)
                return;

            KREnemyBase[] enemies = _monsterParent.GetComponentsInChildren<KREnemyBase>(true);

            int cleanedCount = 0;

            foreach (KREnemyBase enemy in enemies)
            {
                if (enemy == null)
                    continue;

                GameObject enemyObject = enemy.gameObject;
                if (enemyObject == null)
                    continue;

                if (_deactivateMonstersInsteadOfDestroy)
                    enemyObject.SetActive(false);
                else
                    Destroy(enemyObject);

                cleanedCount++;
            }

            _importantAliveCount = 0;
            _skippableAliveCount = 0;
            _registeredEnemyIds.Clear();

            Debug.Log($"[WaveCombatZone] {name}: Monster Parent 하위 몬스터 {cleanedCount}개 정리 완료.");
        }

        // ── 자원 부족 긴급 소환 ──────────────────────────────────────────

        private void StartSupplyMonitor()
        {
            if (_playerHealth == null)
                _playerHealth = FindObjectOfType<KRPlayerDamageFeedback>();

            if (_playerCombat == null)
                _playerCombat = FindObjectOfType<KRCombatSystem>();

            if (_playerHealth == null && _playerCombat == null)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: 플레이어 체력/전투 스크립트를 찾지 못해 보급 소환을 시작하지 않습니다.");
                return;
            }

            if (_supplyMonitorRoutine == null)
                _supplyMonitorRoutine = StartCoroutine(SupplyMonitorRoutine());
        }

        private void StopSupplyMonitor()
        {
            if (_supplyMonitorRoutine != null)
            {
                StopCoroutine(_supplyMonitorRoutine);
                _supplyMonitorRoutine = null;
            }
        }

        private IEnumerator SupplyMonitorRoutine()
        {
            while (_zoneActivated && !_zoneCleared)
            {
                yield return new WaitForSeconds(_supplyCheckInterval);

                if (_zoneCleared) yield break;

                bool spawnCapReached = _maxSupplySpawns >= 0 && _supplySpawnsUsed >= _maxSupplySpawns;
                if (spawnCapReached) continue;

                if (Time.time < _nextSupplyAllowedTime) continue;
                if (_activeSupplyEnemyCount > 0) continue; // 이전 보급 몬스터가 아직 살아있으면 대기

                if (IsHealthLow() || IsAmmoLow())
                {
                    SpawnSupplyEnemies();
                    _nextSupplyAllowedTime = Time.time + _supplyCooldown;
                    _supplySpawnsUsed++;
                }
            }

            _supplyMonitorRoutine = null;
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
                Debug.LogWarning($"[WaveCombatZone] {name}: Supply Enemy Prefab이 비어있어 보급 소환을 건너뜁니다.");
                return;
            }

            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: Spawn Points가 비어있어 보급 소환을 건너뜁니다.");
                return;
            }

            int spawnedCount = 0;

            for (int i = 0; i < _supplyEnemiesPerSpawn; i++)
            {
                Transform spawnPoint = GetRandomValidSpawnPoint();
                if (spawnPoint == null) continue;

                GameObject newEnemy = Instantiate(
                    _supplyEnemyPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    _monsterParent
                );

                if (newEnemy == null) continue;

                KREnemyBase enemyBase = newEnemy.GetComponent<KREnemyBase>()
                    ?? newEnemy.GetComponentInChildren<KREnemyBase>(true);

                if (enemyBase == null)
                {
                    Debug.LogWarning($"[WaveCombatZone] {name}: 보급 프리팹 '{newEnemy.name}'에서 KREnemyBase를 찾지 못했습니다.");
                    continue;
                }

                enemyBase.gameObject.SetActive(true);

                ArenaEnemyLink link = enemyBase.GetComponent<ArenaEnemyLink>();
                if (link == null)
                    link = enemyBase.gameObject.AddComponent<ArenaEnemyLink>();

                link.InitAsSupplyEnemy(this);

                _activeSupplyEnemyCount++;
                spawnedCount++;
            }

            Debug.Log($"[WaveCombatZone] {name}: 자원 부족 감지 → 보급 몬스터 {spawnedCount}마리 소환 ({_supplySpawnsUsed + 1}/{(_maxSupplySpawns < 0 ? "무제한" : _maxSupplySpawns.ToString())}).");
        }

        /// <summary>ArenaEnemyLink.Die()에서 보급용 몬스터가 죽었을 때 호출됩니다. 클리어 판정과 무관합니다.</summary>
        public void NotifySupplyEnemyDied()
        {
            _activeSupplyEnemyCount = Mathf.Max(0, _activeSupplyEnemyCount - 1);
        }

        private void SetGateClosed(bool closed)
        {
            SetBlockers(closed);
            SetPortalSmoke(closed);
        }

        private void SetBlockers(bool blocked)
        {
            if (_portalBlockers == null)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: Portal Blockers 리스트가 null입니다.");
                return;
            }

            Debug.Log($"[WaveCombatZone] {name}: SetBlockers({blocked}) 호출, 리스트 개수={_portalBlockers.Count}");

            foreach (Collider blocker in _portalBlockers)
            {
                if (blocker == null)
                {
                    Debug.LogWarning($"[WaveCombatZone] {name}: Portal Blockers 리스트에 비어있는(None) 슬롯이 있습니다.");
                    continue;
                }

                blocker.enabled = blocked;
                Debug.Log($"[WaveCombatZone] {name}: {blocker.name}.enabled = {blocked}");
            }
        }

        private void SetPortalSmoke(bool visible)
        {
            if (_portalParticle == null)
                return;

            if (visible)
            {
                if (!_portalParticle.gameObject.activeSelf)
                    _portalParticle.gameObject.SetActive(true);

                if (!_portalParticle.isPlaying)
                    _portalParticle.Play(true);
            }
            else
            {
                _portalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_enemiesPerWave < 1)
                _enemiesPerWave = 1;

            if (_spawnInterval < 0f)
                _spawnInterval = 0f;

            if (_maxWaveSpawns < 0)
                _maxWaveSpawns = 0;

            if (_skipThreshold < 1)
                _skipThreshold = 1;

            if (_entryTrigger != null && !_entryTrigger.isTrigger)
            {
                Debug.LogWarning($"[WaveCombatZone] {name}: Entry Trigger로 등록된 Collider는 Is Trigger가 켜져 있어야 합니다.");
            }
        }
#endif
    }
}