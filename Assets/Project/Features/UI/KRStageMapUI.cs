using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.StagePortal
{
    public class KRStageMapUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject rootObject;
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Header("Detail")]
        [SerializeField] private TMP_Text stageNameText;
        [SerializeField] private TMP_Text overviewText;
        [SerializeField] private Image stageIconImage;

        [Header("Buttons")]
        [SerializeField] private Button deployButton;
        [SerializeField] private Button backButton;

        [Header("Launch")]
        [SerializeField] private KRStageLauncher stageLauncher;

        [Header("UX Timing - Seconds")]
        [Min(0f)]
        [SerializeField] private float openFadeSeconds = 0.2f;

        [Min(0f)]
        [SerializeField] private float closeFadeSeconds = 0.15f;

        [Min(0f)]
        [SerializeField] private float launchDelaySeconds = 0f;

        [Header("UX Options")]
        [SerializeField] private bool pauseGameWhileOpen = true;
        [SerializeField] private bool unlockCursorWhileOpen = true;
        [SerializeField] private bool closeWithEscape = true;

        private KRStageData selectedStage;
        private bool isOpen;
        private bool isLaunching;

        private Coroutine rootFadeRoutine;
        private Coroutine launchRoutine;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            SetRootImmediate(false);
            ClearDetail();

            if (deployButton != null)
            {
                deployButton.onClick.AddListener(OnDeployClicked);
                deployButton.interactable = false;
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }
        }

        private void OnDestroy()
        {
            if (deployButton != null)
                deployButton.onClick.RemoveListener(OnDeployClicked);

            if (backButton != null)
                backButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (!isOpen)
                return;

            if (!closeWithEscape)
                return;

            if (isLaunching)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        public void Open()
        {
            if (isOpen)
                return;

            isOpen = true;
            isLaunching = false;
            selectedStage = null;

            ClearDetail();

            if (rootObject != null)
                rootObject.SetActive(true);

            if (pauseGameWhileOpen)
                Time.timeScale = 0f;

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (rootFadeRoutine != null)
                StopCoroutine(rootFadeRoutine);

            rootFadeRoutine = StartCoroutine(FadeCanvasGroup(
                rootCanvasGroup,
                1f,
                openFadeSeconds
            ));
        }

        public void Close()
        {
            if (!isOpen)
                return;

            if (isLaunching)
                return;

            isOpen = false;
            selectedStage = null;

            if (pauseGameWhileOpen)
                Time.timeScale = 1f;

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (rootFadeRoutine != null)
                StopCoroutine(rootFadeRoutine);

            rootFadeRoutine = StartCoroutine(CloseRoutine());
        }

        public void SelectStage(KRStageData stageData)
        {
            if (!isOpen)
                return;

            if (stageData == null)
                return;

            selectedStage = stageData;

            // 디테일 전환은 페이드 없이 즉시 갱신.
            ApplyStageDetail(stageData);
        }

        private void ApplyStageDetail(KRStageData stageData)
        {
            if (stageNameText != null)
                stageNameText.text = stageData.DisplayName;

            if (overviewText != null)
                overviewText.text = stageData.Overview;

            if (stageIconImage != null)
            {
                stageIconImage.sprite = stageData.StageIcon;
                stageIconImage.enabled = stageData.StageIcon != null;
            }

            if (deployButton != null)
                deployButton.interactable = stageData.Unlocked;
        }

        private void ClearDetail()
        {
            if (stageNameText != null)
                stageNameText.text = "스테이지를 선택하세요";

            if (overviewText != null)
                overviewText.text = "";

            if (stageIconImage != null)
            {
                stageIconImage.sprite = null;
                stageIconImage.enabled = false;
            }

            if (deployButton != null)
                deployButton.interactable = false;
        }

        private void OnDeployClicked()
        {
            if (isLaunching)
                return;

            if (selectedStage == null)
                return;

            if (!selectedStage.Unlocked)
                return;

            if (stageLauncher == null)
            {
                Debug.LogWarning($"{nameof(KRStageMapUI)}: StageLauncher가 연결되지 않았습니다.", this);
                return;
            }

            launchRoutine = StartCoroutine(LaunchRoutine());
        }

        private IEnumerator LaunchRoutine()
        {
            isLaunching = true;

            if (deployButton != null)
                deployButton.interactable = false;

            if (backButton != null)
                backButton.interactable = false;

            // 나중에 출정 연출, 사운드, DOTween 등을 넣을 자리.
            if (launchDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(launchDelaySeconds);

            Time.timeScale = 1f;

            stageLauncher.Launch(selectedStage);
        }

        private IEnumerator CloseRoutine()
        {
            yield return FadeCanvasGroup(rootCanvasGroup, 0f, closeFadeSeconds);

            ClearDetail();

            if (rootObject != null)
                rootObject.SetActive(false);
        }

        private void SetRootImmediate(bool show)
        {
            if (rootObject != null)
                rootObject.SetActive(show);

            if (rootCanvasGroup == null)
                return;

            rootCanvasGroup.alpha = show ? 1f : 0f;
            rootCanvasGroup.interactable = show;
            rootCanvasGroup.blocksRaycasts = show;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration)
        {
            if (canvasGroup == null)
                yield break;

            float startAlpha = canvasGroup.alpha;

            canvasGroup.interactable = targetAlpha > 0f;
            canvasGroup.blocksRaycasts = targetAlpha > 0f;

            if (duration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }
    }
}