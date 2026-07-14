using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Player.Combat;

namespace KillRitual.Items
{
    public sealed class KRDropItem : MonoBehaviour
    {
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
        [Tooltip("착지 후 이 거리 이하로 플레이어가 직접 걸어와야 회수(회복 적용 후 파괴)됩니다. " +
                 "[2026-07-06 변경] 자석 추적 기능은 제거되었으므로, 착지 후에는 이 값이 사실상 " +
                 "유일한 수집 판정 범위입니다. '걸어가서 주워야 하는' 느낌은 유지하되, 너무 좁으면 " +
                 "오브 바로 위까지 정확히 밟아야만 먹혀서 '안 먹힌다'고 느껴집니다. " +
                 "[2026-07-09 변경] '오브가 너무 안 먹어진다' 피드백으로 0.35 → 1.0으로 넉넉하게 조정.")]
        [Min(0.01f)]
        [SerializeField] private float _collectRange = 1.0f;

        [Tooltip("[2026-07-08 신규] '날라가는 중에 플레이어랑 부디치면 바로 먹어지게도 해줘' 요청으로 " +
                 "추가 — 오브와 플레이어는 물리적으로 충돌하지 않도록 레이어가 꺼져 있어서 실제 " +
                 "OnCollisionEnter가 발생하지 않습니다. 그래서 착지 전(공중에 날아가는 동안)에는 " +
                 "이 반경으로 3D 거리를 재서 '부딪힌 것'처럼 즉시 회수시킵니다. 착지 후에는 이 값 " +
                 "대신 위 _collectRange(좁은 도보 회수 범위)를 씁니다.")]
        [Min(0.01f)]
        [SerializeField] private float _midairCollectRadius = 0.6f;

        [Tooltip("[2026-07-09 신규] '오브 먹는 판정 흡수도 넣자' 요청으로 추가 — 착지한 오브는 " +
                 "이전엔 플레이어가 몇 미터 밖에 있든 제자리에 가만히 있었습니다(자석 추적 기능은 " +
                 "2026-07-06에 '걸어가서 주워야 하는' 컨셉 때문에 의도적으로 제거됨). 이제 착지 후 " +
                 "이 거리 안에 플레이어가 들어오면 서서히 플레이어 쪽으로 끌려갑니다(자석 흡수). " +
                 "[2026-07-09 변경 — '흡수 범위는 직접접촉 판정범위보다 좁게'] 일부러 _collectRange(1.0)" +
                 "보다 작게 잡았습니다. 즉 자석이 당기기 시작하는 순간엔 이미 도보 회수 판정 범위 " +
                 "안이라 바로 회수되는 경우가 많고, 멀리서부터 끌려오는 느낌은 없습니다 — 장거리 " +
                 "자석이 아니라 마지막 순간의 작은 스냅 정도로만 작동합니다. 0이면 완전히 끕니다.")]
        [Min(0f)]
        [SerializeField] private float _magnetRange = 0.6f;

        [Tooltip("자석으로 끌려갈 때 이동 속도(초당 유닛). 클수록 빠르게 딸려옵니다.")]
        [Min(0.01f)]
        [SerializeField] private float _magnetSpeed = 6f;

        [Header("낙하 속도")]
        [Tooltip("[2026-07-08 신규] '떨어지는 속도 빠르게 가능?' → '포물선 후 떨어질때 가속으로' 요청 " +
                 "반영 — 위로 솟구치는 포물선 구간(정점까지)은 자연스러운 궤적을 위해 그대로 두고, " +
                 "정점을 지나 아래로 떨어지기 시작한 뒤부터만 기본 중력 위에 이 배율만큼 추가 가속도가 " +
                 "붙습니다(최종적으로 중력이 이 배율만큼 작용). 전역 Physics.gravity는 건드리지 않고 " +
                 "이 오브젝트에만 개별 적용됩니다.")]
        [Min(1f)]
        [SerializeField] private float _fallGravityMultiplier = 2.5f;

