using System.Collections;
using System.Collections.Generic;
// =====================================================================
// PlayerManager.cs
// =====================================================================
// [역할]
//   플레이어 오브젝트의 "총괄 지휘자"입니다.
//   - PlayerState(공유 데이터)를 생성하고 초기화
//   - BasicMove / Jump / Dash 모듈 인스턴스를 생성
//   - Update / FixedUpdate 타이밍에 각 모듈의 함수를 순서대로 호출
//
// [유니티 설정 방법]
//   1. Player 오브젝트(Capsule)에 이 스크립트만 Add Component 합니다.
//      → BasicMove, Jump, Dash 스크립트는 직접 붙이지 않아도 됩니다.
//         PlayerManager가 코드로 생성합니다.
//   2. Inspector에서 아래 항목을 설정합니다:
//      - Ground Layer : 바닥 오브젝트의 레이어 (Ground)
//      - 이동/점프/대쉬 수치들
//   3. Main Camera가 씬에 존재해야 합니다 (자동 연결됨).
//
// [팀 협업 팁]
//   각자 담당 모듈(BasicMove, Jump, Dash)만 수정하면 됩니다.
//   PlayerManager는 건드릴 일이 거의 없습니다.
// =====================================================================

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
// RequireComponent: 이 스크립트를 붙이면 Rigidbody도 자동으로 함께 붙음
public class PlayerManager : MonoBehaviour
{
    // =====================================================================
    // [Inspector 설정값 — 이동]
    // Header: Inspector에서 구역 제목을 표시해 줌
    // Tooltip: Inspector에서 마우스를 올리면 설명이 나옴
    // =====================================================================

    [Header("이동 설정")]
    [Tooltip("걷기 속도 (m/s)")]
    public float walkSpeed = 6f;

    [Tooltip("달리기 속도 (m/s)")]
    public float runSpeed = 10f;

    [Tooltip("지상 가속도 — 클수록 목표 속도에 빠르게 도달 (기본 80)")]
    public float groundAcceleration = 80f;

    [Tooltip("지상 감속도 — 입력 없을 때 멈추는 빠르기 (기본 60)")]
    public float groundDeceleration = 60f;

    [Tooltip("공중 가속도 — 지상보다 낮게 설정 (기본 30)")]
    public float airAcceleration = 30f;

    [Tooltip("공중 최대 속도 배율 (0~1, 기본 0.8)")]
    public float airControlMultiplier = 0.8f;

    [Header("점프 설정")]
    [Tooltip("1단 점프 힘 — 가변 중력 적용 시 12~16 권장")]
    public float jumpForce = 13f;

    [Tooltip("2단 점프 힘")]
    public float doubleJumpForce = 11f;

    [Tooltip("상승 중 추가 중력 배율 — 클수록 묵직한 상승 (기본 2.5)")]
    public float riseGravityMultiplier = 2.5f;

    [Tooltip("하강 중 추가 중력 배율 — 클수록 빠른 낙하 (기본 4.0)")]
    public float fallGravityMultiplier = 4.0f;

    [Tooltip("Space 일찍 뗄 때 감속 배율 — 짧은 점프 제어 (기본 5.0)")]
    public float shortHopMultiplier = 5.0f;

    [Header("대쉬 설정")]
    [Tooltip("대쉬 속도/거리")]
    public float dashForce = 18f;

    [Tooltip("대쉬 지속 시간 (초)")]
    public float dashDuration = 0.15f;

    [Tooltip("대쉬 1회 쿨다운 (초)")]
    public float dashCooldown = 2.5f;

    [Tooltip("최대 대쉬 충전 횟수")]
    public int maxDashCharges = 2;

    [Header("바닥 감지")]
    [Tooltip("발 아래 감지 거리")]
    public float groundCheckDistance = 0.1f;

    [Tooltip("바닥 레이어 — Inspector에서 Ground 레이어 선택")]
    public LayerMask groundLayer;

    [Header("카메라 연결")]
    [Tooltip("CameraHolder Empty 오브젝트를 여기에 드래그하세요\n이동 방향의 기준이 되는 Transform입니다")]
    public Transform cameraHolder;

    // =====================================================================
    // [내부 참조 — 코드에서만 사용]
    // =====================================================================

    // 공유 상태 데이터 객체
    // [HideInInspector]: Inspector에 표시하지 않음 (코드 전용)
    [HideInInspector] public PlayerState state;

    // 기능 모듈들 (MonoBehaviour 아님 — 일반 클래스)
    private PlayerGroundChecker groundChecker;
    private BasicMove basicMove;
    private Jump jump;
    private Dash dash;

    // =====================================================================
    // [초기화]
    // =====================================================================

