using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 카메라 정면으로 광선을 쏴서 상호작용 구역을 감지하고,
// F키를 누르면 씬 선택 창을 여는 스크립트입니다.
// 이 스크립트는 플레이어 카메라(Main Camera 등)에 붙이세요.
public class PlayerInteraction : MonoBehaviour
{
    [Header("광선이 닿을 최대 거리")]
    public float interactDistance = 4f;

    [Header("상호작용 가능한 레이어 (Interactable 선택)")]
    public LayerMask interactableLayer;

    [Header("'F를 누르세요' 안내 UI (InteractPrompt 연결)")]
    public GameObject interactPrompt;

    [Header("씬 선택 메뉴 스크립트 (SceneSelectManager 연결)")]
    public SceneSelectMenu sceneSelectMenu;

    void Update()
    {
        // 메뉴가 이미 열려 있으면 감지를 멈춘다 (중복 방지)
        if (sceneSelectMenu != null && sceneSelectMenu.IsOpen)
        {
            // 안내문은 꺼둔다
            if (interactPrompt != null) interactPrompt.SetActive(false);
            return;
        }

        // 카메라 정면 방향으로 광선을 만든다
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // 광선이 interactDistance 거리 안에서 interactableLayer에 닿았는지 검사
        bool isLookingAtZone =
            Physics.Raycast(ray, out hit, interactDistance, interactableLayer);

        if (isLookingAtZone)
        {
            // 구역을 보고 있으면 안내문 켜기
            if (interactPrompt != null) interactPrompt.SetActive(true);

            // F키를 누르면 메뉴 열기
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (sceneSelectMenu != null) sceneSelectMenu.OpenMenu();
                if (interactPrompt != null) interactPrompt.SetActive(false);
            }
        }
        else
        {
            // 안 보고 있으면 안내문 끄기
            if (interactPrompt != null) interactPrompt.SetActive(false);
        }
    }

    // (참고용) Scene 창에서 광선을 노란 선으로 시각화 — 디버깅에 도움
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * interactDistance);
    }
}