        [Tooltip("떨어지기 시작한 뒤 배율이 1배에서 위 _fallGravityMultiplier까지 점점 커지는 데 " +
                 "걸리는 시간(초)입니다. 짧을수록 떨어지자마자 훅 가속되는 느낌이고, 길수록 서서히 " +
                 "가속되는 느낌입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _fallAccelRampTime = 0.3f;

        [Header("수명")]
        [Tooltip("이 시간(초)이 지나도 먹히지 않으면 자동으로 사라집니다.")]
        [Min(1f)]
        [SerializeField] private float _lifetime = 12f;

        [Header("궤적 효과 (낙하 중 꼬리)")]
        [Tooltip("[2026-07-08 신규] \"떨어질때 탄약이 포물선으로 떨어지면서 꼬리를 보여주면 좋겠어\" " +
                 "요청으로 추가했습니다. 낙하(포물선 궤적) 중에만 뒤로 꼬리를 그리고, 착지하면 멈춥니다.")]
        [SerializeField] private bool _showTrail = true;

        [Tooltip("꼬리가 사라지기까지 걸리는 시간(초). 값이 작을수록 꼬리가 짧아집니다.")]
        [Min(0.05f)]
        [SerializeField] private float _trailTime = 0.35f;

        [Tooltip("꼬리의 시작(오브 쪽) 두께입니다.")]
        [Min(0f)]
        [SerializeField] private float _trailStartWidth = 0.15f;

        [Tooltip("꼬리의 끝(멀어질수록) 두께입니다. 0에 가까울수록 끝이 뾰족해집니다.")]
        [Min(0f)]
        [SerializeField] private float _trailEndWidth = 0.02f;

        [Header("착지 후 부유 효과")]
        [Tooltip("[2026-07-08 신규] \"바로 고정이 아니고 떨어진후 그위치에서 위로 살짝 뜨게 해주면 " +
                 "안돼?\" 요청으로 추가 — 착지 즉시 그 자리에 완전히 얼어붙는 대신, 착지 지점을 " +
                 "기준으로 살짝 떠오른 뒤 그 자리에서 위아래로 은은하게 흔들립니다(호버링).")]
        [SerializeField] private bool _floatAfterLanding = true;

        [Tooltip("착지 지점 기준 떠오르는 높이(m).")]
        [Min(0f)]
        [SerializeField] private float _floatHeight = 0.35f;

        [Tooltip("떠오른 뒤 위아래로 흔들리는 폭(진폭, m).")]
        [Min(0f)]
        [SerializeField] private float _floatBobAmplitude = 0.08f;

        [Tooltip("위아래로 흔들리는 속도.")]
        [Min(0f)]
        [SerializeField] private float _floatBobSpeed = 2f;

        [Tooltip("착지 지점에서 떠오르는 데 걸리는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _floatRiseDuration = 0.4f;

        private Transform _player;
        private KRPlayerStats _playerStats;
        private KRCombatSystem _combatSystem;
        private Rigidbody _rb;
        private TrailRenderer _trail;
        private Vector3 _landedPosition;
        private float _floatTimeOffset;
        private float _fallElapsed;
        private bool _collected;
        private bool _landed;

        private void Awake()
        {
            ApplyTypeColor();
            SetupTrail();

            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                // 낙하하는 동안(그리고 착지 순간까지) 회전이 걸리지 않게 미리 잠급니다.
                // 구 콜라이더 + 중력 조합이라 잠그지 않으면 착지 직전에 살짝 굴러 보일 수 있습니다.
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        private void FixedUpdate()
        {
            if (_landed || _rb == null || _rb.isKinematic) return;

            bool isFalling = _rb.velocity.y < 0f;
            if (!isFalling)
            {
                // 아직 위로 솟구치는 중(포물선 정점 전)이라면 가속 타이머를 초기화하고 그대로 둡니다.
                _fallElapsed = 0f;
                return;
            }

            if (_fallGravityMultiplier <= 1f) return;

            _fallElapsed += Time.fixedDeltaTime;
            float ramp = Mathf.Clamp01(_fallElapsed / _fallAccelRampTime);
            float extraMultiplier = (_fallGravityMultiplier - 1f) * ramp;

            _rb.AddForce(Physics.gravity * extraMultiplier, ForceMode.Acceleration);
        }

        private void SetupTrail()
        {
            if (!_showTrail) return;

            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = _trailTime;
            _trail.startWidth = _trailStartWidth;
            _trail.endWidth = _trailEndWidth;
            _trail.minVertexDistance = 0.03f;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;

            Color color = GetTypeColor();
            _trail.material = new Material(Shader.Find("Sprites/Default"));
            _trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            };
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_collected || _landed || _rb == null) return;

            if (IsPlayerCollision(collision))
            {
                Collect();
                return;
            }

            _landed = true;
            _rb.isKinematic = true;
            _landedPosition = transform.position;

            if (_trail != null) _trail.emitting = false;

            if (_floatAfterLanding)
            {
                // 여러 조각이 동시에 착지해도 위아래 흔들림 위상이 서로 겹치지 않도록 랜덤 오프셋을 줍니다.
                _floatTimeOffset = Random.Range(0f, Mathf.PI * 2f);
                StartCoroutine(FloatAfterLandingRoutine());
            }
        }

        private static bool IsPlayerCollision(Collision collision)
        {
            if (collision.collider == null) return false;
            if (collision.collider.CompareTag("Player")) return true;

            return collision.collider.GetComponentInParent<KRCombatSystem>() != null;
        }

        private IEnumerator FloatAfterLandingRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _floatRiseDuration)
            {
                if (_collected) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _floatRiseDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                transform.position = _landedPosition + Vector3.up * (_floatHeight * eased);
                yield return null;
            }

            // 다 떠오른 뒤에는 그 높이를 기준으로 계속 위아래로 은은하게 흔들립니다.
            while (!_collected)
            {
                float bob = Mathf.Sin(Time.time * _floatBobSpeed + _floatTimeOffset) * _floatBobAmplitude;
                transform.position = _landedPosition + Vector3.up * (_floatHeight + bob);
                yield return null;
            }
        }

        private void OnEnable()
        {
            KRManagers.Event.Subscribe<KRCombatEndEvent>(OnCombatEnd);

            Invoke(nameof(DespawnIfNotCollected), _lifetime);
        }

        private void OnDisable()
        {
            KRManagers.Event.Unsubscribe<KRCombatEndEvent>(OnCombatEnd);
            CancelInvoke(nameof(DespawnIfNotCollected));
        }

        private void OnCombatEnd(KRCombatEndEvent evt)
        {
            DespawnIfNotCollected();
        }

        private void DespawnIfNotCollected()
        {
            if (_collected) return;
            Destroy(gameObject);
        }

        public void ConfigureAmount(float amount)
        {
            _restoreAmount = Mathf.Max(0f, amount);
        }

        private void ApplyTypeColor()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            Color color = GetTypeColor();

            // 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 색상만 바꿉니다 (GC 0).
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color); // URP
            mpb.SetColor("_Color", color); // Standard
            rend.SetPropertyBlock(mpb);
        }

