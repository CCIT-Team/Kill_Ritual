using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player
{
    /// <summary>
    /// 플레이어의 체력과 피격 반응을 담당합니다.
    ///
    /// 체력바 처리 방식:
    ///   1. Image.fillAmount를 사용하지 않습니다.
    ///   2. Sliced Image의 RectTransform Width를 직접 조절합니다.
    ///   3. Fill / Follow Bar는 오른쪽 기준으로 고정됩니다.
    ///   4. 체력이 줄어들면 왼쪽에서 오른쪽 방향으로 깎입니다.
    ///   5. Follow Bar는 실제 체력바보다 늦게 따라옵니다.
    /// </summary>
    public sealed class KRPlayerDamageFeedback : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [Tooltip("플레이어의 최대 체력입니다.")]
        [Min(1f)]
        [SerializeField] private float _maxHealth = 100f;

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

        [Header("피격 화면 효과")]
        [Tooltip("피격 시 잠깐 빨갛게 보일 전체화면 Image입니다.")]
        [SerializeField] private Image _damageOverlay;

        [Tooltip("피격 효과가 사라지는 속도. 클수록 빨리 옅어집니다.")]
        [Min(0.1f)]
        [SerializeField] private float _fadeSpeed = 3f;

        private float _health;
        private float _overlayAlpha;

        private RectTransform _healthBarFillRect;
        private RectTransform _healthBarFollowRect;

        private Coroutine _followCoroutine;
        private bool _isInvincible;
        public bool IsDead => _health <= 0f;
        public bool IsGroggy => false;
        public Vector3 Position => transform.position;

        public float CurrentHealth => _health;
        public float MaxHealth => _maxHealth;

        private void Awake()
        {
            _health = _maxHealth;

            CacheHealthBarReferences();

            if (_gameOverUI == null)
            {
                _gameOverUI = FindObjectOfType<KRGameOverUI>();
            }

            UpdateHealthBar(true);
            HideOverlayInstantly();
        }

        private void OnDisable()
        {
            if (_followCoroutine != null)
            {
                StopCoroutine(_followCoroutine);
                _followCoroutine = null;
            }
        }

        private void Update()
        {
            if (_damageOverlay != null && _overlayAlpha > 0f)
            {
                _overlayAlpha = Mathf.Max(0f, _overlayAlpha - _fadeSpeed * Time.deltaTime);
                SetOverlayAlpha(_overlayAlpha);
            }
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead) return;
            if (_isInvincible) return;

            float previousHealth = _health;
            _health = Mathf.Max(0f, _health - context.DamageAmount);

            bool healthReduced = _health < previousHealth;

            // Fill은 즉시 줄어듦.
            // Follow는 데미지를 받았을 때만 늦게 따라옴.
            UpdateHealthBar(!healthReduced);

            if (_damageOverlay != null)
            {
                _overlayAlpha = 1f;
                SetOverlayAlpha(1f);
            }

            if (_health <= 0f)
            {
                TriggerGameOver();
            }
        }

        public void Execute()
        {
            _health = 0f;
            UpdateHealthBar(false);
            TriggerGameOver();
        }

        public void Heal(float amount)
        {
            if (IsDead)
            {
                return;
            }

            _health = Mathf.Min(_maxHealth, _health + amount);

            // 회복 시에는 손실 표시가 필요 없으므로 Fill / Follow 둘 다 즉시 맞춤.
            UpdateHealthBar(true);
        }
        /// <summary>흡혼 시퀀스 중 무적 상태를 설정합니다.</summary>
        public void SetInvincible(bool invincible)
        {
            _isInvincible = invincible;
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
                Debug.LogWarning("[KRPlayerDamageFeedback] 체력바 최대 너비를 가져오지 못했습니다. _healthBarWidthReference에 Background를 연결하거나 _healthBarMaxWidth를 직접 입력하세요.");
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

            // Reference가 없을 경우에만 Fill에서 가져옴.
            // 하지만 이 방식은 Fill이 이미 줄어든 상태면 잘못된 값을 가져올 수 있음.
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

        private void SetOverlayAlpha(float alpha)
        {
            if (_damageOverlay == null)
            {
                return;
            }

            Color c = _damageOverlay.color;
            c.a = alpha;
            _damageOverlay.color = c;
        }

        private void HideOverlayInstantly()
        {
            _overlayAlpha = 0f;
            SetOverlayAlpha(0f);
        }
    }
}