using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Player.Combat;

namespace KillRitual.Items
{
    /// <summary>
    /// 처형 후 바닥에 떨어지는 회복 오브 컴포넌트입니다.
    /// 체력 오브 1종 + 오행 속성별 탄약 오브 5종, 총 6종을 이 클래스 하나로 처리합니다.
    /// ⚠️ 이 컴포넌트는 작두(Jakdu)뿐 아니라 KRDropSpawner를 통한 다른 처형 드롭에도 공용으로
    /// 쓰이는 파일입니다. 여기를 고치면 작두 외의 드롭 오브 동작에도 영향을 줍니다.
    ///
    /// [동작 방식 - 흡수(추적 없음)]
    ///   1. 드롭 직후 중력으로 바닥에 떨어집니다.
    ///   2. [2026-07-06 변경] 플레이어를 향해 날아오는 자석 추적 기능을 제거했습니다.
    ///      기획서상 "잔여 자원"은 전투 중 임시 보급 지점으로, 플레이어가 직접 다가가서
    ///      주워야 하는 개념이라 오브가 스스로 쫓아오면 안 됩니다. 이제는 플레이어가
    ///      _collectRange(좁은 범위) 안까지 직접 걸어와야만 회수됩니다.
    ///   3. 플레이어가 _collectRange 이내로 가까워지면 즉시 회복 효과를 적용하고 사라집니다.
    ///      [2026-07-06 변경] 거리 계산을 수평(XZ) 거리 기준으로 바꿨습니다. 기존엔 Y축(높이)까지
    ///      포함한 3D 직선거리였는데, 오브는 구(Sphere) 콜라이더 반지름만큼 땅 위에 떠서 멈추고
    ///      플레이어 트랜스폼 피벗은 그보다 낮은 위치라서, 항상 남는 높이 차이만으로 _collectRange를
    ///      거의 다 잡아먹어 "바로 옆에 서 있어도 절대 안 주워지는" 문제가 있었습니다.
    ///   4. [2026-07-06 추가] 오브가 바닥(또는 아무 콜라이더)에 처음 닿으면(OnCollisionEnter) 그
    ///      즉시 Rigidbody를 Kinematic으로 바꿔 그 자리에 완전히 고정합니다. 착지 후 굴러가거나
    ///      경사면에서 미끄러지지 않습니다. 회전도 Awake() 시점부터 아예 잠가둬서(FreezeRotation)
    ///      떨어지는 도중에도 데굴데굴 구르는 모양이 나오지 않습니다.
    ///   5. KRCombatZone이 발행하는 KRCombatEndEvent(전투 종료)를 받으면 즉시 사라집니다.
    ///      기획서(3-5, 4-4, 5-2) 기준 "잔여 자원은 전투 종료 시 제거"를 반영한 것입니다.
    ///      다만 존 밖에서 드롭되는 등 이벤트가 안 오는 경우를 대비해, _lifetime초 고정 타이머도
    ///      안전장치로 계속 유지합니다(둘 중 먼저 오는 쪽이 발동).
    ///
    /// [프리팹 구성]
    ///   - 작은 구(Sphere) 오브젝트에 이 컴포넌트를 붙입니다.
    ///   - Rigidbody 필수 (중력 낙하용. Is Kinematic은 false로 시작하되, 착지 순간 스크립트가
    ///     자동으로 true로 전환합니다. Use Gravity=true)
    ///   - Collider는 Is Trigger=false(고체) — 바닥에 물리적으로 얹히기 위해 필요합니다.
    ///     플레이어와는 물리적으로 부딪히면 안 되므로, 이 오브젝트의 레이어("item orb")는
    ///     Project Settings > Physics > Layer Collision Matrix에서 "Player" 레이어와
    ///     충돌하지 않도록 꺼져 있어야 합니다(이미 설정 완료).
    ///   - 속성/종류에 따라 색상만 다르게 설정하면 됩니다.
    /// </summary>
    public sealed class KRDropItem : MonoBehaviour
    {
        /// <summary>오브 종류. 체력 오브와 오행 5속성 탄약 오브를 구분합니다.</summary>
        public enum DropType
        {
            Health,    // 체력 회복
            Fire,      // 화(火) 탄약
            Water,     // 수(水) 탄약
            Wood,      // 목(木) 탄약
            Earth,     // 토(土) 탄약
            Metal      // 금(金) 탄약
        }

