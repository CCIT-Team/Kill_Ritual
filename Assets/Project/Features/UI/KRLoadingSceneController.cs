using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillRitual.UI
{
    public class KRLoadingSceneController : MonoBehaviour
    {
        [Header("Fallback")]
        [SerializeField] private string fallbackTargetSceneName = "Temple";

        [Header("Loading UI")]
        [SerializeField] private Slider progressBar;

        [Header("Loading Settings")]
        [SerializeField] private float minimumLoadingTime = 0.5f;

        [Header("Fade")]
        [SerializeField] private KRScreenFader screenFader;

        private void Start()
        {
            StartCoroutine(LoadingRoutine());
        }

        private IEnumerator LoadingRoutine()
        {
            string targetSceneName = KRSceneTransitionData.HasTargetScene
                ? KRSceneTransitionData.TargetSceneName
                : fallbackTargetSceneName;

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[KRLoadingSceneController] 로드할 Target Scene이 없습니다.");
                yield break;
            }

            SetProgress(0f);

            if (screenFader != null)
                yield return screenFader.FadeInRoutine();

            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(targetSceneName);
            asyncOperation.allowSceneActivation = false;

            float elapsed = 0f;

            while (asyncOperation.progress < 0.9f || elapsed < minimumLoadingTime)
            {
                elapsed += Time.unscaledDeltaTime;

                float loadProgress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
                float timeProgress = minimumLoadingTime <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / minimumLoadingTime);

                float progress = Mathf.Min(loadProgress, timeProgress);
                SetProgress(progress);

                yield return null;
            }

            SetProgress(1f);

            if (screenFader != null)
                yield return screenFader.FadeOutRoutine();

            asyncOperation.allowSceneActivation = true;
        }

        private void SetProgress(float value)
        {
            if (progressBar != null)
                progressBar.value = Mathf.Clamp01(value);
        }
    }
}