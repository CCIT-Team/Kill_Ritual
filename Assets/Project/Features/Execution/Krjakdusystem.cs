// Assets/Project/Features/Player/KRJakduSystem.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual; // [2026-07-06 추가] KRJakduChargeUI(Assets/Project/Features/UI/KRJakduChargeUI.cs)가 이 네임스페이스에 있습니다.

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 작두 시스템 — C키 입력 시 전방 광역 타격으로 탄약을 회수하고 공간을 확보합니다.
    ///
    /// [판정 방식]
    /// KRJakduZone(Box Collider Trigger)이 범위 내 적을 수집하고,
    /// 작두 발동 시 1프레임 활성화 후 수집된 적에게 피해를 적용합니다.
    ///
    /// [기획 요약]
    /// - C키 + 작두 자원 1개 소모
    /// - 등급별 피해 + 처치/적중에 따른 탄약 드롭
    /// - 드롭 탄약: 발동 시점 보유 비율 가장 낮은 속성 1종
    /// - 감속 → 판정 → 반발 가속
    ///
    /// [변경 이력 - 2026-07-06 (1차, 폐기됨)]
    /// ApplyHits() / SpawnAmmoOrb() 수정 — "둠 이터널 스타일 드롭" 요청 반영해서
    /// 처치는 KRDropSpawner로, 적중만 비율 드롭으로 나눴었음. 이후 정식 기획서(작두 시스템
    /// 기획 문서 3-5/4-4)를 확인해보니 기획 의도와 반대 방향이라 2차 변경에서 되돌림.
    ///
    /// [변경 이력 - 2026-07-06 (2차, 현재)]
    /// 정식 기획서 기준으로 재작성:
    ///   - 처치/적중 보상을 분리하지 않고, 작두 1회 발동 안에서 발생한 모든 대상의 보상을
    ///     ApplyHits()에서 전부 합산한 뒤 _dropCap(70%) 상한을 적용합니다(기획 3-5 예시와 동일).
    ///   - KRDropSpawner(처형 드롭 파이프라인)는 더 이상 호출하지 않습니다. 작두는 KRDropSpawner가
    ///     아니라 자체 비율 계산 + KRCombatSystem 자원 지갑을 직접 사용하는 독립 시스템입니다.
    ///   - SpawnAmmoOrb()는 "회수 가능한 만큼 즉시 흡수 + 초과분만 대표 오브젝트 하나로 드롭"
    ///     방식입니다(기획 4-4 "다수의 자원은 대표 오브젝트로 묶어서 표현"). 대상마다 오브를
    ///     흩뿌리지 않고, 작두 1회당 오브가 생성되어도 최대 1개입니다.
    ///
    /// [변경 이력 - 2026-07-08]
    /// "둠 이터널 전기톱처럼 여러 파츠가 나오게" 명시적 요청으로, 위 4-4의 "오브 하나" 원칙만
    /// 의도적으로 뒤집었습니다. 초과분(remaining)을 오브 하나가 아니라 _ammoOrbSplitCount개로
    /// 쪼개서 물리적으로 흩뿌립니다(SpawnAmmoOrbPiece 참고). 자원 총량 계산(기획 3-5/4-4의 비율,
    /// 즉시흡수/초과분 구분)은 그대로 유지했고, 시각적 드롭 표현 방식만 바꿨습니다.
    /// 남은 과제(기획서 대비 아직 미구현, 별도 작업 필요):
    ///   - 갑사/장령 등급 구분 (현재 EnemyGrade는 Fodder/Heavy/Elite/Boss 4종뿐이라 갑사·장령이
    ///     Elite 하나로 합쳐져 있음. 장령 전용 수치(10%/30%)가 아직 코드에 없음)
    ///   - 등급별 넉백/경직 반응 (기획 3-4) — 현재 데미지만 적용하고 물리 반응 없음
    /// </summary>
    public sealed class KRJakduSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("작두 판정 존. 비워두면 자식에서 자동 탐색합니다.")]
        [SerializeField] private KRJakduZone _jakduZone;

        [Tooltip("KRCombatSystem. 비워두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Tooltip("플레이어 카메라. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private Camera _playerCamera;

        [Header("작두 애니메이션 (샤먼소드)")]
        [Tooltip("작두 발동 시 잠깐 나타나는 샤먼소드 손 오브젝트입니다. ShamanSword.controller에는 " +
                 "Swing 모션 하나만 있고 별도 Idle(대기) 애니메이션이 없기 때문에, 평소엔 이 오브젝트 " +
                 "자체를 꺼둔 채로 있다가 작두를 쓸 때만 켜고, 스윙이 끝나면 다시 끕니다. " +
                 "비워두면 애니메이션 연출 없이 판정만 그대로 동작합니다(필수 아님).")]
        [SerializeField] private GameObject _shamanSwordVisualRoot;

        [Tooltip("샤먼소드의 Animator. 비워두면 _shamanSwordVisualRoot의 자식에서 자동 탐색합니다.")]
        [SerializeField] private Animator _shamanSwordAnimator;

        [Tooltip("Swing.anim 클립 길이(초)입니다. 이 시간이 지나면 샤먼소드 손을 자동으로 다시 숨깁니다. " +
                 "이동 감속(①감속 단계)도 이 값만큼 지속됩니다 — 감속이 \"애니메이션 시전 시간 동안\" " +
                 "유지되도록 요청받아, 별도의 _slowDuration 대신 이 값을 그대로 재사용합니다. " +
                 "Swing.anim을 다른 클립으로 바꾸면 이 값도 그 클립 길이에 맞춰 같이 바꿔주세요 " +
                 "(감속 지속시간 + 적 판정 타이밍이 자동으로 같이 바뀝니다). " +
                 "[2026-07-06 재정정] Swing.anim의 실제 m_StopTime을 다시 확인해보니 1.1666666초가 " +
                 "아니라 0.55초였습니다(적 반응이 스윙 동작이 끝난 뒤에도 한참 있다가 나오는 버그의 " +
                 "원인 — 애니메이션은 0.55초에 끝났는데 코드는 1.17초까지 기다렸다가 판정했었음). " +
                 "0.55초로 정정했습니다.")]
        [Min(0.01f)]
        [SerializeField] private float _shamanSwordSwingClipLength = 0.55f;

        [Header("UI")]
        [Tooltip("작두(처형) 자원의 현재 보유 개수만 텍스트로 표시할 UI입니다(Assets/Project/Features/UI/KRJakduChargeUI.cs, " +
                 "TextMeshProUGUI 기반). 비워두면 UI 갱신을 건너뜁니다(필수 아님). Canvas 하위에 배치한 뒤 여기에 직접 드래그해서 연결하세요.")]
        [SerializeField] private KRJakduChargeUI _jakduChargeUI;

        [Header("작두 자원")]
        [Tooltip("작두 자원 현재 보유량.")]
        [SerializeField] private int _currentResource;
        [Tooltip("작두 자원 최대 보유량.")]
        [Min(1)]
        [SerializeField] private int _maxResource = 3;

        [Header("피해량")]
        [Tooltip("기본 피해량.")]
        [Min(0f)]
        [SerializeField] private float _baseDamage = 300f;

        [Header("등급별 피해 배율")]
        [Range(0f, 2f)][SerializeField] private float _multiplierFodder = 1.0f;
        [Range(0f, 2f)][SerializeField] private float _multiplierHeavy = 0.8f;
        [Range(0f, 2f)][SerializeField] private float _multiplierElite = 0.4f;
        [Range(0f, 2f)][SerializeField] private float _multiplierBoss = 0.2f;

        [Header("드롭 비율 (최대 탄약 대비 %)")]
        [Range(0f, 1f)][SerializeField] private float _dropFodderKill = 0.10f;
        [Range(0f, 1f)][SerializeField] private float _dropEliteHit = 0.05f;
        [Range(0f, 1f)][SerializeField] private float _dropEliteKill = 0.20f;
        [Range(0f, 1f)][SerializeField] private float _dropBossHit = 0.20f;
        [Range(0f, 1f)][SerializeField] private float _dropBossKill = 0.50f;
        [Tooltip("1회 작두 최대 드롭 상한.")]
        [Range(0f, 1f)][SerializeField] private float _dropCap = 0.70f;

        [Header("탄약 오브 프리팹")]
        [Tooltip("[0]=화 [1]=수 [2]=목 [3]=토 [4]=금 순서.")]
        [SerializeField] private GameObject[] _ammoOrbPrefabs = new GameObject[5];

        [Tooltip("[2026-07-08 신규] '탄약오브 등록에 비활성화도 할수있게해줘' 요청으로 추가 — 위 " +
                 "프리팹 배열과 같은 순서(화/수/목/토/금)입니다. 여기서 체크를 끄면 프리팹을 " +
                 "지우지 않고도 해당 속성의 드롭을 완전히 끌 수 있습니다(즉시 흡수까지 포함해서 " +
                 "SpawnAmmoOrb() 맨 앞에서 걸러냅니다). 슬롯이 잠겨 무기가 없는 속성(예: 토/금)을 " +
                 "굳이 드롭하지 않게 할 때 사용하세요.")]
        [SerializeField] private bool[] _ammoOrbEnabled = { true, true, true, true, true };

        [Header("탄약 오브 흩뿌리기 (2026-07-08 신규)")]
        [Tooltip("[2026-07-08 신규] '둠 이터널 전기톱처럼 여러 파츠로' 요청 반영 — 기획서(3-5/4-4, " +
                 "'대표 오브젝트 하나') 대신, 초과 자원(remaining)을 오브 하나가 아니라 이 개수만큼 " +
                 "쪼개서 사방으로 흩뿌립니다. 몇 개를 줍든 합계는 항상 remaining과 같도록 개수로 " +
                 "나눠서 각 조각의 회복량을 정합니다. " +
                 "[2026-07-08 수정 — '그냥 나오는 방향 4개로 설정해서 퍼트리면 안됨?'] 방향을 " +
                 "완전 랜덤(Random.insideUnitCircle)으로 뽑던 걸, 이 개수만큼 균등하게 나눈 고정 " +
                 "각도로 바꿨습니다 — 물리 힘/착지 타이밍에 기대지 않고 항상 확실하게 사방으로 " +
                 "갈라지는 걸 보장합니다.")]
        [Min(1)] [SerializeField] private int _ammoOrbSplitCount = 4;

        // [2026-07-08 삭제] _ammoOrbSpawnRadius — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"
        // 요청으로, 스폰 위치를 반지름만큼 미리 벌려두는 방식 자체를 없앴습니다(전부 한 점에서
        // 스폰). 겹침 방지는 이제 Physics.IgnoreCollision(IgnoreCollisionsWithinBurst)이 담당합니다.

        [Tooltip("각 조각이 옆으로 튀는 힘의 크기.")]
        [Min(0f)] [SerializeField] private float _ammoOrbOutwardForce = 3.5f;

        [Tooltip("각 조각이 위로 튀어오르는 힘의 크기.")]
        [Min(0f)] [SerializeField] private float _ammoOrbUpForce = 4f;

        [Tooltip("[2026-07-08 신규] '적 머리쯤에서 떨어져서 바닥과 충돌한뒤에 고정되도록 해줘' 요청으로 " +
                 "추가 — 스폰 기준 높이를 적 발밑(0m) 기준이 아니라 적 머리 정도 높이로 올려서, 위에서 " +
                 "떨어지는 낙하 궤적이 눈에 잘 보이도록 했습니다. 실제 적 콜라이더 높이를 재지 않고 " +
                 "평균적인 적 머리 높이를 고정값으로 씁니다.")]
        [Min(0.1f)] [SerializeField] private float _ammoOrbSpawnHeight = 2f;

        [Header("이동 처리")]
        [Tooltip("판정 전 감속 비율. 0.65 = 현재 속도의 65%.")]
        [Range(0f, 1f)]
        [SerializeField] private float _slowRatio = 0.65f;

        // [2026-07-06 삭제] _slowDuration(감속 지속 시간, 기존 0.1초 고정값) 필드를 제거했습니다.
        // "감속이 애니메이션 시전 시간 동안 유지되어야 한다"는 요청에 따라, 감속 지속 시간을
        // 더 이상 별도 값으로 관리하지 않고 위쪽 _shamanSwordSwingClipLength(Swing.anim 길이,
        // 1.17초)를 그대로 재사용하도록 JakduSequence()를 변경했습니다. 인스펙터에서 이 필드에
        // 저장돼 있던 값은 더 이상 아무 코드에서도 읽지 않습니다(씬/프리팹에는 죽은 값으로 남아있을
        // 수 있으나 무해합니다).

        [Tooltip("판정 후 반발 가속 비율. 1.3 = 현재 속도의 130%.")]
        [Min(1f)]
        [SerializeField] private float _reboundRatio = 1.3f;

        [Tooltip("반발 가속 지속 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _reboundDuration = 0.12f;
        private static readonly KRDamageType[] _allElements =
        {
            KRDamageType.Fire, KRDamageType.Water, KRDamageType.Wood,
            KRDamageType.Earth, KRDamageType.Metal
        };

       
        private bool _isActing;

        /// <summary>현재 진행 중인 "샤먼소드 숨기기 예약" 코루틴입니다. 작두를 연속으로 빠르게 쓸 때
        /// 이전 예약이 새 스윙을 중간에 꺼버리지 않도록, 새로 시작하기 전에 이전 것을 취소합니다.</summary>
        private Coroutine _hideShamanSwordCoroutine;

        /// <summary>이동 속도 배율. 플레이어 이동 스크립트에서 이 값을 곱해 속도를 조절합니다.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        public int CurrentResource => _currentResource;
        public int MaxResource => _maxResource;

        /// <summary>
        /// [2026-07-06 추가] 작두가 지금 자기 자신의 판정(ApplyHits)으로 대상에게 피해를 주고 있는 동안만
        /// true입니다. KREnemyBase.EnterDead() → RefillJakduResourceOnKill()이 "누가 죽였든 상관없이
        /// 처치 시 작두 자원 +1" 훅을 갖고 있는데, 작두 자신의 처치까지 여기 걸리면 방금 소모한 자원을
        /// 같은 프레임 안에서 스스로 되돌려받는 자기환급 버그가 생깁니다(자원 소모 체감이 안 되는 원인).
        /// KREnemyBase.RefillJakduResourceOnKill()이 이 플래그를 확인해서, 작두 자신의 처치는
        /// 자원 재충전 대상에서 제외합니다. (다른 무기/시스템으로 죽인 처치는 그대로 재충전됩니다.)
        /// </summary>
        public static bool IsSelfExecuting { get; private set; }

        /// <summary>
        /// [2026-07-06 추가] 작두 시퀀스(감속→판정→반발)가 지금 진행 중인지 여부입니다.
        /// 플레이어 이동 스크립트(KRPlayerMotor)가 이 값을 보고 작두 사용 중 이동 속도를
        /// 감속시키는 데 사용합니다.
        /// </summary>
        public bool IsActing => _isActing;

        private void Awake()
        {
            if (_jakduZone == null)
                _jakduZone = GetComponentInChildren<KRJakduZone>(includeInactive: true);
            if (_combatSystem == null)
                _combatSystem = GetComponentInParent<KRCombatSystem>();
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_shamanSwordAnimator == null && _shamanSwordVisualRoot != null)
                _shamanSwordAnimator = _shamanSwordVisualRoot.GetComponentInChildren<Animator>(includeInactive: true);

            _currentResource = _maxResource;

            // 시작 시 판정 존 비활성화
            if (_jakduZone != null)
                _jakduZone.gameObject.SetActive(false);

            // [2026-07-06 추가] 샤먼소드 손도 평소엔 꺼둡니다. 작두를 쓸 때만 잠깐 보여줍니다.
            if (_shamanSwordVisualRoot != null)
                _shamanSwordVisualRoot.SetActive(false);

            // [2026-07-06 추가] 게임 시작 시점의 초기 자원(가득 참)을 UI에 즉시 반영합니다.
            UpdateJakduUI();
        }

        private void Update()
        {
            if (_isActing) return;
            if (!Input.GetKeyDown(KeyCode.C)) return;

            if (_currentResource <= 0)
            {
                // TODO: HUD/사운드 피드백
                Debug.Log("[KRJakduSystem] 작두 자원 부족");
                return;
            }

            StartCoroutine(JakduSequence());
        }

        /// <summary>외부에서 작두 자원을 추가합니다. 스테이지 매니저 등에서 호출하세요.</summary>
        public void AddResource(int amount = 1)
        {
            _currentResource = Mathf.Min(_maxResource, _currentResource + amount);

            // [2026-07-06 추가] 자원이 늘어난 즉시(예: KREnemyBase.EnterDead()의 처치 보상) UI에 반영합니다.
            UpdateJakduUI();
        }

        /// <summary>
        /// 현재/최대 작두 자원을 _jakduChargeUI(연결돼 있을 때만)에 전달해 텍스트 UI를 갱신합니다.
        /// _jakduChargeUI가 비어 있으면(연결 안 함) 아무 것도 하지 않습니다 — UI 연결은 필수가 아닙니다.
        /// </summary>
        private void UpdateJakduUI()
        {
            if (_jakduChargeUI == null) return;
            _jakduChargeUI.SetJakduState(_currentResource, _maxResource);
        }

        // ── 작두 시퀀스 ────────────────────────────────────────────────

        private IEnumerator JakduSequence()
        {
            _isActing = true;

            // [2026-07-06 재변경] 기획 확인 결과 "적에게 적중하지 않아도(허공에 휘둘러도) 자원을
            // 소모하는 게 맞는 디자인"이라고 합니다. 한때 "맞췄을 때만 소모"하도록 바꿨던 걸
            // 되돌려서, 발동 즉시 무조건 1을 소모합니다(적중 여부와 무관).
            _currentResource--;
            UpdateJakduUI();

            // [2026-07-06 추가] 작두 애니메이션 트리거 — 평소 꺼져있던 샤먼소드 손을 보여주고
            // Swing 클립을 처음부터 재생합니다.
            PlayShamanSwordSwing();

            // ① 감속 — [2026-07-06 변경] 지속 시간을 고정 0.1초 대신 _shamanSwordSwingClipLength
            // (Swing.anim 실제 길이, 1.17초)로 바꿔서, 스윙 애니메이션이 재생되는 동안 계속
            // 감속 상태가 유지되도록 했습니다.
            SpeedMultiplier = _slowRatio;
            yield return new WaitForSeconds(_shamanSwordSwingClipLength);

            // ② 판정 — 존을 1프레임 활성화해 적을 수집
            // [2026-07-08 삭제] dropElement = GetLowestRatioElement() — "모든 총알이 나오게"
            // 요청으로 더 이상 속성 하나만 고르지 않고 ApplyHits()가 5속성 전부를 처리합니다.

            if (_jakduZone != null)
            {
                _jakduZone.gameObject.SetActive(true);

                // [2026-07-06 변경] 기존 "yield return null"은 렌더 프레임 1번만 대기했습니다.
                // 물리(Physics) 갱신은 FixedUpdate 고정 타임스텝으로 별도 동작하기 때문에,
                // 렌더 프레임 사이에 물리 스텝이 한 번도 안 도는 경우가 많아 트리거 콜백이
                // 전혀 발생하지 않고 _hits가 항상 비어 있는 문제가 있었습니다.
                // WaitForFixedUpdate()로 바꿔 최소 1번의 물리 스텝을 보장합니다.
                yield return new WaitForFixedUpdate();

                System.Collections.Generic.IReadOnlyCollection<IDamageable> hits = _jakduZone.GetHits();

                // 자원은 이미 위에서 소모했습니다(적중 여부와 무관). 여기서는 실제로 피해를 줄 수
                // 있는(살아있는) 대상이 하나라도 있을 때만 피해/드롭 처리를 진행합니다.
                if (HasValidTarget(hits))
                {
                    ApplyHits(hits);
                }

                _jakduZone.gameObject.SetActive(false);
            }

            // ③ 반발 가속
            SpeedMultiplier = _reboundRatio;
            yield return new WaitForSeconds(_reboundDuration);

            SpeedMultiplier = 1f;
            _isActing = false;
        }

        /// <summary>존이 수집한 대상 중 실제로 피해를 적용할 수 있는(null이 아니고 아직 살아있는) 대상이 있는지 확인합니다.</summary>
        private static bool HasValidTarget(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets)
        {
            foreach (IDamageable target in targets)
            {
                if (target != null && !target.IsDead) return true;
            }
            return false;
        }

        // ── 샤먼소드 애니메이션 ────────────────────────────────────────

        /// <summary>
        /// 평소 꺼져있던 샤먼소드 손을 켜고 Swing 애니메이션을 처음부터(0초 지점) 재생합니다.
        /// AnimatorController에 트리거/전환을 만드는 대신 Animator.Play()로 직접 재생을 강제합니다 —
        /// ShamanSword.controller에 Idle 상태가 없어서(현재 Swing 하나뿐) 트리거 기반 전환 구조를
        /// 만들 필요 자체가 없기 때문입니다(오브젝트를 껐다 켜는 것 자체가 "대기 상태"를 대신합니다).
        ///
        /// [2026-07-06 추가] 동시에 현재 장착 중인 원소 무기(Fire/Water/Wood 등)의 손 모델도 함께
        /// 숨깁니다. 안 그러면 샤먼소드 손과 원래 들고 있던 무기 손이 동시에 화면에 겹쳐 보입니다
        /// (흡혼 때 KRCombatSystem.SetCurrentWeaponVisualActive()로 무기를 숨긴 것과 같은 이유).
        /// </summary>
        private void PlayShamanSwordSwing()
        {
            if (_shamanSwordVisualRoot == null) return;

            // 연속으로 빠르게 작두를 쓸 경우, 이전 스윙에 걸려있던 "숨기기 예약"이 방금 시작한
            // 새 스윙을 중간에 꺼버리지 않도록 취소합니다.
            if (_hideShamanSwordCoroutine != null)
                StopCoroutine(_hideShamanSwordCoroutine);

            _combatSystem?.SetCurrentWeaponVisualActive(false);

            _shamanSwordVisualRoot.SetActive(true);
            _shamanSwordAnimator?.Play("Swing", layer: 0, normalizedTime: 0f);

            _hideShamanSwordCoroutine = StartCoroutine(HideShamanSwordAfterSwing());
        }

        /// <summary>
        /// _shamanSwordSwingClipLength(=Swing.anim 길이)만큼 기다린 뒤 샤먼소드 손을 다시 숨기고,
        /// 숨겨뒀던 원래 무기 손을 다시 보여줍니다. 감속/판정/반발로 이어지는 실제 게임플레이
        /// 타이밍(_isActing)과는 독립적으로 동작합니다 — 그래야 짧은 판정 타이밍에 맞춰 스윙
        /// 애니메이션이 잘리지 않고 끝까지 재생됩니다.
        /// </summary>
        private IEnumerator HideShamanSwordAfterSwing()
        {
            yield return new WaitForSeconds(_shamanSwordSwingClipLength);

            if (_shamanSwordVisualRoot != null)
                _shamanSwordVisualRoot.SetActive(false);

            _combatSystem?.SetCurrentWeaponVisualActive(true);

            _hideShamanSwordCoroutine = null;
        }

        // ── 피해 적용 ──────────────────────────────────────────────────

        // [2026-07-08 삭제] dropElement 매개변수 — "모든 총알이 나오게" 요청으로 이제 한 속성만
        // 골라 받는 대신 메서드 내부에서 _allElements 전체를 순회하므로 더 이상 필요 없습니다.
        private void ApplyHits(System.Collections.Generic.IReadOnlyCollection<IDamageable> targets)
        {
            // 기획 3-5: 적중 보상과 처치 보상은 대상마다 개별 지급하지 않고,
            // 이번 작두 발동에서 발생한 모든 대상의 보상을 전부 합산한 뒤 딱 한 번만 자원으로 환산합니다.
            float totalDropRatio = 0f;

            // [2026-07-08 신규] "적 위치에 소환되도록" 요청 반영 — 보상에 실제로 기여한 대상들의
            // 위치를 모아뒀다가, 나중에 그 평균 위치에서 탄약 조각을 흩뿌립니다(여러 마리를 한 번에
            // 처치해도 플레이어가 아니라 적들이 있던 자리 근처에서 나오게).
            Vector3 dropPositionSum = Vector3.zero;
            int dropPositionCount = 0;

            // [2026-07-06 추가] 이 블록 안에서 발생하는 처치는 전부 "작두 자신에 의한 처치"입니다.
            // IsSelfExecuting을 true로 켜두는 동안 KREnemyBase.TakeDamage() → EnterDead()가 동기적으로
            // 호출되며, RefillJakduResourceOnKill()이 이 플래그를 보고 작두 자원 재충전을 건너뜁니다
            // (작두로 죽여서 방금 쓴 자원을 스스로 되돌려받는 자기환급 버그 수정).
            IsSelfExecuting = true;
            try
            {
                foreach (IDamageable target in targets)
                {
                    if (target == null || target.IsDead) continue;

                    EnemyGrade grade = GetGrade(target);
                    float damage = _baseDamage * GetDamageMultiplier(grade);
                    Vector3 targetPosition = target.Position;
                    bool killed = ApplyDamage(target, damage);

                    // 기획 3-5: 적중 보상과 처치 보상은 중복 지급하지 않습니다(killed일 때만 처치 보상).
                    float dropRatio = CalculateDropRatio(grade, killed);
                    totalDropRatio += dropRatio;

                    if (dropRatio > 0f)
                    {
                        dropPositionSum += targetPosition;
                        dropPositionCount++;
                    }

                    // TODO(넉백/경직): 기획 3-4 — 튼튼한 잡졸(장거리 넉백)/갑사(중거리 넉백)/
                    // 장령(짧은 경직)/보스(넉백 없음) 반응은 아직 구현하지 않았습니다. 별도 작업 필요.
                }
            }
            finally
            {
                IsSelfExecuting = false;
            }

            // 기획 3-5: 1회 작두 최대 드랍 상한(70%) 적용
            totalDropRatio = Mathf.Min(totalDropRatio, _dropCap);

            if (totalDropRatio > 0f)
            {
                Vector3 dropPosition = dropPositionCount > 0
                    ? dropPositionSum / dropPositionCount
                    : transform.position + transform.forward;

                // [2026-07-08 신규] "슬롯 잠금의 총이 아닌 모든 총알이 나오게" 요청 반영 — 예전엔
                // GetLowestRatioElement()로 고른 속성 하나에만 보상을 전부 몰아줬는데, 이제 5속성
                // (화/수/목/토/금) 전부에 나눠서 흩뿌립니다. 총 보상 가치가 그냥 5배로 뻥튀기되지
                // 않도록, totalDropRatio를 속성 개수로 나눠서 똑같이 배분합니다 — 합쳐보면 예전에
                // 한 속성에 몰아주던 총량과 동일합니다.
                float perElementRatio = totalDropRatio / _allElements.Length;

                // [2026-07-08 수정 — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"]
                // 이전엔 겹침을 막으려고 반지름을 크게 벌려서(4m) 스폰 자체를 이미 퍼진 위치에서
                // 시작했는데, 그러면 "한 점에서 터져나가는" 느낌이 아니라 애초에 넓게 벌어진 채로
                // 나타나 보였습니다. 요청대로 전부 정확히 같은 지점(dropPosition)에서 스폰하고,
                // 물리 힘(AddForce)만으로 사방(지름 방향)으로 퍼져나가게 바꿨습니다. 같은 지점에
                // 겹쳐서 스폰하면 원래 힘이 밀어내기도 전에 서로 충돌해 그 자리에 얼어붙는 문제가
                // 있었는데(바람속성 버그와 같은 원인), 이번엔 같은 처형 1회에서 스폰된 조각들끼리는
                // Physics.IgnoreCollision으로 서로 충돌 판정 자체를 꺼서(바닥/적과는 그대로 충돌)
                // 겹쳐서 스폰돼도 얼어붙지 않고 힘만으로 자유롭게 퍼지도록 했습니다.
                var burstColliders = new System.Collections.Generic.List<Collider>();
                float burstBaseAngle = Random.Range(0f, 360f);
                for (int i = 0; i < _allElements.Length; i++)
                {
                    SpawnAmmoOrb(_allElements[i], perElementRatio, dropPosition, i, burstBaseAngle, burstColliders);
                }
                IgnoreCollisionsWithinBurst(burstColliders);
            }
        }

        /// <summary>
        /// [2026-07-08 신규 — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"] 같은 처형 1회에서
        /// 스폰된 탄약 조각들은 전부 같은 지점(spawnPos)에서 겹쳐서 시작합니다. 이대로 두면 물리
        /// 힘(AddForce)이 밀어내기도 전에 서로 충돌해 그 자리에 얼어붙으므로(바람속성 버그와 동일
        /// 원인), 이 목록에 모인 조각들끼리는 서로 충돌 판정을 완전히 꺼서(Physics.IgnoreCollision)
        /// 겹쳐서 스폰돼도 부딪히지 않고 순수하게 힘으로만 퍼지도록 합니다. 바닥/적/플레이어 등
        /// 다른 오브젝트와의 충돌은 그대로 유지됩니다(같은 목록 안의 콜라이더끼리만 꺼짐).
        /// </summary>
        private static void IgnoreCollisionsWithinBurst(System.Collections.Generic.List<Collider> colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] == null) continue;

                for (int j = i + 1; j < colliders.Count; j++)
                {
                    if (colliders[j] == null) continue;
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        private bool ApplyDamage(IDamageable target, float damage)
        {
            bool wasDead = target.IsDead;
            var ctx = new KRDamageContext(
                damage, KRDamageType.Metal,
                target.Position,
                (target.Position - transform.position).normalized);
            target.TakeDamage(ctx);
            return !wasDead && target.IsDead;
        }

        // ── 드롭 처리 ──────────────────────────────────────────────────

        // [2026-07-08 삭제] GetLowestRatioElement() — "모든 총알이 나오게" 요청으로 더 이상
        // 속성 하나만 골라 쓰지 않아서(ApplyHits()가 _allElements 전체를 순회) 안 쓰는 메서드가
        // 됐습니다.

        private float CalculateDropRatio(EnemyGrade grade, bool killed)
        {
            return grade switch
            {
                EnemyGrade.Fodder => killed ? _dropFodderKill : 0f,
                EnemyGrade.Heavy => killed ? _dropFodderKill : 0f,
                EnemyGrade.Elite => killed ? _dropEliteKill : _dropEliteHit,
                EnemyGrade.Boss => killed ? _dropBossKill : _dropBossHit,
                _ => 0f,
            };
        }

        /// <summary>
        /// 기획 3-5/4-4 반영: 계산된 비율만큼의 자원 중, 현재 자원 지갑에 회수 가능한 만큼은
        /// 즉시 흡수(RefillResource)합니다.
        /// [2026-07-08 수정 — "둠 이터널 전기톱처럼 여러 파츠로" 요청 반영] 기획서(3-5/4-4)는
        /// 초과분을 "대표 오브젝트 하나"로만 드랍하라고 되어 있었지만, 명시적 요청으로 이 부분만
        /// 기획서와 다르게 갑니다 — 초과분을 오브 하나가 아니라 _ammoOrbSplitCount개로 쪼개서
        /// 사방으로 물리적으로 흩뿌립니다. 각 조각의 회복량은 remaining/개수라서, 몇 개를 줍든
        /// 합계는 항상 remaining과 같습니다(자원 총량 자체는 기획서 수치 그대로 유지).
        /// </summary>
        /// <param name="origin">
        /// [2026-07-08 신규] "적 위치에 소환되도록" 요청 반영 — 예전엔 항상 플레이어 앞에서 났는데,
        /// 이제 ApplyHits()가 계산해서 넘겨준 "실제 보상에 기여한 적들의 평균 위치"를 기준으로
        /// 흩뿌립니다.
        /// </param>
        /// <param name="elementIndex">
        /// [2026-07-08 신규 — "바람속성만 중력/리기드바디가 있는거같아서"] 5속성 중 이 속성의 순번
        /// (0~4)입니다. 발사 "방향"만 균등하게 나누는 데 씁니다(아래 burstBaseAngle 참고). 스폰
        /// 위치 자체는 [2026-07-08 수정 — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"] 요청으로
        /// 더 이상 이 값의 영향을 받지 않고 전부 같은 지점(origin)에서 스폰됩니다.
        /// </param>
        /// <param name="burstBaseAngle">
        /// [2026-07-08 신규] ApplyHits()에서 이번 드랍 1회에 한해 딱 한 번 뽑은 공통 기준 각도입니다.
        /// 5속성이 전부 이 값을 공유해서 발사 방향이 골고루 퍼지도록 합니다(속성마다 따로 랜덤을
        /// 뽑으면 방향이 한쪽으로 몰릴 수 있음). 겹침 방지는 더 이상 방향/거리가 아니라
        /// Physics.IgnoreCollision(아래 burstColliders)이 담당합니다.
        /// </param>
        /// <param name="burstColliders">
        /// [2026-07-08 신규 — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"] 이번 처형 1회에서
        /// 스폰되는 모든 조각(5속성×_ammoOrbSplitCount개)의 Collider를 여기 모아둡니다. 전부 같은
        /// 지점에서 스폰되므로 물리적으로 겹친 채로 시작하는데, ApplyHits()가 이 목록을 이용해
        /// 서로 간의 충돌 판정을 꺼서(Physics.IgnoreCollision) 겹쳐도 얼어붙지 않게 합니다.
        /// </param>
        private void SpawnAmmoOrb(
            KRDamageType element, float ratio, Vector3 origin, int elementIndex, float burstBaseAngle,
            System.Collections.Generic.List<Collider> burstColliders)
        {
            if (_combatSystem == null) return;

            // [2026-07-08 신규] '탄약오브 등록에 비활성화도 할수있게해줘' — 비활성화된 속성은
            // 즉시 흡수/오브 드롭 전부 건너뜁니다(자원 자체를 아예 처리하지 않음).
            int elementIdx = (int)element;
            if (_ammoOrbEnabled != null && elementIdx >= 0 && elementIdx < _ammoOrbEnabled.Length
                && !_ammoOrbEnabled[elementIdx])
            {
                return;
            }

            float maxAmount = _combatSystem.GetMaxResourceAmount(element);
            float dropAmount = maxAmount * ratio;
            if (dropAmount <= 0f) return;

            float current = _combatSystem.GetResourceAmount(element);
            float canReceive = Mathf.Max(0f, maxAmount - current);

            // 기획 4-4: "회수 가능 자원은 플레이어에게 빠르게 흡착" — 즉시 지갑에 채웁니다.
            float instantAmount = Mathf.Min(dropAmount, canReceive);
            if (instantAmount > 0f)
                _combatSystem.RefillResource(element, instantAmount);

            // [2026-07-08 수정] "초과 자원은 바닥에 잔여 자원으로 유지"는 그대로 두되, 오브 하나가
            // 아니라 여러 조각으로 나눠서 흩뿌립니다.
            float remaining = dropAmount - instantAmount;
            if (remaining <= 0f) return;

            int idx = elementIdx;
            if (_ammoOrbPrefabs == null || idx < 0 || idx >= _ammoOrbPrefabs.Length) return;
            if (_ammoOrbPrefabs[idx] == null) return;

            // [2026-07-08 수정 — "잘 안 퍼지는데?" 버그 수정]
            // 오브 프리팹의 SphereCollider 반지름이 0.5m인데, 예전엔 0.5m 높이에서 스폰했습니다 —
            // 즉 구의 바닥면이 스폰 순간 이미 땅에 닿아 있었던 겁니다. KRDropItem은
            // OnCollisionEnter가 한 번이라도 뜨면 그 즉시 Rigidbody를 Kinematic으로 고정해버리는데
            // (착지 판정), 땅에 닿은 채로 스폰되면 AddForce로 준 힘이 실제로 밀어내기도 전에 첫
            // 물리 스텝에서 바로 충돌이 잡혀서 그 자리에 고정돼버립니다 — 그래서 힘을 줘도 안
            // 퍼지고 그 자리에 멈춰 있었던 겁니다. 땅에서 확실히 띄운 채로 시작하게 해, 떨어지는
            // 동안 옆으로 밀리는 궤적이 실제로 보이도록 했습니다.
            // [2026-07-08 수정 — "적 위치에 소환되도록"] 기준점을 플레이어(transform.position)가
            // 아니라 인자로 받은 origin(적들의 평균 위치)으로 바꿨습니다.
            // [2026-07-08 수정 — "적 머리쯤에서 떨어져서 바닥과 충돌한뒤에 고정되도록 해줘"]
            // 고정 1.5m 대신 _ammoOrbSpawnHeight(기본 2m, 대략 적 머리 높이)를 써서 낙하가 더
            // 위에서부터 시작해 눈에 잘 보이도록 했습니다. 착지 후 고정되는 동작 자체는
            // OnCollisionEnter(KRDropItem)에서 기존대로 처리됩니다.
            Vector3 spawnPos = origin + Vector3.up * _ammoOrbSpawnHeight;
            float perPieceAmount = remaining / _ammoOrbSplitCount;

            // [2026-07-08 수정 — "그냥 나오는 방향 4개로 설정해서 퍼트리면 안됨?" /
            // "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"]
            // 같은 속성의 조각들은 여전히 균등 각도(angleStep)로 4방향(발사 방향만) 분산됩니다.
            // 시작 각도는 속성마다 독립적으로 랜덤하게 뽑지 않고, ApplyHits()가 딱 한 번 뽑아
            // 5속성이 공유하는 burstBaseAngle에 속성 순번(elementIndex)만큼 균등한 오프셋을
            // 더해서, 20개 조각의 발사 방향이 골고루 퍼지도록 합니다(겹침 방지 목적은 아님 —
            // 이제 겹침은 Physics.IgnoreCollision이 막습니다).
            float angleStep = 360f / _ammoOrbSplitCount;
            float groupAngleOffset = angleStep / _allElements.Length * elementIndex;
            float startAngle = burstBaseAngle + groupAngleOffset;

            // [2026-07-08 수정 — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"]
            // 예전엔 겹침을 막으려고 반지름만큼(_ammoOrbSpawnRadius) 위치를 미리 벌려서 스폰했는데,
            // 그러면 "한 점에서 터져나가는" 게 아니라 이미 퍼진 채로 나타나 보였습니다. 요청대로
            // 위치 오프셋을 없애고 전부 정확히 spawnPos(한 점)에서 스폰합니다. 방향(direction)은
            // AddForce에 그대로 쓰여서, 스폰 직후 물리 힘만으로 사방(지름 방향)으로 퍼져나갑니다.
            for (int i = 0; i < _ammoOrbSplitCount; i++)
            {
                float angleDeg = startAngle + angleStep * i;
                Vector3 direction = new Vector3(
                    Mathf.Cos(angleDeg * Mathf.Deg2Rad), 0f, Mathf.Sin(angleDeg * Mathf.Deg2Rad));

                SpawnAmmoOrbPiece(_ammoOrbPrefabs[idx], spawnPos, perPieceAmount, direction, burstColliders);
            }
        }

        /// <summary>
        /// [2026-07-08 신규] 탄약 조각 하나를 생성하고, 지정된 방향으로 물리 힘을 줘서 튀어나가게
        /// 합니다.
        /// [2026-07-08 수정 — "중점에서 지름으로 퍼지는 방식으로 하고 싶은데"] 이제 여러 조각이
        /// 전부 같은 spawnPos에서 생성되므로, 이 조각의 Collider를 burstColliders에 등록해 나중에
        /// ApplyHits()가 같은 처형에서 나온 조각끼리 충돌 판정을 끄도록(Physics.IgnoreCollision)
        /// 합니다.
        /// </summary>
        private void SpawnAmmoOrbPiece(
            GameObject prefab, Vector3 spawnPos, float amount, Vector3 direction,
            System.Collections.Generic.List<Collider> burstColliders)
        {
            GameObject orbInstance = Instantiate(prefab, spawnPos, Quaternion.identity);

            var dropItem = orbInstance.GetComponent<KillRitual.Items.KRDropItem>();
            if (dropItem != null)
            {
                dropItem.ConfigureAmount(amount);
            }
            else
            {
                Debug.LogWarning(
                    $"[KRJakduSystem] {prefab.name} 프리팹에 KRDropItem 컴포넌트가 없습니다. " +
                    "회수 가능한 오브가 아닙니다.");
            }

            if (orbInstance.TryGetComponent(out Collider pieceCollider))
            {
                burstColliders.Add(pieceCollider);
            }

            if (orbInstance.TryGetComponent(out Rigidbody rb))
            {
                float forceVariance = Random.Range(0.85f, 1.15f);
                Vector3 force = (direction * _ammoOrbOutwardForce + Vector3.up * _ammoOrbUpForce) * forceVariance;
                rb.AddForce(force, ForceMode.Impulse);
            }
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────

        private static EnemyGrade GetGrade(IDamageable target)
            => target is KillRitual.Enemies.KREnemyBase enemy ? enemy.Grade : EnemyGrade.Fodder;

        private float GetDamageMultiplier(EnemyGrade grade) => grade switch
        {
            EnemyGrade.Fodder => _multiplierFodder,
            EnemyGrade.Heavy => _multiplierHeavy,
            EnemyGrade.Elite => _multiplierElite,
            EnemyGrade.Boss => _multiplierBoss,
            _ => 1f,
        };
    }
}
