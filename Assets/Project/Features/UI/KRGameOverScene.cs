// Assets/Project/Scripts/02_Player/KRGameOverSceneController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillRitual.Player
{
    public sealed class KRGameOverSceneController : MonoBehaviour
    {
        [Tooltip("\"다시 시작\"을 눌렀을 때 불러올 플레이(게임 진행) 씬의 이름입니다. " +
                 "Build Settings에 등록된 이름과 정확히 같아야 합니다. 예: \"TestMonster\"")]
        [SerializeField] private string _playSceneName = "TestMonster";

        [Tooltip("\"메뉴로\"를 눌렀을 때 불러올 메뉴 씬의 이름입니다(선택). 메뉴 버튼을 안 쓰면 비워둬도 됩니다.")]
        [SerializeField] private string _menuSceneName = "";

        public void RestartGame()
        {
            if (string.IsNullOrEmpty(_playSceneName))
            {
                Debug.LogWarning("[KRGameOverSceneController] 다시 시작할 씬 이름이 비어 있습니다. " +
                                 "인스펙터의 Play Scene Name을 설정하세요.");
                return;
            }

            // 혹시 시간이 멈춰 있을 수 있으므로 정상 속도로 되돌린 뒤 씬을 불러옵니다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(_playSceneName);
        }

        public void GoToMenu()
        {
            if (string.IsNullOrEmpty(_menuSceneName))
            {
                Debug.LogWarning("[KRGameOverSceneController] 메뉴 씬 이름이 비어 있습니다. " +
                                 "인스펙터의 Menu Scene Name을 설정하세요.");
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(_menuSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}