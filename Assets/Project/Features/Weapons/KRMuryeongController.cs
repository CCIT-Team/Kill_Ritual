// Assets/Project/Scripts/Features/Player/Combat/KRMuryeongController.cs
using System.Collections;
using KillRitual.Enemies.Projectiles;
using KillRitual.Weapons.Visual;
using UnityEngine;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 무령 입력/판정 컨트롤러.
    ///
    /// 구조:
    /// - EnemyProjectile 레이어의 원본 적 투사체 1개를 잡음
    /// - 원본 투사체는 Destroy
    /// - 카메라 기준 SphereCast로 적 조준 보정
    /// - 무령 전용 투사체 프리팹을 새로 발사
    /// - 반사 성공 후 약간의 딜레이 뒤 흡혼과 같은 슬로우모션/카메라 킥 연출을 재생
    ///
    /// 주의:
    /// - 무령탄 데미지/폭발/충돌 마스크는 KRMuryeongProjectile 프리팹에서만 관리합니다.
    /// - 이 컨트롤러에는 무령탄 데미지 값을 두지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRMuryeongController : MonoBehaviour
    {
        private const float AfterguardDuration = 0.3f;
        private const float AfterguardDamageReductionRate = 0.8f;

        [Header("Input")]
        [SerializeField] private bool _listenInputDirectly = true;
        [SerializeField] private KeyCode _parryKey = KeyCode.LeftControl;

        [Header("References")]
        [SerializeField] private KRMuryeongVisual _visual;
        [SerializeField] private KRCombatSystem _combatSystem;

        [Tooltip("비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private UnityEngine.Camera _viewCamera;

        [Tooltip("무령 판정 중심. 비워두면 이 오브젝트 위치를 사용합니다.")]
        [SerializeField] private Transform _parryOrigin;

        [Tooltip("무령탄 생성 위치. 비워두면 잡아낸 적 투사체 위치에서 생성합니다.")]
        [SerializeField] private Transform _muryeongProjectileSpawnPoint;

        [Header("Catch Enemy Projectile")]
        [Tooltip("EnemyProjectile 레이어만 넣으세요.")]
        [SerializeField] private LayerMask _enemyProjectileLayerMask;

        [Min(0.1f)]
        [SerializeField] private float _catchDistance = 6f;

        [Range(1f, 180f)]
        [SerializeField] private float _catchAngle = 110f;

        [Min(0.01f)]
        [SerializeField] private float _catchWindowDuration = 0.2f;

        [Header("Counter Projectile")]
        [SerializeField] private KRMuryeongProjectile _muryeongProjectilePrefab;

        [Header("Aim Assist")]
        [Tooltip("무령탄 조준 보정 대상. Enemy, Boss를 넣으세요.")]
        [SerializeField] private LayerMask _aimAssistEnemyMask;

        [Tooltip("카메라 전방으로 적을 찾는 거리.")]
        [Min(1f)]
        [SerializeField] private float _aimAssistRange = 60f;

        [Tooltip("일반 Raycast 대신 SphereCast로 넓게 잡는 반경.")]
        [Min(0.01f)]
        [SerializeField] private float _aimAssistRadius = 0.8f;

        [Tooltip("적 중심을 조준할 때 위로 올리는 값. 0.8~1.2 정도 추천.")]
        [SerializeField] private float _targetHeightOffset = 1f;

        [Header("Gauge")]
        [SerializeField] private float _maxGauge = 100f;
        [SerializeField] private float _currentGauge = 100f;

        [Tooltip("등급 구분 없이 무령 1회 성공에 소모되는 게이지.")]
        [SerializeField] private float _reflectCost = 10f;

        [Header("Lockout")]
        [Min(0f)]
        [SerializeField] private float _missLockout = 2f;

        [Header("Reflect Hitstop")]
        [Tooltip("반사 성공 후 히트스탑이 시작되기 전까지의 실제 시간 딜레이입니다. 이펙트가 먼저 보이게 하려면 0.15~0.2 권장.")]
        [Min(0f)]
        [SerializeField] private float _reflectImpactDelay = 0.18f;

        [Tooltip("반사 성공 순간 슬로우모션을 사용할지 여부입니다.")]
        [SerializeField] private bool _useReflectSlowMotion = true;

        [Tooltip("가장 느려졌을 때의 시간 배율입니다.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _reflectSlowMinScale = 0.10f;

        [Tooltip("최소 시간 배율까지 내려가는 시간입니다.")]
        [Min(0.001f)]
        [SerializeField] private float _reflectSlowEnterDuration = 0.083f;

        [Tooltip("가장 느린 상태를 유지하는 시간입니다. 무령은 0.08~0.15 권장.")]
        [Min(0f)]
        [SerializeField] private float _reflectSlowHoldDuration = 0.12f;

        [Tooltip("원래 속도로 돌아오는 시간입니다.")]
        [Min(0.001f)]
        [SerializeField] private float _reflectSlowRecoverDuration = 0.18f;

        [Header("Reflect Camera Kick")]
        [Tooltip("반사 성공 순간 카메라가 튀는 강도입니다.")]
        [Min(0f)]
        [SerializeField] private float _reflectCameraKickAmount = 5f;

        [Tooltip("카메라 킥이 복구되는 시간입니다. 실제 시간 기준입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _reflectCameraKickDuration = 0.2f;

        [Header("Weapon Visual Restore")]
        [Min(0.05f)]
        [SerializeField] private float _weaponVisualRestoreFallbackDelay = 0.6f;

        private static readonly Collider[] CatchBuffer = new Collider[32];

        private float _nextAvailableTime;
        private float _afterguardEndTime;

        private Coroutine _catchRoutine;
        private Coroutine _weaponRestoreRoutine;

        private Coroutine _reflectImpactDelayRoutine;
        private Coroutine _reflectSlowRoutine;
        private Coroutine _reflectCameraKickRoutine;

        private float _defaultFixedDeltaTime;

        public float CurrentGauge => _currentGauge;
        public float MaxGauge => _maxGauge;
        public float GaugeNormalized => _maxGauge <= 0f ? 0f : _currentGauge / _maxGauge;
        public bool IsAfterguardActive => Time.time < _afterguardEndTime;

        private void Reset()
        {
            int enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");

            if (enemyProjectileLayer >= 0)
                _enemyProjectileLayerMask = 1 << enemyProjectileLayer;
        }

        private void Awake()
        {
            if (_visual == null)
                _visual = GetComponentInChildren<KRMuryeongVisual>(true);

            if (_combatSystem == null)
                _combatSystem = GetComponentInParent<KRCombatSystem>();

            if (_viewCamera == null)
                _viewCamera = UnityEngine.Camera.main;

            if (_parryOrigin == null)
                _parryOrigin = transform;

            _maxGauge = Mathf.Max(1f, _maxGauge);
            _currentGauge = Mathf.Clamp(_currentGauge, 0f, _maxGauge);

            _defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void OnValidate()
        {
            _maxGauge = Mathf.Max(1f, _maxGauge);
            _currentGauge = Mathf.Clamp(_currentGauge, 0f, _maxGauge);
            _reflectCost = Mathf.Max(0f, _reflectCost);
        }

        private void OnEnable()
        {
            if (_visual != null)
                _visual.OnHidden += OnMuryeongHidden;
        }

        private void OnDisable()
        {
            if (_visual != null)
                _visual.OnHidden -= OnMuryeongHidden;

            if (_catchRoutine != null)
            {
                StopCoroutine(_catchRoutine);
                _catchRoutine = null;
            }

            if (_reflectImpactDelayRoutine != null)
            {
                StopCoroutine(_reflectImpactDelayRoutine);
                _reflectImpactDelayRoutine = null;
            }

            RestoreReflectTimeScaleImmediately();
            StopReflectCameraKickImmediately();
            RestoreCurrentWeaponVisual();
        }

        private void Update()
        {
            if (!_listenInputDirectly)
                return;

            if (Input.GetKeyDown(_parryKey))
                TryParry();
        }

        public bool TryParry()
        {
            if (Time.time < _nextAvailableTime)
                return false;

            if (_catchRoutine != null)
                return false;

            if (_currentGauge < _reflectCost)
            {
                _nextAvailableTime = Time.time + _missLockout;
                return false;
            }

            HideCurrentWeaponVisual();

            if (_visual != null)
                _visual.PlayParry();

            _catchRoutine = StartCoroutine(CatchWindowRoutine());
            return true;
        }

        private IEnumerator CatchWindowRoutine()
        {
            float endTime = Time.time + _catchWindowDuration;

            while (Time.time <= endTime)
            {
                Collider caughtProjectile = FindBestEnemyProjectile();

                if (caughtProjectile != null)
                {
                    HandleCatchSuccess(caughtProjectile);
                    _catchRoutine = null;
                    yield break;
                }

                yield return null;
            }

            HandleCatchFailure();
            _catchRoutine = null;
        }

        private Collider FindBestEnemyProjectile()
        {
            Vector3 origin = GetParryOrigin();
            Vector3 forward = GetViewForward();

            float cosHalfAngle = Mathf.Cos((_catchAngle * 0.5f) * Mathf.Deg2Rad);

            int count = Physics.OverlapSphereNonAlloc(
                origin,
                _catchDistance,
                CatchBuffer,
                _enemyProjectileLayerMask,
                QueryTriggerInteraction.Collide);

            Collider best = null;
            float bestCameraAngle = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider hit = CatchBuffer[i];

                if (hit == null)
                    continue;

                Vector3 projectilePosition = hit.bounds.center;
                Vector3 toProjectile = projectilePosition - origin;
                float distance = toProjectile.magnitude;

                if (distance <= 0.001f)
                    continue;

                Vector3 directionToProjectile = toProjectile / distance;

                if (Vector3.Dot(forward, directionToProjectile) < cosHalfAngle)
                    continue;

                float cameraAngle = CalculateCameraCenterAngle(projectilePosition);

                if (cameraAngle < bestCameraAngle ||
                    Mathf.Approximately(cameraAngle, bestCameraAngle) && distance < bestDistance)
                {
                    best = hit;
                    bestCameraAngle = cameraAngle;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void HandleCatchSuccess(Collider caughtProjectileCollider)
        {
            if (caughtProjectileCollider == null)
            {
                HandleCatchFailure();
                return;
            }

            if (!TryConsumeGauge())
            {
                HandleCatchFailure();
                return;
            }

            Vector3 caughtPosition = caughtProjectileCollider.bounds.center;

            GameObject caughtRoot = caughtProjectileCollider.attachedRigidbody != null
                ? caughtProjectileCollider.attachedRigidbody.gameObject
                : caughtProjectileCollider.transform.root.gameObject;

            // 원본 적 투사체는 재사용하지 않고 제거.
            Destroy(caughtRoot);

            // 반사탄 발사.
            FireMuryeongProjectile(caughtPosition);

            // 이펙트가 먼저 보이도록 0.15~0.2초 뒤에 히트스탑/카메라 킥.
            StartDelayedReflectImpactMoment();

            ApplyAfterguard();

            // 성공 시 쿨타임 없음.
            _nextAvailableTime = Time.time;
        }

        private void HandleCatchFailure()
        {
            _nextAvailableTime = Time.time + _missLockout;
        }

        private void FireMuryeongProjectile(Vector3 caughtProjectilePosition)
        {
            if (_muryeongProjectilePrefab == null)
            {
                Debug.LogWarning(
                    $"[{nameof(KRMuryeongController)}] Muryeong Projectile Prefab이 비어 있습니다.",
                    this);

                return;
            }

            Vector3 spawnPosition = _muryeongProjectileSpawnPoint != null
                ? _muryeongProjectileSpawnPoint.position
                : caughtProjectilePosition;

            Vector3 direction = ResolveCounterFireDirection(spawnPosition);

            KRMuryeongProjectile projectile = Instantiate(
                _muryeongProjectilePrefab,
                spawnPosition,
                Quaternion.LookRotation(direction, Vector3.up));

            projectile.Initialize(direction, transform);
        }

        private Vector3 ResolveCounterFireDirection(Vector3 spawnPosition)
        {
            Transform viewTransform = GetViewTransform();

            if (viewTransform == null)
                return transform.forward;

            Ray aimRay = new Ray(viewTransform.position, viewTransform.forward);

            if (Physics.SphereCast(
                    aimRay,
                    _aimAssistRadius,
                    out RaycastHit hit,
                    _aimAssistRange,
                    _aimAssistEnemyMask,
                    QueryTriggerInteraction.Collide))
            {
                Vector3 targetPoint = GetAimTargetPoint(hit.collider);
                Vector3 toTarget = targetPoint - spawnPosition;

                if (toTarget.sqrMagnitude > 0.0001f)
                    return toTarget.normalized;
            }

            return viewTransform.forward;
        }

        private Vector3 GetAimTargetPoint(Collider targetCollider)
        {
            if (targetCollider == null)
                return GetViewTransform().position + GetViewForward() * _aimAssistRange;

            Bounds bounds = targetCollider.bounds;

            return new Vector3(
                bounds.center.x,
                bounds.min.y + _targetHeightOffset,
                bounds.center.z);
        }

        private Vector3 GetParryOrigin()
        {
            if (_parryOrigin != null)
                return _parryOrigin.position;

            return transform.position;
        }

        private Transform GetViewTransform()
        {
            if (_viewCamera != null)
                return _viewCamera.transform;

            return transform;
        }

        private Vector3 GetViewForward()
        {
            Transform viewTransform = GetViewTransform();

            if (viewTransform != null)
                return viewTransform.forward;

            return transform.forward;
        }

        private float CalculateCameraCenterAngle(Vector3 worldPosition)
        {
            Transform viewTransform = GetViewTransform();

            Vector3 toTarget = worldPosition - viewTransform.position;

            if (toTarget.sqrMagnitude <= 0.0001f)
                return 0f;

            return Vector3.Angle(viewTransform.forward, toTarget.normalized);
        }

        private bool TryConsumeGauge()
        {
            if (_currentGauge < _reflectCost)
                return false;

            _currentGauge = Mathf.Clamp(_currentGauge - _reflectCost, 0f, _maxGauge);
            return true;
        }

        public void AddGauge(float amount)
        {
            if (amount <= 0f)
                return;

            _currentGauge = Mathf.Clamp(_currentGauge + amount, 0f, _maxGauge);
        }

        public void SetGauge(float value)
        {
            _currentGauge = Mathf.Clamp(value, 0f, _maxGauge);
        }

        private void ApplyAfterguard()
        {
            _afterguardEndTime = Mathf.Max(
                _afterguardEndTime,
                Time.time + AfterguardDuration);
        }

        public float ModifyIncomingDamageByMuryeong(float rawDamage)
        {
            if (rawDamage <= 0f)
                return 0f;

            if (!IsAfterguardActive)
                return rawDamage;

            return rawDamage * (1f - AfterguardDamageReductionRate);
        }

        // ─────────────────────────────────────────────
        // 반사 성공 히트스톱 / 카메라 킥
        // ─────────────────────────────────────────────

        private void StartDelayedReflectImpactMoment()
        {
            if (_reflectImpactDelayRoutine != null)
                StopCoroutine(_reflectImpactDelayRoutine);

            _reflectImpactDelayRoutine = StartCoroutine(DelayedReflectImpactMomentRoutine());
        }

        private IEnumerator DelayedReflectImpactMomentRoutine()
        {
            if (_reflectImpactDelay > 0f)
                yield return new WaitForSecondsRealtime(_reflectImpactDelay);

            PlayReflectImpactMoment();
            _reflectImpactDelayRoutine = null;
        }

        private void PlayReflectImpactMoment()
        {
            StartReflectSlowMotion();

            if (_reflectCameraKickRoutine != null)
                StopCoroutine(_reflectCameraKickRoutine);

            _reflectCameraKickRoutine = StartCoroutine(ReflectCameraKick());
        }

        private void StartReflectSlowMotion()
        {
            if (!_useReflectSlowMotion)
                return;

            if (_reflectSlowRoutine != null)
                StopCoroutine(_reflectSlowRoutine);

            _reflectSlowRoutine = StartCoroutine(ReflectSlowMotionRoutine());
        }

        private IEnumerator ReflectSlowMotionRoutine()
        {
            float elapsed = 0f;

            while (elapsed < _reflectSlowEnterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _reflectSlowEnterDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                SetReflectTimeScale(Mathf.Lerp(1f, _reflectSlowMinScale, eased));
                yield return null;
            }

            SetReflectTimeScale(_reflectSlowMinScale);

            if (_reflectSlowHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(_reflectSlowHoldDuration);

            elapsed = 0f;

            while (elapsed < _reflectSlowRecoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _reflectSlowRecoverDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                SetReflectTimeScale(Mathf.Lerp(_reflectSlowMinScale, 1f, eased));
                yield return null;
            }

            SetReflectTimeScale(1f);
            _reflectSlowRoutine = null;
        }

        private void SetReflectTimeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.01f, 1f);

            Time.timeScale = scale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * scale;
        }

        private void RestoreReflectTimeScaleImmediately()
        {
            if (_reflectSlowRoutine != null)
            {
                StopCoroutine(_reflectSlowRoutine);
                _reflectSlowRoutine = null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
        }

        private IEnumerator ReflectCameraKick()
        {
            if (_viewCamera == null)
                yield break;

            Transform camTransform = _viewCamera.transform;

            Quaternion originalRotation = camTransform.localRotation;
            Quaternion kickRotation = originalRotation * Quaternion.Euler(_reflectCameraKickAmount, 0f, 0f);

            float elapsed = 0f;
            float halfDuration = _reflectCameraKickDuration * 0.5f;

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
            _reflectCameraKickRoutine = null;
        }

        private void StopReflectCameraKickImmediately()
        {
            if (_reflectCameraKickRoutine != null)
            {
                StopCoroutine(_reflectCameraKickRoutine);
                _reflectCameraKickRoutine = null;
            }
        }

        // ─────────────────────────────────────────────
        // 무기 손 숨김 / 복구
        // ─────────────────────────────────────────────

        private void HideCurrentWeaponVisual()
        {
            _combatSystem?.SetCurrentWeaponVisualActive(false);

            if (_weaponRestoreRoutine != null)
                StopCoroutine(_weaponRestoreRoutine);

            _weaponRestoreRoutine = StartCoroutine(WeaponVisualRestoreFallbackRoutine());
        }

        private IEnumerator WeaponVisualRestoreFallbackRoutine()
        {
            yield return new WaitForSeconds(_weaponVisualRestoreFallbackDelay);

            RestoreCurrentWeaponVisual();
        }

        private void OnMuryeongHidden()
        {
            RestoreCurrentWeaponVisual();
        }

        private void RestoreCurrentWeaponVisual()
        {
            if (_weaponRestoreRoutine != null)
            {
                StopCoroutine(_weaponRestoreRoutine);
                _weaponRestoreRoutine = null;
            }

            _combatSystem?.SetCurrentWeaponVisualActive(true);
        }

        private void OnDrawGizmosSelected()
        {
            Transform viewTransform = GetViewTransform();

            if (viewTransform == null)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(viewTransform.position, viewTransform.forward * _aimAssistRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetParryOrigin(), _catchDistance);
        }
    }
}