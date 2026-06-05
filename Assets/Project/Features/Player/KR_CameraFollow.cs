using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("연결 오브젝트")]
    [Tooltip("CameraHolder Empty 오브젝트를 드래그하세요 — 이 오브젝트만 회전합니다")]
    public Transform cameraHolder;

    [Header("마우스 감도")]
    [Tooltip("좌우 감도")]
    public float sensitivityX = 2f;

    [Tooltip("상하 감도")]
    public float sensitivityY = 2f;

    [Header("상하 각도 제한")]
    [Tooltip("위로 볼 수 있는 최대 각도 (0~90)")]
    public float maxLookUp = 80f;

    [Tooltip("아래로 볼 수 있는 최대 각도 (0~90)")]
    public float maxLookDown = 80f;

    [Header("커서 설정")]
    [Tooltip("게임 시작 시 커서 잠금")]
    public bool lockCursorOnStart = true;
    private float xRotation = 0f;

    private float yRotation = 0f;
    void Start()
    {
        if (cameraHolder == null)
        {
            Debug.LogError("[CameraFollow] CameraHolder가 연결되지 않았습니다. " +
                           "Main Camera의 Inspector에서 Camera Holder 필드에 " +
                           "CameraHolder 오브젝트를 드래그하세요.");
            return;
        }

        if (lockCursorOnStart)
            LockCursor();

        float startX = cameraHolder.eulerAngles.x;
        float startY = cameraHolder.eulerAngles.y;
        xRotation = startX > 180f ? startX - 360f : startX;
        yRotation = startY;
    }

    void Update()
    {
        if (cameraHolder == null) return;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            HandleCursorToggle();
            return;
        }

        HandleCursorToggle();

        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookUp, maxLookDown);

        yRotation += mouseX;

        cameraHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                UnlockCursor();
            else
                LockCursor();
        }
    }
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}