        [Header("오브 설정")]
        [Tooltip("이 오브의 종류 (체력/탄약 속성)")]
        [SerializeField] private DropType _dropType = DropType.Health;

        [Tooltip("회복량. 체력 오브는 체력 회복량(절대값), 탄약 오브는 자원 회복량.")]
        [Min(0f)]
        [SerializeField] private float _restoreAmount = 25f;

        [Header("흡수 설정")]
        [Tooltip("이 거리 이하로 플레이어가 직접 다가와야 회수(회복 적용 후 파괴)됩니다. " +
                 "[2026-07-06 변경] 자석 추적 기능은 제거되었으므로, 이 값이 사실상 유일한 " +
                 "수집 판정 범위입니다. 좁게 유지해서 '걸어가서 주워야 하는' 느낌을 유지하세요.")]
        [Min(0.01f)]
        [SerializeField] private float _collectRange = 0.35f;

        [Header("수명")]
        [Tooltip("이 시간(초)이 지나도 먹히지 않으면 자동으로 사라집니다.")]
        [Min(1f)]
        [SerializeField] private float _lifetime = 12f;

        private Transform _player;
        private KRPlayerStats _playerStats;
        private KRCombatSystem _combatSystem;
        private Rigidbody _rb;
        private bool _collected;
        private bool _landed;

        private void Awake()
        {
            ApplyTypeColor();

            // [2026-07-06 추가] 착지 고정 처리를 위해 Rigidbody를 다시 참조합니다(자석 이동용이
            // 아니라, 바닥에 닿는 순간 Kinematic으로 바꿔 그 자리에 고정하기 위한 용도입니다).
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                // 낙하하는 동안(그리고 착지 순간까지) 회전이 걸리지 않게 미리 잠급니다.
                // 구 콜라이더 + 중력 조합이라 잠그지 않으면 착지 직전에 살짝 굴러 보일 수 있습니다.
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        /// <summary>
        /// [2026-07-06 추가] 오브가 바닥(또는 다른 고체 콜라이더)에 처음 닿는 순간 호출됩니다.
        /// Rigidbody를 Kinematic으로 전환해 그 자리에 완전히 고정시킵니다(굴러가거나 미끄러지지 않음).
        /// 콜라이더가 Is Trigger=false(고체)라서 물리적으로 부딪히는 모든 것에 대해 호출되므로,
        /// 첫 충돌에서만 한 번 동작하도록 _landed로 막아둡니다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_landed || _rb == null) return;

            _landed = true;
            _rb.isKinematic = true;
        }

        private void OnEnable()
        {
            KRManagers.Event.Subscribe<KRCombatEndEvent>(OnCombatEnd);

            // [2026-07-06 추가] 전투 종료 이벤트가 오지 않는 경우(KRCombatZone이 없는 씬,
            // 존 범위 밖에서 드롭된 경우 등)를 대비한 안전장치입니다.
            // 기존 "Destroy(gameObject, _lifetime)"를 대체하며, 조건부 파괴 메서드를 통해
            // 이미 회수된 오브를 이중으로 파괴 시도하지 않도록 합니다.
            Invoke(nameof(DespawnIfNotCollected), _lifetime);
        }

        private void OnDisable()
        {
            KRManagers.Event.Unsubscribe<KRCombatEndEvent>(OnCombatEnd);
            CancelInvoke(nameof(DespawnIfNotCollected));
        }

        /// <summary>전투 종료 시 호출됩니다. 아직 회수되지 않은 잔여 자원 오브를 제거합니다.</summary>
        private void OnCombatEnd(KRCombatEndEvent evt)
        {
            DespawnIfNotCollected();
        }

        private void DespawnIfNotCollected()
        {
            if (_collected) return;
            Destroy(gameObject);
        }

