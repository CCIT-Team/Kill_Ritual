using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillRitual.UI
{
    /// <summary>
    /// LoadingScene에서 목표 씬을 비동기 로딩하는 컨트롤러.
    /// 목표 씬이 활성화된 뒤에도 마지막 페이드 해제를 해야 하므로,
    /// 로딩이 끝날 때까지 DontDestroyOnLoad로 유지한다.
    /// </summary>
    public sealed class KRLoadingSceneController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Slider progressBar;

        [Header("Timing")]
        [SerializeField] private float minimumLoadingTime = 0.5f;
        [SerializeField] private int postActivationFrameDelay = 3;

        private void Awake()
        {
            // LoadingScene이 언로드되어도 이 컨트롤러가 마지막 FadeIn까지 실행할 수 있게 유지
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(LoadTargetScene());
        }

        private IEnumerator LoadTargetScene()
        {
            KRSceneTransition transition = KRSceneTransition.EnsureInstance();

            SetProgress(0f);

            // 검은 화면을 걷어서 LoadingScene UI를 보여줌
            yield return transition.FadeIn();

            string targetSceneName = KRSceneLoader.TargetSceneName;

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("불러올 목표 씬이 없음");
                transition.FinishTransition();
                Destroy(gameObject);
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            operation.allowSceneActivation = false;

            float elapsed = 0f;

            // activation 전까지 Unity 비동기 로딩은 보통 progress 0.9에서 멈춤
            while (operation.progress < 0.9f || elapsed < minimumLoadingTime)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                SetProgress(progress);

                yield return null;
            }

            SetProgress(1f);

            // 목표 씬으로 넘어가기 전 검은 화면으로 다시 덮음
            yield return transition.FadeOut();

            // 여기서 LoadingScene이 언로드됨.
            // 그래서 이 스크립트는 DontDestroyOnLoad 상태여야 아래 코드가 계속 실행됨.
            operation.allowSceneActivation = true;

            while (!operation.isDone)
                yield return null;

            // 목표 씬 활성화 직후 메시/텍스처 팝인을 가리기 위한 대기
            for (int i = 0; i < postActivationFrameDelay; i++)
                yield return new WaitForEndOfFrame();

            // 검은 화면을 걷어서 목표 씬을 보여줌
            yield return transition.FadeIn();

            transition.FinishTransition();

            // LoadingSceneController는 역할이 끝났으므로 제거
            Destroy(gameObject);
        }

        private void SetProgress(float value)
        {
            if (progressBar != null)
                progressBar.value = Mathf.Clamp01(value);
        }
    }
}