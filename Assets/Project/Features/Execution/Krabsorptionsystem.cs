// Assets/Project/Features/Player/KRAbsorptionSystem.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Enemies;

namespace KillRitual.Player.Combat
{
    public sealed class KRAbsorptionSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("흡혼 판정 박스 트리거 존. Player 하위의 AbsorptionZone 오브젝트를 연결하세요.")]
        [SerializeField] private KRAbsorptionZone _absorptionZone;

        [Tooltip("체력을 회복시킬 KRPlayerDamageFeedback. 비워두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private KRPlayerDamageFeedback _damageFeedback;

        [Tooltip("플레이어 이동을 담당하는 CharacterController. 비워두면 자동 탐색합니다.")]
        [SerializeField] private CharacterController _characterController;

        [Tooltip("플레이어 카메라. 히트스톱/카메라 킥/FOV 연출에 사용합니다. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("흡혼 애니메이션을 재생할 Animator. 비워두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private Animator _animator;

        [Tooltip("플레이어의 KRCombatSystem. 흡혼 중 장착 무기 모델을 숨기는 데 사용합니다.")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Header("돌진 설정")]
        [Tooltip("적 앞에서 멈추는 거리.")]
        [Min(0.1f)]
        [SerializeField] private float _lungeStopDistance = 1.3f;

        [Tooltip("돌진 최소 프레임 수. 플레이어와 적이 가장 가까울 때 적용됩니다.")]
        [Min(1)]
        [SerializeField] private int _lungeMinFrames = 5;

        [Tooltip("돌진 최대 프레임 수. 플레이어와 적이 가장 멀 때 적용됩니다.")]
        [Min(1)]
        [SerializeField] private int _lungeMaxFrames = 10;

        [Tooltip("돌진 프레임 계산 기준 최대 거리(m). 이 거리 이상이면 무조건 최대 프레임이 적용됩니다.")]
        [Min(0.1f)]
        [SerializeField] private float _lungeMaxDistanceRef = 5f;

        [Header("타격 싱크 설정")]
        [Tooltip("적 사망 애니메이션과 플레이어 Strike 애니메이션을 도착 몇 프레임 전에 미리 시작할지 정합니다. 적 리액션/주먹 신전이 3프레임 뒤에 나오면 3으로 둡니다.")]
        [Min(0)]
        [SerializeField] private int _preImpactLeadFrames = 3;

        [Tooltip("돌진이 끝나는 프레임에 슬로우모션과 카메라 킥을 시작합니다.")]
        [SerializeField] private bool _playImpactEffectOnLungeEnd = true;

        [Header("카메라 시선 보정")]
        [Tooltip("돌진 중 Player 몸체의 Y축만 적 방향으로 돌립니다. Animator와 손 리그는 건드리지 않습니다.")]
        [SerializeField] private bool _lookAtTargetDuringLunge = true;

        [Tooltip("타격 선행 프레임에 Player 몸체 방향을 적에게 즉시 맞춥니다.")]
        [SerializeField] private bool _snapYawBeforeStrike = true;

        [Tooltip("몸체가 적 방향으로 돌아가는 속도입니다.")]
        [Min(1f)]
        [SerializeField] private float _bodyYawRotateSpeed = 1440f;

        [Tooltip("적 위치에서 어느 정도 위를 볼지 정합니다. 적 Position이 발밑이면 1.0~1.3 권장.")]
        [SerializeField] private float _lookTargetHeightOffset = 1.1f;

        [Header("돌진 FOV 연출")]
        [Tooltip("돌진 순간 FOV를 올려 가속감을 줍니다.")]
        [SerializeField] private bool _useLungeFovBoost = true;

        [Tooltip("기본 FOV에 더할 값입니다. 기본 FOV 60이면 75가 됩니다.")]
        [Min(0f)]
        [SerializeField] private float _lungeFovAdd = 15f;

        [Tooltip("FOV가 원래 값으로 돌아오는 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _fovRecoverDuration = 0.12f;

        [Header("카메라 킥 설정")]
        [Tooltip("처치 순간 카메라가 흔들리는 강도.")]
        [Min(0f)]
        [SerializeField] private float _cameraKickAmount = 5f;

        [Tooltip("카메라 킥이 복구되는 시간(초, 실제 시간 기준).")]
        [Min(0.01f)]
        [SerializeField] private float _cameraKickDuration = 0.2f;

        [Header("킬 슬로우모션 설정")]
        [Tooltip("처치 순간 킬 슬로우모션을 사용할지 여부입니다.")]
        [SerializeField] private bool _useKillSlowMotion = true;

        [Tooltip("가장 느려졌을 때의 시간 배율입니다. 0.10이면 90% 느려진 상태입니다.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _killSlowMinScale = 0.10f;

        [Tooltip("최소 시간 배율까지 내려가는 시간입니다. 60fps 기준 5프레임은 약 0.083초입니다.")]
        [Min(0.001f)]
        [SerializeField] private float _killSlowEnterDuration = 0.083f;

        [Tooltip("가장 느린 상태를 유지하는 시간입니다. 처형 순간을 0.5초 정도 보여주려면 0.5로 둡니다.")]
        [Min(0f)]
        [SerializeField] private float _killSlowHoldDuration = 0.50f;

        [Tooltip("원래 속도로 돌아오는 시간입니다.")]
        [Min(0.001f)]
        [SerializeField] private float _killSlowRecoverDuration = 0.20f;

        [Header("시퀀스 시간")]
        [Tooltip("도움닫기 구간 시간(초). CharacterController가 없을 때 대기 시간으로 사용됩니다.")]
        [Min(0.01f)]
        [SerializeField] private float _windUpDuration = 0.01f;

        [Tooltip("타격 후 Recover로 넘어가기 전 최소 유지 시간입니다. 실제 시간 기준으로 처리됩니다.")]
        [Min(0.01f)]
        [SerializeField] private float _strikeDuration = 0.3f;

        [Tooltip("체력 회복 구간 시간(초, 실제 시간 기준).")]
        [Min(0.01f)]
        [SerializeField] private float _recoveryDuration = 0.01f;

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

        // Animator 파라미터 ID — 기존 코드 유지
        private static readonly int kWindUpHash = Animator.StringToHash("AbsorptionWindUp");
        private static readonly int kStrikeHash = Animator.StringToHash("AbsorptionStrike");
        private static readonly int kRecoverHash = Animator.StringToHash("AbsorptionRecover");

        public bool HasExecutableTarget => _absorptionZone != null && _absorptionZone.HasTarget;
        public bool IsExecuting { get; private set; }

        private IDamageable _pendingImpactTarget;
        private EnemyGrade _pendingTargetGrade = EnemyGrade.Fodder;

        private bool _preImpactStarted;
        private bool _impactMomentPlayed;
        private bool _isLunging;

        private float _defaultCameraFov = 60f;
        private Coroutine _fovRoutine;

        private Coroutine _killSlowRoutine;
        private float _defaultFixedDeltaTime;

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

            if (_combatSystem == null)
                _combatSystem = GetComponent<KRCombatSystem>();

            if (_playerCamera != null)
                _defaultCameraFov = _playerCamera.fieldOfView;

            _defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void OnDisable()
        {
            RestoreTimeScaleImmediately();
            RestoreFovImmediately();
        }

        private void Update()
        {
            if (IsExecuting) return;
            if (!Input.GetKeyDown(KeyCode.E)) return;

            IDamageable target = _absorptionZone?.GetNearestTarget();
            if (target == null) return;

            StartCoroutine(AbsorptionSequence(target));
        }

        // ─────────────────────────────────────────────
        // 흡혼 시퀀스
        // ─────────────────────────────────────────────

        private IEnumerator AbsorptionSequence(IDamageable target)
        {
            IsExecuting = true;

            _pendingImpactTarget = target;
            _pendingTargetGrade = GetGrade(target);

            _preImpactStarted = false;
            _impactMomentPlayed = false;

            _combatSystem?.SetCurrentWeaponVisualActive(false);

            // ① 도움닫기
            SetInvincible(true);
            _animator?.SetTrigger(kWindUpHash);

            yield return StartCoroutine(LungeToTarget(target));

            // 돌진 도중 선행 타격이 실패했다면 시퀀스 중단
            if (!_preImpactStarted)
            {
                SetInvincible(false);
                RestoreFovImmediately();
                _combatSystem?.SetCurrentWeaponVisualActive(true);

                _pendingImpactTarget = null;
                IsExecuting = false;
                yield break;
            }

            // 돌진 완료 프레임 = 플레이어 주먹 완전 신전 + 적 리액션 시작 프레임
            if (_playImpactEffectOnLungeEnd)
                PlayImpactMoment();

            // 슬로우모션이 유지되는 동안 주먹과 적 리액션을 보여주기 위해 Recover를 늦춤
            float recoverDelay = Mathf.Max(_strikeDuration, _killSlowEnterDuration + _killSlowHoldDuration);
            yield return new WaitForSecondsRealtime(recoverDelay);

            // ③ 회복
            SetInvincible(false);
            _animator?.SetTrigger(kRecoverHash);
            ApplyHeal(CalculateHeal(_pendingTargetGrade));

            yield return new WaitForSecondsRealtime(_recoveryDuration);

            RestoreFovImmediately();

            _combatSystem?.SetCurrentWeaponVisualActive(true);

            _pendingImpactTarget = null;
            IsExecuting = false;
        }

        public void NotifyPunchImpact()
        {
            if (_pendingImpactTarget == null)
                return;

            if (!_preImpactStarted)
            {
                BeginPreImpact(_pendingImpactTarget);
            }

            if (!_isLunging)
            {
                PlayImpactMoment();
            }
        }

        private IEnumerator LungeToTarget(IDamageable target)
        {
            if (_characterController == null)
            {
                yield return new WaitForSecondsRealtime(_windUpDuration);

                if (target != null && !target.IsDead)
                    BeginPreImpact(target);

                yield break;
            }

            Vector3 startPos = transform.position;

            Vector3 dirToPlayer = (startPos - target.Position).normalized;
            Vector3 targetPos = target.Position + dirToPlayer * _lungeStopDistance;
            targetPos.y = startPos.y;

            float distance = Vector3.Distance(startPos, targetPos);
            float distRatio = Mathf.Clamp01(distance / _lungeMaxDistanceRef);

            int frameCount = Mathf.RoundToInt(Mathf.Lerp(_lungeMinFrames, _lungeMaxFrames, distRatio));
            frameCount = Mathf.Max(1, frameCount);

            int preImpactFrame = Mathf.Clamp(frameCount - _preImpactLeadFrames, 0, frameCount - 1);

            if (_useLungeFovBoost)
                StartLungeFovBoost();

            _isLunging = true;

            for (int frame = 0; frame < frameCount; frame++)
            {
                if (!_preImpactStarted && frame == preImpactFrame)
                {
                    BeginPreImpact(target);
                }

                float t = Mathf.SmoothStep(0f, 1f, (float)(frame + 1) / frameCount);
                Vector3 desired = Vector3.Lerp(startPos, targetPos, t);
                Vector3 delta = desired - transform.position;

                _characterController.Move(delta);

                if (_lookAtTargetDuringLunge && target != null && !_preImpactStarted)
                    RotateBodyYawToTarget(target, Time.unscaledDeltaTime, false);

                yield return null;
            }

            _isLunging = false;
        }

        private void BeginPreImpact(IDamageable target)
        {
            if (_preImpactStarted) return;
            if (target == null || target.IsDead) return;

            _preImpactStarted = true;
            _pendingImpactTarget = target;
            _pendingTargetGrade = GetGrade(target);

            if (_lookAtTargetDuringLunge && _snapYawBeforeStrike)
                RotateBodyYawToTarget(target, 1f, true);

            // 플레이어 주먹 애니메이션 시작
            _animator?.SetTrigger(kStrikeHash);

            // 적 사망 애니메이션/사망 처리 시작
            // 적 애니메이션의 리액션이 3프레임 뒤라면,
            // 돌진 종료 3프레임 전에 호출해야 도착 순간과 맞습니다.
            target.Execute(KillRitual.Core.Interfaces.ExecutionSource.Absorption);
        }

        private void PlayImpactMoment()
        {
            if (_impactMomentPlayed) return;

            _impactMomentPlayed = true;

            StartKillSlowMotion();
            StartCoroutine(CameraKick());
        }

        // ─────────────────────────────────────────────
        // 카메라가 적을 보게 하는 보정
        // 주의: Camera Transform 직접 회전 아님.
        // Player 몸체 Yaw만 회전.
        // ─────────────────────────────────────────────

        private void RotateBodyYawToTarget(IDamageable target, float deltaTime, bool instant)
        {
            if (target == null) return;

            Vector3 aimPoint = target.Position + Vector3.up * _lookTargetHeightOffset;

            Vector3 flatDirection = aimPoint - transform.position;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

            if (instant)
            {
                transform.rotation = targetRotation;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _bodyYawRotateSpeed * Mathf.Max(0.0001f, deltaTime)
            );
        }

        // ─────────────────────────────────────────────
        // FOV 연출
        // ─────────────────────────────────────────────

        private void StartLungeFovBoost()
        {
            if (_playerCamera == null) return;

            if (_fovRoutine != null)
                StopCoroutine(_fovRoutine);

            _playerCamera.fieldOfView = _defaultCameraFov + _lungeFovAdd;
            _fovRoutine = StartCoroutine(RecoverFov());
        }

        private IEnumerator RecoverFov()
        {
            if (_playerCamera == null)
                yield break;

            float startFov = _playerCamera.fieldOfView;
            float elapsed = 0f;

            while (elapsed < _fovRecoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fovRecoverDuration);

                _playerCamera.fieldOfView = Mathf.Lerp(startFov, _defaultCameraFov, t);

                yield return null;
            }

            _playerCamera.fieldOfView = _defaultCameraFov;
            _fovRoutine = null;
        }

        private void RestoreFovImmediately()
        {
            if (_fovRoutine != null)
            {
                StopCoroutine(_fovRoutine);
                _fovRoutine = null;
            }

            if (_playerCamera != null)
                _playerCamera.fieldOfView = _defaultCameraFov;
        }

        // ─────────────────────────────────────────────
        // 킬 슬로우모션 / 카메라 킥
        // ─────────────────────────────────────────────

        private void StartKillSlowMotion()
        {
            if (!_useKillSlowMotion) return;

            if (_killSlowRoutine != null)
                StopCoroutine(_killSlowRoutine);

            _killSlowRoutine = StartCoroutine(KillSlowMotionRoutine());
        }

        private IEnumerator KillSlowMotionRoutine()
        {
            float elapsed = 0f;

            // 1. 약 5프레임 동안 급감속
            while (elapsed < _killSlowEnterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _killSlowEnterDuration);

                // 초반에 빠르게 떨어지고 끝에서 살짝 붙는 감속 곡선
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                SetTimeScale(Mathf.Lerp(1f, _killSlowMinScale, eased));
                yield return null;
            }

            SetTimeScale(_killSlowMinScale);

            // 2. 감속 상태 유지
            if (_killSlowHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(_killSlowHoldDuration);

            elapsed = 0f;

            // 3. 원래 속도로 복귀
            while (elapsed < _killSlowRecoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _killSlowRecoverDuration);

                float eased = Mathf.SmoothStep(0f, 1f, t);

                SetTimeScale(Mathf.Lerp(_killSlowMinScale, 1f, eased));
                yield return null;
            }

            SetTimeScale(1f);
            _killSlowRoutine = null;
        }

        private void SetTimeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.01f, 1f);

            Time.timeScale = scale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * scale;
        }

        private void RestoreTimeScaleImmediately()
        {
            if (_killSlowRoutine != null)
            {
                StopCoroutine(_killSlowRoutine);
                _killSlowRoutine = null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
        }

        private IEnumerator CameraKick()
        {
            if (_playerCamera == null) yield break;

            Transform camTransform = _playerCamera.transform;

            Quaternion originalRotation = camTransform.localRotation;
            Quaternion kickRotation = originalRotation * Quaternion.Euler(_cameraKickAmount, 0f, 0f);

            float elapsed = 0f;
            float halfDuration = _cameraKickDuration * 0.5f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                camTransform.localRotation = Quaternion.Lerp(originalRotation, kickRotation, t);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                camTransform.localRotation = Quaternion.Lerp(kickRotation, originalRotation, t);
                yield return null;
            }

            camTransform.localRotation = originalRotation;
        }

        // ─────────────────────────────────────────────
        // 체력 회복 계산
        // ─────────────────────────────────────────────

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

        // ─────────────────────────────────────────────
        // 등급 / 무적
        // ─────────────────────────────────────────────

        private static EnemyGrade GetGrade(IDamageable target)
            => target is KREnemyBase enemy ? enemy.Grade : EnemyGrade.Fodder;

        private void SetInvincible(bool invincible)
            => _damageFeedback?.SetInvincible(invincible);
    }

    public enum EnemyGrade
    {
        Fodder,
        Heavy,
        Elite,
        Boss
    }
}