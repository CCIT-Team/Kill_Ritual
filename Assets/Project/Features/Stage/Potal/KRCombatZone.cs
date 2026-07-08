// Assets/Project/Features/CombatZones/WaveCombatZone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Enemies;

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
            TryActivateFromTrigger(other);
        }

        /// <summary>
        /// ZoneEntryRelay 같은 자식 콜라이더에서 호출할 수 있습니다.
        /// </summary>
        public void NotifyEntryTriggered(Collider other)
        {
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

        private void SetGateClosed(bool closed)
        {
            SetBlockers(closed);
            SetPortalSmoke(closed);
        }

        private void SetBlockers(bool blocked)
        {
            if (_portalBlockers == null)
                return;

            foreach (Collider blocker in _portalBlockers)
            {
                if (blocker == null) continue;
                blocker.enabled = blocked;
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