using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// =====================================================================
/// CameraFollow.cs
///
/// [역할] 마우스 입력으로 카메라 시점을 회전시키는 스크립트
/// [부착 위치] Main Camera
///
/// [동작 방식]
///   - 상하 회전(X축): CameraHolder Empty 오브젝트를 회전
///                     (카메라만 위아래로 움직이고 몸통은 고정)
///   - 좌우 회전(Y축): CameraHolder를 월드 Y축 기준으로 회전 +
///                     PlayerRoot에도 같은 Y값을 전달
///                     (몸통도 같이 돌아야 이동 방향이 카메라와 일치)
///
/// [왜 빙글빙글 돌았나?]
///   이전 구조의 문제:
///     PlayerController가 transform.Rotate(Vector3.up * mouseX) 로
///     매 프레임 회전을 '누적'했고,
///     CameraFollow도 동시에 cameraHolder.rotation 을 월드 좌표로 설정했음.
///     두 스크립트가 같은 오브젝트 계층의 회전을 동시에 건드리면서
///     회전값이 서로 간섭 → 빙글빙글 현상 발생.
///
///   수정된 구조:
///     카메라 회전의 '원천'은 이 스크립트(CameraFollow) 하나뿐.
///     PlayerController는 회전을 스스로 계산하지 않고,
///     이 스크립트가 SetYRotation()으로 전달하는 값만 받아서 적용.
///     → 회전 계산이 한 곳에서만 일어나므로 간섭 없음.
///
/// [팀원 참고사항]
///   Inspector에서 Player Root 필드에 PlayerRoot 오브젝트를 반드시 연결할 것.
///   연결하지 않으면 몸통이 돌지 않아 WASD 이동 방향이 카메라와 따로 놀게 됨.
///
/// [수정 내역]
///   v1.0 - KR_CameraFollow.cs 기반으로 재작성
///   v1.1 - GetAxis → GetAxisRaw 변경 (드리프트 수정)
///          PlayerController 연동 추가 (SetYRotation 호출)
///          월드 회전 방식 유지 (로컬 회전의 Gimbal Lock 문제 방지)
/// =====================================================================
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("=== 연결 오브젝트 ===")]
    [Tooltip(
        "CameraHolder Empty 오브젝트를 여기에 드래그하세요.\n" +
        "이 오브젝트가 상하/좌우 회전을 담당합니다.\n" +
        "Main Camera는 이 오브젝트의 자식이어야 합니다.")]
    public Transform cameraHolder;

    [Tooltip(
        "PlayerRoot 오브젝트를 여기에 드래그하세요.\n" +
        "좌우 회전값을 PlayerRoot에 전달해서\n" +
        "WASD 이동 방향이 카메라와 일치하게 됩니다.")]
    public PlayerController playerController;

    [Header("=== 마우스 감도 ===")]
    [Tooltip("좌우 감도. 클수록 좌우 회전이 빠름")]
    public float sensitivityX = 2f;

    [Tooltip("상하 감도. 클수록 상하 회전이 빠름")]
    public float sensitivityY = 2f;

    [Header("=== 상하 각도 제한 ===")]
    [Tooltip("위로 볼 수 있는 최대 각도 (0~90)")]
    public float maxLookUp = 80f;

    [Tooltip("아래로 볼 수 있는 최대 각도 (0~90)")]
    public float maxLookDown = 80f;

    [Header("=== 커서 설정 ===")]
    [Tooltip("게임 시작 시 커서를 자동으로 잠글지 여부")]
    public bool lockCursorOnStart = true;

    // =====================================================================
    // 내부 상태 변수
    // =====================================================================

    /// <summary>카메라 상하 회전 누적값 (위 = 음수, 아래 = 양수)</summary>
    private float xRotation = 0f;

    /// <summary>카메라 좌우 회전 누적값. PlayerRoot에도 이 값을 전달함</summary>
    private float yRotation = 0f;

    // =====================================================================
    // Unity 생명주기
    // =====================================================================

    private void Start()
    {
        // 필수 연결 확인
        if (cameraHolder == null)
        {
            Debug.LogError("[CameraFollow] CameraHolder가 연결되지 않았습니다!\n" +
                           "Main Camera의 Inspector → Camera Holder 필드에\n" +
                           "CameraHolder 오브젝트를 드래그하세요.");
            return;
        }

        if (playerController == null)
        {
            Debug.LogWarning("[CameraFollow] PlayerController가 연결되지 않았습니다.\n" +
                             "Main Camera의 Inspector → Player Controller 필드에\n" +
                             "PlayerRoot 오브젝트를 드래그하세요.\n" +
                             "연결하지 않으면 WASD 이동 방향이 카메라와 다르게 됩니다.");
        }

        if (lockCursorOnStart)
            LockCursor();

        // 시작 시 CameraHolder의 현재 각도를 초기값으로 읽어옴
        // (씬에서 미리 각도를 설정해둔 경우 이어서 사용)
        float startX = cameraHolder.eulerAngles.x;
        xRotation = startX > 180f ? startX - 360f : startX;
        yRotation = cameraHolder.eulerAngles.y;
    }

    private void Update()
    {
        if (cameraHolder == null) return;

        // ESC 키로 커서 잠금/해제 토글
        HandleCursorToggle();

        // 커서가 잠겨있을 때만 카메라 회전 처리
        if (Cursor.lockState != CursorLockMode.Locked) return;

        HandleCameraRotation();
    }

    // =====================================================================
    // 카메라 회전
    // =====================================================================

    /// <summary>
    /// 마우스 입력으로 상하/좌우 회전값을 누적하고 CameraHolder에 적용.
    /// 좌우 회전값(yRotation)은 PlayerController에도 전달.
    ///
    /// GetAxisRaw 사용 이유:
    ///   GetAxis는 내부 보간 때문에 마우스를 멈춰도 잔여값이 남아
    ///   카메라가 계속 움직이는 드리프트 현상이 발생함.
    ///   GetAxisRaw는 보간 없이 이번 프레임의 실제 값만 반환 → 드리프트 없음.
    ///
    /// 월드 회전(cameraHolder.rotation) 사용 이유:
    ///   로컬 회전(localRotation)은 부모 오브젝트가 회전할 때
    ///   예상치 못한 축 뒤틀림(Gimbal Lock)이 발생할 수 있음.
    ///   월드 좌표계 기준으로 회전하면 항상 안정적.
    /// </summary>
    private void HandleCameraRotation()
    {
        // GetAxisRaw: 보간 없이 이번 프레임의 실제 마우스 이동량만 반환
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivityY;

        // 상하 회전 누적 및 제한
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookUp, maxLookDown);

        // 좌우 회전 누적
        yRotation += mouseX;

        // CameraHolder를 월드 좌표 기준으로 회전 적용
        cameraHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

        // PlayerRoot에 좌우 회전값 전달
        // PlayerController는 이 값으로 자신의 rotation을 설정함
        // → WASD 이동이 카메라가 바라보는 방향과 일치
        if (playerController != null)
            playerController.SetYRotation(yRotation);
    }

    // =====================================================================
    // 커서 잠금 토글
    // =====================================================================

    private void HandleCursorToggle()
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