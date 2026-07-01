// Assets/Project/Scripts/02_Player/KRGameOverUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillRitual.Player
{
    /// <summary>
    /// 게임오버가 되면 "게임오버 전용 씬"으로 화면을 통째로 전환합니다.
    ///
    /// [이전 방식과의 차이]
    /// 예전에는 현재 화면 위에 "GAME OVER" 글자를 덮어 그리고 시간을 멈췄지만,
    /// 이제는 아예 다른 씬(GameOver 씬)으로 넘어갑니다. 그 씬에서 글자·버튼·배경을
    /// 유니티 화면으로 자유롭게 꾸밀 수 있어 확장에 유리합니다.
    ///
    /// [사용법] 플레이어가 죽으면 KRPlayerDamageFeedback이 이 컴포넌트의 ShowGameOver()를
    /// 호출합니다. 그러면 인스펙터에 지정한 이름의 씬으로 전환됩니다.
    ///
    /// [주의] 전환할 씬(GameOver)은 반드시 File → Build Settings의 "Scenes In Build"
    /// 목록에 등록돼 있어야 합니다. 등록돼 있지 않으면 전환되지 않고 콘솔에 경고가 뜹니다.
    /// </summary>
    public sealed class KRGameOverUI : MonoBehaviour
    {
        [Tooltip("게임오버 시 전환할 씬의 이름입니다. File → Build Settings에 등록된 이름과 " +
                 "정확히 같아야 합니다(대소문자 구분). 예: \"GameOver\"")]
        [SerializeField] private string _gameOverSceneName = "GameOver";

        private bool _isGameOver;

        /// <summary>
        /// 게임오버 씬으로 전환합니다. 플레이어가 죽는 순간 KRPlayerDamageFeedback이 호출합니다.
        /// </summary>
        public void ShowGameOver()
        {
            if (_isGameOver)
            {
                return; // 이미 게임오버 처리가 시작됐다면 중복 실행하지 않습니다.
            }

            _isGameOver = true;

            // 혹시 시간이 멈춰 있던 상태(다른 곳에서 Time.timeScale=0을 했을 수 있음)를 대비해,
            // 씬을 넘어가기 전에 시간 흐름을 정상(1)으로 되돌립니다. 이걸 안 하면 새 씬도 멈춰 있습니다.
            Time.timeScale = 1f;

            // FPS 플레이 중에는 마우스 커서가 숨겨져 있으므로, 게임오버 화면에서
            // 버튼을 클릭할 수 있도록 커서를 다시 보이게 하고 잠금을 풉니다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            LoadGameOverScene();
        }

        private void LoadGameOverScene()
        {
            if (string.IsNullOrEmpty(_gameOverSceneName))
            {
                Debug.LogWarning("[KRGameOverUI] 전환할 게임오버 씬 이름이 비어 있습니다. " +
                                 "인스펙터의 Game Over Scene Name을 설정하세요.");
                return;
            }

            // 지정한 이름의 씬을 불러옵니다(현재 씬은 자동으로 닫힙니다).
            SceneManager.LoadScene(_gameOverSceneName);
        }
    }
}