using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Player;

namespace KillRitual.Items
{
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

        [Tooltip("드롭이 결정되면 오브 하나 대신 이 개수만큼 만들어 사방으로 흩뿌립니다.")]
        [Min(1)] [SerializeField] private int _ammoOrbCount = 4;

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

        // SpawnDrops()가 처형마다 FindGameObjectWithTag를 반복하지 않도록 Awake에서 한 번만 찾아 캐싱합니다.
        private KRPlayerDamageFeedback _playerFeedback;

        private void Awake()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerFeedback = player.GetComponentInParent<KRPlayerDamageFeedback>();
            }

            if (_playerFeedback == null)
            {
                Debug.LogWarning("[KRDropSpawner] KRPlayerDamageFeedback을 찾지 못했습니다. " +
                                 "Player 태그와 KRPlayerDamageFeedback 컴포넌트를 확인하세요.");
            }
        }

        public void SpawnDrops(Vector3 position, KRDamageType currentElement)
        {
            // 체력은 오브 없이 KRPlayerDamageFeedback.Heal()로 즉시 직접 회복하며 HP바도 함께 갱신됩니다.
            if (_healthRestoreOnExecute > 0f)
            {
                _playerFeedback?.Heal(_healthRestoreOnExecute);
            }

            // 탄약 오브는 currentElement 하나만 드롭합니다.
            Vector3 spawnBase = GetSpawnPosition();

            int idx = (int)currentElement;
            if (_ammoOrbPrefabs != null && idx >= 0 && idx < _ammoOrbPrefabs.Length
                && _ammoOrbPrefabs[idx] != null && Random.value <= _ammoOrbChance)
            {
                for (int i = 0; i < _ammoOrbCount; i++)
                    SpawnOrb(_ammoOrbPrefabs[idx], spawnBase);
            }
        }

        private Vector3 GetSpawnPosition()
        {
            // 적에 붙은 Collider들 중 가장 높은 최상단(bounds.max.y)을 머리 위 기준으로 사용합니다.
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
                float forceVariance = Random.Range(0.7f, 1.3f);
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                Vector3 force = (new Vector3(randomDir.x, 0f, randomDir.y) * _bounceOutwardForce
                              + Vector3.up * _bounceUpForce) * forceVariance;
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}