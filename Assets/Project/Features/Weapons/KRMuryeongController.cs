// Assets/Project/Scripts/Features/Player/Combat/KRMuryeongController.cs
using System.Collections;
using KillRitual.Enemies.Projectiles;
using KillRitual.Weapons.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.Player.Combat
{
    [DisallowMultipleComponent]
    public sealed class KRMuryeongController : MonoBehaviour
    {
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

        [Header("Gauge Regeneration")]
        [Tooltip("무령 게이지가 자동 회복되는지 여부입니다.")]
        [SerializeField] private bool _useGaugeRegen = true;

        [Tooltip("초당 무령 게이지 회복량입니다.")]
        [Min(0f)]
        [SerializeField] private float _gaugeRegenPerSecond = 1f;

        [Tooltip("게이지를 사용한 뒤 자동 회복이 시작되기 전 대기 시간입니다.")]
        [Min(0f)]
        [SerializeField] private float _gaugeRegenDelayAfterUse = 0.5f;

        [Header("Gauge Bar UI")]
        [Tooltip("무령 게이지 전체 너비 기준이 되는 RectTransform입니다. 보통 Background를 넣으세요.")]
        [SerializeField] private RectTransform _gaugeBarWidthReference;

        [Tooltip("현재 무령 게이지를 표시하는 실제 Fill Image입니다. Image Type은 Sliced 권장.")]
        [SerializeField] private Image _gaugeBarFill;

        [Tooltip("게이지 감소량을 보여주는 뒤따라오는 바입니다. Fill 뒤, Background 앞에 배치하세요. Image Type은 Sliced 권장.")]
        [SerializeField] private Image _gaugeBarFollow;

        [Tooltip("게이지가 가득 찼을 때의 너비입니다. 0이면 Width Reference의 현재 너비를 자동으로 사용합니다.")]
        [Min(0f)]
        [SerializeField] private float _gaugeBarMaxWidth = 0f;

        [Tooltip("실행 시 Fill / Follow의 Anchor와 Pivot을 오른쪽 기준으로 강제 설정합니다.")]
        [SerializeField] private bool _forceGaugeRightAligned = true;

        [Tooltip("게이지 감소 후 Follow Bar가 움직이기 전 대기 시간입니다.")]
        [Min(0f)]
        [SerializeField] private float _gaugeFollowDelay = 0.15f;

        [Tooltip("Follow Bar가 현재 게이지바 위치까지 줄어드는 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _gaugeFollowCatchUpDuration = 0.18f;

        [Header("Afterguard")]
        [Tooltip("무령 반사 성공 후 피해 감소가 유지되는 실제 시간입니다.")]
        [Min(0f)]
        [SerializeField] private float _afterguardDuration = 2.3f;

        [Tooltip("잔흔 중 감소시킬 피해 비율입니다. 0.8이면 80% 감소, 즉 20%만 받습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _afterguardDamageReductionRate = 0.8f;

        [Header("Lockout")]
        [Min(0f)]
        [SerializeField] private float _missLockout = 2f;

        [Header("Reflect Hitstop")]
        [Tooltip("반사 성공 후 히트스탑이 시작되기 전까지의 실제 시간 딜레이입니다.")]
        [Min(0f)]
        [SerializeField] private float _reflectImpactDelay = 0.18f;

        [Tooltip("반사 성공 순간 슬로우모션을 사용할지 여부입니다.")]
        [SerializeField] private bool _useReflectSlowMotion = true;

        [Range(0.01f, 1f)]
        [SerializeField] private float _reflectSlowMinScale = 0.10f;

        [Min(0.001f)]
        [SerializeField] private float _reflectSlowEnterDuration = 0.083f;

        [Tooltip("가장 느린 상태를 유지하는 시간입니다. 무령은 0.08~0.15 권장.")]
        [Min(0f)]
        [SerializeField] private float _reflectSlowHoldDuration = 0.12f;

        [Min(0.001f)]
        [SerializeField] private float _reflectSlowRecoverDuration = 0.18f;

        [Header("Reflect Camera Kick")]
        [Min(0f)]
        [SerializeField] private float _reflectCameraKickAmount = 5f;

        [Min(0.01f)]
        [SerializeField] private float _reflectCameraKickDuration = 0.2f;

        [Header("Weapon Visual Restore")]
        [Min(0.05f)]
        [SerializeField] private float _weaponVisualRestoreFallbackDelay = 0.6f;

        private static readonly Collider[] CatchBuffer = new Collider[32];

        private float _nextAvailableTime;
        private float _afterguardEndRealtime;
        private float _lastGaugeUseRealtime;

        private Coroutine _catchRoutine;
        private Coroutine _weaponRestoreRoutine;
        private Coroutine _reflectImpactDelayRoutine;
        private Coroutine _reflectSlowRoutine;
        private Coroutine _reflectCameraKickRoutine;
        private Coroutine _gaugeFollowCoroutine;

        private RectTransform _gaugeBarFillRect;
        private RectTransform _gaugeBarFollowRect;

        private float _defaultFixedDeltaTime;

        public float CurrentGauge => _currentGauge;
        public float MaxGauge => _maxGauge;
        public float GaugeNormalized => _maxGauge <= 0f ? 0f : _currentGauge / _maxGauge;
        public bool IsAfterguardActive => Time.unscaledTime < _afterguardEndRealtime;

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
            _reflectCost = Mathf.Max(0f, _reflectCost);

            _defaultFixedDeltaTime = Time.fixedDeltaTime;

            CacheGaugeBarReferences();
            UpdateGaugeBar(true);
        }

        private void OnValidate()
        {
            _maxGauge = Mathf.Max(1f, _maxGauge);
            _currentGauge = Mathf.Clamp(_currentGauge, 0f, _maxGauge);
            _reflectCost = Mathf.Max(0f, _reflectCost);
            _gaugeRegenPerSecond = Mathf.Max(0f, _gaugeRegenPerSecond);
            _gaugeRegenDelayAfterUse = Mathf.Max(0f, _gaugeRegenDelayAfterUse);
        }

        private void OnEnable()
        {
            if (_visual != null)
                _visual.OnHidden += OnMuryeongHidden;

            CacheGaugeBarReferences();
            UpdateGaugeBar(true);
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

            if (_gaugeFollowCoroutine != null)
            {
                StopCoroutine(_gaugeFollowCoroutine);
                _gaugeFollowCoroutine = null;
            }

            RestoreReflectTimeScaleImmediately();
            StopReflectCameraKickImmediately();
            RestoreCurrentWeaponVisual();
        }

        private void Update()
        {
            UpdateGaugeRegen();

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

            Destroy(caughtRoot);

            FireMuryeongProjectile(caughtPosition);
            StartDelayedReflectImpactMoment();
            ApplyAfterguard();

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
                Debug.LogWarning($"[{nameof(KRMuryeongController)}] Muryeong Projectile Prefab이 비어 있습니다.", this);
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

            float previousGauge = _currentGauge;

            _currentGauge = Mathf.Clamp(_currentGauge - _reflectCost, 0f, _maxGauge);
            _lastGaugeUseRealtime = Time.unscaledTime;

            bool gaugeReduced = _currentGauge < previousGauge;
            UpdateGaugeBar(!gaugeReduced);

            return true;
        }

        private void UpdateGaugeRegen()
        {
            if (!_useGaugeRegen)
                return;

            if (_gaugeRegenPerSecond <= 0f)
                return;

            if (_currentGauge >= _maxGauge)
                return;

            if (Time.unscaledTime < _lastGaugeUseRealtime + _gaugeRegenDelayAfterUse)
                return;

            _currentGauge = Mathf.Min(
                _maxGauge,
                _currentGauge + _gaugeRegenPerSecond * Time.unscaledDeltaTime);

            UpdateGaugeBar(true);
        }

        public void AddGauge(float amount)
        {
            if (amount <= 0f)
                return;

            _currentGauge = Mathf.Clamp(_currentGauge + amount, 0f, _maxGauge);
            UpdateGaugeBar(true);
        }

        public void SetGauge(float value)
        {
            _currentGauge = Mathf.Clamp(value, 0f, _maxGauge);
            UpdateGaugeBar(true);
        }

        private void CacheGaugeBarReferences()
        {
            if (_gaugeBarFill != null)
                _gaugeBarFillRect = _gaugeBarFill.rectTransform;

            if (_gaugeBarFollow != null)
                _gaugeBarFollowRect = _gaugeBarFollow.rectTransform;

            if (_forceGaugeRightAligned)
            {
                ForceRightAligned(_gaugeBarFillRect);
                ForceRightAligned(_gaugeBarFollowRect);
            }

            Canvas.ForceUpdateCanvases();

            if (_gaugeBarMaxWidth <= 0f)
                _gaugeBarMaxWidth = GetGaugeReferenceWidth();

            if (_gaugeBarMaxWidth <= 0.01f)
            {
                _gaugeBarMaxWidth = 100f;
                Debug.LogWarning(
                    "[KRMuryeongController] 무령 게이지바 최대 너비를 가져오지 못했습니다. " +
                    "_gaugeBarWidthReference에 Background를 연결하거나 _gaugeBarMaxWidth를 직접 입력하세요.",
                    this);
            }

            SetGaugeBarWidth(_gaugeBarFillRect, GetGaugeWidthFromRatio(GaugeNormalized));
            SetGaugeBarWidth(_gaugeBarFollowRect, GetGaugeWidthFromRatio(GaugeNormalized));
        }

        private float GetGaugeReferenceWidth()
        {
            if (_gaugeBarWidthReference != null)
            {
                float referenceWidth = Mathf.Abs(_gaugeBarWidthReference.rect.width);

                if (referenceWidth > 0.01f)
                    return referenceWidth;

                referenceWidth = Mathf.Abs(_gaugeBarWidthReference.sizeDelta.x);

                if (referenceWidth > 0.01f)
                    return referenceWidth;
            }

            if (_gaugeBarFillRect != null)
            {
                float fillWidth = Mathf.Abs(_gaugeBarFillRect.rect.width);

                if (fillWidth > 0.01f)
                    return fillWidth;

                fillWidth = Mathf.Abs(_gaugeBarFillRect.sizeDelta.x);

                if (fillWidth > 0.01f)
                    return fillWidth;
            }

            return 0f;
        }

        private void ForceRightAligned(RectTransform rect)
        {
            if (rect == null)
                return;

            Vector2 size = rect.sizeDelta;
            Vector2 anchoredPosition = rect.anchoredPosition;

            rect.anchorMin = new Vector2(1f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.pivot = new Vector2(1f, rect.pivot.y);

            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private void UpdateGaugeBar(bool followInstantly)
        {
            float ratio = GaugeNormalized;
            float targetWidth = GetGaugeWidthFromRatio(ratio);

            SetGaugeBarWidth(_gaugeBarFillRect, targetWidth);

            if (_gaugeBarFill != null)
                _gaugeBarFill.gameObject.SetActive(ratio > 0f);

            UpdateGaugeFollowBar(targetWidth, ratio, followInstantly);
        }

        private float GetGaugeWidthFromRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            return Mathf.Clamp(_gaugeBarMaxWidth * ratio, 0f, _gaugeBarMaxWidth);
        }

        private void UpdateGaugeFollowBar(float targetWidth, float targetRatio, bool instant)
        {
            if (_gaugeBarFollowRect == null)
                return;

            if (_gaugeFollowCoroutine != null)
            {
                StopCoroutine(_gaugeFollowCoroutine);
                _gaugeFollowCoroutine = null;
            }

            if (instant)
            {
                SetGaugeBarWidth(_gaugeBarFollowRect, targetWidth);

                if (_gaugeBarFollow != null)
                    _gaugeBarFollow.gameObject.SetActive(targetRatio > 0f);

                return;
            }

            if (_gaugeBarFollow != null)
                _gaugeBarFollow.gameObject.SetActive(true);

            _gaugeFollowCoroutine = StartCoroutine(GaugeFollowBarRoutine(targetWidth, targetRatio));
        }

        private IEnumerator GaugeFollowBarRoutine(float targetWidth, float targetRatio)
        {
            if (_gaugeFollowDelay > 0f)
                yield return new WaitForSecondsRealtime(_gaugeFollowDelay);

            float startWidth = GetCurrentGaugeBarWidth(_gaugeBarFollowRect);
            startWidth = Mathf.Clamp(startWidth, 0f, _gaugeBarMaxWidth);

            float elapsed = 0f;

            while (elapsed < _gaugeFollowCatchUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / _gaugeFollowCatchUpDuration);
                float width = Mathf.Lerp(startWidth, targetWidth, t);

                SetGaugeBarWidth(_gaugeBarFollowRect, width);

                yield return null;
            }

            SetGaugeBarWidth(_gaugeBarFollowRect, targetWidth);

            if (_gaugeBarFollow != null)
                _gaugeBarFollow.gameObject.SetActive(targetRatio > 0f);

            _gaugeFollowCoroutine = null;
        }

        private float GetCurrentGaugeBarWidth(RectTransform rect)
        {
            if (rect == null)
                return 0f;

            float width = Mathf.Abs(rect.rect.width);

            if (width <= 0.01f)
                width = Mathf.Abs(rect.sizeDelta.x);

            return Mathf.Clamp(width, 0f, _gaugeBarMaxWidth);
        }

        private void SetGaugeBarWidth(RectTransform rect, float width)
        {
            if (rect == null)
                return;

            width = Mathf.Clamp(width, 0f, _gaugeBarMaxWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void ApplyAfterguard()
        {
            _afterguardEndRealtime = Mathf.Max(
                _afterguardEndRealtime,
                Time.unscaledTime + _afterguardDuration);
        }

        public float ModifyIncomingDamageByMuryeong(float rawDamage)
        {
            if (rawDamage <= 0f)
                return 0f;

            if (!IsAfterguardActive)
                return rawDamage;

            return rawDamage * (1f - _afterguardDamageReductionRate);
        }

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