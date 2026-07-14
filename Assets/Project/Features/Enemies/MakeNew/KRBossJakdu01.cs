// Assets/Project/Features/Enemies/MakeNew/KRBossJakdu01.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    public sealed class KRBossJakdu01 : KREnemyBase
    {
        private enum BossPhase { Phase1, Phase2 }

        [Header("페이즈 전환")]
        [Tooltip("이 체력 비율 이하로 내려가면 2페이즈(강화)로 전환합니다.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float _phase2HealthRatio = 0.5f;

        [Tooltip("[2026-07-08 신규] '그로기 처형시 죽는거 말고 한 500딜정도' 요청 반영 — 보스는 " +
                 "그로기(다운) 상태에서 처형당해도 즉사하지 않고, 대신 이 값만큼 고정 피해를 " +
                 "입습니다. 이 피해도 다른 피해와 똑같은 경로(TakeDamageDirect)를 거치므로, " +
                 "1페이즈 중이면 ClampFinalDamage()의 2페이즈 문턱 보정도 그대로 적용됩니다.")]
        [Min(0f)][SerializeField] private float _executeDamage = 500f;

        [Tooltip("[2026-07-08 신규] '포효모션이 무조건 우선' 요청 반영 — 2페이즈 전환 포효를 " +
                 "독점 재생하는 동안 다른 패턴이 못 끼어들게 붙잡아 두는 시간(초)입니다. " +
                 "[2026-07-08 수정 — '애니메이션이 캔슬되는거 같아서' 버그 수정] 이 클립의 실제 " +
                 "프레임레이트를 FBX에서 직접 확인해보니 30fps가 아니라 25fps(PAL)였습니다 — 그동안 " +
                 "30fps로 잘못 가정해서 실제 재생시간을 20% 짧게 계산하고 있었습니다. 200프레임 " +
                 "/25fps = 8초(1배속), ExitTime 0.9 기준 실제 종료는 약 7.2초라서, 6.1초였던 이 " +
                 "값으로는 애니메이션이 끝나기 전에 _isPatternActive가 풀려서 다음 패턴이 끼어들어 " +
                 "포효가 중간에 캔슬됐습니다. 7.5초로 늘려서 실제 종료 시점보다 확실히 뒤로 " +
                 "맞췄습니다.")]
        [Min(0.1f)][SerializeField] private float _roarDuration = 7.5f;

        [Header("보스 UI - 체력 / 페이즈")]
        [Tooltip("보스 전체 체력 스크롤바입니다. Scrollbar의 size를 HP 비율로 사용합니다. 방향은 UI 오브젝트의 Direction 설정을 따릅니다.")]
        [SerializeField] private Scrollbar _bossHealthScrollbar;

        [Tooltip("위쪽에 미리 배치해둔 페이즈 조각/표식 오브젝트입니다. 순서대로 하나씩 사라집니다. 총 2페이즈면 2개를 넣으면 됩니다.")]
        [SerializeField] private GameObject[] _phaseBreakObjects;

        [Tooltip("true면 페이즈 조각을 SetActive(false)로 숨깁니다. false면 Destroy()합니다. UI는 보통 true가 안전합니다.")]
        [SerializeField] private bool _deactivatePhaseBreakObject = true;

        [Tooltip("시작 시 페이즈 조각을 전부 다시 켭니다. 보스 프리팹이 재사용되거나 테스트 중 비활성화 상태가 남는 것을 막습니다.")]
        [SerializeField] private bool _initializePhaseBreakObjectsOnAwake = true;

        [Header("보스 UI - 표시 타이밍")]
        [Tooltip("보스 HP바 전체 루트입니다. 가능하면 보스 이름/HP/페이즈 조각을 감싼 최상위 패널을 넣으세요. 비워두면 스크롤바와 페이즈 조각을 개별로 숨깁니다.")]
        [SerializeField] private GameObject _bossUiRoot;

        [Tooltip("켜두면 플레이어를 감지하기 전까지 보스 UI를 숨깁니다. UpdateChase/UpdateAttack이 처음 호출되는 순간 표시됩니다.")]
        [SerializeField] private bool _hideBossUiUntilPlayerDetected = true;

        private int _consumedPhaseBreakCount;
        private bool _deathUiConsumed;
        private bool _bossUiRevealed;

        [Header("몸통 방어 (부위 판정이 아닌 애매한 곳)")]
        [Tooltip("[2026-07-07 이름 변경] 예전엔 '철갑 방어'였지만, 이제 몸통 자체도 부위(_body)로 " +
                 "따로 관리되므로 이건 그 어떤 부위 콜라이더에도 안 걸린 애매한 지점(보스 루트의 " +
                 "커다란 캡슐 콜라이더)에 맞았을 때만 쓰이는 보정치입니다. 부위를 정확히 노리도록 " +
                 "유도하기 위해 낮게 유지합니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _fallbackDamageRatio = 0.15f;

        [Tooltip("애매한 곳에 맞았을 때도 '맞긴 맞았다'는 걸 보여줄 VFX. 비워두면 자동으로 흰색 " +
                 "구체 오브젝트로 대체됩니다(준비물 없이 바로 동작).")]
        [SerializeField] private GameObject _armorBlockVfxPrefab;

        [Header("부위 (KRBossBodyPart) — [2026-07-07 재설계] 어깨/코/등 → 머리/몸통/앞다리/뒷다리/꼬리")]
        [SerializeField] private KRBossBodyPart _head;
        [SerializeField] private KRBossBodyPart _body;
        [SerializeField] private KRBossBodyPart _frontLegs;
        [SerializeField] private KRBossBodyPart _backLegs;
        [SerializeField] private KRBossBodyPart _tail;

        [Header("부위 파괴 - 다리 (2026-07-07 신규)")]
        [Tooltip("앞다리/뒷다리 중 하나가 파괴될 때마다 이동속도에 곱해지는 배율(누적 곱). " +
                 "예: 0.65면 다리 하나 파괴 시 65%, 둘 다 파괴되면 65%×65%≈42%로 느려집니다.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _legBreakSpeedMultiplier = 0.65f;

        [Header("이동 / 패턴 진행")]
        [Tooltip("초당 회전 각도(도). 유한하게 두면 거대한 몸집답게 천천히 돌게 되고, 플레이어가 " +
                 "실제로 등/옆으로 돌아가서 때릴 수 있게 됩니다.")]
        [Min(10f)]
        [SerializeField] private float _turnSpeedDegreesPerSecond = 120f;

        [Tooltip("플레이어와 이 거리보다 멀면 접근하고, 가까우면 패턴을 고릅니다.")]
        [Min(1f)]
        [SerializeField] private float _preferredDistance = 9f;

        [Tooltip("[2026-07-07 신규] '너무 멀면 전력 질주' — 플레이어가 (기준거리×이 배율)보다 멀리 " +
                 "떨어지면 평소 추격 속도보다 더 빠르게 달려서 거리를 좁힙니다. 예: 기준거리 9, " +
                 "이 값 2면 18m 넘게 벌어졌을 때만 전력 질주합니다.")]
        [Min(1f)]
        [SerializeField] private float _sprintDistanceMultiplier = 2f;

        [Tooltip("전력 질주 시 이동속도에 곱해지는 배율(다리 파괴 감속과 별개로 곱해집니다).")]
        [Range(1f, 3f)]
        [SerializeField] private float _sprintSpeedMultiplier = 1.6f;

        [Tooltip("[2026-07-08 신규] '근거리 살살 접근' — 기준거리(_preferredDistance) 바로 바깥쪽, " +
                 "이 폭(m)만큼의 구간에서는 뛰지 않고 걸어서 다가옵니다. 예: 기준거리 9, 이 값 3이면 " +
                 "9~12m 구간에서만 Walk를 씁니다. 그보다 멀면 평소처럼 Run(또는 전력 질주)입니다.")]
        [Min(0f)]
        [SerializeField] private float _walkZoneWidth = 3f;

        [Tooltip("걷기(Walk) 구간에서 이동속도에 곱해지는 배율.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _walkSpeedMultiplier = 0.5f;

        [Tooltip("패턴 하나가 끝난 뒤 다음 패턴까지의 기본 대기 시간(초, 1페이즈 기준).")]
        [Min(0.1f)]
        [SerializeField] private float _patternCooldown = 2.5f;

        [Tooltip("2페이즈에서 패턴 쿨다운에 곱해지는 배율. 1보다 작으면 더 자주 공격합니다.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _phase2CooldownMultiplier = 0.6f;

        [Header("패턴1 - 철갑 발사")]
        [Tooltip("발사할 철갑 조각 프리팹(KRBossArmorShard 부착).")]
        [SerializeField] private KRBossArmorShard _armorShardPrefab;
        [SerializeField] private Transform _shoulderLMuzzle;
        [SerializeField] private Transform _shoulderRMuzzle;
        [Min(1)][SerializeField] private int _shardsPerShoulder = 3;
        [Min(0.1f)][SerializeField] private float _shardSpeed = 20f;
        [Min(0f)][SerializeField] private float _shardDamage = 15f;
        [SerializeField] private LayerMask _shardHitLayerMask = ~0;
        [SerializeField] private LayerMask _shardDamageableLayerMask = ~0;
        [Tooltip("[2026-07-08 신규] '모션이랑 투사체 발사랑 싱크 안 맞아' 문제 수정 — Attack " +
                 "트리거를 건 시점부터 실제로 철갑을 던지는(발사하는) 순간까지의 지연 시간입니다. " +
                 "[2026-07-08 최종 수정] '모션과 동시에' 요청에 따라 0으로 맞췄습니다 — 트리거를 " +
                 "건 바로 그 프레임(모션 시작과 동시)에 곧바로 발사됩니다.")]
        [Min(0f)][SerializeField] private float _shardLaunchDelay = 0f;
        [Tooltip("[2026-07-08 신규] '걷기 모션이 다시 빠졌다' 버그 수정 — 철갑을 던진 뒤 코루틴이 " +
                 "끝날 때까지 추가로 기다리는 시간입니다. " +
                 "[2026-07-08 수정 — '애니메이션이 캔슬되는거 같아서' 버그 재수정] 클립 실제 " +
                 "프레임레이트가 30fps가 아니라 25fps(PAL, FBX에서 직접 확인)였습니다 — 그동안 " +
                 "재생시간을 20% 짧게 계산해서, 공격1(2배속) 실제 종료(약 3.6초)보다 다음 패턴이 " +
                 "먼저 잡혀서 애니메이션이 끝까지 재생되지 못하고 캔슬됐습니다. 1.3초로 늘려서 " +
                 "(텔레그래프 0.35초 + 발사 0초 + 이 값 + 쿨다운 2.5초 = 약 4.15초) 실제 종료 " +
                 "시점보다 확실히 뒤로 맞췄습니다.")]
        [Min(0f)][SerializeField] private float _shardRecoveryDelay = 1.3f;
        [Tooltip("2페이즈: 바닥에 꽂힌 철갑이 터지기까지의 지연 시간(초).")]
        [Min(0.1f)][SerializeField] private float _shardExplodeDelay = 1.5f;
        [Min(0.1f)][SerializeField] private float _shardExplosionRadius = 2.5f;

        [Header("패턴2 - 물기")]
        [Tooltip("[2026-07-08 변경] 컨셉을 다시 '물기'로 확정했습니다(꼬리 휘두르기 → 물기). " +
                 "판정 기준점도 '물기'에 맞게 머리(_head) 콜라이더 위치로 바꿨습니다(꼬리 기준이면 " +
                 "'무는' 공격과 안 맞아서). 필드 이름(_trunk*)은 예전 그대로 남아있습니다.\n" +
                 "[2026-07-07 각도 제한 삭제] 처음엔 몸 뒤쪽(-transform.forward)만 맞도록 각도까지 " +
                 "제한했는데, 이러면 보통 정면에서 쫓아오다 이 패턴에 걸린 플레이어는 범위 안에 " +
                 "있어도 거의 항상 안 맞는 버그가 됐습니다. 지금은 각도 제한 없이 꼬리 위치 기준 " +
                 "원형 범위로 단순화되어 있습니다.\n" +
                 "[2026-07-08 변경] '원거리공격은 10m 이상, 물기는 10m 미만' 요청 반영 — 이 값이 " +
                 "이제 물기의 실제 타격 사거리일 뿐 아니라, 원거리(철갑발사)/물기/돌진 패턴 선택을 " +
                 "가르는 근접·원거리 경계값 역할도 겸합니다(IsPatternViableAtDistance() 참고). " +
                 "기본값을 6→10으로 올린 이유도 이것 하나입니다.")]
        [Min(0.05f)][SerializeField] private float _trunkWindup = 0.6f;
        [Min(0.5f)][SerializeField] private float _trunkStrikeRange = 10f;
        [Min(0f)][SerializeField] private float _trunkDamage = 25f;
        [Tooltip("연속 타격 사이의 간격(초, 2페이즈 3연타용).")]
        [Min(0.05f)][SerializeField] private float _trunkComboInterval = 0.35f;

        [Header("패턴3 - 돌진")]
        [Min(0.1f)][SerializeField] private float _chargeWindup = 1f;
        [Min(1f)][SerializeField] private float _chargeSpeed = 22f;
        [Tooltip("[2026-07-08 수정] '돌진거리 두배까지 이동하게 해줘' 요청으로 20m → 40m로 늘렸습니다.")]
        [Min(1f)][SerializeField] private float _chargeMaxDistance = 40f;
        [Min(0f)][SerializeField] private float _chargeDamage = 30f;
        [Tooltip("벽 감지용 레이어 — 플레이어/적 레이어는 반드시 제외하세요. 지형/벽 레이어만 포함.")]
        [SerializeField] private LayerMask _chargeWallLayerMask = ~0;
        [Tooltip("돌진 전용 피해 판정 콜라이더(KRBossChargeHitbox). 돌진 중에만 켜져서 " +
                 "정확한 Trigger 판정을 합니다.")]
        [SerializeField] private KRBossChargeHitbox _chargeHitbox;
        [Min(0.1f)][SerializeField] private float _wallStunDuration = 1.5f;
        [Tooltip("[2026-07-07 신규] 돌진 중 벽에 부딪혔을 때 그 충격으로 앞다리(_frontLegs) 자신에게 " +
                 "들어가는 자해 피해. 무리한 돌진을 반복하면 스스로 다리가 부러질 수 있게 하는 " +
                 "리스크/리워드 장치입니다 — '돌진도 부위 파괴와 연동'해 달라는 요청 반영.")]
        [Min(0f)][SerializeField] private float _chargeSelfDamageOnWallHit = 35f;
        [Tooltip("[2026-07-07 신규] 돌진(및 벽 충돌 시 경직/2연속 돌진까지) 끝난 뒤, 플레이어 쪽으로 " +
                 "다시 몸을 돌리는 데 걸리는 시간(초). 패턴 진행 중엔 FacePlayer가 멈춰 있으므로, " +
                 "돌진 직후 남은 각도와 상관없이 항상 이 시간만큼 걸려서 천천히 재조준하도록 " +
                 "코루틴으로 별도 처리합니다 — '뒤도는데 한 2초는 걸리면 좋겠다'는 요청 반영.")]
        [Min(0.1f)][SerializeField] private float _chargeTurnBackDuration = 2f;

        [Header("신규 패턴(2페이즈 전용) - 철갑 폭우")]
        [Min(1)][SerializeField] private int _armorRainCount = 10;
        [Min(1f)][SerializeField] private float _armorRainRadius = 8f;
        [Min(0f)][SerializeField] private float _armorRainDamage = 10f;
        [Min(0.1f)][SerializeField] private float _armorRainFallSpeed = 12f;
        [Min(0.1f)][SerializeField] private float _armorRainDuration = 1.8f;

        [Header("시각 신호")]
        [Tooltip("모든 패턴 예고(윈드업) 구간 동안 표시할 경고색.")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.5f, 0f, 1f);

        [Header("공격 범위 시각화 (2026-07-08 신규)")]
        [Tooltip("'공격 범위를 시각적으로 보여달라'는 요청 반영 — 물기/철갑 폭우는 바닥에 원, " +
                 "돌진은 바닥에 직선으로 실제 판정 범위를 예고~실행 구간 동안 표시합니다. " +
                 "별도 머티리얼/프리팹 준비 없이 런타임에 LineRenderer를 자동 생성해서 씁니다.")]
        [SerializeField] private bool _showAttackRangeIndicator = true;
        [SerializeField] private Color _rangeIndicatorColor = new Color(1f, 0.15f, 0.1f, 0.9f);
        [Min(3)][SerializeField] private int _rangeCircleSegments = 48;
        [Min(0.01f)][SerializeField] private float _rangeIndicatorLineWidth = 0.18f;
        [Tooltip("범위 표시선이 바닥에 파묻혀 안 보이지 않도록 띄우는 높이(Z-fighting 방지).")]
        [Min(0f)][SerializeField] private float _rangeIndicatorYOffset = 0.05f;

        [Header("애니메이션")]
        [Tooltip("모델에 붙일 Animator. 비워두면 자식에서 자동으로 찾습니다. " +
                 "[2026-07-07 신규] 새 모델(Four Legged Predator.fbx)에 실제로 들어있는 클립 7종 " +
                 "(Idle/walk/Run/attack/Powerfull_attack/Roar/Sleeping) 중 Sleeping을 제외한 " +
                 "6종을 아래 파라미터로 씁니다. KRBossMastodon.controller에 각 상태를 만들고 " +
                 "이 클립들을 Motion으로 드래그해서 연결하세요.")]
        [SerializeField] private Animator _visualAnimator;
        private static readonly int kSpeedParam = Animator.StringToHash("Speed");
        private static readonly int kAttackTrigger = Animator.StringToHash("Attack");
        private static readonly int kPowerfulAttackTrigger = Animator.StringToHash("PowerfulAttack");
        private static readonly int kRoarTrigger = Animator.StringToHash("Roar");
        private static readonly int kRunTrigger = Animator.StringToHash("Run");

        [Header("공격 모션 (프로시저럴)")]
        [Tooltip("실제 스켈레탈 공격 애니메이션 클립 대신, 몸통 전체를 스케일/위치로 움찔거리게 " +
                 "만들어 '준비 동작 → 타격 순간'의 느낌을 코드로 흉내냅니다(스쿼시-스트레치). " +
                 "회전(Rotation)은 안 건드립니다 — FacePlayer()와 충돌하기 때문입니다.")]
        [SerializeField] private bool _enableProceduralAttackMotion = true;
        private Vector3 _bodyBaseScale = Vector3.one;

        private BossPhase _phase = BossPhase.Phase1;
        private bool _isPatternActive;
        private float _patternActiveSince;
        private int _lastPatternIndex = -1;

        private int _patternRepeatCount;

        private float _nextPatternTime;
        private bool _lastChargeHitWall;
        private int _brokenLegCount;
        private float _legSpeedMultiplier = 1f;

        private LineRenderer _circleIndicator;
        private LineRenderer _chargeLineIndicator;

        [Tooltip("'패턴진행중=True'가 이 시간(초)보다 오래 지속되면 강제로 초기화합니다. " +
                 "플레이 모드 중 스크립트 수정으로 코루틴이 죽는 경우의 안전장치입니다.")]
        [Min(3f)]
        [SerializeField] private float _patternStuckTimeoutSeconds = 12f;

        protected override void Awake()
        {
            base.Awake();

            if (_visualAnimator == null)
                _visualAnimator = GetComponentInChildren<Animator>();

            _bodyBaseScale = transform.localScale;

            if (_visualAnimator != null)
                _visualAnimator.applyRootMotion = false;

            if (_frontLegs != null) _frontLegs.OnBroken += HandleLegBroken;
            if (_backLegs != null) _backLegs.OnBroken += HandleLegBroken;

            _groggyHealthRatio = -1f;

            InitializeBossHealthUI();
        }

        // ── 보스 UI - 체력 / 페이즈 ─────────────────────────────────────

        private void InitializeBossHealthUI()
        {
            ConfigureHealthScrollbar(_bossHealthScrollbar);

            _consumedPhaseBreakCount = 0;
            _deathUiConsumed = false;
            _bossUiRevealed = !_hideBossUiUntilPlayerDetected;

            if (_initializePhaseBreakObjectsOnAwake && _phaseBreakObjects != null)
            {
                for (int i = 0; i < _phaseBreakObjects.Length; i++)
                {
                    if (_phaseBreakObjects[i] != null)
                        _phaseBreakObjects[i].SetActive(true);
                }
            }

            SetBossUiVisible(_bossUiRevealed);
            UpdateBossHealthUI();
        }

        private static void ConfigureHealthScrollbar(Scrollbar scrollbar)
        {
            if (scrollbar == null) return;

            // HP바 용도에서는 value가 아니라 size가 실제 표시량입니다.
            // value는 핸들의 위치를 움직이므로 0으로 고정해 좌/우 기준만 Direction 설정에 맡깁니다.
            scrollbar.numberOfSteps = 0;
            scrollbar.value = 0f;
        }

        private void UpdateBossHealthUI()
        {
            float totalRatio = _maxHealth > 0f
                ? Mathf.Clamp01(_health / _maxHealth)
                : 0f;

            SetScrollbarAmount(_bossHealthScrollbar, totalRatio);
            RefreshPhaseBreakObjectVisibility();
        }

        private void RevealBossUiIfNeeded()
        {
            if (_bossUiRevealed) return;

            _bossUiRevealed = true;
            SetBossUiVisible(true);
            UpdateBossHealthUI();
        }

        private void SetBossUiVisible(bool visible)
        {
            if (_bossUiRoot != null)
            {
                if (_bossUiRoot.activeSelf != visible)
                    _bossUiRoot.SetActive(visible);
            }
            else
            {
                if (_bossHealthScrollbar != null && _bossHealthScrollbar.gameObject.activeSelf != visible)
                    _bossHealthScrollbar.gameObject.SetActive(visible);
            }

            RefreshPhaseBreakObjectVisibility();
        }

        private void RefreshPhaseBreakObjectVisibility()
        {
            if (_phaseBreakObjects == null) return;

            for (int i = 0; i < _phaseBreakObjects.Length; i++)
            {
                GameObject phaseObject = _phaseBreakObjects[i];
                if (phaseObject == null) continue;

                bool shouldBeVisible = _bossUiRevealed && i >= _consumedPhaseBreakCount;
                if (phaseObject.activeSelf != shouldBeVisible)
                    phaseObject.SetActive(shouldBeVisible);
            }
        }

        private static void SetScrollbarAmount(Scrollbar scrollbar, float value)
        {
            if (scrollbar == null) return;

            float clamped = Mathf.Clamp01(value);
            scrollbar.size = clamped;
            scrollbar.value = 0f;
        }

        private void ConsumeNextPhaseBreakObject()
        {
            if (_phaseBreakObjects == null || _phaseBreakObjects.Length == 0) return;

            while (_consumedPhaseBreakCount < _phaseBreakObjects.Length &&
                   _phaseBreakObjects[_consumedPhaseBreakCount] == null)
            {
                _consumedPhaseBreakCount++;
            }

            if (_consumedPhaseBreakCount >= _phaseBreakObjects.Length) return;

            GameObject target = _phaseBreakObjects[_consumedPhaseBreakCount];
            _consumedPhaseBreakCount++;

            if (_deactivatePhaseBreakObject)
                target.SetActive(false);
            else
                Destroy(target);

            RefreshPhaseBreakObjectVisibility();
        }

        private void HandleLegBroken()
        {
            _brokenLegCount++;
            _legSpeedMultiplier = Mathf.Pow(_legBreakSpeedMultiplier, _brokenLegCount);

            Debug.Log($"[불가살이] {name}: 다리 파괴! (누적 {_brokenLegCount}개) " +
                      $"이동속도 {_legSpeedMultiplier:P0}로 감소, 돌진 패턴 봉인");
        }

        private LineRenderer CreateIndicatorLineRenderer(string objectName, bool loop)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = _rangeIndicatorLineWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Tile;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = _rangeIndicatorColor;
            lr.endColor = _rangeIndicatorColor;
            lr.enabled = false;
            return lr;
        }

        private void EnsureIndicators()
        {
            if (_circleIndicator == null)
                _circleIndicator = CreateIndicatorLineRenderer("[AttackRangeCircle]", loop: true);
            if (_chargeLineIndicator == null)
                _chargeLineIndicator = CreateIndicatorLineRenderer("[ChargePathArea]", loop: true);
        }

        private void ShowCircleIndicator(Vector3 center, float radius)
        {
            if (!_showAttackRangeIndicator) return;
            EnsureIndicators();

            int segments = Mathf.Max(3, _rangeCircleSegments);
            center.y += _rangeIndicatorYOffset;

            _circleIndicator.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _circleIndicator.SetPosition(i, point);
            }
            _circleIndicator.enabled = true;
        }

        private void HideCircleIndicator()
        {
            if (_circleIndicator != null) _circleIndicator.enabled = false;
        }

        private void ShowChargeLineIndicator(Vector3 origin, Vector3 direction, float length, float width)
        {
            if (!_showAttackRangeIndicator) return;
            EnsureIndicators();

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            direction = direction.normalized;

            origin.y += _rangeIndicatorYOffset;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float halfWidth = Mathf.Max(0.05f, width) * 0.5f;

            Vector3 nearLeft = origin + right * halfWidth;
            Vector3 nearRight = origin - right * halfWidth;
            Vector3 farLeft = nearLeft + direction * length;
            Vector3 farRight = nearRight + direction * length;

            _chargeLineIndicator.positionCount = 4;
            _chargeLineIndicator.SetPosition(0, nearLeft);
            _chargeLineIndicator.SetPosition(1, farLeft);
            _chargeLineIndicator.SetPosition(2, farRight);
            _chargeLineIndicator.SetPosition(3, nearRight);
            _chargeLineIndicator.enabled = true;
        }

        private void HideChargeLineIndicator()
        {
            if (_chargeLineIndicator != null) _chargeLineIndicator.enabled = false;
        }

        // ── KREnemyBase 추상 메서드 구현 ─────────────────────────────────

        protected override void UpdateChase()
        {
            RevealBossUiIfNeeded();

            if (!_isPatternActive) FacePlayer(_turnSpeedDegreesPerSecond);
            TickBossLogic();
        }

        protected override void UpdateAttack()
        {
            RevealBossUiIfNeeded();

            if (!_isPatternActive) FacePlayer(_turnSpeedDegreesPerSecond);
            TickBossLogic();
        }

        private float _nextMoveDebugLogTime;

        private void TickBossLogic()
        {

            if (_isPatternActive)
            {
                if (Time.time - _patternActiveSince > _patternStuckTimeoutSeconds)
                {
                    Debug.LogWarning($"[불가살이] {name}: 패턴이 {_patternStuckTimeoutSeconds}초 넘게 " +
                                      "끝나지 않아 강제로 복구합니다 (코루틴이 죽었던 것으로 추정).");
                    StopAllCoroutines();
                    _isPatternActive = false;
                    OverrideColor = null;
                    HideCircleIndicator();
                    HideChargeLineIndicator();
                }
                else
                {
                    return;
                }
            }

            if (_player == null) return;

            float distance = DistanceToPlayer();

            if (distance > _preferredDistance)
            {
                if (Time.time >= _nextPatternTime)
                {
                    StartCoroutine(RunRandomPattern(forceRangedOnly: true));
                    return;
                }

                MoveTowards(_player.position);

                bool isSprinting = distance > _preferredDistance * _sprintDistanceMultiplier;
                bool isWalkZone = !isSprinting && distance <= _preferredDistance + _walkZoneWidth;

                _visualAnimator?.SetFloat(kSpeedParam, 1f);
                if (_agent != null)
                {
                    float speedMultiplier = isWalkZone ? _walkSpeedMultiplier : (isSprinting ? _sprintSpeedMultiplier : 1f);
                    _agent.speed = _moveSpeed * _legSpeedMultiplier * speedMultiplier;
                }

                return;
            }

            StopMoving();
            _visualAnimator?.SetFloat(kSpeedParam, 0f);

            if (Time.time < _nextPatternTime) return;

            StartCoroutine(RunRandomPattern());
        }

        // ── 페이즈 전환 ──────────────────────────────────────────────────

        protected override float ClampFinalDamage(float amount)
        {
            if (_phase != BossPhase.Phase1) return amount;

            float floor = _maxHealth * _phase2HealthRatio;
            if (_health - amount >= floor) return amount;

            float clamped = Mathf.Max(0f, _health - floor);
            Debug.Log($"[불가살이] {name}: 2페이즈 진입 문턱에서 초과피해 차단 " +
                      $"(요청 {amount:F1} → 실제 적용 {clamped:F1}, 체력 {_health:F0} → {floor:F0} 고정)");
            return clamped;
        }

        protected override void PerformExecution(
            KillRitual.Core.Interfaces.ExecutionSource source)
        {
            Debug.Log($"[불가살이] {name}: 그로기 처형 — 즉사 대신 고정피해 {_executeDamage} 적용");
            var context = new KRDamageContext(
                _executeDamage, KRDamageType.Metal, transform.position, Vector3.zero);
            TakeDamageDirect(context);
        }

        private bool _health60Logged;

        protected override void OnHealthChanged(float ratio)
        {

            if (_phase == BossPhase.Phase1 && ratio <= _phase2HealthRatio)
            {
                _phase = BossPhase.Phase2;
                ConsumeNextPhaseBreakObject();
                UpdateBossHealthUI();

                Debug.Log($"[불가살이] {name}: 2페이즈 진입 (체력 {ratio:P0}) — 공격 속도 증가, " +
                          "철갑 발사 폭발/코 채찍 3연타/돌진 연속/철갑 폭우 해금");

                StopAllCoroutines();
                OverrideColor = null;
                HideCircleIndicator();
                HideChargeLineIndicator();
                StartCoroutine(PhaseTransitionRoar());
                return;
            }

            UpdateBossHealthUI();
        }

        private IEnumerator PhaseTransitionRoar()
        {
            _isPatternActive = true;
            _patternActiveSince = Time.time;

            PlayActionTrigger(kRoarTrigger);
            yield return new WaitForSeconds(_roarDuration);

            _isPatternActive = false;
            _nextPatternTime = Time.time + _patternCooldown * _phase2CooldownMultiplier;
        }

        protected override void OnDeath()
        {
            if (!_deathUiConsumed)
            {
                _deathUiConsumed = true;
                UpdateBossHealthUI();
                ConsumeNextPhaseBreakObject();
            }

            base.OnDeath();
        }

        protected override float ModifyIncomingDamage(KRDamageContext context)
        {
            if (_armorBlockVfxPrefab != null)
            {
                GameObject vfx = Instantiate(_armorBlockVfxPrefab, context.HitPoint, Quaternion.identity);
                Destroy(vfx, 2f);
            }
            else
            {
                SpawnProceduralArmorFlash(context.HitPoint);
            }

            return context.DamageAmount * _fallbackDamageRatio;
        }

        private static readonly int kFlashColorId = Shader.PropertyToID("_Color");
        private static readonly int kFlashBaseColorId = Shader.PropertyToID("_BaseColor");

        private void SpawnProceduralArmorFlash(Vector3 point)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "ArmorBlockFlash(임시 오브젝트)";

            Collider flashCollider = flash.GetComponent<Collider>();
            if (flashCollider != null) Destroy(flashCollider);

            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * 0.35f;

            Renderer flashRenderer = flash.GetComponent<Renderer>();
            if (flashRenderer != null)
            {
                Material instanceMat = flashRenderer.material;
                if (instanceMat.HasProperty(kFlashColorId)) instanceMat.SetColor(kFlashColorId, Color.white);
                if (instanceMat.HasProperty(kFlashBaseColorId)) instanceMat.SetColor(kFlashBaseColorId, Color.white);
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    instanceMat.EnableKeyword("_EMISSION");
                    instanceMat.SetColor("_EmissionColor", Color.white * 3f);
                }
            }

            Destroy(flash, 0.15f);
        }

        // ── 패턴 선택/진행 ────────────────────────────────────────────────

        private IEnumerator RunRandomPattern(bool forceRangedOnly = false)
        {
            _isPatternActive = true;
            _patternActiveSince = Time.time;
            StopMoving();

            int index;
            if (forceRangedOnly)
            {
                index = 0;
                RegisterPatternChoice(index);
            }
            else
            {
                float distance = DistanceToPlayerFromHead();
                index = PickPatternIndex(distance);
            }

            yield return StartCoroutine(GetPatternCoroutine(index));

            float cooldown = _patternCooldown * (_phase == BossPhase.Phase2 ? _phase2CooldownMultiplier : 1f);
            _nextPatternTime = Time.time + cooldown;
            _isPatternActive = false;
        }

        private void RegisterPatternChoice(int index)
        {
            _patternRepeatCount = (index == _lastPatternIndex) ? _patternRepeatCount + 1 : 1;
            _lastPatternIndex = index;
        }

        private int PickPatternIndex(float distance)
        {
            int count = _phase == BossPhase.Phase2 ? 4 : 3;

            bool excludeLastForRepeat = _patternRepeatCount >= 2;

            var candidates = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (excludeLastForRepeat && i == _lastPatternIndex) continue;
                if (IsPatternViableAtDistance(i, distance)) candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < count; i++)
                    if (IsPatternViableAtDistance(i, distance)) candidates.Add(i);
            }

            // 그래도 후보가 없으면(돌진 다리 파괴 + 애매한 거리 등 극단적 상황) 물기(1)를 기본값으로
            // 씁니다 — 사거리 밖이면 TryHitTrunkStrike()가 알아서 헛스윙 처리하므로, 근접거리에서
            // 불공정한 대미지를 주는 철갑 발사(0)보다 훨씬 안전한 기본값입니다.
            if (candidates.Count == 0) candidates.Add(1);

            int index = PickByRouletteWheel(candidates);

            string candidateNames = string.Join(", ", candidates.ConvertAll(i => PatternName(i)));
            Debug.Log($"[불가살이/패턴선택] 거리={distance:F1}m (근접/원거리 경계 {_trunkStrikeRange}m) " +
                      $"후보=[{candidateNames}] → 선택={PatternName(index)} " +
                      $"(직전패턴={PatternName(_lastPatternIndex)}, 연속횟수={_patternRepeatCount})");

            RegisterPatternChoice(index);
            return index;
        }

        private int PickByRouletteWheel(List<int> candidates)
        {
            const float kRecentlyUsedWeight = 0.3f;
            const float kDefaultWeight = 1f;

            float totalWeight = 0f;
            var weights = new float[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = candidates[i] == _lastPatternIndex ? kRecentlyUsedWeight : kDefaultWeight;
                totalWeight += weights[i];
            }

            float roll = Random.Range(0f, totalWeight);
            float cursor = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cursor += weights[i];
                if (roll <= cursor) return candidates[i];
            }

            // 부동소수점 오차로 roll이 마지막 누적값을 살짝 넘는 극히 드문 경우의 안전망입니다.
            return candidates[candidates.Count - 1];
        }

        private static string PatternName(int index)
        {
            switch (index)
            {
                case 0: return "철갑발사";
                case 1: return "물기";
                case 2: return "돌진";
                case 3: return "철갑폭우";
                default: return $"?{index}";
            }
        }

        private bool IsPatternViableAtDistance(int index, float distance)
        {
            switch (index)
            {
                case 0: return distance >= _trunkStrikeRange;
                case 1: return distance < _trunkStrikeRange;
                case 2:
                    bool legsBroken = (_frontLegs != null && _frontLegs.IsBroken) ||
                                       (_backLegs != null && _backLegs.IsBroken);
                    return !legsBroken;
                case 3: return distance <= _armorRainRadius;
                default: return true;
            }
        }

        private IEnumerator GetPatternCoroutine(int index)
        {
            switch (index)
            {
                case 0: return Pattern_ArmorLaunch();
                case 1: return Pattern_TrunkWhip();
                case 2: return Pattern_Charge();
                case 3: return Pattern_ArmorRainstorm();
                default: return Pattern_ArmorLaunch();
            }
        }

        // ── 프로시저럴 공격 모션 (스쿼시-스트레치) ──────────────────────────

        private IEnumerator ScalePunch(Vector3 targetScaleMultiplier, float toDuration, float backDuration)
        {
            Vector3 punched = Vector3.Scale(_bodyBaseScale, targetScaleMultiplier);

            float t = 0f;
            while (t < toDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(_bodyBaseScale, punched, t / toDuration);
                yield return null;
            }

            t = 0f;
            while (t < backDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(punched, _bodyBaseScale, t / backDuration);
                yield return null;
            }

            transform.localScale = _bodyBaseScale;
        }

        private IEnumerator HopBounce(float height, float upDuration, float downDuration)
        {
            Vector3 basePos = transform.position;
            Vector3 peak = basePos + Vector3.up * height;

            float t = 0f;
            while (t < upDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(basePos, peak, t / upDuration);
                yield return null;
            }

            t = 0f;
            while (t < downDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(peak, basePos, t / downDuration);
                yield return null;
            }

            transform.position = basePos;
        }

        private Coroutine _activeScalePunchRoutine;

        private void PlayScalePunch(Vector3 targetScaleMultiplier, float toDuration, float backDuration)
        {
            if (!_enableProceduralAttackMotion) return;
            if (_activeScalePunchRoutine != null) StopCoroutine(_activeScalePunchRoutine);
            _activeScalePunchRoutine = StartCoroutine(ScalePunch(targetScaleMultiplier, toDuration, backDuration));
        }

        private void PlayHopBounce(float height, float upDuration, float downDuration)
        {
            if (!_enableProceduralAttackMotion) return;
            StartCoroutine(HopBounce(height, upDuration, downDuration));
        }

        private void PlayActionTrigger(int triggerHash)
        {
            if (_visualAnimator == null) return;

            if (triggerHash != kAttackTrigger) _visualAnimator.ResetTrigger(kAttackTrigger);
            if (triggerHash != kPowerfulAttackTrigger) _visualAnimator.ResetTrigger(kPowerfulAttackTrigger);
            if (triggerHash != kRoarTrigger) _visualAnimator.ResetTrigger(kRoarTrigger);
            if (triggerHash != kRunTrigger) _visualAnimator.ResetTrigger(kRunTrigger);

            _visualAnimator.SetTrigger(triggerHash);
        }

        // ── 패턴 1: 철갑 발사 ────────────────────────────────────────────

        private IEnumerator Pattern_ArmorLaunch()
        {
            Debug.Log($"[불가살이] {name}: 패턴1 - 철갑 발사");

            OverrideColor = _telegraphColor;
            PlayScalePunch(new Vector3(1.08f, 0.9f, 1.08f), 0.35f, 0.05f);
            yield return new WaitForSeconds(0.35f);
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.92f, 1.12f, 0.92f), 0.08f, 0.2f);
            PlayActionTrigger(kAttackTrigger);

            yield return new WaitForSeconds(_shardLaunchDelay);

            FireShardsFromMuzzle(_shoulderLMuzzle);
            FireShardsFromMuzzle(_shoulderRMuzzle);

            yield return new WaitForSeconds(_shardRecoveryDelay);
        }

        private void FireShardsFromMuzzle(Transform muzzle)
        {
            if (muzzle == null || _armorShardPrefab == null || _player == null) return;

            for (int i = 0; i < _shardsPerShoulder; i++)
            {
                Vector3 dir = (_player.position - muzzle.position).normalized;

                if (_shardsPerShoulder > 1)
                {
                    float spread = Mathf.Lerp(-12f, 12f, i / (float)(_shardsPerShoulder - 1));
                    dir = Quaternion.AngleAxis(spread, Vector3.up) * dir;
                }

                GameObject instance = Instantiate(_armorShardPrefab.gameObject, muzzle.position, Quaternion.identity);
                KRBossArmorShard shard = instance.GetComponent<KRBossArmorShard>();
                shard?.Launch(dir * _shardSpeed, _shardDamage, _shardHitLayerMask, _shardDamageableLayerMask, this,
                    willExplode: _phase == BossPhase.Phase2,
                    explodeDelay: _shardExplodeDelay,
                    explosionRadius: _shardExplosionRadius);
            }
        }

        private IEnumerator Pattern_TrunkWhip()
        {
            int swings = _phase == BossPhase.Phase2 ? 3 : 1;
            Debug.Log($"[불가살이] {name}: 패턴2 - 물기 ({swings}연타)");

            for (int i = 0; i < swings; i++)
            {
                Vector3 originPos = _head != null ? _head.Position : transform.position;
                ShowCircleIndicator(originPos, _trunkStrikeRange);

                OverrideColor = _telegraphColor;
                PlayScalePunch(new Vector3(1.05f, 0.95f, 0.9f), _trunkWindup * 0.8f, _trunkWindup * 0.2f);
                yield return new WaitForSeconds(_trunkWindup);
                OverrideColor = null;
                PlayScalePunch(new Vector3(0.95f, 1.02f, 1.15f), 0.06f, 0.15f);

                if (i == 0) PlayActionTrigger(kPowerfulAttackTrigger);

                TryHitTrunkStrike();
                Debug.Log($"[불가살이] {name}: 물기 {i + 1}/{swings}타 적중 판정");
                HideCircleIndicator();

                if (i < swings - 1)
                    yield return new WaitForSeconds(_trunkComboInterval);
            }

        }

        private float DistanceToPlayerFromHead()
        {
            if (_player == null) return float.MaxValue;

            Vector3 originPos = _head != null ? _head.Position : transform.position;
            Vector3 toPlayer = _player.position - originPos;
            toPlayer.y = 0f;
            return toPlayer.magnitude;
        }

        private void TryHitTrunkStrike()
        {
            if (_player == null) return;

            float distance = DistanceToPlayerFromHead();
            if (distance > _trunkStrikeRange)
            {
                Debug.Log($"[불가살이] 물기 판정 - 빗나감 (거리 {distance:F2}m > 사거리 {_trunkStrikeRange}m)");
                return;
            }

            IDamageable target = FindPlayerDamageable(_player);
            if (target == null || target.IsDead) return;

            Vector3 originPos = _head != null ? _head.Position : transform.position;
            Vector3 toPlayer = _player.position - originPos;
            toPlayer.y = 0f;
            Vector3 hitDirection = distance > 0.0001f ? toPlayer.normalized : transform.forward;
            var context = new KRDamageContext(_trunkDamage, KRDamageType.Fire, _player.position, hitDirection);
            Debug.Log($"[불가살이] 물기 판정 - 명중 (원점 {originPos}, 거리 {distance:F2}m, 사거리 {_trunkStrikeRange}m)");
            target.TakeDamage(context);
        }

        // ── 패턴 3: 돌진 ────────────────────────────────────────────────

        private IEnumerator Pattern_Charge()
        {
            Debug.Log($"[불가살이] {name}: 패턴3 - 돌진 준비 ({_chargeWindup}초 차징)");

            Vector3 direction = transform.forward;
            if (_player != null)
            {
                Vector3 toPlayer = _player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f) direction = toPlayer.normalized;
            }

            ShowChargeLineIndicator(transform.position, direction, _chargeMaxDistance,
                _chargeHitbox != null ? _chargeHitbox.GetWidth() : 3f);

            OverrideColor = _telegraphColor;
            PlayScalePunch(new Vector3(1.1f, 0.8f, 0.95f), _chargeWindup * 0.85f, _chargeWindup * 0.15f);
            yield return StartCoroutine(RotateTowardsDirectionOverTime(direction, _chargeWindup));
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.9f, 1.05f, 1.2f), 0.1f, 0.25f);

            PlayActionTrigger(kRunTrigger);

            Debug.Log($"[불가살이] {name}: 돌진 개시 (윈드업 시작 시점에 고정한 방향)");
            yield return StartCoroutine(DoChargeDash(direction));
            HideChargeLineIndicator();

            if (_lastChargeHitWall)
            {
                Debug.Log($"[불가살이] {name}: 벽 충돌");

                if (_phase == BossPhase.Phase2)
                {
                    yield return new WaitForSeconds(0.15f);
                    Debug.Log($"[불가살이] {name}: 2페이즈 - 반대 방향 추가 돌진");
                    ShowChargeLineIndicator(transform.position, -direction, _chargeMaxDistance,
                        _chargeHitbox != null ? _chargeHitbox.GetWidth() : 3f);
                    PlayActionTrigger(kRunTrigger);
                    yield return StartCoroutine(DoChargeDash(-direction));
                    HideChargeLineIndicator();
                }
                else
                {
                    Debug.Log($"[불가살이] {name}: 경직 {_wallStunDuration}초");
                    yield return new WaitForSeconds(_wallStunDuration);
                }
            }

            Debug.Log($"[불가살이] {name}: 돌진 후 재조준 시작 ({_chargeTurnBackDuration}초)");
            yield return StartCoroutine(TurnBackTowardsPlayer(_chargeTurnBackDuration));
        }

        private IEnumerator TurnBackTowardsPlayer(float duration)
        {
            if (_player == null) yield break;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;

            yield return StartCoroutine(RotateTowardsDirectionOverTime(toPlayer, duration));
        }

        private IEnumerator RotateTowardsDirectionOverTime(Vector3 direction, float duration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || duration <= 0f) yield break;

            Quaternion startRot = transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t / duration);
                yield return null;
            }

            transform.rotation = targetRot;
        }

        private IEnumerator DoChargeDash(Vector3 direction)
        {
            _lastChargeHitWall = false;

            bool agentWasEnabled = _agent != null && _agent.enabled;
            if (agentWasEnabled) _agent.isStopped = true;

            _chargeHitbox?.Activate(_chargeDamage);

            float traveled = 0f;
            var hits = new RaycastHit[4];

            while (traveled < _chargeMaxDistance)
            {
                float step = _chargeSpeed * Time.deltaTime;

                int hitCount = Physics.RaycastNonAlloc(
                    transform.position + Vector3.up, direction, hits, step + 0.5f, _chargeWallLayerMask);

                bool blocked = false;
                for (int i = 0; i < hitCount; i++)
                {
                    Collider hitCollider = hits[i].collider;
                    if (hitCollider != null && hitCollider.transform.root == transform.root) continue;
                    blocked = true;
                    break;
                }

                if (blocked)
                {
                    _lastChargeHitWall = true;
                    break;
                }

                transform.position += direction * step;
                traveled += step;

                yield return null;
            }

            _chargeHitbox?.Deactivate();
            if (agentWasEnabled) _agent.isStopped = false;

            if (_lastChargeHitWall && _frontLegs != null && !_frontLegs.IsBroken && _chargeSelfDamageOnWallHit > 0f)
            {
                Debug.Log($"[불가살이] {name}: 벽에 부딪힌 충격으로 앞다리에 자해 피해 " +
                          $"{_chargeSelfDamageOnWallHit}");
                var selfContext = new KRDamageContext(
                    _chargeSelfDamageOnWallHit, KRDamageType.Metal, transform.position, Vector3.zero);
                _frontLegs.TakeDamage(selfContext);
            }
        }

        // ── 신규 패턴(2페이즈): 철갑 폭우 ────────────────────────────────

        private IEnumerator Pattern_ArmorRainstorm()
        {
            Debug.Log($"[불가살이] {name}: 신규 패턴 - 철갑 폭우");

            ShowCircleIndicator(transform.position, _armorRainRadius);

            OverrideColor = _telegraphColor;
            PlayHopBounce(0.8f, 0.35f, 0.15f);
            PlayScalePunch(new Vector3(1.15f, 1.1f, 1.15f), 0.35f, 0.15f);
            PlayActionTrigger(kPowerfulAttackTrigger);
            yield return new WaitForSeconds(0.5f);
            OverrideColor = null;

            Debug.Log($"[불가살이] {name}: 철갑 {_armorRainCount}개를 주변에 낙하시킵니다");
            for (int i = 0; i < _armorRainCount; i++)
                SpawnArmorRainDrop();

            yield return new WaitForSeconds(_armorRainDuration);
            HideCircleIndicator();

        }

        private void SpawnArmorRainDrop()
        {
            if (_armorShardPrefab == null) return;

            Vector2 randomCircle = Random.insideUnitCircle * _armorRainRadius;
            Vector3 targetPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Vector3 spawnPos = targetPos + Vector3.up * 15f;

            GameObject instance = Instantiate(_armorShardPrefab.gameObject, spawnPos, Quaternion.identity);
            KRBossArmorShard shard = instance.GetComponent<KRBossArmorShard>();

            shard?.Launch(Vector3.down * _armorRainFallSpeed, _armorRainDamage,
                _shardHitLayerMask, _shardDamageableLayerMask, this, willExplode: false);
        }
    }
}
