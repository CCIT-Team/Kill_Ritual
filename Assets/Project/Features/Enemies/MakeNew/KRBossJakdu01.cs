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
    /// 꼬일 위험이 있어 유지했습니다), 내용은 전면 재설계된 "불가살이" 기획으로 완전히 교체했습니다.
    ///
    /// [2026-07-07 전면 재작성 — 불가살이 기획]
    /// 콘셉트: 쇠를 먹고 자라 철갑을 두른 불사의 요수. 평상시엔 몸통에 거의 피해를 못 주지만,
    /// 특정 공격 패턴이 끝난 직후 그 패턴과 연관된 부위(어깨/코/머리·앞다리/등)가 잠깐 노출되고,
    /// 그 부위만 정상적으로 피해를 줄 수 있습니다. 회피만으로는 전투가 진행되지 않고,
    /// "패턴을 피한 뒤 노출된 부위로 빠르게 이동해서 반격"하는 리듬을 요구합니다.
    ///
    /// 이전(작두 보스) 설계에 있던 오행 약점 게이트/작두 처형 연동/무령 패링 피니시/잡몹 소환은
    /// 전부 이번 기획과 맞지 않아 제거했습니다. 부위별 약점은 KRBossBodyPart(신규,
    /// Assets/Project/Features/Enemies/MakeNew/KRBossBodyPart.cs)가 전담합니다 — 이 클래스는
    /// "언제 어느 부위를 노출시킬지"만 결정하고, 실제 피격 판정/피해 감쇠는 그쪽에 위임합니다.
    ///
    /// [페이즈 구성]
    /// 1페이즈(100%~50%): 철갑 발사 / 코 채찍 / 돌진 중 랜덤 순환.
    /// 2페이즈(50% 이하): 위 세 패턴이 전부 강화되고, 신규 패턴 "철갑 폭우"가 추가되며,
    ///                     패턴 사이 쿨다운이 짧아집니다(공격 빈도 증가).
    /// </summary>
    public sealed class KRBossJakdu01 : KREnemyBase
    {
        private enum BossPhase { Phase1, Phase2 }

        [Header("페이즈 전환")]
        [Tooltip("이 체력 비율 이하로 내려가면 2페이즈(강화)로 전환합니다.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float _phase2HealthRatio = 0.5f;

        [Header("몸통 방어 (부위 아닌 곳)")]
        [Tooltip("부위별 약점(어깨/코/머리·앞다리/등)이 아닌 몸통에 직접 맞았을 때 받는 피해 비율. " +
                 "0.02 = 거의 무적. 완전 0으로 두면 아예 안 깎입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _generalBodyArmorRatio = 0.02f;

        [Tooltip("몸통(부위 아닌 곳)에 맞았을 때도 '막혔다'는 걸 보여줄 VFX. " +
                 "Assets/Project/Art/VFX/MetalImpacts.prefab 추천. 비워두면 표시 없음.")]
        [SerializeField] private GameObject _armorBlockVfxPrefab;

        [Header("부위 (KRBossBodyPart)")]
        [Tooltip("패턴1(철갑 발사) 종료 후 노출되는 왼쪽 어깨.")]
        [SerializeField] private KRBossBodyPart _shoulderL;
        [Tooltip("패턴1(철갑 발사) 종료 후 노출되는 오른쪽 어깨.")]
        [SerializeField] private KRBossBodyPart _shoulderR;
        [Tooltip("패턴2(코 채찍) 종료 후 노출되는 코.")]
        [SerializeField] private KRBossBodyPart _trunk;
        [Tooltip("패턴3(돌진) 종료 후 노출되는 머리.")]
        [SerializeField] private KRBossBodyPart _head;
        [Tooltip("패턴3(돌진) 종료 후 노출되는 앞다리.")]
        [SerializeField] private KRBossBodyPart _frontLegs;
        [Tooltip("신규 패턴(철갑 폭우, 2페이즈 전용) 종료 후 노출되는 등. 가장 긴 약점 노출 구간입니다.")]
        [SerializeField] private KRBossBodyPart _back;

        [Header("이동 / 패턴 진행")]
        [Tooltip("[2026-07-07 추가] 초당 회전 각도(도). 예전엔 매 프레임 즉시 플레이어 쪽으로 스냅해서 " +
                 "플레이어가 등/옆으로 돌아갈 방법이 아예 없었습니다(항상 정면이 즉시 플레이어를 향함). " +
                 "이 값을 유한하게 두면 거대한 몸집답게 천천히 돌게 되고, 플레이어가 실제로 등 약점 " +
                 "뒤로 돌아가서 때릴 수 있게 됩니다.")]
        [Min(10f)]
        [SerializeField] private float _turnSpeedDegreesPerSecond = 120f;

        [Tooltip("플레이어와 이 거리보다 멀면 접근하고, 가까우면 패턴을 고릅니다.")]
        [Min(1f)]
        [SerializeField] private float _preferredDistance = 9f;

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
        [Min(0.1f)] [SerializeField] private float _shoulderExposeDuration = 4f;
        [SerializeField] private LayerMask _shardHitLayerMask = ~0;
        [SerializeField] private LayerMask _shardDamageableLayerMask = ~0;
        [Tooltip("2페이즈: 바닥에 꽂힌 철갑이 터지기까지의 지연 시간(초).")]
        [Min(0.1f)] [SerializeField] private float _shardExplodeDelay = 1.5f;
        [Min(0.1f)] [SerializeField] private float _shardExplosionRadius = 2.5f;

        [Header("패턴2 - 코 채찍")]
        [Min(0.05f)] [SerializeField] private float _trunkWindup = 0.6f;
        [Min(0.5f)] [SerializeField] private float _trunkStrikeRange = 6f;
        [Tooltip("정면 기준 판정 각도(도). 60이면 좌우 합쳐 120도 범위.")]
        [Range(1f, 90f)] [SerializeField] private float _trunkStrikeHalfAngle = 60f;
        [Min(0f)] [SerializeField] private float _trunkDamage = 25f;
        [Min(0.1f)] [SerializeField] private float _trunkExposeDuration = 2f;
        [Tooltip("2페이즈 3연타의 마지막 타격 이후 노출 시간(더 깁니다).")]
        [Min(0.1f)] [SerializeField] private float _trunkExposeDurationPhase2 = 3.5f;
        [Tooltip("연속 타격 사이의 간격(초, 2페이즈 3연타용).")]
        [Min(0.05f)] [SerializeField] private float _trunkComboInterval = 0.35f;

        [Header("패턴3 - 돌진")]
        [Min(0.1f)] [SerializeField] private float _chargeWindup = 1f;
        [Min(1f)] [SerializeField] private float _chargeSpeed = 15f;
        [Min(1f)] [SerializeField] private float _chargeMaxDistance = 20f;
        [Min(0f)] [SerializeField] private float _chargeDamage = 30f;
        [Tooltip("[2026-07-07 변경] 더 이상 안 씀 — 예전엔 이 반경으로 거리 기반 판정을 했지만, " +
                 "이제 _chargeHitbox(실제 Trigger 콜라이더)가 판정을 전담합니다. 하위호환을 위해 " +
                 "필드는 남겨뒀습니다.")]
        [Min(0.5f)] [SerializeField] private float _chargeHitRadius = 2.5f;
        [Tooltip("벽 감지용 레이어 — 플레이어/적 레이어는 반드시 제외하세요(자기 자신이나 " +
                 "플레이어에 막혀 멈추면 안 되므로). 지형/벽 레이어만 포함.")]
        [SerializeField] private LayerMask _chargeWallLayerMask = ~0;
        [Tooltip("[2026-07-07 추가] 돌진 전용 피해 판정 콜라이더(KRBossChargeHitbox). " +
                 "예전엔 Vector3.Distance로 '플레이어가 이 반경 안이면 맞은 것'으로 대충 판정했는데, " +
                 "실제 몸통 모양/방향과 안 맞아서 부정확했습니다. 이제 이 콜라이더가 돌진 중에만 " +
                 "켜져서 정확한 Trigger 판정을 합니다.")]
        [SerializeField] private KRBossChargeHitbox _chargeHitbox;
        [Min(0.1f)] [SerializeField] private float _headFrontLegsExposeDuration = 3f;
        [Min(0.1f)] [SerializeField] private float _wallStunDuration = 1.5f;

        [Header("신규 패턴(2페이즈 전용) - 철갑 폭우")]
        [Min(1)] [SerializeField] private int _armorRainCount = 10;
        [Min(1f)] [SerializeField] private float _armorRainRadius = 8f;
        [Min(0f)] [SerializeField] private float _armorRainDamage = 10f;
        [Min(0.1f)] [SerializeField] private float _armorRainFallSpeed = 12f;
        [Min(0.1f)] [SerializeField] private float _armorRainDuration = 1.8f;
        [Min(0.1f)] [SerializeField] private float _backExposeDuration = 6f;

        [Header("시각 신호")]
        [Tooltip("모든 패턴 예고(윈드업) 구간 동안 표시할 경고색.")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.5f, 0f, 1f);

        [Header("애니메이션")]
        [Tooltip("마스토돈 모델(MastodonVisual)에 붙일 Animator. 비워두면 자식에서 자동으로 찾습니다. " +
                 "01_Take001.fbx(걷기 사이클) 하나뿐이라 별도 Idle 클립 없이 이동 중에만 " +
                 "Speed=1로 재생하고, 멈추면 Speed=0으로 그 자리에서 정지시키는 방식으로 씁니다.")]
        [SerializeField] private Animator _visualAnimator;
        private static readonly int kSpeedParam = Animator.StringToHash("Speed");

        // [2026-07-07 제거] 뼈대(UniqueID_513 체인) 공격 애니메이션 트리거 실행 코드는
        // 재생 시 발작하듯 보이는 문제 이후 제거했습니다. KRBossAttackSwipe01.anim /
        // KRBossMastodon.controller의 AttackSwipe 상태 자체는 에셋으로는 남아있지만
        // (임의로 에셋을 삭제하지 않기 위함), 더 이상 코드에서 호출하지 않습니다.

        [Header("공격 모션 (프로시저럴)")]
        [Tooltip("[2026-07-07 추가] 진짜 스켈레탈(뼈대) 공격 애니메이션 클립은 손으로 키프레임을 " +
                 "찍어야 하는 작업이라(Blender/Unity Animation 창처럼 눈으로 보면서 만드는 도구가 필요) " +
                 "텍스트 기반으로는 만들 수 없습니다. 대신 몸통 전체를 스케일/위치로 움찔거리게 만들어 " +
                 "'준비 동작 → 타격 순간'의 느낌을 코드로 흉내냅니다(스쿼시-스트레치). " +
                 "회전(Rotation)은 안 건드립니다 — FacePlayer()가 매 프레임 플레이어를 향해 돌리는 것과 " +
                 "충돌하기 때문입니다.")]
        [SerializeField] private bool _enableProceduralAttackMotion = true;
        private Vector3 _bodyBaseScale = Vector3.one;

        private BossPhase _phase = BossPhase.Phase1;
        private bool _isPatternActive;
        private float _patternActiveSince;
        private int _lastPatternIndex = -1;
        private float _nextPatternTime;
        private bool _lastChargeHitWall;

        [Tooltip("[2026-07-07 추가] '패턴진행중=True'가 이 시간(초)보다 오래 지속되면 강제로 " +
                 "초기화합니다. 정상적인 패턴은 몇 초 안에 끝나므로, 이 이상 걸리면 코루틴이 " +
                 "예외로 죽었거나(플레이 모드 중 스크립트 수정 → 도메인 리로드로 코루틴이 끊기는 " +
                 "경우가 대표적) 뭔가 멈춰버린 것으로 보고 강제 복구합니다. 이게 없으면 그 뒤로 " +
                 "보스가 영원히 그 자리에 멈춰서 이동도 패턴도 안 하게 됩니다.")]
        [Min(3f)]
        [SerializeField] private float _patternStuckTimeoutSeconds = 12f;

        protected override void Awake()
        {
            base.Awake();

            if (_visualAnimator == null)
                _visualAnimator = GetComponentInChildren<Animator>();

            _bodyBaseScale = transform.localScale;

            // [2026-07-07 추가] "이동을 안 한다"는 문제의 두 번째 용의자 — Apply Root Motion이 켜져
            // 있으면, 애니메이터가 매 프레임 애니메이션 클립(01_Take001.fbx, 제자리 걷기 루프)의
            // 루트 이동값으로 transform 위치를 덮어써버려서, NavMeshAgent나 돌진 코드가 옮긴 위치가
            // 무효화될 수 있습니다. 이 경고가 뜨면 Animator 인스펙터에서 Apply Root Motion을 꺼주세요.
            if (_visualAnimator != null && _visualAnimator.applyRootMotion)
                Debug.LogWarning($"[불가살이] {name}: Animator의 Apply Root Motion이 켜져 있습니다 — " +
                                  "제자리 걷기 애니메이션이 이동 코드가 옮긴 위치를 매 프레임 덮어써서 " +
                                  "보스가 안 움직이는 것처럼 보일 수 있습니다. 꺼주세요.");
        }

        // ── KREnemyBase 추상 메서드 구현 ─────────────────────────────────

        protected override void UpdateChase()
        {
            FacePlayer(_turnSpeedDegreesPerSecond);
            TickBossLogic();
        }

        protected override void UpdateAttack()
        {
            FacePlayer(_turnSpeedDegreesPerSecond);
            TickBossLogic();
        }

        /// <summary>
        /// 매 프레임 호출됩니다. 패턴이 진행 중이면 아무 것도 하지 않고(각 패턴 코루틴이 알아서
        /// 처리), 아니면 거리에 따라 접근하거나 쿨다운이 다 됐을 때 랜덤 패턴을 시작합니다.
        /// </summary>
        private float _nextMoveDebugLogTime;

        private void TickBossLogic()
        {
            // [2026-07-07 추가] "경고는 안 뜨는데 왜 안 움직이냐" 문제 — 경고 3종(Idle 고착/NavMesh
            // 이탈/Root Motion)은 전부 "조건이 참일 때만" 뜨는 것들이라, 그 셋 다 원인이 아니면
            // 아무것도 안 뜨는 게 당연합니다. 그래서 조건 없이 1초에 한 번씩 실제 내부 상태를
            // 그대로 찍어서, 정확히 어느 분기로 빠지는지 눈으로 보게 합니다.
            if (Time.time >= _nextMoveDebugLogTime)
            {
                _nextMoveDebugLogTime = Time.time + 1f;
                float d = _player != null ? DistanceToPlayer() : -1f;
                Debug.Log($"[불가살이/이동진단] 거리={d:F1} (기준 {_preferredDistance}) " +
                          $"패턴진행중={_isPatternActive} agent활성={(_agent != null && _agent.enabled)} " +
                          $"onNavMesh={(_agent != null && _agent.isOnNavMesh)} " +
                          $"agent속도={(_agent != null ? _agent.velocity.magnitude : -1f):F2} " +
                          $"pathStatus={(_agent != null ? _agent.pathStatus.ToString() : "N/A")} " +
                          $"hasPath={(_agent != null && _agent.hasPath)} " +
                          $"pathPending={(_agent != null && _agent.pathPending)} " +
                          $"isStopped={(_agent != null && _agent.isStopped)} " +
                          $"위치={transform.position}");
            }

            if (_isPatternActive)
            {
                // [2026-07-07 추가] 실제로 발견된 버그 — 패턴 코루틴이 (주로 플레이 모드 중
                // 스크립트를 수정해서 생기는 도메인 리로드로) 중간에 끊기면 _isPatternActive가
                // 영원히 true로 남아서 그 뒤로 보스가 이동도 패턴도 전혀 안 하게 됩니다.
                // 정상 패턴은 몇 초면 끝나므로, 타임아웃을 넘기면 강제로 복구합니다.
                if (Time.time - _patternActiveSince > _patternStuckTimeoutSeconds)
                {
                    Debug.LogWarning($"[불가살이] {name}: 패턴이 {_patternStuckTimeoutSeconds}초 넘게 " +
                                      "끝나지 않아 강제로 복구합니다 (코루틴이 죽었던 것으로 추정 — " +
                                      "플레이 모드 중 스크립트 수정 시 자주 발생).");
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
                _visualAnimator?.SetFloat(kSpeedParam, 1f);
                return;
            }

            StopMoving();
            _visualAnimator?.SetFloat(kSpeedParam, 0f);

            if (Time.time < _nextPatternTime) return;

            StartCoroutine(RunRandomPattern());
        }

        // ── 페이즈 전환 ──────────────────────────────────────────────────

        /// <summary>[2026-07-07] KREnemyBase 훅 오버라이드. 체력 임계값으로 2페이즈 전환을 감지합니다.</summary>
        protected override void OnHealthChanged(float ratio)
        {
            if (_phase == BossPhase.Phase1 && ratio <= _phase2HealthRatio)
            {
                _phase = BossPhase.Phase2;
                Debug.Log($"[불가살이] {name}: 2페이즈 진입 (체력 {ratio:P0}) — 공격 속도 증가, " +
                          "철갑 발사 폭발/코 채찍 3연타/돌진 연속/철갑 폭우 해금");
            }
        }

        /// <summary>
        /// [2026-07-07] 몸통(부위가 아닌 곳)에 직접 맞았을 때만 적용되는 방어 게이트입니다.
        /// 부위별 피해(KRBossBodyPart)는 TakeDamageDirect()로 별도 처리되어 이 훅을 거치지 않습니다.
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
                // [2026-07-07 추가] KRBossBodyPart와 동일하게, VFX 프리팹이 없어도 흰 구체 오브젝트로
                // 즉석에서 "막혔다"는 반응을 보여줍니다. 준비물 없이 바로 동작합니다.
                SpawnProceduralArmorFlash(context.HitPoint);
            }

            return context.DamageAmount * _generalBodyArmorRatio;
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

            // [2026-07-07 변경] 거리 기준을 패턴 선택에 반영하려면 "패턴을 고르는 그 순간"의
            // 거리가 필요합니다. TickBossLogic()이 이미 한 번 거리를 재고 호출하지만, 코루틴이
            // 실제로 시작되는 시점(다음 프레임)엔 값이 미세하게 달라질 수 있어 여기서 다시 잽니다.
            float distance = DistanceToPlayer();
            int index = PickPatternIndex(distance);
            yield return StartCoroutine(GetPatternCoroutine(index));

            float cooldown = _patternCooldown * (_phase == BossPhase.Phase2 ? _phase2CooldownMultiplier : 1f);
            _nextPatternTime = Time.time + cooldown;
            _isPatternActive = false;
        }

        /// <summary>
        /// [2026-07-07 변경] "거리 기준으로 패턴을 고르되 2연속은 안 되게" — 두 조건을 함께 봅니다.
        /// 1) 거리 조건: IsPatternViableAtDistance()로 지금 거리에서 의미가 있는 패턴만 후보에 넣습니다
        ///    (예: 코앞에 있는데 돌진을 쓰면 어색하고, 멀리 있는데 코 채찍을 쓰면 허공을 침).
        /// 2) 2연속 방지: 방금 쓴 패턴(_lastPatternIndex)은 후보에서 제외합니다.
        /// 두 조건을 동시에 만족하는 패턴이 하나도 없으면(거리가 애매한 경우) 2연속 방지만이라도
        /// 지켜서 고르고, 그마저도 없으면 최후의 수단으로 아무거나 고릅니다 — 보스가 멈춰버리는
        /// 것보단 낫습니다.
        /// </summary>
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
        /// [2026-07-07 추가] 패턴별 "이 거리에서 쓰는 게 말이 되는가"를 판단합니다.
        /// - 철갑 발사(0): 원거리 무기라 거리 제한 없음.
        /// - 코 채찍(1): 근접 패턴 — 실제 타격 사거리(_trunkStrikeRange) 안일 때만.
        /// - 돌진(2): 거리를 좁히는 패턴 — 코 채찍 사거리보다 멀 때만(가까우면 굳이 안 씀).
        /// - 철갑 폭우(3, 2페이즈): 범위 공격 — 범위(_armorRainRadius) 안일 때만(너무 멀면 허공에 낭비).
        /// </summary>
        private bool IsPatternViableAtDistance(int index, float distance)
        {
            switch (index)
            {
                case 0: return true;
                case 1: return distance <= _trunkStrikeRange;
                case 2: return distance > _trunkStrikeRange;
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

        /// <summary>
        /// 몸 전체를 targetScaleMultiplier(기준 스케일에 곱하는 배율)까지 toDuration 동안 갔다가,
        /// backDuration 동안 원래 스케일로 돌아옵니다. 예: (0.85, 1.15, 0.85)면 앞뒤로 눌리고
        /// 위아래로 늘어나는 "웅크림" 느낌, (1.2, 0.9, 1.2)면 "부풀었다 터지는" 느낌을 냅니다.
        /// 회전은 절대 건드리지 않습니다(FacePlayer와 충돌 방지).
        /// </summary>
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

        /// <summary>제자리에서 살짝 위로 뛰었다 내려오는 모션 — "철갑 폭우" 같은 준비 동작에 씁니다.
        /// position만 건드리고 끝나면 정확히 원래 높이로 복귀시킵니다.</summary>
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

        /// <summary>
        /// [2026-07-07 수정] "발작하듯 재시작" 버그의 원인 중 하나 — 이전에 실행 중이던 스케일
        /// 펀치가 끝나기 전에 새 펀치가 또 StartCoroutine되면, 여러 코루틴이 동시에 transform.localScale을
        /// 서로 다른 목표값으로 매 프레임 덮어쓰면서 몸이 부들부들 떠는 것처럼 보입니다. 이제 새 펀치를
        /// 시작하기 전에 이전 코루틴을 확실히 멈춥니다.
        /// </summary>
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
            // 준비 동작: 살짝 웅크렸다가(스쿼시) 쏘는 순간 팡 튀는 손맛.
            PlayScalePunch(new Vector3(1.08f, 0.9f, 1.08f), 0.35f, 0.05f);
            yield return new WaitForSeconds(0.35f);
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.92f, 1.12f, 0.92f), 0.08f, 0.2f);

            FireShardsFromMuzzle(_shoulderLMuzzle);
            FireShardsFromMuzzle(_shoulderRMuzzle);

            _shoulderL?.SetExposed(true);
            _shoulderR?.SetExposed(true);
            Debug.Log($"[불가살이] {name}: 양 어깨 노출 ({_shoulderExposeDuration}초) - " +
                      (_phase == BossPhase.Phase2 ? "바닥에 꽂힌 철갑이 곧 폭발합니다" : "지금이 공격 타이밍"));

            yield return new WaitForSeconds(_shoulderExposeDuration);

            _shoulderL?.SetExposed(false);
            _shoulderR?.SetExposed(false);
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

        // ── 패턴 2: 코 채찍 ──────────────────────────────────────────────

        private IEnumerator Pattern_TrunkWhip()
        {
            int swings = _phase == BossPhase.Phase2 ? 3 : 1;
            Debug.Log($"[불가살이] {name}: 패턴2 - 코 채찍 ({swings}연타)");

            for (int i = 0; i < swings; i++)
            {
                OverrideColor = _telegraphColor;
                // 준비: 코를 움츠리듯 앞뒤로 살짝 눌렸다가(코일링) 타격 순간 쭉 뻗는 스트레치.
                PlayScalePunch(new Vector3(1.05f, 0.95f, 0.9f), _trunkWindup * 0.8f, _trunkWindup * 0.2f);
                yield return new WaitForSeconds(_trunkWindup);
                OverrideColor = null;
                PlayScalePunch(new Vector3(0.95f, 1.02f, 1.15f), 0.06f, 0.15f);

                TryHitTrunkStrike();
                Debug.Log($"[불가살이] {name}: 코 채찍 {i + 1}/{swings}타 적중 판정");

                if (i < swings - 1)
                    yield return new WaitForSeconds(_trunkComboInterval);
            }

            float exposeDuration = _phase == BossPhase.Phase2 ? _trunkExposeDurationPhase2 : _trunkExposeDuration;
            _trunk?.SetExposed(true);
            Debug.Log($"[불가살이] {name}: 코 노출 ({exposeDuration}초)");

            yield return new WaitForSeconds(exposeDuration);

            _trunk?.SetExposed(false);
        }

        private void TryHitTrunkStrike()
        {
            if (_player == null) return;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > _trunkStrikeRange) return;
            if (distance <= 0.0001f) return;

            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > _trunkStrikeHalfAngle) return;

            IDamageable target = FindPlayerDamageable(_player);
            if (target == null || target.IsDead) return;

            var context = new KRDamageContext(_trunkDamage, KRDamageType.Fire, _player.position, toPlayer.normalized);
            target.TakeDamage(context);
        }

        // ── 패턴 3: 돌진 ────────────────────────────────────────────────

        private IEnumerator Pattern_Charge()
        {
            Debug.Log($"[불가살이] {name}: 패턴3 - 돌진 준비 ({_chargeWindup}초 차징)");

            OverrideColor = _telegraphColor;
            // 준비: 몸을 낮게 웅크렸다가(돌진 직전 자세) 개시 순간 앞으로 쭉 뻗는 스트레치.
            PlayScalePunch(new Vector3(1.1f, 0.8f, 0.95f), _chargeWindup * 0.85f, _chargeWindup * 0.15f);
            yield return new WaitForSeconds(_chargeWindup);
            OverrideColor = null;
            PlayScalePunch(new Vector3(0.9f, 1.05f, 1.2f), 0.1f, 0.25f);

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
                    // [2페이즈 강화] 벽에 부딪힌 직후 반대 방향으로 한 번 더 즉시 돌진합니다.
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

            Debug.Log($"[불가살이] {name}: 머리/앞다리 노출 ({_headFrontLegsExposeDuration}초)");
            _head?.SetExposed(true);
            _frontLegs?.SetExposed(true);

            yield return new WaitForSeconds(_headFrontLegsExposeDuration);

            _head?.SetExposed(false);
            _frontLegs?.SetExposed(false);
        }

        /// <summary>
        /// NavMeshAgent를 잠시 멈추고 transform을 직접 이동시켜 빠른 직선 돌진을 구현합니다.
        /// 레이캐스트로 전방 벽을 감지해 부딪히면 즉시 멈추고 _lastChargeHitWall을 true로 남깁니다.
        /// </summary>
        private IEnumerator DoChargeDash(Vector3 direction)
        {
            _lastChargeHitWall = false;

            bool agentWasEnabled = _agent != null && _agent.enabled;
            if (agentWasEnabled) _agent.isStopped = true;

            // [2026-07-07 변경] 거리 기반 TryHitChargeCollision() 대신 실제 Trigger 콜라이더로
            // 판정합니다. 돌진 시작 시 켜고, 끝나면(정상 종료든 벽 충돌이든) 반드시 끕니다.
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
        }

        // ── 신규 패턴(2페이즈): 철갑 폭우 ────────────────────────────────

        private IEnumerator Pattern_ArmorRainstorm()
        {
            Debug.Log($"[불가살이] {name}: 신규 패턴 - 철갑 폭우");

            OverrideColor = _telegraphColor;
            // 준비: 뒷다리로 살짝 일어서듯 튀어오르는 모션 — "하늘로 철갑을 불러낸다"는 느낌.
            PlayHopBounce(0.8f, 0.35f, 0.15f);
            PlayScalePunch(new Vector3(1.15f, 1.1f, 1.15f), 0.35f, 0.15f);
            yield return new WaitForSeconds(0.5f);
            OverrideColor = null;

            Debug.Log($"[불가살이] {name}: 철갑 {_armorRainCount}개를 주변에 낙하시킵니다");
            for (int i = 0; i < _armorRainCount; i++)
                SpawnArmorRainDrop();

            yield return new WaitForSeconds(_armorRainDuration);

            Debug.Log($"[불가살이] {name}: 철갑 폭우 종료 - 등 노출 ({_backExposeDuration}초, 최대 약점 구간)");
            _back?.SetExposed(true);

            yield return new WaitForSeconds(_backExposeDuration);

            _back?.SetExposed(false);
        }

        private void SpawnArmorRainDrop()
        {
            if (_armorShardPrefab == null) return;

            Vector2 randomCircle = Random.insideUnitCircle * _armorRainRadius;
            Vector3 targetPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Vector3 spawnPos = targetPos + Vector3.up * 15f;

            GameObject instance = Instantiate(_armorShardPrefab.gameObject, spawnPos, Quaternion.identity);
            KRBossArmorShard shard = instance.GetComponent<KRBossArmorShard>();

            // 낙하 자체가 위협이므로(떨어지는 도중 플레이어를 맞추면 즉시 피해) 폭발 옵션은 끕니다.
            shard?.Launch(Vector3.down * _armorRainFallSpeed, _armorRainDamage,
                _shardHitLayerMask, _shardDamageableLayerMask, this, willExplode: false);
        }
    }
}
