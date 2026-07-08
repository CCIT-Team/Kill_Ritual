using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Player.Combat;

namespace KillRitual.Items
{
    /// <summary>
    /// 처형 드롭(KRDropItem)과 달리, 레벨 디자이너가 씬에 직접 배치해 둔 3D 오브젝트에
    /// 붙이는 "고정 배치형" 회복 아이템 컴포넌트입니다.
    ///
    /// [KRDropItem과의 차이]
    ///   - KRDropItem: 처형 성공 시 KRDropSpawner가 런타임에 Instantiate하는 오브입니다.
    ///     Rigidbody로 중력 낙하 후 바닥에 닿으면(OnCollisionEnter) 고정되는 물리 기반 오브입니다.
    ///   - KRFloorItem(이 스크립트): 씬 에디터에서 사람이 직접 위치를 잡아 미리 배치해 둔
    ///     3D 오브젝트용입니다. 이미 바닥 위 정확한 위치에 놓여 있으므로 낙하/착지 물리가
    ///     필요 없습니다. Rigidbody 없이도 동작합니다.
    ///   - 흡수 판정(수평 거리, _collectRange), 타입별 색상, 회복 효과 적용 로직은 KRDropItem과
    ///     동일한 방식을 그대로 따릅니다.
    ///
    /// [사용 방법]
    ///   1. 씬에 원하는 3D 오브젝트(모델/메시)를 원하는 위치에 배치합니다.
    ///   2. 그 오브젝트에 이 컴포넌트를 붙입니다. Collider/Rigidbody는 필요 없습니다
    ///      (트리거 판정을 물리 콜라이더가 아니라 거리 계산으로 하기 때문입니다).
    ///   3. Inspector에서 Type(오행/체력)과 회복량을 지정합니다. 색상은 Awake 시 자동으로
    ///      타입에 맞게 적용됩니다.
    ///   4. 필요하면 _despawnOnCombatEnd를 켜서 전투 종료 시 함께 사라지도록 할 수 있습니다.
    ///      기본값은 꺼짐(false)입니다 — 레벨에 고정 배치된 아이템은 보통 전투와 무관하게
    ///      계속 존재해야 하는 경우가 많기 때문입니다. 상황에 맞게 조절하세요.
    /// </summary>
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

        /// <summary>DropType에 따라 오브 색상을 자동으로 설정합니다. 별도 머티리얼 없이 런타임에 적용됩니다.</summary>
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