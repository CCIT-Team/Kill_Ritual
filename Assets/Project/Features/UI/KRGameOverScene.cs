// Assets/Project/Scripts/02_Player/KRGameOverSceneController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillRitual.Player
{
    /// <summary>
    /// 게임오버 씬(GameOver)에 배치하는 컨트롤러입니다.
    /// "다시 시작" 버튼과 "메뉴로" 버튼이 눌렸을 때 알맞은 씬으로 전환합니다.
    ///
    /// [사용법]
    ///   1. 게임오버 씬에 빈 오브젝트(Create Empty)를 하나 만들고 이 스크립트를 붙입니다.
    ///   2. 인스펙터에서 다시 시작할 씬 이름(_playSceneName)과 메뉴 씬 이름(_menuSceneName)을 적습니다.
    ///   3. "다시 시작" 버튼의 OnClick에 이 컴포넌트의 RestartGame()을 연결합니다.
    ///   4. (선택) "메뉴로" 버튼의 OnClick에 GoToMenu()를 연결합니다.
    ///
    /// [주의] 전환할 씬들은 모두 File → Build Settings의 "Scenes In Build"에 등록돼 있어야 합니다.
    /// </summary>
    public sealed class KRGameOverSceneController : MonoBehaviour
    {
        [Tooltip("\"다시 시작\"을 눌렀을 때 불러올 플레이(게임 진행) 씬의 이름입니다. " +
                 "Build Settings에 등록된 이름과 정확히 같아야 합니다. 예: \"TestMonster\"")]
        [SerializeField] private string _playSceneName = "TestMonster";

        [Tooltip("\"메뉴로\"를 눌렀을 때 불러올 메뉴 씬의 이름입니다(선택). 메뉴 버튼을 안 쓰면 비워둬도 됩니다.")]
        [SerializeField] private string _menuSceneName = "";

        /// <summary>"다시 시작" 버튼이 호출합니다. 플레이 씬을 처음부터 다시 불러옵니다.</summary>
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

        /// <summary>"메뉴로" 버튼이 호출합니다(선택). 메뉴 씬을 불러옵니다.</summary>
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

        /// <summary>"게임 종료" 버튼이 호출합니다(선택). 빌드된 게임을 종료합니다(에디터에서는 효과 없음).</summary>
        public void QuitGame()
        {
            Application.Quit();
        }
    }
}