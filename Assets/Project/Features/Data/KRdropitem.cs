using System.Collections;
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
    ///      [2026-07-08 추가] "날라가는 중에 플레이어랑 부디치면 바로 먹어지게도 해줘" 요청으로,
    ///      착지 전(공중에 날아가는 동안)에는 _collectRange 대신 _midairCollectRadius로 3D 거리를
    ///      재서 판정합니다. 오브-플레이어는 물리 충돌이 꺼져 있어 실제 부딪혀도 반응이 없으므로,
    ///      이 거리 판정이 "부딪힘"을 대신합니다.
    ///   4. [2026-07-06 추가] 오브가 바닥(또는 아무 콜라이더)에 처음 닿으면(OnCollisionEnter) 그
    ///      즉시 Rigidbody를 Kinematic으로 바꿔 굴러가거나 경사면에서 미끄러지지 않게 합니다.
    ///      회전도 Awake() 시점부터 아예 잠가둬서(FreezeRotation) 떨어지는 도중에도 데굴데굴
    ///      구르는 모양이 나오지 않습니다.
    ///      [2026-07-08 변경] 착지 지점에 완전히 얼어붙지 않고, FloatAfterLandingRoutine()으로
    ///      그 자리 위로 살짝 떠오른 뒤 은은하게 위아래로 흔들립니다(호버링).
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
        /// [2026-07-08 신규] "떨어지는 속도 빠르게 가능?" → "포물선 후 떨어질때 가속으로" 요청으로
        /// 추가했습니다. 위로 솟구치는 포물선 구간(속도.y >= 0, 정점까지)은 건드리지 않아 발사
        /// 궤적이 자연스럽게 유지되고, 정점을 지나 아래로 떨어지기 시작한 순간(속도.y &lt; 0)부터만
        /// 추가 중력 가속도를 얹습니다. 떨어지기 시작한 뒤 경과 시간(_fallElapsed)에 비례해 배율을
        /// 1배 → _fallGravityMultiplier배까지 점점 키워서(_fallAccelRampTime초에 걸쳐), 떨어지는
        /// 도중에 점점 더 빨라지는 "가속" 느낌을 줍니다. 전역 Physics.gravity는 바꾸지 않으므로 다른
        /// 오브젝트(플레이어, 몬스터 등)에는 영향이 없습니다.
        /// </summary>
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

        /// <summary>
        /// [2026-07-08 신규] 낙하 포물선 궤적을 따라 TrailRenderer로 짧은 꼬리를 그립니다.
        /// 프리팹에 미리 붙여둘 필요 없이 런타임에 자동 생성/설정하며, 오브 색상(GetTypeColor)에
        /// 맞춰 꼬리 색도 같이 맞춥니다. 머티리얼은 셰이더만 있으면 되므로 URP/Standard 관계없이
        /// 항상 보이는 Sprites/Default로 즉석 생성합니다.
        /// </summary>
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

        /// <summary>
        /// [2026-07-06 추가] 오브가 바닥(또는 다른 고체 콜라이더)에 처음 닿는 순간 호출됩니다.
        /// Rigidbody를 Kinematic으로 전환해 굴러가거나 미끄러지지 않게 고정합니다.
        /// 콜라이더가 Is Trigger=false(고체)라서 물리적으로 부딪히는 모든 것에 대해 호출되므로,
        /// 첫 충돌에서만 한 번 동작하도록 _landed로 막아둡니다.
        /// [2026-07-08 변경] "바로 고정이 아니고 떨어진후 그위치에서 위로 살짝 뜨게 해주면 안돼?"
        /// 요청으로, 착지 지점에 딱 얼어붙는 대신 FloatAfterLandingRoutine()을 시작해 그 자리
        /// 위로 살짝 떠오른 뒤 은은하게 위아래로 흔들리도록 했습니다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_collected || _landed || _rb == null) return;

            // [2026-07-08 신규] "떨어지는 과정에서 플레이어랑 닿으면 멈추는데?" 버그 수정 — 이
            // 메서드는 원래 "바닥에 착지" 판정용인데, 부딪힌 대상이 무엇인지 구분하지 않아서
            // 플레이어와 부딪혀도 그대로 착지 처리(Kinematic 고정)되어 공중에서 딱 멈춰버렸습니다.
            // 플레이어와의 충돌이면 착지시키는 대신 즉시 회수 처리합니다.
            if (IsPlayerCollision(collision))
            {
                Collect();
                return;
            }

            _landed = true;
            _rb.isKinematic = true;
            _landedPosition = transform.position;

            // [2026-07-08 신규] 착지 후에는 낙하가 끝나므로, 꼬리가 계속 남지 않도록 생성을 멈춥니다.
            if (_trail != null) _trail.emitting = false;

            if (_floatAfterLanding)
            {
                // 여러 조각이 동시에 착지해도 위아래 흔들림 위상이 서로 겹치지 않도록 랜덤 오프셋을 줍니다.
                _floatTimeOffset = Random.Range(0f, Mathf.PI * 2f);
                StartCoroutine(FloatAfterLandingRoutine());
            }
        }

        /// <summary>
        /// [2026-07-08 신규] 부딪힌 대상이 플레이어인지 확인합니다. 태그("Player")를 우선 확인하고,
        /// 태그가 없는 자식 콜라이더(예: 별도 히트박스)일 경우를 대비해 부모 계층에서
        /// KRCombatSystem 컴포넌트 존재 여부로도 한 번 더 확인합니다.
        /// </summary>
        private static bool IsPlayerCollision(Collision collision)
        {
            if (collision.collider == null) return false;
            if (collision.collider.CompareTag("Player")) return true;

            return collision.collider.GetComponentInParent<KRCombatSystem>() != null;
        }

        /// <summary>
        /// [2026-07-08 신규] "바로 고정이 아니고 떨어진후 그위치에서 위로 살짝 뜨게 해주면 안돼?"
        /// 요청으로 추가했습니다. 착지 지점(_landedPosition)을 기준으로 _floatHeight만큼 부드럽게
        /// 떠오른 뒤(_floatRiseDuration), 그 높이에서 사인파로 위아래로 살짝 흔들리며 "떠 있는"
        /// 느낌을 줍니다. Rigidbody는 착지 순간 이미 Kinematic으로 바뀌어 있어 물리와 무관하게
        /// transform만 직접 움직입니다.
        /// </summary>
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

            Color color = GetTypeColor();

            // 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 색상만 바꿉니다 (GC 0).
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color); // URP
            mpb.SetColor("_Color", color); // Standard
            rend.SetPropertyBlock(mpb);
        }

        /// <summary>DropType에 대응하는 색상입니다. 오브 본체 색상과 트레일(꼬리) 색상이 같이 참조합니다.</summary>
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

            // [2026-07-08 신규] "날라가는 중에 플레이어랑 부디치면 바로 먹어지게도 해줘" 요청으로
            // 착지 전/후 판정을 분리했습니다. 오브-플레이어 물리 충돌은 레이어로 꺼져 있어서 날아가는
            // 중에 실제로 몸에 스쳐도 OnCollisionEnter가 뜨지 않으므로, 착지 전에는 _midairCollectRadius로
            // 3D 거리(높이 포함)를 재서 "부딪힌 것"을 대신 판정합니다.
            if (!_landed)
            {
                float midairDistance = Vector3.Distance(transform.position, _player.position);
                if (midairDistance <= _midairCollectRadius)
                {
                    Collect();
                }
                return;
            }

            // [2026-07-06 변경, 2026-07-09 재도입] 한 번 삭제했던 자석 추적을 "오브 먹는 판정
            // 흡수도 넣자" 요청으로 다시 넣었습니다. 이번엔 즉시 순간이동시키는 방식이 아니라,
            // FloatAfterLandingRoutine()이 기준으로 삼는 _landedPosition 자체를 플레이어 쪽으로
            // 서서히 이동시킵니다 — 뜨고 흔들리는(float/bob) 연출은 그 위에 그대로 얹히므로 두
            // 시스템이 transform.position을 서로 덮어쓰며 충돌하지 않습니다. _magnetRange를 0으로
            // 두면 예전처럼 제자리에 고정됩니다.
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

            // [2026-07-06 변경, 2026-07-09 되돌림] 원래 Vector3.Distance(3D 직선거리) 대신 수평(XZ)
            // 거리만 썼습니다 — 오브는 구 콜라이더 반지름만큼 땅 위에 떠서 멈추고 플레이어 트랜스폼
            // 피벗은 그보다 낮아서, Y축까지 포함하면 그 고정 높이 차이만으로 당시 _collectRange(0.35)를
            // 다 써버려 아무리 가까이 가도 주울 수 없었기 때문입니다. 이제 _collectRange를 1.0으로
            // 넉넉하게 늘렸고 "y축 무시 없애줘" 요청도 있어 Y축을 포함한 3D 거리로 되돌렸습니다.
            // 높이 차이가 커도 1.0m 예산 안에서 흡수될 가능성이 높지만, 만약 다시 "가까이 가도
            // 안 먹힌다"는 문제가 재현되면 이 Y축 포함이 원인일 수 있습니다.
            float distance = Vector3.Distance(transform.position, _player.position);

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