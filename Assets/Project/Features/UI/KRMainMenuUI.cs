using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillRitual.UI
{
    public class KRMainMenuUI : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string loadingSceneName = "LoadingScene";
        [SerializeField] private string firstPlayableSceneName = "Temple";

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionButton;
        [SerializeField] private Button quitButton;

        [Header("Option Panel")]
        [SerializeField] private GameObject optionPanel;
        [SerializeField] private Button optionCloseButton;

        [Header("Quit Confirm Panel")]
        [SerializeField] private GameObject quitConfirmPanel;
        [SerializeField] private Button quitConfirmButton;
        [SerializeField] private Button quitCancelButton;

        [Header("Fade")]
        [SerializeField] private KRScreenFader screenFader;

        private bool isTransitioning;

        private void Awake()
        {
            CloseAllPanels();

            if (startButton != null)
                startButton.onClick.AddListener(OnClickStartGame);

            if (optionButton != null)
                optionButton.onClick.AddListener(OpenOptionPanel);

            if (quitButton != null)
                quitButton.onClick.AddListener(OpenQuitConfirmPanel);

            if (optionCloseButton != null)
                optionCloseButton.onClick.AddListener(CloseOptionPanel);

            if (quitConfirmButton != null)
                quitConfirmButton.onClick.AddListener(OnClickConfirmQuit);

            if (quitCancelButton != null)
                quitCancelButton.onClick.AddListener(CloseQuitConfirmPanel);
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnClickStartGame);

            if (optionButton != null)
                optionButton.onClick.RemoveListener(OpenOptionPanel);

            if (quitButton != null)
                quitButton.onClick.RemoveListener(OpenQuitConfirmPanel);

            if (optionCloseButton != null)
                optionCloseButton.onClick.RemoveListener(CloseOptionPanel);

            if (quitConfirmButton != null)
                quitConfirmButton.onClick.RemoveListener(OnClickConfirmQuit);

            if (quitCancelButton != null)
                quitCancelButton.onClick.RemoveListener(CloseQuitConfirmPanel);
        }

        private void OnClickStartGame()
        {
            if (isTransitioning)
                return;

            StartCoroutine(StartGameRoutine());
        }

        private IEnumerator StartGameRoutine()
        {
            isTransitioning = true;
            SetMainButtonsInteractable(false);
            CloseAllPanels();

            string targetSceneName = ResolveStartTargetScene();

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[KRMainMenuUI] Target Scene Name이 비어 있습니다.");
                isTransitioning = false;
                SetMainButtonsInteractable(true);
                yield break;
            }

            KRSceneTransitionData.SetGameStart(
                targetSceneName,
                KRGameStartMode.NewGame
            );

            if (screenFader != null)
                yield return screenFader.FadeOutRoutine();

            SceneManager.LoadScene(loadingSceneName);
        }

        private string ResolveStartTargetScene()
        {
            /*
             * 지금은 새 게임 시작만 처리.
             *
             * 나중에 세이브를 붙이면 여기만 바꾸면 됨.
             *
             * 예시:
             * if (KRSaveManager.HasSave())
             *     return KRSaveManager.GetLastSavedSceneName();
             *
             * return firstPlayableSceneName;
             */

            return firstPlayableSceneName;
        }

        private void OpenOptionPanel()
        {
            if (isTransitioning)
                return;

            if (optionPanel != null)
                optionPanel.SetActive(true);

            if (quitConfirmPanel != null)
                quitConfirmPanel.SetActive(false);
        }

        private void CloseOptionPanel()
        {
            if (optionPanel != null)
                optionPanel.SetActive(false);
        }

        private void OpenQuitConfirmPanel()
        {
            if (isTransitioning)
                return;

            if (quitConfirmPanel != null)
                quitConfirmPanel.SetActive(true);

            if (optionPanel != null)
                optionPanel.SetActive(false);
        }

        private void CloseQuitConfirmPanel()
        {
            if (quitConfirmPanel != null)
                quitConfirmPanel.SetActive(false);
        }

        private void CloseAllPanels()
        {
            if (optionPanel != null)
                optionPanel.SetActive(false);

            if (quitConfirmPanel != null)
                quitConfirmPanel.SetActive(false);
        }

        private void OnClickConfirmQuit()
        {
            if (isTransitioning)
                return;

            StartCoroutine(QuitRoutine());
        }

        private IEnumerator QuitRoutine()
        {
            isTransitioning = true;
            SetMainButtonsInteractable(false);

            if (screenFader != null)
                yield return screenFader.FadeOutRoutine();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetMainButtonsInteractable(bool interactable)
        {
            if (startButton != null)
                startButton.interactable = interactable;

            if (optionButton != null)
                optionButton.interactable = interactable;

            if (quitButton != null)
                quitButton.interactable = interactable;
        }
    }
}