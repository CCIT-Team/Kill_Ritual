using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;

namespace KillRitual.Items
{
    /// <summary>
    /// 처형 성공 시 회복 오브를 바닥에 드롭하는 컴포넌트입니다.
    /// KREnemyBase(또는 적 오브젝트)에 붙입니다.
    ///
    /// [동작 방식]
    /// 처형 이벤트(KRExecutionSuccessEvent) 대신, KREnemyBase.Execute()가 호출될 때
    /// 직접 이 컴포넌트의 SpawnDrops()를 호출하는 방식을 씁니다.
    /// (이벤트 방식은 어느 적이 처형됐는지 위치를 알 수 없어서, 직접 호출이 더 적합합니다.)
    ///
    /// [KREnemyBase 연동 방법]
    /// KREnemyBase.Execute() 안에 아래 한 줄을 추가하세요:
    ///   GetComponent&lt;KRDropSpawner&gt;()?.SpawnDrops(transform.position, currentElement);
    ///
    /// [프리팹 설정]
    /// _ammoOrbPrefabs[0~4]에 화수목토금 순서로 탄약 오브 프리팹을 연결하세요.
    /// 체력은 오브 없이 _healthRestoreOnExecute 값만큼 즉시 직접 회복됩니다.
    /// </summary>
    public sealed class KRDropSpawner : MonoBehaviour
    {
        [Header("처형 체력 회복 (직접 데이터)")]
        [Tooltip("처형 성공 시 즉시 회복되는 체력량(절대값). 오브 없이 바로 적용됩니다.")]
        [Min(0f)]
        [SerializeField] private float _healthRestoreOnExecute = 25f;

        [Tooltip("탄약 오브 프리팹 5개. [0]=화 [1]=수 [2]=목 [3]=토 [4]=금 순서로 넣으세요.")]
        [SerializeField] private GameObject[] _ammoOrbPrefabs = new GameObject[5];

        [Header("드롭 설정")]
        [Tooltip("탄약 오브를 드롭할 확률 (0~1). 1이면 항상 드롭.")]
        [Range(0f, 1f)]
        [SerializeField] private float _ammoOrbChance = 1f;

        [Tooltip("드롭된 오브가 퍼지는 반경. 여러 오브가 한곳에 겹치지 않도록 랜덤하게 흩뿌립니다.")]
        [Min(0f)]
        [SerializeField] private float _spreadRadius = 0.5f;

        [Tooltip("드롭 위치의 높이 보정. 바닥이 아닌 살짝 위에서 생성되어 자연스럽게 떨어집니다.")]
        [Min(0f)]
        [SerializeField] private float _spawnHeightOffset = 0.8f;

        [Header("드롭 물리")]
        [Tooltip("드롭 시 오브가 위로 튀어오르는 힘의 크기.")]
        [Min(0f)]
        [SerializeField] private float _bounceUpForce = 4f;

        [Tooltip("드롭 시 오브가 옆으로 퍼지는 힘의 크기.")]
        [Min(0f)]
        [SerializeField] private float _bounceOutwardForce = 3f;

        /// <summary>
        /// 처형 성공 시 호출합니다. KREnemyBase.Execute()에서 직접 호출하세요.
        /// </summary>
        /// <param name="position">드롭 위치 (적 오브젝트의 위치)</param>
        /// <param name="currentElement">현재 플레이어가 장착한 속성 (탄약 오브 종류 결정)</param>
        public void SpawnDrops(Vector3 position, KillRitual.Core.Damage.KRDamageType currentElement)
        {
            // 체력은 오브 없이 즉시 직접 회복합니다.
            if (_healthRestoreOnExecute > 0f)
            {
                var playerStats = GameObject.FindGameObjectWithTag("Player")
                    ?.GetComponentInParent<KillRitual.Player.Combat.KRPlayerStats>();
                playerStats?.Heal(_healthRestoreOnExecute);
            }

            // 탄약 오브 — 5속성 전부 드롭합니다.
            Vector3 spawnBase = GetSpawnPosition();

            if (_ammoOrbPrefabs != null)
            {
                for (int i = 0; i < _ammoOrbPrefabs.Length; i++)
                {
                    if (_ammoOrbPrefabs[i] != null && Random.value <= _ammoOrbChance)
                    {
                        SpawnOrb(_ammoOrbPrefabs[i], spawnBase);
                    }
                }
            }
        }

        private Vector3 GetSpawnPosition()
        {
            // 적에 붙은 Collider의 최상단(bounds.max.y)을 머리 위 기준으로 사용합니다.
            // 여러 Collider가 있을 경우 가장 높은 지점을 찾습니다.
            Collider[] colliders = GetComponentsInChildren<Collider>();
            float highestY = transform.position.y;

            foreach (Collider col in colliders)
            {
                if (!col.isTrigger && col.bounds.max.y > highestY)
                {
                    highestY = col.bounds.max.y;
                }
            }

            return new Vector3(transform.position.x, highestY + _spawnHeightOffset, transform.position.z);
        }

        private void SpawnOrb(GameObject prefab, Vector3 basePosition)
        {
            // 오브마다 약간씩 다른 위치에 생성해 겹치지 않게 합니다.
            Vector2 randomCircle = Random.insideUnitCircle * _spreadRadius;
            Vector3 spawnPosition = basePosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);

            // 생성 직후 랜덤한 방향으로 힘을 가해 물리적으로 퍼지게 합니다.
            if (instance.TryGetComponent(out Rigidbody rb))
            {
                // 수평으로 랜덤한 방향, 위쪽으로 고정된 힘을 동시에 가합니다.
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                Vector3 force = new Vector3(randomDir.x, 0f, randomDir.y) * _bounceOutwardForce
                              + Vector3.up * _bounceUpForce;
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}