        private Color GetTypeColor()
        {
            return _dropType switch
            {
                DropType.Health => new Color(0.9f, 0.15f, 0.15f),  // 빨강 — 체력
                DropType.Fire => new Color(1f, 0.45f, 0.1f),   // 주황 — 화(火)
                DropType.Water => new Color(0.2f, 0.6f, 1f),     // 파랑 — 수(水)
                DropType.Wood => new Color(0.2f, 0.85f, 0.3f),   // 초록 — 목(木)
                DropType.Earth => new Color(0.75f, 0.55f, 0.2f),  // 갈황 — 토(土)
                DropType.Metal => new Color(0.9f, 0.85f, 0.4f),   // 은백 — 금(金)
                _ => Color.white
            };
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

            if (!_landed)
            {
                float midairDistance = Vector3.Distance(transform.position, _player.position);
                if (midairDistance <= _midairCollectRadius)
                {
                    Collect();
                }
                return;
            }

            if (_magnetRange > 0f)
            {
                Vector3 toPlayerXZ = _player.position - _landedPosition;
                toPlayerXZ.y = 0f;

                if (toPlayerXZ.magnitude <= _magnetRange)
                {
                    Vector3 targetXZ = new Vector3(_player.position.x, _landedPosition.y, _player.position.z);
                    _landedPosition = Vector3.MoveTowards(_landedPosition, targetXZ, _magnetSpeed * Time.deltaTime);
                }
            }

            float distance = Vector3.Distance(transform.position, _player.position);

            if (distance <= _collectRange)
            {
                Collect();
            }
        }

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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _collectRange);
        }
    }
}