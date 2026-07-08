// Assets/Project/Features/Enemies/MakeNew/KRBossJakdu01.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 1스테이지 보스 컨트롤러입니다. 클래스/파일 이름은 예전 "작두 보스" 설계 때 이름을
    /// 그대로 쓰고 있지만(씬/프리팹이 이미 이 스크립트를 참조하고 있어 이름을 바꾸면 GUID 연결이
    /// 꼬일 위험이 있어 유지했습니다), 내용은 두 번 전면 재설계됐습니다.
    ///
    /// [2026-07-07 두 번째 전면 재작성 — "부위타격" 중심 컨셉으로 교체]
    /// 첫 번째 재작성("불가살이": 평소 거의 무적, 특정 패턴 직후에만 부위가 잠깐 노출)을 버리고,
    /// 몬스터헌터류의 "부위별 체력 + 파괴(break)" 시스템으로 교체했습니다. 계기는 새로 받은
    /// 모델(Four Legged Predator.fbx)이 머리/몸통/다리 텍스처가 이미 부위별로 나뉘어 있어서,
    /// 그 구분을 그대로 게임플레이에 살리는 게 자연스럽다는 판단입니다.
    ///
    /// [무엇이 바뀌었나]
    /// - 부위(KRBossBodyPart)는 이제 항상 맞을 수 있습니다. "패턴이 끝난 뒤에만 노출" 같은
    ///   시간 제한이 전부 사라졌습니다 — 패턴들은 이제 순수하게 "예고 → 공격" 위협일 뿐이고,
    ///   부위를 노리는 건 전투 내내 가능합니다.
    /// - 부위마다 자기 체력이 있고(KRBossBodyPart._partHealth), 0이 되면 "파괴"되며 실제
    ///   전투에 영향을 주는 행동 변화가 걸립니다(이동속도 감소/돌진 봉인 — 아래 참고).
    ///   [2026-07-08 삭제] 다리 파괴 시 강제 그로기(다운)는 뺐습니다 — 이제 이동속도 감소/돌진
    ///   봉인만 걸리고, 그로기는 예전처럼 체력이 낮아졌을 때만 자연스럽게 걸립니다.
    /// - 부위 구성도 재설계했습니다: 어깨(Shoulder_L/R)·코(Trunk)·등(Back) → 머리/몸통/앞다리/
    ///   뒷다리/꼬리 5부위로 변경(모델의 실제 텍스처 구분과 일치).
    /// - 돌진 패턴이 부위 파괴와 직접 연동됩니다: 앞다리나 뒷다리 중 하나라도 파괴되면 돌진을
    ///   아예 못 쓰고(다리 없이 못 뛴다는 논리), 돌진 중 벽에 부딪히면 그 충격으로 앞다리 자신에게
    ///   피해가 들어갑니다(무리하게 자주 돌진하면 스스로 다리를 부러뜨리게 되는 리스크/리워드).
    /// </summary>
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

        // [2026-07-08 삭제] _legBreakGroggyDuration — "다리 파괴시 그로기되는거 빼줘" 요청으로
        // 다리 파괴 시 강제 그로기 자체를 없애면서 같이 삭제했습니다.

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
        // [2026-07-08 삭제] _shardMinRange(원거리 최소 사거리) — 안 쓰는 필드였습니다.
        // '원거리 10m 이상/물기 10m 미만' 경계를 _trunkStrikeRange 하나로 통일하면서 대체됨.
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
        // [2026-07-08 삭제] _trunkStrikeHalfAngle(각도 제한용) — 각도 제한 자체를 없애면서 안 쓰는 필드였습니다.
        [Min(0f)][SerializeField] private float _trunkDamage = 25f;
        [Tooltip("연속 타격 사이의 간격(초, 2페이즈 3연타용).")]
        [Min(0.05f)][SerializeField] private float _trunkComboInterval = 0.35f;

        [Header("패턴3 - 돌진")]
        [Min(0.1f)][SerializeField] private float _chargeWindup = 1f;
        [Min(1f)][SerializeField] private float _chargeSpeed = 22f;
        [Tooltip("[2026-07-08 수정] '돌진거리 두배까지 이동하게 해줘' 요청으로 20m → 40m로 늘렸습니다.")]
        [Min(1f)][SerializeField] private float _chargeMaxDistance = 40f;
        [Min(0f)][SerializeField] private float _chargeDamage = 30f;
        // [2026-07-08 삭제] _chargeHitRadius(예전 원형 판정 반경) — _chargeHitbox(Trigger 콜라이더)가
        // 판정을 전담하게 되면서 안 쓰는 필드였습니다.
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

        // [2026-07-08 신규] "같은 공격 패턴 3연속 금지" 요청 반영 — 직전 패턴이 연속으로 몇 번째
        // 나왔는지 셉니다. 2번 연속까지는 허용하고, 이 값이 2 이상일 때(=이미 2연속)만 그 패턴을
        // 후보에서 제외해서 3번째 연속 사용을 막습니다.
        private int _patternRepeatCount;

        private float _nextPatternTime;
        private bool _lastChargeHitWall;
        private int _brokenLegCount;
        private float _legSpeedMultiplier = 1f;

        // [2026-07-08 신규] 공격 범위 시각화용 LineRenderer — 씬/프리팹에 미리 안 만들어둬도
        // 최초 필요 시점에 코드로 자동 생성됩니다(EnsureIndicators() 참고).
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

            // [2026-07-08 신규] 이동/회전이 전부 코드로 제어됩니다 — MoveTowards()(NavMeshAgent),
            // DoChargeDash()의 수동 transform.position 이동, TurnBackTowardsPlayer()의 수동
            // transform.rotation Slerp, FacePlayer()의 수동 회전까지 전부 스크립트가 직접 계산합니다.
            // 여기에 애니메이션 클립 자체의 루트모션(특히 attack/Powerfull_attack처럼 제자리에서
            // 안 멈추는 클립)까지 더해지면, 실제 위치가 코드가 계산한 위치와 어긋나서 꼬리 휘두르기
            // 사거리 판정이나 돌진 거리 계산이 눈에 보이는 것과 안 맞게 됩니다.
            // 그래서 에디터의 Apply Root Motion 체크박스는 그대로 켜둬도 되지만(사용자가 명시적으로
            // 켠 설정이라 되돌리지 않습니다), 런타임에서는 여기서 강제로 꺼서 위치/회전 제어권을
            // 코드가 전담하도록 합니다. 클립 재생 자체(다리 움직임 등 제자리 애니메이션)에는 영향
            // 없고, "클립에 저장된 이동/회전량을 실제로 반영할지"만 꺼집니다.
            if (_visualAnimator != null)
                _visualAnimator.applyRootMotion = false;

            // [2026-07-07 신규] 부위 파괴 이벤트 구독 — 다리(앞/뒤) 파괴 시 이동속도 감소를
            // 적용합니다(2026-07-08: 강제 다운은 삭제). 돌진 가능 여부는
            // IsPatternViableAtDistance()에서 _frontLegs.IsBroken / _backLegs.IsBroken을 직접
            // 확인합니다(별도 이벤트 불필요).
            if (_frontLegs != null) _frontLegs.OnBroken += HandleLegBroken;
            if (_backLegs != null) _backLegs.OnBroken += HandleLegBroken;

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

        /// <summary>
        /// [2026-07-07 신규] 다리(앞다리 또는 뒷다리) 파괴 시 공통으로 호출됩니다.
        /// 이동속도를 곱셈으로 줄입니다(양쪽 다 부러지면 더 느려짐).
        /// [2026-07-08 삭제] "다리 파괴시 그로기되는거 빼줘" 요청으로 강제 그로기(ForceGroggy)
        /// 호출을 없앴습니다 — 이제 다리가 부러져도 그로기(다운)되지 않고, 그로기는 체력이
        /// _groggyHealthRatio 이하로 내려갔을 때(KREnemyBase 쪽)만 자연스럽게 걸립니다.
        /// </summary>
        private void HandleLegBroken()
        {
            _brokenLegCount++;
            // [2026-07-07 변경] 여기서 바로 _agent.speed를 정하지 않습니다 — 전력 질주 배율과
            // 곱셈이 겹쳐야 하므로, 실제 속도 계산은 TickBossLogic()이 매 틱마다 담당하고
            // 여기서는 "다리 파괴로 인한 배율"만 갱신해 둡니다.
            _legSpeedMultiplier = Mathf.Pow(_legBreakSpeedMultiplier, _brokenLegCount);

            Debug.Log($"[불가살이] {name}: 다리 파괴! (누적 {_brokenLegCount}개) " +
                      $"이동속도 {_legSpeedMultiplier:P0}로 감소, 돌진 패턴 봉인");
        }

        // ── 공격 범위 시각화 (2026-07-08 신규) ──────────────────────────────
        // "공격 범위 시각적으로 보여줘" 요청 반영. 원형 판정(물기/철갑 폭우)은 바닥에 원으로,
        // 직선 판정(돌진)은 바닥에 선으로 표시합니다. 씬에 미리 준비해둘 프리팹/머티리얼이
        // 필요 없도록 런타임에 LineRenderer를 코드로 생성해서 씁니다(내장 Sprites/Default 셰이더).

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
                // [2026-07-08 수정] "선말고 면으로" 요청 반영 — 이제 중심선 하나가 아니라 실제
                // 콜라이더 폭만큼의 직사각형 테두리를 그리므로 닫힌 루프(loop: true)로 만듭니다.
                _chargeLineIndicator = CreateIndicatorLineRenderer("[ChargePathArea]", loop: true);
        }

        /// <summary>물기(원형 범위)나 철갑 폭우처럼 "중심점 + 반지름"으로 표현되는 범위를 바닥에 원으로 표시합니다.</summary>
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

        /// <summary>
        /// 돌진처럼 "시작점 + 방향 + 길이 + 폭"으로 표현되는 범위를 바닥에 표시합니다.
        /// [2026-07-08 수정] "돌진 시각화 콜라이더 넓이만큼 보이게, 선말고 면으로" 요청 반영 —
        /// 예전엔 중심선 하나만 그렸는데, 이제 실제 ChargeHitbox 폭(width)만큼 좌우로 벌어진
        /// 직사각형 테두리로 그려서 실제 판정 범위(면) 전체를 보여줍니다.
        /// </summary>
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

            // [2026-07-07 변경] 패턴(예고~공격)이 진행 중일 땐 더 이상 매 프레임 플레이어 쪽으로
            // 재조준하지 않습니다. 예전엔 여기서 무조건 FacePlayer를 불러서, 회전속도가 유한해도
            // 결국 시간이 지나면 항상 정면이 플레이어를 향하게 됐습니다(플레이어가 옆/뒤로 돌아가도
            // 보스가 서서히 따라 돌아버림). 이제는 "접근 중(추격)"에만 조준하고, 일단 패턴을 시작하면
            // 그 순간의 방향을 그대로 유지합니다 — 꼬리 휘두르기 등을 노리고 옆/뒤로 도는 플레이어에게
            // 실제로 의미 있는 사각(死角)을 만들어 주기 위함입니다.
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
            if (Time.time >= _nextMoveDebugLogTime)
            {
                _nextMoveDebugLogTime = Time.time + 1f;
                float d = _player != null ? DistanceToPlayer() : -1f;
                Debug.Log($"[불가살이/이동진단] 거리={d:F1} (기준 {_preferredDistance}) " +
                          $"패턴진행중={_isPatternActive} agent활성={(_agent != null && _agent.enabled)} " +
                          $"onNavMesh={(_agent != null && _agent.isOnNavMesh)} " +
                          $"agent속도={(_agent != null ? _agent.velocity.magnitude : -1f):F2} " +
                          $"위치={transform.position}");
            }

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
                // [2026-07-08 신규] "무작정 접근만 하면 패턴이 단조롭다"는 피드백 반영 — 접근
                // 중에도 패턴 쿨다운이 다 찼으면 잠깐 멈춰서 원거리 철갑 발사(패턴0, 유일하게
                // 거리 제한 없는 패턴)를 섞습니다. 근접 패턴(꼬리 휘두르기/돌진/철갑 폭우)은
                // 어차피 이 거리에서 안 맞으니 제외하고 철갑 발사만 강제로 골라 씁니다.
                if (Time.time >= _nextPatternTime)
                {
                    StartCoroutine(RunRandomPattern(forceRangedOnly: true));
                    return;
                }

                MoveTowards(_player.position);

                // [2026-07-08 변경] "뛰기(Run) 애니메이션 상태 자체를 없애자" 요청 반영 — Run은
                // 더 이상 이동 중 Speed 파라미터로 계속 들어가는 상태가 아니라, 돌진(Pattern_Charge)
                // 전용 트리거로만 재생됩니다. 그래서 평소 이동 애니메이션은 항상 Walk(1) 하나로
                // 통일합니다 — 실제 이동속도(_agent.speed)는 예전처럼 전력 질주/평소 추격/살살
                // 접근 세 단계를 그대로 유지하지만(체감 속도 차이는 그대로 있음), 재생되는 클립만
                // Walk로 고정됩니다.
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

        /// <summary>
        /// [2026-07-08 신규] "2페이즈 되기 전에 해당 체력 초과하면 데미지 안 들어가게" 요청 반영.
        /// 1페이즈 중 한 방의 피해가 2페이즈 진입 체력(_phase2HealthRatio) 문턱을 넘어서 그대로
        /// 깎여버리면, 페이즈 전환(포효 모션 등)을 온전히 보여줄 틈도 없이 체력이 훅 빠지거나
        /// 심하면 그 한 방으로 거의 죽어버릴 수 있습니다. 그래서 1페이즈 동안은 이 문턱 아래로
        /// 내려가는 초과분을 잘라내고, 딱 문턱 체력에 맞춰서 멈춥니다(문턱 자체는 통과 — 이
        /// 프레임에 OnHealthChanged가 바로 이어서 호출되어 2페이즈로 전환됩니다). 일단 2페이즈로
        /// 전환된 뒤에는(_phase != Phase1) 더 이상 자르지 않고 정상적으로 죽을 수 있습니다.
        /// </summary>
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

        /// <summary>
        /// [2026-07-08 신규] "그로기 처형시 죽는거 말고 한 500딜정도" 요청 반영. 기본 구현은
        /// 처형 = 즉사(EnterDead)지만, 보스는 그로기 상태에서 처형당해도 죽지 않고 고정 피해
        /// (_executeDamage)만 입도록 오버라이드합니다. TakeDamageDirect()로 넣어서 일반 피해와
        /// 동일한 경로(체력 반영/ClampFinalDamage/OnHealthChanged/그로기·사망 판정)를 그대로
        /// 타므로, 이 한 방으로 진짜 죽거나(체력이 이미 얼마 안 남아 있었다면) 2페이즈로
        /// 전환되는 것도 자연스럽게 가능합니다 — "안 죽는다"가 아니라 "이걸로 즉사만 안 한다"는
        /// 뜻입니다.
        /// </summary>
        protected override void PerformExecution(
            KillRitual.Core.Interfaces.ExecutionSource source)
        {
            Debug.Log($"[불가살이] {name}: 그로기 처형 — 즉사 대신 고정피해 {_executeDamage} 적용");
            var context = new KRDamageContext(
                _executeDamage, KRDamageType.Metal, transform.position, Vector3.zero);
            TakeDamageDirect(context);
        }

        // [2026-07-08 신규] "2페이즈 시전이 안 되는 것 같다" 진단용 — 체력 60% 지점을 딱 한 번
        // 지날 때 로그를 남깁니다. 콘솔에서 이 로그가 뜨는지부터 확인하면 원인을 좁힐 수 있습니다:
        // 이 로그조차 안 뜨면 애초에 이 훅(TakeDamage 계열)으로 피해가 안 들어가고 있다는 뜻이고,
        // 이건 뜨는데 그 아래 "2페이즈 진입" 로그가 60% 이후 안 뜨면 _phase2HealthRatio 값
        // (현재 인스펙터 값)이나 ClampFinalDamage()를 의심하면 됩니다.
        private bool _health60Logged;

        protected override void OnHealthChanged(float ratio)
        {
            if (!_health60Logged && ratio <= 0.6f)
            {
                _health60Logged = true;
                Debug.Log($"[불가살이/체력진단] {name}: 체력 60% 도달 (현재 {ratio:P0}, 페이즈 " +
                          $"{_phase}) — 2페이즈 진입 문턱은 {_phase2HealthRatio:P0}입니다.");
            }

            if (_phase == BossPhase.Phase1 && ratio <= _phase2HealthRatio)
            {
                _phase = BossPhase.Phase2;
                ConsumeNextPhaseBreakObject();
                UpdateBossHealthUI();

                Debug.Log($"[불가살이] {name}: 2페이즈 진입 (체력 {ratio:P0}) — 공격 속도 증가, " +
                          "철갑 발사 폭발/코 채찍 3연타/돌진 연속/철갑 폭우 해금");

                // [2026-07-08 신규] "2페이즈 전환시 무조건 포효모션이 우선되도록" 요청 반영.
                // 예전엔 그냥 트리거만 걸었는데, OnHealthChanged는 _isPatternActive와 무관하게
                // 아무 때나(다른 패턴이 한창 진행 중이어도) 호출될 수 있어서, 트리거를 건 직후에
                // 진행 중이던 다른 패턴이 계속 자기 트리거를 걸거나 새 패턴이 곧바로 시작되면
                // PlayActionTrigger()의 상호배타 로직 때문에 방금 건 Roar 트리거가 그대로
                // 취소되고 포효가 아예 재생되지 못하는 경우가 있었습니다. 그래서 지금 진행 중이던
                // 패턴 코루틴을 즉시 끊고, 포효를 그 자체로 하나의 "패턴"처럼 _isPatternActive로
                // 점유해서 다른 어떤 트리거도 끼어들 수 없게 만들었습니다.
                StopAllCoroutines();
                OverrideColor = null;
                HideCircleIndicator();
                HideChargeLineIndicator();
                StartCoroutine(PhaseTransitionRoar());
                return;
            }

            UpdateBossHealthUI();
        }

        /// <summary>
        /// [2026-07-08 신규] 2페이즈 전환 포효를 다른 패턴이 끼어들 수 없게 독점 재생합니다.
        /// 페이즈변경 상태는 원래 속도(1배)인데, 클립 실제 프레임레이트가 25fps(PAL)라서
        /// 200프레임 = 8초, ExitTime 0.9 기준 실제 종료는 약 7.2초입니다. 그 실제 재생시간보다
        /// 넉넉하게(_roarDuration) _isPatternActive를 켜 둔 채로 기다린 뒤에야 일반 패턴 로직에
        /// 제어를 돌려줍니다.
        /// </summary>
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

        /// <summary>
        /// [2026-07-07 변경] 부위 콜라이더 어디에도 안 걸린 애매한 곳(루트의 큰 캡슐 콜라이더)에
        /// 직접 맞았을 때만 적용되는 보정입니다. 부위별 피해(KRBossBodyPart)는 TakeDamageDirect()로
        /// 별도 처리되어 이 훅을 거치지 않습니다.
        /// </summary>
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

        /// <param name="forceRangedOnly">
        /// [2026-07-08 신규] true면 패턴을 랜덤으로 고르지 않고 무조건 철갑 발사(0번, 유일한
        /// 원거리 패턴)만 실행합니다. 아직 접근 중(거리가 _preferredDistance보다 먼 상태)일 때
        /// TickBossLogic()이 이걸 호출해서 "그냥 걸어오기만 하면 단조롭다"는 피드백에 대응합니다.
        /// </param>
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
                float distance = DistanceToPlayer();
                index = PickPatternIndex(distance);
            }

            yield return StartCoroutine(GetPatternCoroutine(index));

            float cooldown = _patternCooldown * (_phase == BossPhase.Phase2 ? _phase2CooldownMultiplier : 1f);
            _nextPatternTime = Time.time + cooldown;
            _isPatternActive = false;
        }

        /// <summary>
        /// [2026-07-08 신규] 패턴 선택 결과를 기록하면서 "3연속 금지"용 반복 횟수도 같이 갱신합니다.
        /// forceRangedOnly 경로(PickPatternIndex를 거치지 않음)와 정상 선택 경로 둘 다 이 메서드로
        /// 기록을 남겨야 반복 횟수가 정확히 유지됩니다.
        /// </summary>
        private void RegisterPatternChoice(int index)
        {
            _patternRepeatCount = (index == _lastPatternIndex) ? _patternRepeatCount + 1 : 1;
            _lastPatternIndex = index;
        }

        private int PickPatternIndex(float distance)
        {
            int count = _phase == BossPhase.Phase2 ? 4 : 3;

            // [2026-07-08 변경 — "같은 패턴 3연속 금지" 요청 반영]
            // 예전엔 직전에 쓴 패턴을 무조건 후보에서 뺐습니다(최대 1회 연속만 허용). 이제는
            // "2연속까지는 허용, 그 상태에서 또 같은 패턴이 뽑히면(=3연속이 됨) 후보에서 제외"로
            // 바꿨습니다 — _patternRepeatCount가 2 이상(=이미 2연속 사용됨)일 때만 직전 패턴을
            // 후보에서 뺍니다.
            bool excludeLastForRepeat = _patternRepeatCount >= 2;

            var candidates = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (excludeLastForRepeat && i == _lastPatternIndex) continue;
                if (IsPatternViableAtDistance(i, distance)) candidates.Add(i);
            }

            // [2026-07-08 수정 — "시작하자마자 죽는다" 재발 방지]
            // 예전엔 여기서 거리 적합성(IsPatternViableAtDistance)을 아예 무시하고 "마지막에 쓴
            // 패턴만 아니면 아무거나" 식으로 후보를 채웠습니다. 그러면 근접거리인데도 원거리
            // 전용인 철갑 발사(0)가 뽑힐 수 있었고, 6발이 부채꼴로 동시 명중해 큰 피해를 줬습니다.
            // 이제는 "3연속 금지" 제약만 풀고, 거리 적합성은 그대로 지킵니다.
            if (candidates.Count == 0)
            {
                for (int i = 0; i < count; i++)
                    if (IsPatternViableAtDistance(i, distance)) candidates.Add(i);
            }

            // 그래도 후보가 없으면(돌진 다리 파괴 + 애매한 거리 등 극단적 상황) 물기(1)를 기본값으로
            // 씁니다 — 사거리 밖이면 TryHitTrunkStrike()가 알아서 헛스윙 처리하므로, 근접거리에서
            // 불공정한 대미지를 주는 철갑 발사(0)보다 훨씬 안전한 기본값입니다.
            if (candidates.Count == 0) candidates.Add(1);

            int index = candidates[Random.Range(0, candidates.Count)];

            // [2026-07-08 신규] "물기 패턴이 안 나온다" 진단용 — 매번 후보 목록과 실제 선택 결과를
            // 남깁니다. 물기(1)가 후보 목록에 계속 안 잡힌다면 거리 조건(distance < _trunkStrikeRange)
            // 자체를 못 만족하고 있다는 뜻이라, 이 로그만 보면 원인이 "안 뽑힘"인지 "애초에 후보가
            // 아니었음"인지 바로 구분됩니다.
            string candidateNames = string.Join(", ", candidates.ConvertAll(i => PatternName(i)));
            Debug.Log($"[불가살이/패턴선택] 거리={distance:F1}m (근접/원거리 경계 {_trunkStrikeRange}m) " +
                      $"후보=[{candidateNames}] → 선택={PatternName(index)} " +
                      $"(직전패턴={PatternName(_lastPatternIndex)}, 연속횟수={_patternRepeatCount})");

            RegisterPatternChoice(index);
            return index;
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

        /// <summary>
        /// 패턴별 "이 거리/상태에서 쓰는 게 말이 되는가"를 판단합니다.
        /// [2026-07-08 재설계 — "원거리공격은 10m 이상, 물기는 10m 미만" 요청 반영]
        /// _trunkStrikeRange(기본 10) 하나를 근접/원거리를 가르는 공통 경계값으로 씁니다.
        /// 예전엔 원거리(0)는 _shardMinRange(5), 물기(1)/돌진(2)은 _trunkStrikeRange(6)로
        /// 서로 다른 기준을 썼는데, 그러면 두 값 사이(5~6m) 같은 애매한 구간이 생기거나
        /// 반대로 겹치는 구간이 생겨서 의도를 벗어난 선택이 나올 수 있었습니다. 지금은:
        /// - 철갑 발사(0): distance >= _trunkStrikeRange (10m 이상 — 진짜 원거리일 때만)
        /// - 물기(1): distance &lt; _trunkStrikeRange (10m 미만 — 근접일 때만)
        /// - 돌진(2): 거리를 좁히는 패턴이라 철갑 발사와 같은 "원거리" 구간(10m 이상)에서만
        ///   쓰고, 앞다리/뒷다리 중 하나라도 파괴됐으면 아예 못 씁니다(다리 없이 못 뛰므로).
        /// - 철갑 폭우(3, 2페이즈): 범위 공격 — 범위 안일 때만(근접/원거리 구분과 무관).
        /// </summary>
        private bool IsPatternViableAtDistance(int index, float distance)
        {
            switch (index)
            {
                case 0: return distance >= _trunkStrikeRange;
                case 1: return distance < _trunkStrikeRange;
                case 2:
                    bool legsBroken = (_frontLegs != null && _frontLegs.IsBroken) ||
                                       (_backLegs != null && _backLegs.IsBroken);
                    return !legsBroken && distance >= _trunkStrikeRange;
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

        /// <summary>
        /// [2026-07-08 신규 — "돌진 중에 원거리 공격 모션이 나온다" 버그 예방]
        /// Attack/PowerfulAttack/Roar/Run 네 트리거는 전부 AnyState에서 서로 배타적으로 딱 하나만
        /// 재생돼야 하는 "한 번 재생 액션"들입니다. Animator의 Trigger 파라미터는 조건을 실제로
        /// 소비하는 전이가 그 프레임에 평가되지 않으면 값이 계속 true로 남아있을 수 있는데, 그
        /// 상태에서 다른 트리거를 새로 걸면(예: 돌진의 Run) 예전에 걸어뒀던 트리거(예: 철갑발사의
        /// Attack)가 나중에 뒤늦게 함께/대신 소비되면서 의도한 것과 다른 애니메이션이 튀어나올 수
        /// 있습니다. 새 트리거를 걸기 직전에 나머지 셋을 전부 ResetTrigger로 확실히 꺼서, 항상
        /// 지금 의도한 것 하나만 남도록 보장합니다.
        /// </summary>
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

            // [2026-07-08 신규 — "모션이랑 투사체 발사랑 싱크 안 맞아" 버그 수정]
            // 예전엔 트리거를 건 바로 그 프레임에 곧바로 철갑을 발사했습니다. 그런데 실제 attack
            // 클립은 200프레임짜리 애니메이션이라, 트리거를 건 순간엔 아직 팔/입을 들어올리는
            // 예비 동작만 시작된 상태입니다 — 실제로 "던지는" 동작이 나오기도 전에 투사체가 먼저
            // 튀어나가 버렸던 겁니다.
            // [2026-07-08 수정] 처음엔 5.5배로 압축했다가("모션이 부자연스럽다") 1배로 되돌렸다가
            // ("걷기 모션이 안 나온다"), '모션시간 2배 줄이고' 요청에 따라 공격1 상태 m_Speed를
            // 2배로 맞췄습니다(컨트롤러 쪽). 지연 시간(_shardLaunchDelay)은 '모션과 동시에' 요청에
            // 따라 최종적으로 0으로 맞췄습니다 — 트리거를 건 바로 그 프레임에 발사됩니다.
            yield return new WaitForSeconds(_shardLaunchDelay);

            FireShardsFromMuzzle(_shoulderLMuzzle);
            FireShardsFromMuzzle(_shoulderRMuzzle);

            // [2026-07-07 변경] 이전엔 여기서 어깨(_shoulderL/R)를 잠깐 노출시켰지만, 이제 부위는
            // 항상 맞을 수 있으므로 그 개념 자체가 사라졌습니다 — 패턴은 순수 공격 시퀀스입니다.

            // [2026-07-08 신규 — "걷기 모션이 다시 빠졌다" / "애니메이션이 캔슬되는거 같아서" 버그 수정]
            // 처음엔 클립이 30fps짜리라고 잘못 가정해서 실제 재생시간을 너무 짧게 계산했습니다.
            // FBX를 직접 확인해보니 실제 프레임레이트는 25fps(PAL)였습니다 — 200프레임 = 8초
            // (1배속), 공격1은 2배속이니 실제 재생시간 약 4초, ExitTime 0.9 기준 실제 종료는 약
            // 3.6초입니다. 이 대기(_shardRecoveryDelay)를 포함한 코루틴 총 시간 + 쿨다운이 그
            // 3.6초보다 늦게 끝나야, 애니메이션이 자기 힘으로 대기/이동에 복귀한 뒤에야 다음
            // 철갑발사가 잡힙니다. 그렇지 않으면 다음 트리거가 AnyState로 먼저 끼어들어
            // 애니메이션이 끝까지 재생되지 못하고 캔슬됩니다.
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

        // ── 패턴 2: 물기 ────────────────────────────────────────────────
        // [2026-07-08 변경] "꼬리 휘두르기"에서 다시 "물기"로 컨셉 변경(요청 반영). 판정 자체는
        // 그대로 꼬리 위치 기준 원형 범위를 씁니다 — 바뀐 건 이름/로그/재생하는 애니메이션뿐이고,
        // 히트 판정 로직(TryHitTrunkStrike)은 손 안 댔습니다.

        private IEnumerator Pattern_TrunkWhip()
        {
            int swings = _phase == BossPhase.Phase2 ? 3 : 1;
            Debug.Log($"[불가살이] {name}: 패턴2 - 물기 ({swings}연타)");

            for (int i = 0; i < swings; i++)
            {
                // [2026-07-08 신규] 판정 원점(_head)을 중심으로 실제 사거리(_trunkStrikeRange)를
                // 바닥에 원으로 보여줍니다 — 윈드업 동안 켜졌다가 타격 순간 곧바로 꺼집니다.
                Vector3 originPos = _head != null ? _head.Position : transform.position;
                ShowCircleIndicator(originPos, _trunkStrikeRange);

                OverrideColor = _telegraphColor;
                PlayScalePunch(new Vector3(1.05f, 0.95f, 0.9f), _trunkWindup * 0.8f, _trunkWindup * 0.2f);
                yield return new WaitForSeconds(_trunkWindup);
                OverrideColor = null;
                PlayScalePunch(new Vector3(0.95f, 1.02f, 1.15f), 0.06f, 0.15f);

                // [2026-07-08 변경] "강공격 애니메이션으로 바꾸자" 요청 반영 — Attack 대신
                // PowerfulAttack 트리거를 씁니다. 첫 타에서만 쏘는 이유는 그대로입니다: 2페이즈
                // 3연타 내내 매번 재발동시키면 "재생 중 애니메이션이 발작하듯 재시작"하는 문제가
                // 재현됩니다(AnyState→PowerfulAttack 전환이 자기 자신으로도 걸리면서 끊겼다 재시작).
                if (i == 0) PlayActionTrigger(kPowerfulAttackTrigger);

                TryHitTrunkStrike();
                Debug.Log($"[불가살이] {name}: 물기 {i + 1}/{swings}타 적중 판정");
                HideCircleIndicator();

                if (i < swings - 1)
                    yield return new WaitForSeconds(_trunkComboInterval);
            }

            // [2026-07-07 변경] 이전엔 여기서 머리(구 _trunk)를 잠깐 노출시켰지만, 이제 부위는
            // 항상 맞을 수 있으므로 노출 창 개념이 사라졌습니다.
        }

        /// <summary>
        /// [2026-07-07 변경] "꼬리 콜라이더를 기준으로 가자"는 요청 반영 — 판정 원점을 보스 루트
        /// (transform.position)가 아니라 실제 꼬리(_tail) 콜라이더 위치로 바꿨습니다.
        /// [2026-07-07 재수정 - 범위 버그 수정] 처음엔 "몸 뒤쪽만" 맞도록 각도까지 제한했는데,
        /// 이러면 평소처럼 정면에서 쫓아오다 이 패턴에 걸리는 일반적인 상황에서 플레이어가 사거리
        /// 안에 있어도 거의 항상 빗나가는 문제가 있었습니다(각도 조건이 항상 실패). 이제는 방향/각도
        /// 조건 없이, 원점을 중심으로 한 순수 원형 범위 판정으로 바꿨습니다 — 정면이든 후면이든
        /// 사거리 안이면 맞습니다.
        /// [2026-07-08 재수정 - 기준점 오류 수정] 패턴 컨셉을 "꼬리 휘두르기"에서 "물기"로 되돌리면서
        /// 정작 판정 기준점은 꼬리(_tail)에 그대로 둔 채였습니다 — 물기인데 꼬리를 기준으로 맞고
        /// 안 맞고가 갈리는 건 앞뒤가 안 맞아서, 기준점을 머리(_head) 콜라이더 위치로 바꿨습니다.
        /// _head가 비어있으면(아직 안 연결했으면) 보스 루트 위치로 대체합니다.
        /// </summary>
        private void TryHitTrunkStrike()
        {
            if (_player == null) return;

            Vector3 originPos = _head != null ? _head.Position : transform.position;

            Vector3 toPlayer = _player.position - originPos;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > _trunkStrikeRange)
            {
                Debug.Log($"[불가살이] 물기 판정 - 빗나감 (거리 {distance:F2}m > 사거리 {_trunkStrikeRange}m)");
                return;
            }

            IDamageable target = FindPlayerDamageable(_player);
            if (target == null || target.IsDead) return;

            Vector3 hitDirection = distance > 0.0001f ? toPlayer.normalized : transform.forward;
            var context = new KRDamageContext(_trunkDamage, KRDamageType.Fire, _player.position, hitDirection);
            Debug.Log($"[불가살이] 물기 판정 - 명중 (원점 {originPos}, 거리 {distance:F2}m, 사거리 {_trunkStrikeRange}m)");
            target.TakeDamage(context);
        }

        // ── 패턴 3: 돌진 ────────────────────────────────────────────────

        private IEnumerator Pattern_Charge()
        {
            Debug.Log($"[불가살이] {name}: 패턴3 - 돌진 준비 ({_chargeWindup}초 차징)");

            // [2026-07-08 변경] "돌진이 예측 불가능하고 피하기 힘들다"는 피드백 반영 — 예전엔
            // 윈드업 동안 몸이 어느 쪽을 보고 있든 상관없이, 윈드업이 "끝나는 순간"의 플레이어
            // 위치로 방향을 다시 계산해서 그대로 돌진했습니다. 즉 몸이 보여주는 방향(전조)과
            // 실제 돌진 방향이 서로 무관해서, 플레이어 입장에선 아무리 옆으로 피해도 소용없는
            // "조준 사격"처럼 느껴졌을 겁니다.
            // 이제는 방향을 윈드업 "시작" 시점에 딱 한 번만 정하고, 그 방향으로 윈드업 내내
            // 실제로 몸을 돌리는 걸 보여준 다음(WaitForSeconds 대신 회전 코루틴을 그 자리에
            // 씁니다 — 전체 윈드업 시간은 그대로 유지됩니다) 그 고정된 방향으로만 돌진합니다.
            // 플레이어는 몸이 돌아가는 걸 보고 미리 옆으로 피할 수 있습니다.
            Vector3 direction = transform.forward;
            if (_player != null)
            {
                Vector3 toPlayer = _player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f) direction = toPlayer.normalized;
            }

            // [2026-07-08 신규] 윈드업 "시작" 시점에 방향이 고정되므로, 그 즉시 바닥에 돌진 경로를
            // (길이 = _chargeMaxDistance, 폭 = 실제 ChargeHitbox 폭)만큼 면으로 미리 보여줍니다 —
            // 몸 회전 전조와 함께 이중으로 방향을 알려줘서 회피 판단을 더 쉽게 해줍니다.
            ShowChargeLineIndicator(transform.position, direction, _chargeMaxDistance,
                _chargeHitbox != null ? _chargeHitbox.GetWidth() : 3f);

            OverrideColor = _telegraphColor;
            PlayScalePunch(new Vector3(1.1f, 0.8f, 0.95f), _chargeWindup * 0.85f, _chargeWindup * 0.15f);
            yield return StartCoroutine(RotateTowardsDirectionOverTime(direction, _chargeWindup));
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.9f, 1.05f, 1.2f), 0.1f, 0.25f);

            // [2026-07-08 변경] "돌진 애니메이션은 뛰는 모션으로 바꾸자" + "뛰기(Run) 상태 자체를
            // 없애자" 요청 반영 — Run은 이제 이동 Speed 파라미터가 아니라 돌진 전용 트리거로만
            // 재생됩니다(Attack/PowerfulAttack/Roar와 같은 방식: AnyState→Run 트리거 진입,
            // 재생이 끝나면 컨트롤러가 자동으로 Idle로 돌아갑니다 — 수동으로 안 꺼도 됩니다).
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

            // [2026-07-07 변경] 이전엔 여기서 머리/앞다리를 잠깐 노출시켰지만, 이제 부위는 항상
            // 맞을 수 있으므로 노출 창 개념이 사라졌습니다. 대신 벽 충돌 시 앞다리 자해 피해가
            // DoChargeDash() 안에서 걸립니다(아래 참고) — 돌진 자체가 부위 파괴와 연동됩니다.

            // [2026-07-07 신규] 돌진이 끝나면(벽에 부딪혔든 최대거리까지 갔든) 플레이어 쪽으로
            // 천천히 다시 돌아봅니다. 패턴 진행 중엔 UpdateChase/UpdateAttack의 FacePlayer가
            // 멈춰 있으므로, 이 회전은 여기서 직접 코루틴으로 처리해야만 실제로 일어납니다.
            Debug.Log($"[불가살이] {name}: 돌진 후 재조준 시작 ({_chargeTurnBackDuration}초)");
            yield return StartCoroutine(TurnBackTowardsPlayer(_chargeTurnBackDuration));
        }

        /// <summary>
        /// [2026-07-07 신규] 지정한 시간(duration) 동안 플레이어 쪽으로 부드럽게 회전합니다.
        /// 실제 회전은 공용 헬퍼(RotateTowardsDirectionOverTime)에 위임합니다.
        /// </summary>
        private IEnumerator TurnBackTowardsPlayer(float duration)
        {
            if (_player == null) yield break;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;

            yield return StartCoroutine(RotateTowardsDirectionOverTime(toPlayer, duration));
        }

        /// <summary>
        /// [2026-07-08 신규] 지정한 방향(direction)을 향해 지정한 시간(duration) 동안 부드럽게
        /// 회전합니다. 회전속도가 아니라 "걸리는 시간"을 고정하는 방식이라(Slerp의 t를 시간으로
        /// 진행), 남은 각도가 크든 작든 항상 duration초 만에 회전이 끝나 일관된 느낌을 줍니다.
        /// 돌진 윈드업 방향 고정(Pattern_Charge)과 돌진 후 재조준(TurnBackTowardsPlayer) 둘 다
        /// 이 헬퍼 하나를 공유합니다.
        /// </summary>
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

        /// <summary>
        /// NavMeshAgent를 잠시 멈추고 transform을 직접 이동시켜 빠른 직선 돌진을 구현합니다.
        /// 레이캐스트로 전방 벽을 감지해 부딪히면 즉시 멈추고 _lastChargeHitWall을 true로 남깁니다.
        /// [2026-07-07 변경] 벽에 부딪히면 그 충격으로 앞다리(_frontLegs)에 자해 피해를 줍니다 —
        /// "돌진도 부위 파괴와 연동"해 달라는 요청 반영. 반복해서 무리하게 돌진하면 스스로
        /// 앞다리를 부러뜨릴 수 있습니다(부러지면 IsPatternViableAtDistance()가 돌진 자체를 봉인).
        /// </summary>
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

                // [2026-07-08 버그 수정] "돌진이 너무 적게 나간다"는 문제의 원인 — _chargeWallLayerMask가
                // 기본값 Everything(레이어 구분을 별도로 안 해둔 상태)이라, 방금 Activate()로 켠
                // _chargeHitbox 자신이나 머리/앞다리 등 보스 자신의 부위 콜라이더까지 레이캐스트에
                // 걸려서 "벽에 부딪혔다"고 착각해 돌진 시작하자마자(0에 가까운 거리에서) 멈춰버렸던
                // 겁니다. 레이어 세팅에 의존하지 않고, 맞은 콜라이더가 보스 자신의 계층구조 소속이면
                // (transform.root가 이 보스 루트와 같으면) 무시하도록 코드에서 직접 걸러냅니다.
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

            // [2026-07-08 신규] 낙하 범위(_armorRainRadius)를 보스 발밑 중심으로 원으로 미리
            // 보여줍니다 — 예고 시작부터 실제로 철갑이 다 떨어질 때까지 계속 켜둡니다.
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

            // [2026-07-07 변경] 이전엔 여기서 등(구 _back)을 잠깐 노출시켰지만, 이제 그 부위 자체가
            // (뒷다리로 재편) 없어졌고 노출 창 개념도 사라졌습니다.
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
