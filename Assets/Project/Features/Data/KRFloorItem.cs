using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Player.Combat;

namespace KillRitual.Items
{
    public sealed class KRFloorItem : MonoBehaviour
    {
        [Header("오브 설정")]
        [Tooltip("이 아이템의 종류 (체력/탄약 속성)")]
        [SerializeField] private KRDropItem.DropType _dropType = KRDropItem.DropType.Health;

        [Tooltip("회복량. 체력 아이템은 체력 회복량(절대값), 탄약 아이템은 자원 회복량.")]
        [Min(0f)]
        [SerializeField] private float _restoreAmount = 25f;

        [Header("흡수 설정")]
        [Tooltip("이 거리 이하로 플레이어가 다가와야 회수(회복 적용 후 파괴/비활성화)됩니다. " +
                 "높이(Y) 차이는 무시하고 수평(XZ) 거리만 기준으로 판정합니다.")]
        [Min(0.01f)]
        [SerializeField] private float _collectRange = 0.6f;

        [Header("전투 연동 (선택)")]
        [Tooltip("켜면 KRCombatEndEvent(전투 종료) 발생 시 이 아이템도 함께 사라집니다. " +
                 "레벨에 고정 배치된 아이템은 보통 전투와 무관하므로 기본값은 꺼짐입니다.")]
        [SerializeField] private bool _despawnOnCombatEnd = false;

        [Header("회수 후 처리")]
        [Tooltip("체크하면 회수 시 오브젝트를 완전히 파괴합니다. 끄면 비활성화(SetActive(false))만 " +
                 "하므로, 필요하다면 직접 다시 활성화해 재사용할 수 있습니다.")]
        [SerializeField] private bool _destroyOnCollect = true;

        private Transform _player;
        private KRPlayerStats _playerStats;
        private KRCombatSystem _combatSystem;
        private bool _collected;

        private void Awake()
        {
            ApplyTypeColor();
        }

        private void OnEnable()
        {
            _collected = false;

            if (_despawnOnCombatEnd)
            {
                KRManagers.Event.Subscribe<KRCombatEndEvent>(OnCombatEnd);
            }
        }

        private void OnDisable()
        {
            if (_despawnOnCombatEnd)
            {
                KRManagers.Event.Unsubscribe<KRCombatEndEvent>(OnCombatEnd);
            }
        }

        private void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
                _playerStats = playerObj.GetComponentInParent<KRPlayerStats>();
                _combatSystem = playerObj.GetComponentInParent<KRCombatSystem>();
            }
        }

        private void Update()
        {
            if (_collected || _player == null) return;

            Vector3 delta = transform.position - _player.position;
            delta.y = 0f;

            if (delta.magnitude <= _collectRange)
            {
                Collect();
            }
        }

        private void OnCombatEnd(KRCombatEndEvent evt)
        {
            if (_collected) return;
            _collected = true;
            gameObject.SetActive(false);
        }

        private void Collect()
        {
            if (_collected) return;
            _collected = true;

            ApplyRestore();

            if (_destroyOnCollect)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyRestore()
        {
            switch (_dropType)
            {
                case KRDropItem.DropType.Health:
                    _playerStats?.Heal(_restoreAmount);
                    break;

                case KRDropItem.DropType.Fire:
                    _combatSystem?.RefillResource(KRDamageType.Fire, _restoreAmount);
                    break;

                case KRDropItem.DropType.Water:
                    _combatSystem?.RefillResource(KRDamageType.Water, _restoreAmount);
                    break;

                case KRDropItem.DropType.Wood:
                    _combatSystem?.RefillResource(KRDamageType.Wood, _restoreAmount);
                    break;

                case KRDropItem.DropType.Earth:
                    _combatSystem?.RefillResource(KRDamageType.Earth, _restoreAmount);
                    break;

                case KRDropItem.DropType.Metal:
                    _combatSystem?.RefillResource(KRDamageType.Metal, _restoreAmount);
                    break;
            }
        }

        private void ApplyTypeColor()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            Color color = _dropType switch
            {
                KRDropItem.DropType.Health => new Color(0.9f, 0.15f, 0.15f),  // 빨강 — 체력
                KRDropItem.DropType.Fire => new Color(1f, 0.45f, 0.1f),   // 주황 — 화(火)
                KRDropItem.DropType.Water => new Color(0.2f, 0.6f, 1f),     // 파랑 — 수(水)
                KRDropItem.DropType.Wood => new Color(0.2f, 0.85f, 0.3f),   // 초록 — 목(木)
                KRDropItem.DropType.Earth => new Color(0.75f, 0.55f, 0.2f),  // 갈황 — 토(土)
                KRDropItem.DropType.Metal => new Color(0.9f, 0.85f, 0.4f),   // 은백 — 금(金)
                _ => Color.white
            };

            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color); // URP
            mpb.SetColor("_Color", color); // Standard
            rend.SetPropertyBlock(mpb);
        }

        // 에디터 기즈모 — 흡수 범위를 씬 뷰에서 확인할 수 있습니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _collectRange);
        }
    }
}