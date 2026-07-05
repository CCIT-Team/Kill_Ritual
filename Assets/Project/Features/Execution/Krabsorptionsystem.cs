// Assets/Project/Features/Player/KRAbsorptionSystem.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Enemies;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 흡혼 시스템 — 그로기 상태의 적을 처형하고 체력을 회복하는 전담 컴포넌트입니다.
    /// KRCombatSystem과 같은 Player 오브젝트에 붙입니다.
    ///
    /// [대상 탐색 방식]
    /// KRAbsorptionZone(Box Trigger)이 그로기 적을 감지하면
    /// E키 입력 시 그 중 가장 가까운 대상을 처형합니다.
    /// 판정 범위는 씬 뷰에서 Box Collider 크기를 직접 조절하면 됩니다.
    ///
    /// [시퀀스]
    ///   도움닫기(0.6s, 무적) → 돌입 처치(0.3s, 무적) → 회복(0.5s)
    /// </summary>
    public sealed class KRAbsorptionSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("흡혼 판정 박스 트리거 존. Player 하위의 AbsorptionZone 오브젝트를 연결하세요.")]
        [SerializeField] private KRAbsorptionZone _absorptionZone;

        [Tooltip("체력을 회복시킬 KRPlayerDamageFeedback. 비워두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private KRPlayerDamageFeedback _damageFeedback;

        [Tooltip("플레이어 이동을 담당하는 CharacterController. 비워두면 자동 탐색합니다.")]
        [SerializeField] private CharacterController _characterController;

        [Tooltip("플레이어 카메라. 히트스톱/카메라 킥에 사용합니다. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("흡혼 애니메이션을 재생할 Animator. 비워두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private Animator _animator;

        [Header("돌진 설정")]
        [Tooltip("적 앞에서 멈추는 거리. 너무 작으면 적을 관통합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _lungeStopDistance = 0.8f;

        [Tooltip("돌진 최소 프레임 수. 플레이어와 적이 가장 가까울 때 적용됩니다.")]
        [Min(1)]
        [SerializeField] private int _lungeMinFrames = 5;

        [Tooltip("돌진 최대 프레임 수. 플레이어와 적이 가장 멀 때 적용됩니다.")]
        [Min(1)]
        [SerializeField] private int _lungeMaxFrames = 10;

        [Tooltip("돌진 프레임 계산 기준 최대 거리(m). 이 거리 이상이면 무조건 최대 프레임이 적용됩니다.")]
        [Min(0.1f)]
        [SerializeField] private float _lungeMaxDistanceRef = 5f;

        [Header("카메라 킥 설정")]
        [Tooltip("처치 순간 카메라가 흔들리는 강도.")]
        [Min(0f)]
        [SerializeField] private float _cameraKickAmount = 3f;

        [Tooltip("카메라 킥이 복구되는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _cameraKickDuration = 0.15f;

        [Header("히트스톱 설정")]
        [Tooltip("처치 순간 시간이 느려지는 배율. 0에 가까울수록 완전 정지에 가깝습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hitStopTimeScale = 0.05f;

        [Tooltip("히트스톱 지속 시간(초, 실제 시간 기준).")]
        [Min(0.01f)]
        [SerializeField] private float _hitStopDuration = 0.08f;

        [Header("시퀀스 시간")]
        [Tooltip("도움닫기 구간 시간(초). 이 구간은 무적입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _windUpDuration = 0.6f;

        [Tooltip("돌입 처치 구간 시간(초). 이 구간은 무적입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _strikeDuration = 0.3f;

        [Tooltip("체력 회복 구간 시간(초). 이 구간은 무적이 풀립니다.")]
        [Min(0.01f)]
        [SerializeField] private float _recoveryDuration = 0.5f;

        [Header("체력 회복 설정 (적 등급별, 최대 체력 대비 %)")]
        [Range(0f, 1f)][SerializeField] private float _healRatioFodder = 0.10f;
        [Range(0f, 1f)][SerializeField] private float _healRatioHeavy = 0.20f;
        [Range(0f, 1f)][SerializeField] private float _healRatioElite = 0.30f;
        [Range(0f, 1f)][SerializeField] private float _healRatioBoss = 0.40f;

        [Header("위험 체력 보정")]
        [Tooltip("현재 체력이 최대 체력의 이 비율 이하일 때 최소 보장 체력선까지 추가 회복합니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dangerHealthRatio = 0.30f;

        [Tooltip("위험 체력 상태일 때 보장되는 최소 체력 비율.")]
        [Range(0f, 1f)]
        [SerializeField] private float _guaranteedHealthRatio = 0.50f;

        // Animator 파라미터 ID — string 비교 대신 int로 캐싱해 성능을 높입니다.
        private static readonly int kWindUpHash = Animator.StringToHash("AbsorptionWindUp");
        private static readonly int kStrikeHash = Animator.StringToHash("AbsorptionStrike");
        private static readonly int kRecoverHash = Animator.StringToHash("AbsorptionRecover");

        /// <summary>현재 처형 가능한 대상이 존 안에 있는지 여부. KRCombatDebugOverlay 참조용.</summary>
        public bool HasExecutableTarget => _absorptionZone != null && _absorptionZone.HasTarget;

        /// <summary>현재 흡혼 시퀀스가 실행 중인지 여부.</summary>
        public bool IsExecuting { get; private set; }

        private void Awake()
        {
            if (_damageFeedback == null)
                _damageFeedback = GetComponentInParent<KRPlayerDamageFeedback>();

            if (_absorptionZone == null)
                _absorptionZone = GetComponentInChildren<KRAbsorptionZone>();

            if (_characterController == null)
                _characterController = GetComponentInParent<CharacterController>();

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_animator == null)
                _animator = GetComponentInParent<Animator>();
        }

        private void Update()
        {
            if (IsExecuting) return;
            if (!Input.GetKeyDown(KeyCode.E)) return;

            IDamageable target = _absorptionZone?.GetNearestTarget();
            if (target == null) return;

            StartCoroutine(AbsorptionSequence(target));
        }

        // ── 흡혼 시퀀스 ────────────────────────────────────────────────

        private IEnumerator AbsorptionSequence(IDamageable target)
        {
            IsExecuting = true;

            // ① 도움닫기 — 무적 시작 + 애니메이션
            SetInvincible(true);
            _animator?.SetTrigger(kWindUpHash);

            yield return StartCoroutine(LungeToTarget(target));

            // 도움닫기 중 대상이 이미 죽었으면 중단
            if (target.IsDead)
            {
                SetInvincible(false);
                IsExecuting = false;
                yield break;
            }

            // ② 돌입 처치 — 히트스톱 + 카메라 킥 + 애니메이션
            EnemyGrade grade = GetGrade(target);
            target.Execute(KillRitual.Core.Interfaces.ExecutionSource.Absorption);
            _animator?.SetTrigger(kStrikeHash);

            StartCoroutine(HitStop());
            StartCoroutine(CameraKick());

            yield return new WaitForSeconds(_strikeDuration);

            // ③ 회복 — 무적 해제 + 애니메이션
            SetInvincible(false);
            _animator?.SetTrigger(kRecoverHash);
            ApplyHeal(CalculateHeal(grade));

            yield return new WaitForSeconds(_recoveryDuration);

            IsExecuting = false;
        }

        /// <summary>
        /// 도움닫기 — 플레이어를 적 바로 앞(_lungeStopDistance)까지 부드럽게 이동시킵니다.
        /// CharacterController.Move를 사용하므로 벽에 막히면 자연스럽게 멈춥니다.
        /// </summary>
        private IEnumerator LungeToTarget(IDamageable target)
        {
            if (_characterController == null)
            {
                yield return new WaitForSeconds(_windUpDuration);
                yield break;
            }

            Vector3 startPos = transform.position;

            // 적 위치에서 플레이어 방향으로 _lungeStopDistance만큼 떨어진 지점이 목표
            Vector3 dirToPlayer = (startPos - target.Position).normalized;
            Vector3 targetPos = target.Position + dirToPlayer * _lungeStopDistance;
            targetPos.y = startPos.y;

            // 거리에 따라 프레임 수 결정 (가까울수록 적은 프레임, 멀수록 많은 프레임)
            float distance = Vector3.Distance(startPos, targetPos);
            float distRatio = Mathf.Clamp01(distance / _lungeMaxDistanceRef);
            int frameCount = Mathf.RoundToInt(Mathf.Lerp(_lungeMinFrames, _lungeMaxFrames, distRatio));

            for (int frame = 0; frame < frameCount; frame++)
            {
                float t = Mathf.SmoothStep(0f, 1f, (float)(frame + 1) / frameCount);
                Vector3 desired = Vector3.Lerp(startPos, targetPos, t);
                Vector3 delta = desired - transform.position;

                _characterController.Move(delta);

                yield return null; // 한 프레임 대기
            }
        }

        /// <summary>처치 순간 Time.timeScale을 일시적으로 낮춰 히트스톱을 구현합니다.</summary>
        private IEnumerator HitStop()
        {
            Time.timeScale = _hitStopTimeScale;

            // WaitForSecondsRealtime — timeScale과 무관하게 실제 시간으로 대기합니다.
            yield return new WaitForSecondsRealtime(_hitStopDuration);

            Time.timeScale = 1f;
        }

        /// <summary>처치 순간 카메라를 아래로 킥했다가 복구합니다.</summary>
        private IEnumerator CameraKick()
        {
            if (_playerCamera == null) yield break;

            Transform camTransform = _playerCamera.transform;

            // 시작 시점의 로컬 회전을 저장합니다.
            Quaternion originalRotation = camTransform.localRotation;

            // 아래로 킥할 목표 회전
            Quaternion kickRotation = originalRotation * Quaternion.Euler(_cameraKickAmount, 0f, 0f);

            float elapsed = 0f;
            float halfDuration = _cameraKickDuration * 0.5f;

            // 전반부: 원래 → 킥 방향으로 이동
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                camTransform.localRotation = Quaternion.Lerp(originalRotation, kickRotation, t);
                yield return null;
            }

            elapsed = 0f;

            // 후반부: 킥 방향 → 원래 회전으로 복구
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                camTransform.localRotation = Quaternion.Lerp(kickRotation, originalRotation, t);
                yield return null;
            }

            // 완전히 원래 회전으로 복구
            camTransform.localRotation = originalRotation;
        }

        // ── 체력 회복 계산 ─────────────────────────────────────────────

        private float CalculateHeal(EnemyGrade grade)
        {
            if (_damageFeedback == null) return 0f;

            float maxHp = _damageFeedback.MaxHealth;
            float currentHp = _damageFeedback.CurrentHealth;
            float baseHeal = maxHp * GradeToHealRatio(grade);

            float dangerLine = maxHp * _dangerHealthRatio;
            float guaranteedLine = maxHp * _guaranteedHealthRatio;

            if (currentHp < dangerLine)
                return Mathf.Max(baseHeal, guaranteedLine - currentHp);

            return baseHeal;
        }

        private float GradeToHealRatio(EnemyGrade grade) => grade switch
        {
            EnemyGrade.Fodder => _healRatioFodder,
            EnemyGrade.Heavy => _healRatioHeavy,
            EnemyGrade.Elite => _healRatioElite,
            EnemyGrade.Boss => _healRatioBoss,
            _ => _healRatioFodder,
        };

        private void ApplyHeal(float amount)
        {
            if (_damageFeedback == null || amount <= 0f) return;
            _damageFeedback.Heal(amount);
        }

        // ── 등급/무적 ──────────────────────────────────────────────────

        private static EnemyGrade GetGrade(IDamageable target)
            => target is KREnemyBase enemy ? enemy.Grade : EnemyGrade.Fodder;

        private void SetInvincible(bool invincible)
            => _damageFeedback?.SetInvincible(invincible);
    }

    /// <summary>적 등급 열거형. KREnemyBase와 KRAbsorptionSystem이 공유합니다.</summary>
    public enum EnemyGrade
    {
        Fodder,  // 잡졸
        Heavy,   // 튼튼한 잡졸
        Elite,   // 갑사/장령
        Boss     // 보스
    }
}