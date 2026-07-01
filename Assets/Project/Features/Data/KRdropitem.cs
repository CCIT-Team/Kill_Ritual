using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Player.Combat;

namespace KillRitual.Items
{
    /// <summary>
    /// 처형 후 바닥에 떨어지는 회복 오브 컴포넌트입니다.
    /// 체력 오브 1종 + 오행 속성별 탄약 오브 5종, 총 6종을 이 클래스 하나로 처리합니다.
    ///
    /// [동작 방식 - 자석 + 흡수]
    ///   1. 드롭 직후 중력으로 바닥에 떨어집니다.
    ///   2. 플레이어가 _magnetRange 안에 들어오면 플레이어 쪽으로 빠르게 날아옵니다.
    ///   3. 플레이어와 닿으면(_collectRange) 즉시 회복 효과를 적용하고 사라집니다.
    ///   4. _lifetime초가 지나도 먹히지 않으면 자동으로 사라집니다.
    ///
    /// [프리팹 구성]
    ///   - 작은 구(Sphere) 오브젝트에 이 컴포넌트를 붙입니다.
    ///   - Rigidbody 필수 (중력 낙하용, Is Kinematic=false, Use Gravity=true)
    ///   - Collider는 Is Trigger=true (물리 충돌 없이 겹침만 감지)
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

        [Header("자석/흡수 설정")]
        [Tooltip("이 거리 안에 플레이어가 들어오면 오브가 플레이어 쪽으로 날아오기 시작합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _magnetRange = 6f;

        [Tooltip("자석으로 끌려오는 속도 (미터/초). 거리가 가까울수록 더 빠르게 날아옵니다.")]
        [Min(0.1f)]
        [SerializeField] private float _magnetSpeed = 8f;

        [Tooltip("이 거리 이하로 플레이어에게 가까워지면 즉시 흡수(회복 적용 후 파괴)됩니다.")]
        [Min(0.01f)]
        [SerializeField] private float _collectRange = 0.5f;

        [Header("수명")]
        [Tooltip("이 시간(초)이 지나도 먹히지 않으면 자동으로 사라집니다.")]
        [Min(1f)]
        [SerializeField] private float _lifetime = 12f;

        private Transform _player;
        private KRPlayerStats _playerStats;
        private KRCombatSystem _combatSystem;
        private Rigidbody _rb;
        private bool _isBeingMagneted;
        private bool _collected;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            Destroy(gameObject, _lifetime);
            ApplyTypeColor();
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

            float distance = Vector3.Distance(transform.position, _player.position);

            // 흡수 판정 — 충분히 가까우면 즉시 먹힘
            if (distance <= _collectRange)
            {
                Collect();
                return;
            }

            // 자석 판정 — 범위 안에 들어오면 날아가기 시작
            if (distance <= _magnetRange)
            {
                _isBeingMagneted = true;
            }

            if (_isBeingMagneted)
            {
                // Rigidbody 중력을 끄고 직접 이동 제어합니다.
                // 거리가 가까울수록 더 빠르게 끌려오는 이즈인 효과를 줍니다.
                if (_rb != null)
                {
                    _rb.useGravity = false;
                    _rb.velocity = Vector3.zero;
                }

                Vector3 direction = (_player.position - transform.position).normalized;
                float speed = _magnetSpeed * (1f + (1f - Mathf.Clamp01(distance / _magnetRange)));
                transform.position += direction * speed * Time.deltaTime;
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

        // 에디터 기즈모 — 자석 범위와 흡수 범위를 씬 뷰에서 확인할 수 있습니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _magnetRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _collectRange);
        }
    }
}