    void Awake()
    {
        // 1. 공유 상태 객체 생성 및 컴포넌트 연결
        state = new PlayerState();
        state.rb = GetComponent<Rigidbody>();
        state.col = GetComponent<Collider>();
        state.groundLayer = groundLayer;

        // Rigidbody 회전 잠금 설정
        // FreezeRotationX + FreezeRotationZ: 물리 충돌로 앞뒤/옆으로 쓰러지지 않게 잠금
        // FreezeRotationY는 잠그지 않음: CameraFollow가 Y축을 다루지 않더라도
        //   Rigidbody가 Y를 잠그면 일부 물리 상황에서 예기치 않은 힘이 발생할 수 있음
        // FreezePositionY는 잠그지 않음: 중력과 점프가 Y 위치를 담당함
        state.rb.constraints = RigidbodyConstraints.FreezeRotationX
                             | RigidbodyConstraints.FreezeRotationZ;

        // 2. 카메라 연결
        // cameraHolder: Inspector에서 직접 CameraHolder Empty를 연결합니다.
        // CameraHolder의 Transform을 기준으로 이동 방향을 계산합니다.
        // (BasicMove에서 camForward.y = 0 처리로 수직 이동은 발생하지 않습니다)
        if (cameraHolder != null)
        {
            state.cameraTransform = cameraHolder;
        }
        else
        {
            // cameraHolder를 연결 안 했을 경우 Main Camera로 대체
            Camera mainCam = Camera.main;
            state.cameraTransform = mainCam != null ? mainCam.transform : this.transform;
            Debug.LogWarning("[PlayerManager] CameraHolder가 연결되지 않았습니다. " +
                             "Inspector에서 CameraHolder를 연결하세요.");
        }

        // 3. 대쉬 충전 배열 초기화
        state.dashCharges = maxDashCharges;
        state.dashCooldownTimers = new float[maxDashCharges];

        // 4. 각 기능 모듈 생성 (new = 객체 생성)
        //    생성 시 state와 필요한 수치들을 넘겨줌
        groundChecker = new PlayerGroundChecker(state, groundCheckDistance);

        basicMove = new BasicMove(state, walkSpeed, runSpeed,
                                  airControlMultiplier, airAcceleration,
                                  groundAcceleration, groundDeceleration);

        jump = new Jump(state, jumpForce, doubleJumpForce,
                       riseGravityMultiplier, fallGravityMultiplier, shortHopMultiplier);

        // Rigidbody 기본 drag를 0으로 설정
        // BasicMove가 지상/공중 상황에 맞게 drag를 직접 조절합니다
        state.rb.drag = 0f;

        dash = new Dash(state, dashForce, dashDuration, dashCooldown, maxDashCharges);
    }

    // =====================================================================
    // [Update — 매 프레임: 입력 감지 & 상태 체크]
    // =====================================================================

    void Update()
    {
        // 실행 순서가 중요합니다:
        // 1. 바닥 감지 먼저 → 2. 점프/대쉬 입력 판단 → 3. 쿨다운 업데이트

        groundChecker.CheckGrounded();  // 바닥 감지 (isGrounded 갱신)
        jump.HandleInput();             // Space 입력 감지
        dash.HandleInput();             // Ctrl 입력 감지
        dash.UpdateCooldowns();         // 대쉬 쿨다운 차감 및 충전 회복
    }

    // =====================================================================
    // [FixedUpdate — 물리 프레임: 실제 이동 처리]
    // =====================================================================

    void FixedUpdate()
    {
        // 물리 이동은 반드시 FixedUpdate에서 처리합니다
        // (Update에서 하면 프레임 속도에 따라 이동 속도가 달라짐)

        // 점프 힘 적용을 가장 먼저
        // → 이후 basicMove가 velocity를 덮어쓰기 전에 Y속도를 확정해야 함
        jump.ApplyJump();

        if (state.isDashing)
        {
            dash.HandleMovement(); // 대쉬 중: 대쉬 이동 처리
        }
        else
        {
            basicMove.HandleMovement(); // 평상시: 일반 이동 처리
        }
    }

    void OnDrawGizmosSelected()
    {
        if (state == null || state.col == null) return;
        groundChecker?.DrawGizmo();
    }

    /// <summary>현재 대쉬 충전 횟수</summary>
    public int DashCharges => state != null ? state.dashCharges : 0;

    /// <summary>최대 대쉬 충전 횟수</summary>
    public int MaxDashCharges => maxDashCharges;

    /// <summary>각 슬롯 쿨다운 남은 시간 배열 (UI 게이지용)</summary>
    public float[] DashTimers => state?.dashCooldownTimers;

    /// <summary>현재 바닥 여부</summary>
    public bool IsGrounded => state != null && state.isGrounded;

    /// <summary>현재 점프 횟수</summary>
    public int JumpCount => state != null ? state.jumpCount : 0;
}