        /// <summary>드롭 직전 회복량을 동적으로 재설정합니다. 비율 기반 드롭(예: 작두 처형)에서 사용합니다.</summary>
        public void ConfigureAmount(float amount)
        {
            _restoreAmount = Mathf.Max(0f, amount);
        }

        /// <summary>DropType에 따라 오브 색상을 자동으로 설정합니다. 별도 머티리얼 없이 런타임에 적용됩니다.</summary>
        private void ApplyTypeColor()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            Color color = _dropType switch
            {
                DropType.Health => new Color(0.9f, 0.15f, 0.15f),  // 빨강 — 체력
                DropType.Fire => new Color(1f, 0.45f, 0.1f),   // 주황 — 화(火)
                DropType.Water => new Color(0.2f, 0.6f, 1f),     // 파랑 — 수(水)
                DropType.Wood => new Color(0.2f, 0.85f, 0.3f),   // 초록 — 목(木)
                DropType.Earth => new Color(0.75f, 0.55f, 0.2f),  // 갈황 — 토(土)
                DropType.Metal => new Color(0.9f, 0.85f, 0.4f),   // 은백 — 금(金)
                _ => Color.white
            };

            // 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 색상만 바꿉니다 (GC 0).
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color); // URP
            mpb.SetColor("_Color", color); // Standard
            rend.SetPropertyBlock(mpb);
        }

        private void Start()
        {
            // 플레이어와 필요한 컴포넌트를 찾습니다.
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

            // [2026-07-06 변경] 자석 추적 로직을 삭제했습니다. 이제 오브는 플레이어를 쫓지 않고
            // 중력으로 떨어진 자리에 그대로 있으며, 플레이어가 _collectRange 안까지 직접
            // 걸어와야만 회수됩니다. (기존에는 _magnetRange(6m) 안에 들어오면 플레이어 쪽으로
            // 날아오는 자석 판정이 있었으나, "걸어가서 주워야 하는 잔여 자원" 컨셉과 맞지 않아 제거)
            //
            // [2026-07-06 변경] Vector3.Distance(3D 직선거리) 대신 수평(XZ) 거리만 계산합니다.
            // 오브는 구 콜라이더 반지름만큼 땅 위에 떠서 멈추고 플레이어 트랜스폼 피벗은 그보다
            // 낮아서, Y축까지 포함하면 항상 남는 높이 차이만으로 좁은 _collectRange를 다 써버려
            // 실제로는 아무리 가까이 가도 주울 수 없는 문제가 있었습니다.
            Vector3 delta = transform.position - _player.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (distance <= _collectRange)
            {
                Collect();
            }
        }

        /// <summary>플레이어가 오브를 흡수합니다. 회복 효과를 적용하고 오브젝트를 파괴합니다.</summary>
        private void Collect()
        {
            if (_collected) return;
            _collected = true;

            ApplyRestore();
            Destroy(gameObject);
        }

        private void ApplyRestore()
        {
            switch (_dropType)
            {
                case DropType.Health:
                    _playerStats?.Heal(_restoreAmount);
                    break;

                case DropType.Fire:
                    _combatSystem?.RefillResource(KRDamageType.Fire, _restoreAmount);
                    break;

                case DropType.Water:
                    _combatSystem?.RefillResource(KRDamageType.Water, _restoreAmount);
                    break;

                case DropType.Wood:
                    _combatSystem?.RefillResource(KRDamageType.Wood, _restoreAmount);
                    break;

                case DropType.Earth:
                    _combatSystem?.RefillResource(KRDamageType.Earth, _restoreAmount);
                    break;

                case DropType.Metal:
                    _combatSystem?.RefillResource(KRDamageType.Metal, _restoreAmount);
                    break;
            }
        }

        // 에디터 기즈모 — 좁아진 흡수 범위를 씬 뷰에서 확인할 수 있습니다.
        // [2026-07-06 변경] 자석 범위 기즈모는 자석 기능 제거와 함께 삭제했습니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _collectRange);
        }
    }
}