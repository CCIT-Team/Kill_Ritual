// Assets/Project/Scripts/Player/KRPlayerDamageFeedback.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual.Player.Combat;
using KillRitual.UI;

namespace KillRitual.Player
{
    /// <summary>
    /// 플레이어 체력, 피격 UI, 저체력 비네트, 피격 카메라 흔들림을 담당합니다.
    ///
    /// 체력바:
    ///   - Image.fillAmount를 쓰지 않고 RectTransform Width를 직접 조절합니다.
    ///   - Fill / Follow Bar는 오른쪽 기준으로 고정됩니다.
    ///   - 체력이 줄어들면 왼쪽에서 오른쪽 방향으로 깎입니다.
    ///
    /// 화면 피격 효과:
    ///   - KRScreenDamageVignette에 체력 비율을 전달합니다.
    ///   - 피격 순간에는 Flash()를 호출합니다.
    ///   - 저체력 상태에서는 체력 비율에 따라 빨간 테두리가 지속 표시됩니다.
    ///
    /// 카메라 피격 흔들림:
    ///   - CameraRoot / CameraShake는 건드리지 않습니다.
    ///   - HitShake Transform 하나만 localPosition / localRotation으로 짧게 흔듭니다.
    ///   - 기존 Look, FOV, 다른 셰이크와 충돌하지 않도록 별도 계층을 사용합니다.
    ///
    /// 무령 잔흔:
    ///   - 무령 반사 성공 직후 일정 시간 동안 받는 피해를 KRMuryeongController에서 감산합니다.
    /// </summary>
    public sealed class KRPlayerDamageFeedback : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [Tooltip("플레이어의 최대 체력입니다.")]
        [Min(1f)]
        [SerializeField] private float _maxHealth = 100f;

        [Header("무령 잔흔")]
        [Tooltip("무령 반사 성공 후 피해 감소를 적용할 컨트롤러입니다. 비워두면 부모/자식 계층에서 자동 탐색합니다.")]
        [SerializeField] private KRMuryeongController _muryeongController;

        [Header("피격 콜라이더")]
        [Tooltip("켜면 시작 시 플레이어 루트의 CapsuleCollider를 피격 판정용 Trigger로 강제 활성화합니다. 기존 판정을 건드리기 싫으면 끄세요.")]
        [SerializeField] private bool _autoEnableHitCollider = false;

        [Header("체력바 UI 연결")]
        [Tooltip("체력바 전체 너비 기준이 되는 RectTransform입니다. 보통 Background를 넣으세요.")]
        [SerializeField] private RectTransform _healthBarWidthReference;

        [Tooltip("현재 체력을 표시하는 실제 Fill Image입니다. Image Type은 Sliced로 설정하세요.")]
        [SerializeField] private Image _healthBarFill;

        [Tooltip("체력 감소량을 보여주는 뒤따라오는 바입니다. Fill 뒤, Background 앞에 배치하세요. Image Type은 Sliced로 설정하세요.")]
        [SerializeField] private Image _healthBarFollow;

        [Tooltip("체력바가 가득 찼을 때의 너비입니다. 0이면 Width Reference의 현재 너비를 자동으로 사용합니다.")]
        [Min(0f)]
        [SerializeField] private float _healthBarMaxWidth = 0f;

        [Tooltip("실행 시 Fill / Follow의 Anchor와 Pivot을 오른쪽 기준으로 강제 설정합니다.")]
        [SerializeField] private bool _forceRightAligned = true;

        [Tooltip("체력 감소 후 Follow Bar가 움직이기 전 대기 시간입니다.")]
        [Min(0f)]
        [SerializeField] private float _followDelay = 0.15f;

