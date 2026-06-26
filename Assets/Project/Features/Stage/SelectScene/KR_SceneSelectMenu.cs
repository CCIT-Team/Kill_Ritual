using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 선택 창(Panel)을 열고 닫고, 버튼으로 씬을 전환하는 스크립트입니다.
public class SceneSelectMenu : MonoBehaviour
{
    [Header("씬 선택 창 패널 (SceneSelectPanel을 연결하세요)")]
    public GameObject panel;

    // 창이 현재 열려 있는지 외부에서 확인할 수 있도록 공개
    public bool IsOpen { get; private set; }

    void Start()
    {
        // 시작할 때는 창을 닫아둔다
        CloseMenu();
    }
    void Update()
    {
        // 창이 열려 있을 때 ESC를 누르면 닫는다
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }
    }

    // 창 열기
    public void OpenMenu()
    {
        panel.SetActive(true);
        IsOpen = true;

        // FPS는 보통 마우스가 잠겨 있으므로, 창이 열리면 커서를 풀어준다
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 창 닫기
    public void CloseMenu()
    {
        panel.SetActive(false);
        IsOpen = false;

        // 창을 닫으면 다시 FPS 조준 상태로 (커서 잠금)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 버튼에 연결할 씬 전환 함수
    public void LoadScene(string sceneName)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 1) 어느 맵으로 갈지 팀 공용 그릇에 저장 (시작 모드는 기본 NewGame)
        KillRitual.UI.KRSceneTransitionData.SetGameStart(sceneName);

        // 2) 맵을 직접 부르지 않고, 로딩 씬으로 먼저 이동
        //    (로딩 씬이 알아서 맵을 비동기로 불러옴)
        SceneManager.LoadScene("Loading");
    }
}