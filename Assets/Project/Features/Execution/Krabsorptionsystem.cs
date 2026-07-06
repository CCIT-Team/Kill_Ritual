// Assets/Project/Features/Player/KRAbsorptionSystem.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Enemies;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 흡혼 시스템 — 그로기 상태의 적을 처형하고 체력을 회복하는 전담 컴포넌트입니다.
    ///
    /// 기준:
    /// - 애니메이터 처리는 기존 코드 그대로 유지
    /// - 돌진 이동 방식도 기존 코드 그대로 유지
    /// - 추가된 것은 FOV 가속감 + 적 방향 Yaw 보정뿐
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

        [Header("카메라 시선 보정")]
        [Tooltip("돌진 중 Player 몸체의 Y축만 적 방향으로 돌립니다. Animator와 손 리그는 건드리지 않습니다.")]
        [SerializeField] private bool _lookAtTargetDuringLunge = true;

        [Tooltip("타격 직전에 Player 몸체 방향을 적에게 즉시 맞춥니다.")]
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

        [Tooltip("카메라 킥이 복구되는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _cameraKickDuration = 0.2f;

        [Header("히트스톱 설정")]
        [Tooltip("처치 순간 시간이 느려지는 배율. 현재 인스펙터 값 기준.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hitStopTimeScale = 0.8f;

        [Tooltip("히트스톱 지속 시간(초, 실제 시간 기준).")]
        [Min(0.01f)]
        [SerializeField] private float _hitStopDuration = 0.2f;

        [Header("시퀀스 시간")]
        [Tooltip("도움닫기 구간 시간(초). 현재 인스펙터 값 기준.")]
        [Min(0.01f)]
        [SerializeField] private float _windUpDuration = 0.01f;

        [Tooltip("돌입 처치 구간 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _strikeDuration = 0.3f;

        [Tooltip("체력 회복 구간 시간(초). 현재 인스펙터 값 기준.")]
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
        private bool _impactApplied;

        private float _defaultCameraFov = 60f;
        private Coroutine _fovRoutine;

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

            _combatSystem?.SetCurrentWeaponVisualActive(false);

            // ① 도움닫기 — 기존 애니메이터 호출 그대로
            SetInvincible(true);
            _animator?.SetTrigger(kWindUpHash);

            yield return StartCoroutine(LungeToTarget(target));

            if (target == null || target.IsDead)
            {
                SetInvincible(false);
                RestoreFovImmediately();
                _combatSystem?.SetCurrentWeaponVisualActive(true);
                IsExecuting = false;
                yield break;
            }

            _pendingImpactTarget = target;
            _impactApplied = false;

            if (_lookAtTargetDuringLunge && _snapYawBeforeStrike)
                RotateBodyYawToTarget(target, 1f, true);

            // ② 돌입 처치 — 기존 애니메이터 호출 그대로
            _animator?.SetTrigger(kStrikeHash);

            yield return new WaitForSeconds(_strikeDuration);

            if (!_impactApplied)
                NotifyPunchImpact();

            // ③ 회복 — 기존 애니메이터 호출 그대로
            SetInvincible(false);
            _animator?.SetTrigger(kRecoverHash);
            ApplyHeal(CalculateHeal(GetGrade(target)));

            yield return new WaitForSeconds(_recoveryDuration);

            RestoreFovImmediately();

            _combatSystem?.SetCurrentWeaponVisualActive(true);
            _pendingImpactTarget = null;
            IsExecuting = false;
        }

        /// <summary>
        /// Punch.anim의 타격 프레임에서 KRPunchImpactRelay를 통해 호출됩니다.
        /// </summary>
        public void NotifyPunchImpact()
        {
            if (_impactApplied) return;
            if (_pendingImpactTarget == null || _pendingImpactTarget.IsDead) return;

            _impactApplied = true;

            _pendingImpactTarget.Execute(KillRitual.Core.Interfaces.ExecutionSource.Absorption);

            StartCoroutine(HitStop());
            StartCoroutine(CameraKick());
        }

        /// <summary>
        /// 도움닫기 — 기존 돌진 계산 유지.
        /// 추가된 것은 FOV 상승과 Player Yaw 보정뿐입니다.
        /// </summary>
        private IEnumerator LungeToTarget(IDamageable target)
        {
            if (_characterController == null)
            {
                yield return new WaitForSeconds(_windUpDuration);
                yield break;
            }

            Vector3 startPos = transform.position;

            // 기존 코드 그대로
            Vector3 dirToPlayer = (startPos - target.Position).normalized;
            Vector3 targetPos = target.Position + dirToPlayer * _lungeStopDistance;
            targetPos.y = startPos.y;

            float distance = Vector3.Distance(startPos, targetPos);
            float distRatio = Mathf.Clamp01(distance / _lungeMaxDistanceRef);
            int frameCount = Mathf.RoundToInt(Mathf.Lerp(_lungeMinFrames, _lungeMaxFrames, distRatio));

            if (_useLungeFovBoost)
                StartLungeFovBoost();

            for (int frame = 0; frame < frameCount; frame++)
            {
                float t = Mathf.SmoothStep(0f, 1f, (float)(frame + 1) / frameCount);
                Vector3 desired = Vector3.Lerp(startPos, targetPos, t);
                Vector3 delta = desired - transform.position;

                _characterController.Move(delta);

                if (_lookAtTargetDuringLunge && target != null && !target.IsDead)
                    RotateBodyYawToTarget(target, Time.unscaledDeltaTime, false);

                yield return null;
            }
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
        // 히트스톱 / 카메라 킥
        // ─────────────────────────────────────────────

        private IEnumerator HitStop()
        {
            Time.timeScale = _hitStopTimeScale;

            yield return new WaitForSecondsRealtime(_hitStopDuration);

            Time.timeScale = 1f;
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

    /// <summary>적 등급 열거형. KREnemyBase와 KRAbsorptionSystem이 공유합니다.</summary>
    public enum EnemyGrade
    {
        Fodder,
        Heavy,
        Elite,
        Boss
    }
}