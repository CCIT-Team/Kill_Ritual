using System.Collections;
using System.Collections.Generic;
// Assets/Project/Scripts/02_Player/KRGameOverUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillRitual.Player
{
    /// <summary>
    /// "GAME OVER" 문구를 화면 중앙에 띄우고, 게임을 멈추는 간단한 게임오버 화면입니다.
    /// 재시작(R) 버튼과 종료(ESC를 통한 커서 해제)도 포함합니다.
    ///
    /// UI Canvas나 별도 폰트 에셋 없이 OnGUI만으로 동작하므로, 처음 만드는 단계에서
    /// 추가 설정 없이 바로 쓸 수 있습니다. 나중에 멋진 UI Canvas로 교체하면 됩니다.
    /// </summary>
    public sealed class KRGameOverUI : MonoBehaviour
    {
        [Tooltip("게임오버 시 마우스 커서를 다시 보이게 할지 여부.")]
        [SerializeField] private bool _unlockCursorOnGameOver = true;

        [Tooltip("게임오버 시 시간을 멈출지 여부(true면 모든 움직임이 정지합니다).")]
        [SerializeField] private bool _pauseTimeOnGameOver = true;

        private bool _isGameOver;

        /// <summary>게임오버 화면을 띄웁니다. KRPlayerDamageFeedback이 사망 시 호출합니다.</summary>
        public void ShowGameOver()
        {
            if (_isGameOver)
            {
                return; // 이미 게임오버면 중복 처리하지 않습니다.
            }

            _isGameOver = true;

            if (_unlockCursorOnGameOver)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (_pauseTimeOnGameOver)
            {
                Time.timeScale = 0f; // 시간을 멈춥니다. 재시작 시 다시 1로 되돌립니다.
            }
        }

        private void Update()
        {
            if (!_isGameOver)
            {
                return;
            }

            // 게임오버 상태에서 R 키를 누르면 현재 씬을 다시 로드해 재시작합니다.
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
        }

        private void RestartGame()
        {
            // 멈췄던 시간을 반드시 원래대로 돌려놓아야 다시 시작했을 때 게임이 움직입니다.
            Time.timeScale = 1f;

            // 현재 씬을 처음부터 다시 불러옵니다.
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }

        private void OnGUI()
        {
            if (!_isGameOver)
            {
                return;
            }

            // 화면 전체를 어둡게 덮습니다.
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // "GAME OVER" 큰 글씨.
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.red;

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
            hintStyle.normal.textColor = Color.white;

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;

            GUI.Label(new Rect(centerX - 400, centerY - 80, 800, 120), "GAME OVER", titleStyle);
            GUI.Label(new Rect(centerX - 400, centerY + 40, 800, 40), "R 키를 눌러 재시작", hintStyle);
        }
    }
}