        [Tooltip("Follow Bar가 현재 체력바 위치까지 줄어드는 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _followCatchUpDuration = 0.18f;

        [Header("게임오버 연결")]
        [Tooltip("게임오버 화면을 그리는 KRGameOverUI 컴포넌트. 비워두면 자동으로 씬에서 찾습니다.")]
        [SerializeField] private KRGameOverUI _gameOverUI;

        [Header("피격 / 저체력 화면 효과")]
        [Tooltip("화면 피격/저체력 비네트 UI입니다. 비워두면 자식 또는 씬에서 자동 탐색합니다.")]
        [SerializeField] private KRScreenDamageVignette _screenDamageVignette;

        [Tooltip("이 데미지를 기준으로 피격 플래시가 최대 강도에 가까워집니다. 예: 30이면 30 이상 피해에서 거의 최대 플래시.")]
        [Min(1f)]
        [SerializeField] private float _damageFlashReferenceDamage = 30f;

        [Header("피격 카메라 흔들림")]
        [Tooltip("피격 시 흔들 Transform입니다. 현재 구조에서는 CameraRoot/CameraShake/HitShake 중 HitShake를 넣으세요.")]
        [SerializeField] private Transform _hitShakeRoot;

        [Tooltip("비워두면 이름이 HitShake인 자식 Transform을 자동 탐색합니다.")]
        [SerializeField] private bool _autoFindHitShakeRoot = true;

        [Tooltip("이 데미지를 기준으로 카메라 흔들림이 최대 강도에 가까워집니다.")]
        [Min(1f)]
        [SerializeField] private float _shakeReferenceDamage = 30f;

        [Tooltip("피격 흔들림 지속 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _shakeDuration = 0.16f;

        [Tooltip("피격 흔들림 위치 세기입니다.")]
        [Min(0f)]
        [SerializeField] private float _shakePositionStrength = 0.045f;

        [Tooltip("피격 흔들림 회전 세기입니다.")]
        [Min(0f)]
        [SerializeField] private float _shakeRotationStrength = 1.35f;

        [Tooltip("흔들림 진동 속도입니다.")]
        [Min(1f)]
        [SerializeField] private float _shakeFrequency = 38f;

        [Tooltip("작은 피해에도 최소한 이 비율만큼 흔들립니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shakeMinNormalizedStrength = 0.35f;

        [Tooltip("히트스탑/슬로우모션 중에도 카메라 흔들림이 정상 속도로 재생되게 합니다.")]
        [SerializeField] private bool _useUnscaledTimeForShake = true;

        private float _health;

        private RectTransform _healthBarFillRect;
        private RectTransform _healthBarFollowRect;

        private Coroutine _followCoroutine;
        private Coroutine _hitShakeCoroutine;

        private bool _isInvincible;

        private Vector3 _hitShakeBaseLocalPosition;
        private Quaternion _hitShakeBaseLocalRotation;
        private bool _hasCachedHitShakeBaseTransform;

        public bool IsDead => _health <= 0f;
        public bool IsGroggy => false;
        public Vector3 Position => transform.position;

        public float CurrentHealth => _health;
        public float MaxHealth => _maxHealth;

        private void Awake()
        {
            _health = _maxHealth;

            if (_autoEnableHitCollider)
            {
                EnsureHitColliderActive();
            }

            CacheMuryeongController();
            CacheHealthBarReferences();
            CacheScreenDamageVignette();
            CacheHitShakeRoot();

            if (_gameOverUI == null)
            {
                _gameOverUI = FindObjectOfType<KRGameOverUI>();
            }

            UpdateHealthBar(true);
            UpdateScreenDamageVignette();
        }

        private void OnDisable()
        {
            if (_followCoroutine != null)
            {
                StopCoroutine(_followCoroutine);
                _followCoroutine = null;
            }

            if (_hitShakeCoroutine != null)
            {
                StopCoroutine(_hitShakeCoroutine);
                _hitShakeCoroutine = null;
            }

            RestoreHitShakeTransform();
        }

        public void TakeDamage(KRDamageContext context)
        {
            float distanceFromPlayer = context.HitPoint != Vector3.zero
                ? Vector3.Distance(transform.position, context.HitPoint)
                : -1f;

            if (IsDead)
            {
                Debug.Log(
                    $"[플레이어 피격] 무시(이미 사망) — 요청 데미지 {context.DamageAmount}, " +
                    $"속성 {context.Type}, 피격지점 {context.HitPoint}, " +
                    $"플레이어와 거리 {(distanceFromPlayer >= 0f ? distanceFromPlayer.ToString("F2") : "미기록")}m"
                );
                return;
            }

            if (_isInvincible)
            {
                Debug.Log(
                    $"[플레이어 피격] 무시(무적 상태) — 요청 데미지 {context.DamageAmount}, " +
                    $"속성 {context.Type}, 피격지점 {context.HitPoint}, " +
                    $"플레이어와 거리 {(distanceFromPlayer >= 0f ? distanceFromPlayer.ToString("F2") : "미기록")}m"
                );
                return;
            }

            float rawDamage = context.DamageAmount;
            float incomingDamage = rawDamage;

            if (_muryeongController != null)
            {
                incomingDamage = _muryeongController.ModifyIncomingDamageByMuryeong(incomingDamage);
            }

            if (incomingDamage <= 0f)
            {
                Debug.Log(
                    $"[플레이어 피격] 무시(최종 피해 0 이하) — 원본 데미지 {rawDamage}, " +
                    $"무령 적용 후 {incomingDamage}, 속성 {context.Type}, 피격지점 {context.HitPoint}"
                );
                return;
            }

            float previousHealth = _health;
            _health = Mathf.Max(0f, _health - incomingDamage);

            Debug.Log(
                $"[플레이어 피격] 데미지 적용 — 원본 {rawDamage:F1} → 최종 {incomingDamage:F1}, " +
                $"속성 {context.Type}, 체력 {previousHealth:F1} → {_health:F1} / {_maxHealth:F1}, " +
                $"피격지점 {context.HitPoint}, 플레이어 위치 {transform.position}, " +
                $"거리 {(distanceFromPlayer >= 0f ? distanceFromPlayer.ToString("F2") : "미기록")}m, " +
                $"방향 {context.Direction}"
            );

            bool healthReduced = _health < previousHealth;

            // Fill은 즉시 줄어듦.
            // Follow는 데미지를 받았을 때만 늦게 따라옴.
            UpdateHealthBar(!healthReduced);

            // 체력이 낮을수록 지속 빨간 비네트가 표시됨.
            UpdateScreenDamageVignette();

            // 피격 순간 플래시.
            FlashScreenDamage(incomingDamage);

            // 피격 순간 HitShake만 흔듦.
            PlayHitShake(incomingDamage, context.Direction);

            if (_health <= 0f)
            {
                Debug.Log("[플레이어 피격] 체력 0 도달 — 게임오버 처리");
                TriggerGameOver();
            }
        }

        public void Execute(ExecutionSource source = ExecutionSource.Default)
        {
            _health = 0f;
            UpdateHealthBar(false);
            UpdateScreenDamageVignette();
            TriggerGameOver();
        }

        public void Heal(float amount)
        {
            if (IsDead)
            {
                return;
            }

            if (amount <= 0f)
            {
                return;
            }

            _health = Mathf.Min(_maxHealth, _health + amount);

            // 회복 시에는 손실 표시가 필요 없으므로 Fill / Follow 둘 다 즉시 맞춤.
            UpdateHealthBar(true);
            UpdateScreenDamageVignette();
        }

        /// <summary>흡혼 시퀀스 중 무적 상태를 설정합니다.</summary>
        public void SetInvincible(bool invincible)
        {
            _isInvincible = invincible;
        }

        private void CacheMuryeongController()
        {
            if (_muryeongController != null)
            {
                return;
            }

            _muryeongController = GetComponentInParent<KRMuryeongController>();

            if (_muryeongController == null)
            {
                _muryeongController = GetComponentInChildren<KRMuryeongController>(true);
            }

            if (_muryeongController == null)
            {
                Debug.LogWarning("[KRPlayerDamageFeedback] KRMuryeongController를 찾지 못했습니다. 무령 잔흔 피해 감소가 적용되지 않습니다.");
            }
        }

        private void CacheScreenDamageVignette()
        {
            if (_screenDamageVignette != null)
            {
                return;
            }

            _screenDamageVignette = GetComponentInChildren<KRScreenDamageVignette>(true);

            if (_screenDamageVignette == null)
            {
                _screenDamageVignette = FindObjectOfType<KRScreenDamageVignette>(true);
            }

            if (_screenDamageVignette == null)
            {
                Debug.LogWarning(
                    "[KRPlayerDamageFeedback] KRScreenDamageVignette를 찾지 못했습니다. " +
                    "피격/저체력 화면 비네트가 표시되지 않습니다."
                );
            }
        }

        private void CacheHitShakeRoot()
        {
            if (_hitShakeRoot == null && _autoFindHitShakeRoot)
            {
                _hitShakeRoot = FindChildByName(transform, "HitShake");

                if (_hitShakeRoot == null && Camera.main != null)
                {
                    Transform cameraTransform = Camera.main.transform;
                    Transform parent = cameraTransform.parent;

                    while (parent != null)
                    {
                        if (parent.name == "HitShake")
                        {
                            _hitShakeRoot = parent;
                            break;
                        }

                        parent = parent.parent;
                    }
                }
            }

            if (_hitShakeRoot == null)
            {
                Debug.LogWarning(
                    "[KRPlayerDamageFeedback] HitShake Transform을 찾지 못했습니다. " +
                    "피격 카메라 흔들림이 적용되지 않습니다."
                );
                return;
            }

            CacheHitShakeBaseTransform();
        }

        private Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                if (child.name == childName)
                {
                    return child;
                }

                Transform found = FindChildByName(child, childName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void CacheHitShakeBaseTransform()
        {
            if (_hitShakeRoot == null)
            {
                return;
            }

            _hitShakeBaseLocalPosition = _hitShakeRoot.localPosition;
            _hitShakeBaseLocalRotation = _hitShakeRoot.localRotation;
            _hasCachedHitShakeBaseTransform = true;
        }

        private void EnsureHitColliderActive()
        {
            CapsuleCollider hitCollider = GetComponent<CapsuleCollider>();

            if (hitCollider == null)
            {
                Debug.LogWarning(
                    "[KRPlayerDamageFeedback] 피격 판정용 CapsuleCollider를 찾지 못했습니다 — " +
                    "레이캐스트 기반 투사체가 플레이어를 못 맞힐 수 있습니다."
                );
                return;
            }

            hitCollider.enabled = true;
            hitCollider.isTrigger = true;
        }

        private void CacheHealthBarReferences()
        {
            if (_healthBarFill != null)
            {
                _healthBarFillRect = _healthBarFill.rectTransform;
            }

            if (_healthBarFollow != null)
            {
                _healthBarFollowRect = _healthBarFollow.rectTransform;
            }

            if (_forceRightAligned)
            {
                ForceRightAligned(_healthBarFillRect);
                ForceRightAligned(_healthBarFollowRect);
            }

            Canvas.ForceUpdateCanvases();

            if (_healthBarMaxWidth <= 0f)
            {
                _healthBarMaxWidth = GetReferenceWidth();
            }

            if (_healthBarMaxWidth <= 0.01f)
            {
                _healthBarMaxWidth = 100f;
                Debug.LogWarning(
                    "[KRPlayerDamageFeedback] 체력바 최대 너비를 가져오지 못했습니다. " +
                    "_healthBarWidthReference에 Background를 연결하거나 _healthBarMaxWidth를 직접 입력하세요."
                );
            }

            SetBarWidth(_healthBarFillRect, _healthBarMaxWidth);
            SetBarWidth(_healthBarFollowRect, _healthBarMaxWidth);
        }

        private float GetReferenceWidth()
        {
            if (_healthBarWidthReference != null)
            {
                float referenceWidth = Mathf.Abs(_healthBarWidthReference.rect.width);

                if (referenceWidth > 0.01f)
                {
                    return referenceWidth;
                }

                referenceWidth = Mathf.Abs(_healthBarWidthReference.sizeDelta.x);

                if (referenceWidth > 0.01f)
                {
                    return referenceWidth;
                }
            }

            if (_healthBarFillRect != null)
            {
                float fillWidth = Mathf.Abs(_healthBarFillRect.rect.width);

                if (fillWidth > 0.01f)
                {
                    return fillWidth;
                }

                fillWidth = Mathf.Abs(_healthBarFillRect.sizeDelta.x);

                if (fillWidth > 0.01f)
                {
                    return fillWidth;
                }
            }

            return 0f;
        }

        private void ForceRightAligned(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Vector2 size = rect.sizeDelta;
            Vector2 anchoredPosition = rect.anchoredPosition;

            rect.anchorMin = new Vector2(1f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.pivot = new Vector2(1f, rect.pivot.y);

            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private void UpdateHealthBar(bool followInstantly)
        {
            float ratio = _maxHealth > 0f ? Mathf.Clamp01(_health / _maxHealth) : 0f;
            float targetWidth = GetWidthFromRatio(ratio);

            SetBarWidth(_healthBarFillRect, targetWidth);

            if (_healthBarFill != null)
            {
                _healthBarFill.gameObject.SetActive(ratio > 0f);
            }

            UpdateFollowBar(targetWidth, ratio, followInstantly);
        }

        private float GetWidthFromRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            return Mathf.Clamp(_healthBarMaxWidth * ratio, 0f, _healthBarMaxWidth);
        }

        private void UpdateFollowBar(float targetWidth, float targetRatio, bool instant)
        {
            if (_healthBarFollowRect == null)
            {
                return;
            }

            if (_followCoroutine != null)
            {
                StopCoroutine(_followCoroutine);
                _followCoroutine = null;
            }

            if (instant)
            {
                SetBarWidth(_healthBarFollowRect, targetWidth);

                if (_healthBarFollow != null)
                {
                    _healthBarFollow.gameObject.SetActive(targetRatio > 0f);
                }

                return;
            }

            if (_healthBarFollow != null)
            {
                _healthBarFollow.gameObject.SetActive(true);
            }

            _followCoroutine = StartCoroutine(FollowBarRoutine(targetWidth, targetRatio));
        }

        private IEnumerator FollowBarRoutine(float targetWidth, float targetRatio)
        {
            yield return new WaitForSeconds(_followDelay);

            float startWidth = GetCurrentWidth(_healthBarFollowRect);
            startWidth = Mathf.Clamp(startWidth, 0f, _healthBarMaxWidth);

            float elapsed = 0f;

            while (elapsed < _followCatchUpDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / _followCatchUpDuration);
                float width = Mathf.Lerp(startWidth, targetWidth, t);

                SetBarWidth(_healthBarFollowRect, width);

                yield return null;
            }

            SetBarWidth(_healthBarFollowRect, targetWidth);

            if (_healthBarFollow != null)
            {
                _healthBarFollow.gameObject.SetActive(targetRatio > 0f);
            }

            _followCoroutine = null;
        }

        private float GetCurrentWidth(RectTransform rect)
        {
            if (rect == null)
            {
                return 0f;
            }

            float width = Mathf.Abs(rect.rect.width);

            if (width <= 0.01f)
            {
                width = Mathf.Abs(rect.sizeDelta.x);
            }

            return Mathf.Clamp(width, 0f, _healthBarMaxWidth);
        }

        private void SetBarWidth(RectTransform rect, float width)
        {
            if (rect == null)
            {
                return;
            }

            width = Mathf.Clamp(width, 0f, _healthBarMaxWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void FlashScreenDamage(float incomingDamage)
        {
            if (_screenDamageVignette == null)
            {
                return;
            }

            float normalizedDamage = Mathf.Clamp01(incomingDamage / _damageFlashReferenceDamage);
            _screenDamageVignette.Flash(normalizedDamage);
        }

        private void UpdateScreenDamageVignette()
        {
            if (_screenDamageVignette == null)
            {
                return;
            }

            float healthRatio = _maxHealth > 0f
                ? Mathf.Clamp01(_health / _maxHealth)
                : 0f;

            _screenDamageVignette.SetHealthRatio(healthRatio);
        }

        private void PlayHitShake(float incomingDamage, Vector3 damageDirection)
        {
            if (_hitShakeRoot == null)
            {
                return;
            }

            if (!_hasCachedHitShakeBaseTransform)
            {
                CacheHitShakeBaseTransform();
            }

            float normalizedDamage = Mathf.Clamp01(incomingDamage / _shakeReferenceDamage);
            float strength = Mathf.Lerp(_shakeMinNormalizedStrength, 1f, normalizedDamage);

            if (_hitShakeCoroutine != null)
            {
                StopCoroutine(_hitShakeCoroutine);
                RestoreHitShakeTransform();
            }

            _hitShakeCoroutine = StartCoroutine(HitShakeRoutine(strength, damageDirection));
        }

        private IEnumerator HitShakeRoutine(float strength, Vector3 damageDirection)
        {
            if (_hitShakeRoot == null)
            {
                yield break;
            }

            if (!_hasCachedHitShakeBaseTransform)
            {
                CacheHitShakeBaseTransform();
            }

            float elapsed = 0f;
            float seed = Random.value * 100f;

            Vector3 localDamageDirection = Vector3.zero;

            if (damageDirection.sqrMagnitude > 0.0001f)
            {
                // 월드 방향을 HitShake 로컬 방향으로 변환.
                localDamageDirection = _hitShakeRoot.InverseTransformDirection(damageDirection.normalized);
                localDamageDirection.z = 0f;

                if (localDamageDirection.sqrMagnitude > 0.0001f)
                {
                    localDamageDirection.Normalize();
                }
            }

            while (elapsed < _shakeDuration)
            {
                float deltaTime = _useUnscaledTimeForShake
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                elapsed += deltaTime;

                float t = Mathf.Clamp01(elapsed / _shakeDuration);
                float envelope = 1f - t;
                envelope *= envelope;

                float time = elapsed * _shakeFrequency;

                float noiseX = Mathf.PerlinNoise(seed, time) * 2f - 1f;
                float noiseY = Mathf.PerlinNoise(seed + 13.37f, time) * 2f - 1f;
                float noiseRoll = Mathf.PerlinNoise(seed + 29.91f, time) * 2f - 1f;

                Vector3 randomOffset = new Vector3(noiseX, noiseY, 0f) * (_shakePositionStrength * strength * envelope);

                // 피해 방향이 있으면 살짝 반대 방향으로 밀리는 느낌 추가.
                Vector3 directionalOffset = Vector3.zero;

                if (localDamageDirection.sqrMagnitude > 0.0001f)
                {
                    directionalOffset = -localDamageDirection * (_shakePositionStrength * 0.45f * strength * envelope);
                }

                float pitch = -noiseY * _shakeRotationStrength * strength * envelope;
                float yaw = noiseX * _shakeRotationStrength * 0.65f * strength * envelope;
                float roll = noiseRoll * _shakeRotationStrength * strength * envelope;

                _hitShakeRoot.localPosition = _hitShakeBaseLocalPosition + randomOffset + directionalOffset;
                _hitShakeRoot.localRotation = _hitShakeBaseLocalRotation * Quaternion.Euler(pitch, yaw, roll);

                yield return null;
            }

            RestoreHitShakeTransform();
            _hitShakeCoroutine = null;
        }

        private void RestoreHitShakeTransform()
        {
            if (_hitShakeRoot == null)
            {
                return;
            }

            if (!_hasCachedHitShakeBaseTransform)
            {
                return;
            }

            _hitShakeRoot.localPosition = _hitShakeBaseLocalPosition;
            _hitShakeRoot.localRotation = _hitShakeBaseLocalRotation;
        }

        private void TriggerGameOver()
        {
            if (_gameOverUI != null)
            {
                _gameOverUI.ShowGameOver();
            }
            else
            {
                Debug.LogWarning("[KRPlayerDamageFeedback] KRGameOverUI가 연결되지 않아 게임오버 화면을 띄울 수 없습니다.");
            }
        }
    }
}