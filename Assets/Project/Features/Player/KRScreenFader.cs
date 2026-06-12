using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillRitual.UI
{
    public class KRScreenFader : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Fade Settings")]
        [SerializeField] private float fadeInDuration = 0.6f;
        [SerializeField] private float fadeOutDuration = 0.6f;
        [SerializeField] private bool fadeInOnStart = true;
        [SerializeField] private bool startFromBlack = true;

        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                Debug.LogError("[KRScreenFader] CanvasGroup이 없습니다.");
                enabled = false;
                return;
            }

            // 메인 메뉴는 시작할 때 검은 화면으로 덮지 않는다.
            if (IsMainMenuScene())
            {
                SetClear();
                return;
            }

            if (startFromBlack)
                SetBlack();
            else
                SetClear();
        }

        private void Start()
        {
            // 메인 메뉴는 자동 Fade In도 하지 않는다.
            // 단, 버튼 클릭 시 FadeOutRoutine()은 정상 작동한다.
            if (IsMainMenuScene())
                return;

            if (fadeInOnStart)
                FadeIn();
        }

        private bool IsMainMenuScene()
        {
            return SceneManager.GetActiveScene().name == MainMenuSceneName;
        }

        public void SetBlack()
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
        }

        public void SetClear()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public void FadeIn()
        {
            StartFade(0f, fadeInDuration);
        }

        public void FadeOut()
        {
            StartFade(1f, fadeOutDuration);
        }

        public IEnumerator FadeInRoutine()
        {
            yield return FadeRoutine(0f, fadeInDuration);
        }

        public IEnumerator FadeOutRoutine()
        {
            yield return FadeRoutine(1f, fadeOutDuration);
        }

        public IEnumerator FadeInRoutine(float duration)
        {
            yield return FadeRoutine(0f, duration);
        }

        public IEnumerator FadeOutRoutine(float duration)
        {
            yield return FadeRoutine(1f, duration);
        }

        private void StartFade(float targetAlpha, float duration)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            canvasGroup.blocksRaycasts = true;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            if (duration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;

                    float t = Mathf.Clamp01(elapsed / duration);
                    t = Mathf.SmoothStep(0f, 1f, t);

                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                    yield return null;
                }
            }

            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
            canvasGroup.interactable = false;

            fadeRoutine = null;
        }
    }
}