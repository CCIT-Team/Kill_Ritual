// Assets/Project/Features/Enemies/MakeNew/KRBossJakdu01.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    ///   전투에 영향을 주는 행동 변화가 걸립니다(이동속도 감소/돌진 봉인/강제 다운 — 아래 참고).
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

        [Tooltip("다리가 파괴되는 순간 강제로 그로기(다운)시키는 시간(초).")]
        [Min(0.1f)]
        [SerializeField] private float _legBreakGroggyDuration = 2.5f;

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
        [Min(1)] [SerializeField] private int _shardsPerShoulder = 3;
        [Min(0.1f)] [SerializeField] private float _shardSpeed = 20f;
        [Min(0f)] [SerializeField] private float _shardDamage = 15f;
        [SerializeField] private LayerMask _shardHitLayerMask = ~0;
        [SerializeField] private LayerMask _shardDamageableLayerMask = ~0;
        [Tooltip("2페이즈: 바닥에 꽂힌 철갑이 터지기까지의 지연 시간(초).")]
        [Min(0.1f)] [SerializeField] private float _shardExplodeDelay = 1.5f;
        [Min(0.1f)] [SerializeField] private float _shardExplosionRadius = 2.5f;

        [Header("패턴2 - 꼬리 휘두르기")]
        [Tooltip("[2026-07-07 변경] '물기'가 아니라 '꼬리 휘두르기'로 확정 — 판정 기준점을 보스 " +
                 "루트가 아니라 실제 꼬리(_tail) 콜라이더 위치로 바꿨습니다.\n" +
                 "[2026-07-07 재수정] 처음엔 몸 뒤쪽(-transform.forward)만 맞도록 각도까지 " +
                 "제한했는데, 이러면 보통 정면에서 쫓아오다 이 패턴에 걸린 플레이어는 범위 안에 " +
                 "있어도 거의 항상 안 맞는 버그가 됐습니다(평소엔 플레이어가 보스 정면에 있는 게 " +
                 "일반적이라서). '범위가 이상하다'는 피드백이 이 문제였을 가능성이 커서, 각도 제한을 " +
                 "없애고 꼬리 위치 기준 원형 범위로 단순화했습니다.")]
        [Min(0.05f)] [SerializeField] private float _trunkWindup = 0.6f;
        [Min(0.5f)] [SerializeField] private float _trunkStrikeRange = 6f;
        [Tooltip("[2026-07-07 더 이상 안 씀] 각도 제한을 없애서 이 필드는 판정에 안 쓰입니다. " +
                 "혹시 나중에 방향성 있는 판정으로 되돌릴 때를 대비해 필드만 남겨뒀습니다.")]
        [Range(1f, 90f)] [SerializeField] private float _trunkStrikeHalfAngle = 60f;
        [Min(0f)] [SerializeField] private float _trunkDamage = 25f;
        [Tooltip("연속 타격 사이의 간격(초, 2페이즈 3연타용).")]
        [Min(0.05f)] [SerializeField] private float _trunkComboInterval = 0.35f;

        [Header("패턴3 - 돌진")]
        [Min(0.1f)] [SerializeField] private float _chargeWindup = 1f;
        [Min(1f)] [SerializeField] private float _chargeSpeed = 15f;
        [Min(1f)] [SerializeField] private float _chargeMaxDistance = 20f;
        [Min(0f)] [SerializeField] private float _chargeDamage = 30f;
        [Tooltip("더 이상 안 씀 — _chargeHitbox(실제 Trigger 콜라이더)가 판정을 전담합니다. " +
                 "하위호환을 위해 필드는 남겨뒀습니다.")]
        [Min(0.5f)] [SerializeField] private float _chargeHitRadius = 2.5f;
        [Tooltip("벽 감지용 레이어 — 플레이어/적 레이어는 반드시 제외하세요. 지형/벽 레이어만 포함.")]
        [SerializeField] private LayerMask _chargeWallLayerMask = ~0;
        [Tooltip("돌진 전용 피해 판정 콜라이더(KRBossChargeHitbox). 돌진 중에만 켜져서 " +
                 "정확한 Trigger 판정을 합니다.")]
        [SerializeField] private KRBossChargeHitbox _chargeHitbox;
        [Min(0.1f)] [SerializeField] private float _wallStunDuration = 1.5f;
        [Tooltip("[2026-07-07 신규] 돌진 중 벽에 부딪혔을 때 그 충격으로 앞다리(_frontLegs) 자신에게 " +
                 "들어가는 자해 피해. 무리한 돌진을 반복하면 스스로 다리가 부러질 수 있게 하는 " +
                 "리스크/리워드 장치입니다 — '돌진도 부위 파괴와 연동'해 달라는 요청 반영.")]
        [Min(0f)] [SerializeField] private float _chargeSelfDamageOnWallHit = 35f;
        [Tooltip("[2026-07-07 신규] 돌진(및 벽 충돌 시 경직/2연속 돌진까지) 끝난 뒤, 플레이어 쪽으로 " +
                 "다시 몸을 돌리는 데 걸리는 시간(초). 패턴 진행 중엔 FacePlayer가 멈춰 있으므로, " +
                 "돌진 직후 남은 각도와 상관없이 항상 이 시간만큼 걸려서 천천히 재조준하도록 " +
                 "코루틴으로 별도 처리합니다 — '뒤도는데 한 2초는 걸리면 좋겠다'는 요청 반영.")]
        [Min(0.1f)] [SerializeField] private float _chargeTurnBackDuration = 2f;

        [Header("신규 패턴(2페이즈 전용) - 철갑 폭우")]
        [Min(1)] [SerializeField] private int _armorRainCount = 10;
        [Min(1f)] [SerializeField] private float _armorRainRadius = 8f;
        [Min(0f)] [SerializeField] private float _armorRainDamage = 10f;
        [Min(0.1f)] [SerializeField] private float _armorRainFallSpeed = 12f;
        [Min(0.1f)] [SerializeField] private float _armorRainDuration = 1.8f;

        [Header("시각 신호")]
        [Tooltip("모든 패턴 예고(윈드업) 구간 동안 표시할 경고색.")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.5f, 0f, 1f);

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
        private float _nextPatternTime;
        private bool _lastChargeHitWall;
        private int _brokenLegCount;
        private float _legSpeedMultiplier = 1f;

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

            if (_visualAnimator != null && _visualAnimator.applyRootMotion)
                Debug.LogWarning($"[불가살이] {name}: Animator의 Apply Root Motion이 켜져 있습니다 — " +
                                  "이동 코드가 옮긴 위치를 애니메이션이 매 프레임 덮어쓸 수 있습니다. 꺼주세요.");

            // [2026-07-07 신규] 부위 파괴 이벤트 구독 — 다리(앞/뒤) 파괴 시 이동속도 감소 +
            // 강제 다운을 적용합니다. 돌진 가능 여부는 IsPatternViableAtDistance()에서
            // _frontLegs.IsBroken / _backLegs.IsBroken을 직접 확인합니다(별도 이벤트 불필요).
            if (_frontLegs != null) _frontLegs.OnBroken += HandleLegBroken;
            if (_backLegs != null) _backLegs.OnBroken += HandleLegBroken;
        }

        /// <summary>
        /// [2026-07-07 신규] 다리(앞다리 또는 뒷다리) 파괴 시 공통으로 호출됩니다.
        /// 이동속도를 곱셈으로 줄이고(양쪽 다 부러지면 더 느려짐), 그 충격으로 잠깐 다운시킵니다.
        /// </summary>
        private void HandleLegBroken()
        {
            _brokenLegCount++;
            // [2026-07-07 변경] 여기서 바로 _agent.speed를 정하지 않습니다 — 전력 질주 배율과
            // 곱셈이 겹쳐야 하므로, 실제 속도 계산은 TickBossLogic()이 매 틱마다 담당하고
            // 여기서는 "다리 파괴로 인한 배율"만 갱신해 둡니다.
            _legSpeedMultiplier = Mathf.Pow(_legBreakSpeedMultiplier, _brokenLegCount);

            Debug.Log($"[불가살이] {name}: 다리 파괴! (누적 {_brokenLegCount}개) " +
                      $"이동속도 {_legSpeedMultiplier:P0}로 감소, 돌진 패턴 봉인, 강제 다운");

            ForceGroggy(_legBreakGroggyDuration);
        }

        // ── KREnemyBase 추상 메서드 구현 ─────────────────────────────────

        protected override void UpdateChase()
        {
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
                MoveTowards(_player.position);
                // [2026-07-07 변경] Speed=2(Run) — 이 포식자는 걷는 것보다 뛰어서 쫓아오는 게
                // 어울려서, 이동 중엔 Run 클립을 쓰도록 값을 올렸습니다. Walk 상태/클립은
                // 컨트롤러에 그대로 남겨뒀지만(향후 "천천히 견제" 같은 동작에 쓸 수 있게),
                // 지금 코드는 Idle(0)↔Run(2)만 오갑니다.
                _visualAnimator?.SetFloat(kSpeedParam, 2f);

                // [2026-07-07 신규] 너무 멀면 전력 질주 — 기준거리의 _sprintDistanceMultiplier배보다
                // 더 멀어지면 NavMeshAgent의 실제 speed 자체를 올려서 더 빨리 따라잡습니다.
                // 다리 파괴 감속(_legSpeedMultiplier)과 곱셈으로 함께 적용됩니다.
                bool isSprinting = distance > _preferredDistance * _sprintDistanceMultiplier;
                if (_agent != null)
                    _agent.speed = _moveSpeed * _legSpeedMultiplier * (isSprinting ? _sprintSpeedMultiplier : 1f);

                return;
            }

            StopMoving();
            _visualAnimator?.SetFloat(kSpeedParam, 0f);

            if (Time.time < _nextPatternTime) return;

            StartCoroutine(RunRandomPattern());
        }

        // ── 페이즈 전환 ──────────────────────────────────────────────────

        protected override void OnHealthChanged(float ratio)
        {
            if (_phase == BossPhase.Phase1 && ratio <= _phase2HealthRatio)
            {
                _phase = BossPhase.Phase2;
                Debug.Log($"[불가살이] {name}: 2페이즈 진입 (체력 {ratio:P0}) — 공격 속도 증가, " +
                          "철갑 발사 폭발/코 채찍 3연타/돌진 연속/철갑 폭우 해금");
                _visualAnimator?.SetTrigger(kRoarTrigger);
            }
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

        private IEnumerator RunRandomPattern()
        {
            _isPatternActive = true;
            _patternActiveSince = Time.time;
            StopMoving();

            float distance = DistanceToPlayer();
            int index = PickPatternIndex(distance);
            yield return StartCoroutine(GetPatternCoroutine(index));

            float cooldown = _patternCooldown * (_phase == BossPhase.Phase2 ? _phase2CooldownMultiplier : 1f);
            _nextPatternTime = Time.time + cooldown;
            _isPatternActive = false;
        }

        private int PickPatternIndex(float distance)
        {
            int count = _phase == BossPhase.Phase2 ? 4 : 3;

            var candidates = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (i == _lastPatternIndex) continue;
                if (IsPatternViableAtDistance(i, distance)) candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < count; i++)
                    if (i != _lastPatternIndex) candidates.Add(i);
            }

            if (candidates.Count == 0) candidates.Add(0);

            int index = candidates[Random.Range(0, candidates.Count)];
            _lastPatternIndex = index;
            return index;
        }

        /// <summary>
        /// 패턴별 "이 거리/상태에서 쓰는 게 말이 되는가"를 판단합니다.
        /// - 철갑 발사(0): 원거리 무기라 거리 제한 없음.
        /// - 꼬리 휘두르기(1): 근접 패턴 — 실제 타격 사거리 안일 때만.
        /// - 돌진(2): [2026-07-07 변경] 거리를 좁히는 패턴이라 코 채찍 사거리보다 멀 때만 쓰는 것에
        ///   더해, 앞다리나 뒷다리 중 하나라도 파괴됐으면 아예 못 씁니다(다리 없이 못 뛰므로).
        /// - 철갑 폭우(3, 2페이즈): 범위 공격 — 범위 안일 때만.
        /// </summary>
        private bool IsPatternViableAtDistance(int index, float distance)
        {
            switch (index)
            {
                case 0: return true;
                case 1: return distance <= _trunkStrikeRange;
                case 2:
                    bool legsBroken = (_frontLegs != null && _frontLegs.IsBroken) ||
                                       (_backLegs != null && _backLegs.IsBroken);
                    return !legsBroken && distance > _trunkStrikeRange;
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

        // ── 패턴 1: 철갑 발사 ────────────────────────────────────────────

        private IEnumerator Pattern_ArmorLaunch()
        {
            Debug.Log($"[불가살이] {name}: 패턴1 - 철갑 발사");

            OverrideColor = _telegraphColor;
            PlayScalePunch(new Vector3(1.08f, 0.9f, 1.08f), 0.35f, 0.05f);
            yield return new WaitForSeconds(0.35f);
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.92f, 1.12f, 0.92f), 0.08f, 0.2f);
            _visualAnimator?.SetTrigger(kAttackTrigger);

            FireShardsFromMuzzle(_shoulderLMuzzle);
            FireShardsFromMuzzle(_shoulderRMuzzle);

            // [2026-07-07 변경] 이전엔 여기서 어깨(_shoulderL/R)를 잠깐 노출시켰지만, 이제 부위는
            // 항상 맞을 수 있으므로 그 개념 자체가 사라졌습니다 — 패턴은 순수 공격 시퀀스입니다.
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

        // ── 패턴 2: 꼬리 휘두르기 ────────────────────────────────────────

        private IEnumerator Pattern_TrunkWhip()
        {
            int swings = _phase == BossPhase.Phase2 ? 3 : 1;
            Debug.Log($"[불가살이] {name}: 패턴2 - 꼬리 휘두르기 ({swings}연타)");

            for (int i = 0; i < swings; i++)
            {
                OverrideColor = _telegraphColor;
                PlayScalePunch(new Vector3(1.05f, 0.95f, 0.9f), _trunkWindup * 0.8f, _trunkWindup * 0.2f);
                yield return new WaitForSeconds(_trunkWindup);
                OverrideColor = null;
                PlayScalePunch(new Vector3(0.95f, 1.02f, 1.15f), 0.06f, 0.15f);

                // [2026-07-07 신규] Attack 트리거는 첫 타에서만 쏩니다 — 2페이즈 3연타 내내 매번
                // 재발동시키면 이전에 겪은 "재생 중 애니메이션이 발작하듯 재시작"하는 문제가 똑같이
                // 재현됩니다(AnyState→Attack 전환이 자기 자신으로도 걸리면서 클립이 끊겼다 재시작).
                if (i == 0) _visualAnimator?.SetTrigger(kAttackTrigger);

                TryHitTrunkStrike();
                Debug.Log($"[불가살이] {name}: 꼬리 휘두르기 {i + 1}/{swings}타 적중 판정");

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
        /// 조건 없이, 꼬리 위치를 중심으로 한 순수 원형 범위 판정으로 바꿨습니다 — 정면이든 후면이든
        /// 사거리 안이면 맞습니다.
        /// _tail이 비어있으면(아직 안 연결했으면) 보스 루트 위치로 대체합니다.
        /// </summary>
        private void TryHitTrunkStrike()
        {
            if (_player == null) return;

            Vector3 originPos = _tail != null ? _tail.Position : transform.position;

            Vector3 toPlayer = _player.position - originPos;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > _trunkStrikeRange) return;

            IDamageable target = FindPlayerDamageable(_player);
            if (target == null || target.IsDead) return;

            Vector3 hitDirection = distance > 0.0001f ? toPlayer.normalized : transform.forward;
            var context = new KRDamageContext(_trunkDamage, KRDamageType.Fire, _player.position, hitDirection);
            target.TakeDamage(context);
        }

        // ── 패턴 3: 돌진 ────────────────────────────────────────────────

        private IEnumerator Pattern_Charge()
        {
            Debug.Log($"[불가살이] {name}: 패턴3 - 돌진 준비 ({_chargeWindup}초 차징)");

            OverrideColor = _telegraphColor;
            PlayScalePunch(new Vector3(1.1f, 0.8f, 0.95f), _chargeWindup * 0.85f, _chargeWindup * 0.15f);
            yield return new WaitForSeconds(_chargeWindup);
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.9f, 1.05f, 1.2f), 0.1f, 0.25f);
            _visualAnimator?.SetTrigger(kPowerfulAttackTrigger);

            Vector3 direction = transform.forward;
            if (_player != null)
            {
                Vector3 toPlayer = _player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f) direction = toPlayer.normalized;
            }

            Debug.Log($"[불가살이] {name}: 돌진 개시");
            yield return StartCoroutine(DoChargeDash(direction));

            if (_lastChargeHitWall)
            {
                Debug.Log($"[불가살이] {name}: 벽 충돌");

                if (_phase == BossPhase.Phase2)
                {
                    yield return new WaitForSeconds(0.15f);
                    Debug.Log($"[불가살이] {name}: 2페이즈 - 반대 방향 추가 돌진");
                    yield return StartCoroutine(DoChargeDash(-direction));
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
        /// 회전속도가 아니라 "걸리는 시간"을 고정하는 방식이라(Slerp의 t를 시간으로 진행),
        /// 남은 각도가 크든 작든 항상 duration초 만에 재조준이 끝나 일관된 느낌을 줍니다.
        /// </summary>
        private IEnumerator TurnBackTowardsPlayer(float duration)
        {
            if (_player == null || duration <= 0f) yield break;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) yield break;

            Quaternion startRot = transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);

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

                if (hitCount > 0)
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

            OverrideColor = _telegraphColor;
            PlayHopBounce(0.8f, 0.35f, 0.15f);
            PlayScalePunch(new Vector3(1.15f, 1.1f, 1.15f), 0.35f, 0.15f);
            _visualAnimator?.SetTrigger(kPowerfulAttackTrigger);
            yield return new WaitForSeconds(0.5f);
            OverrideColor = null;

            Debug.Log($"[불가살이] {name}: 철갑 {_armorRainCount}개를 주변에 낙하시킵니다");
            for (int i = 0; i < _armorRainCount; i++)
                SpawnArmorRainDrop();

            yield return new WaitForSeconds(_armorRainDuration);

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
