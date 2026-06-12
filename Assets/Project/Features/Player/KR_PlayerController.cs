using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// =====================================================================
/// PlayerController.cs
///
/// [역할] 플레이어의 모든 이동 관련 기능을 담당하는 핵심 스크립트
/// [부착 위치] PlayerRoot (Empty GameObject, Rigidbody가 있는 오브젝트)
///
/// [담당 기능]
///   1. 걷기 / 달리기  - WASD + Shift
///   2. 점프 (2단 점프) - Space
///   3. 대쉬           - LeftAlt (쿨타임 충전식, 최대 2회)
///   4. 공중 제어       - 공중에서 WASD로 방향/속도 조정
///
/// [카메라 관련]
///   이 스크립트는 카메라를 직접 건드리지 않습니다.
///   카메라 회전은 Main Camera에 붙은 CameraFollow.cs 가 전담합니다.
///   PlayerRoot의 좌우 회전은 CameraFollow가 외부에서 호출하는
///   SetYRotation() 함수를 통해 이루어집니다.
///
/// [팀원 참고사항]
///   - Inspector에서 수치를 조정할 수 있도록 [SerializeField]를 사용함
///   - private 변수지만 Inspector에서 보이게 하려면 [SerializeField] 사용
///
/// [수정 내역]
///   v1.0 - 최초 작성
///   v1.1 - GetAxis → GetAxisRaw 변경 (카메라 드리프트 수정)
///          추가 중력(extraGravityMultiplier) 추가 (점프 묵직함 개선)
///   v1.2 - 카메라 로직 완전 분리 → CameraFollow.cs 로 이관
///          빙글빙글 도는 버그 원인 제거:
///          PlayerRoot 좌우 회전을 transform.Rotate() 에서
///          SetYRotation() 수신 방식으로 변경
/// =====================================================================
/// </summary>
public class PlayerController : MonoBehaviour
{
    // =====================================================================
    // Inspector 노출 변수들
    // =====================================================================

    [Header("=== 이동 속도 설정 ===")]
    [SerializeField, Tooltip("걷기 속도 (단위: m/s)")]
    private float walkSpeed = 5f;

    [SerializeField, Tooltip("달리기 속도 (단위: m/s)")]
    private float runSpeed = 9f;

    [SerializeField, Tooltip("공중에서 이동 방향을 바꾸는 힘. 클수록 공중 제어가 민첩해짐")]
    private float airControlForce = 15f;

    [SerializeField, Tooltip("공중에서 낼 수 있는 최대 수평 속도")]
    private float maxAirSpeed = 8f;

    [Header("=== 점프 설정 ===")]
    [SerializeField, Tooltip("점프 힘. 클수록 높이 점프 (권장값: 중력 -25 기준 13)")]
    private float jumpForce = 13f;

    [SerializeField, Tooltip("최대 점프 가능 횟수 (2 = 2단 점프)")]
    private int maxJumpCount = 2;

    [Header("=== 중력 설정 ===")]
    [SerializeField, Tooltip(
        "추가 중력 배율. 기본 중력에 이 배율만큼 중력을 더 가함.\n" +
        "0이면 추가 중력 없음.\n" +
        "권장: Project Settings Gravity Y = -25 일 때 1.5~2.0")]
    private float extraGravityMultiplier = 2f;

    [Header("=== 대쉬 설정 ===")]
    [SerializeField, Tooltip("대쉬 이동 거리/속도. 클수록 멀리 대쉬")]
    private float dashForce = 20f;

    [SerializeField, Tooltip("대쉬 한 번의 지속 시간 (초)")]
    private float dashDuration = 0.15f;

    [SerializeField, Tooltip("대쉬 1회 충전에 걸리는 시간 (초)")]
    private float dashCooldown = 3f;

    [SerializeField, Tooltip("대쉬 최대 보유 횟수")]
    private int maxDashCount = 2;

    [Header("=== 지면 감지 설정 ===")]
    [SerializeField, Tooltip("바닥으로 인식할 레이어. Inspector에서 'Ground' 레이어 선택")]
    private LayerMask groundLayer;

    [SerializeField, Tooltip("바닥 감지 구체의 반지름 (캡슐 반지름 0.5보다 약간 작게)")]
    private float groundCheckRadius = 0.4f;

    [SerializeField, Tooltip("바닥 감지 구체 위치 오프셋")]
    private float groundCheckDistance = 0.1f;

    // =====================================================================
    // 컴포넌트 참조
    // =====================================================================

    /// <summary>물리 처리를 담당하는 Rigidbody 컴포넌트</summary>
    private Rigidbody rb;

    // =====================================================================
    // 상태 변수들
    // =====================================================================

    private Vector3 moveInput;
    private float currentSpeed;

    private int remainingJumps;
    private bool isGrounded;

    private int currentDashCount;
    private float[] dashCooldownTimers;
    private bool isDashing;
    private float dashTimer;
    private Vector3 dashDirection;

    // =====================================================================
    // 외부에서 카메라가 전달하는 Y 회전값
    // CameraFollow.cs 가 매 프레임 SetYRotation()을 통해 이 값을 넣어줌
    // =====================================================================

    /// <summary>
    /// CameraFollow가 계산한 좌우(Y축) 회전값.
    /// PlayerRoot는 이 값으로 방향을 맞춰 이동 방향이 카메라와 일치하게 됨.
    /// </summary>
    private float targetYRotation = 0f;

    /// <summary>
    /// CameraFollow.cs 에서 호출.
    /// 마우스 좌우 입력으로 계산된 Y 회전값을 PlayerRoot에 전달.
    /// </summary>
    public void SetYRotation(float yAngle)
    {
        targetYRotation = yAngle;
    }

    // =====================================================================
    // Unity 생명주기
    // =====================================================================

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        remainingJumps = maxJumpCount;
        currentDashCount = maxDashCount;
        currentSpeed = walkSpeed;
        dashCooldownTimers = new float[maxDashCount];

        // 커서 잠금은 CameraFollow.cs 에서 담당
    }

    private void Update()
    {
        HandleGroundCheck();
        HandleMovementInput();
        HandleJumpInput();
        HandleDashInput();
        HandleDashCooldown();

        // CameraFollow가 전달한 Y 회전값으로 PlayerRoot 방향 설정
        // transform.Rotate() 대신 직접 지정 → 매 프레임 회전이 중첩 누적되지 않음
        // 이것이 빙글빙글 버그의 핵심 수정 지점
        transform.rotation = Quaternion.Euler(0f, targetYRotation, 0f);
    }

    private void FixedUpdate()
    {
        if (isDashing)
            ApplyDash();
        else
            ApplyMovement();
    }

    // =====================================================================
    // 지면 감지
    // =====================================================================

    private void HandleGroundCheck()
    {
        Vector3 checkPosition = transform.position + Vector3.down * (1f - groundCheckDistance);

        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(checkPosition, groundCheckRadius, groundLayer);

        if (!wasGrounded && isGrounded)
            OnLanded();
    }

    private void OnLanded()
    {
        remainingJumps = maxJumpCount;
    }

    // =====================================================================
    // 이동 입력
    // =====================================================================

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = (transform.right * horizontal + transform.forward * vertical).normalized;
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
    }

    // =====================================================================
    // 물리 이동 적용
    // =====================================================================

    /// <summary>
    /// Rigidbody에 실제 속도/힘을 적용.
    /// 추가 중력을 매 FixedUpdate마다 가해 묵직한 낙하감 구현.
    /// </summary>
    private void ApplyMovement()
    {
        // 추가 중력 (0이면 비활성)
        if (extraGravityMultiplier > 0f)
            rb.AddForce(Physics.gravity * extraGravityMultiplier, ForceMode.Acceleration);

        if (isGrounded)
        {
            // 지상: 수평 속도를 목표값으로 즉시 교체, 수직(Y)은 유지
            Vector3 targetVelocity = moveInput * currentSpeed;
            rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);
        }
        else
        {
            // 공중: AddForce로 부드러운 방향 조정
            rb.AddForce(new Vector3(moveInput.x, 0f, moveInput.z) * airControlForce, ForceMode.Force);

            // 공중 수평 속도 상한
            Vector3 hVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (hVel.magnitude > maxAirSpeed)
            {
                Vector3 capped = hVel.normalized * maxAirSpeed;
                rb.velocity = new Vector3(capped.x, rb.velocity.y, capped.z);
            }
        }
    }

    // =====================================================================
    // 점프
    // =====================================================================

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && remainingJumps > 0)
            PerformJump();
    }

    private void PerformJump()
    {
        // Y 속도 초기화 후 위쪽으로 즉각적인 힘 적용
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        remainingJumps--;
        isGrounded = false; // 감지 딜레이 방지용 즉시 처리
    }

    // =====================================================================
    // 대쉬
    // =====================================================================

    private void HandleDashInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && currentDashCount > 0 && !isDashing)
            StartDash();
    }

    private void StartDash()
    {
        dashDirection = moveInput.magnitude > 0.1f ? moveInput : transform.forward;

        isDashing = true;
        dashTimer = dashDuration;

        currentDashCount--;
        dashCooldownTimers[currentDashCount] = dashCooldown;

        // 대쉬 시작 시 수직 속도 제거 (공중 대쉬가 아래로 꺾이지 않게)
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
    }

    /// <summary>
    /// Lerp 보간으로 대쉬 속도를 자연스럽게 감속
    /// (순간이동처럼 보이지 않도록)
    /// </summary>
    private void ApplyDash()
    {
        dashTimer -= Time.fixedDeltaTime;

        if (dashTimer > 0f)
        {
            float t = dashTimer / dashDuration;
            float speed = Mathf.Lerp(0f, dashForce, t);
            rb.velocity = new Vector3(dashDirection.x * speed, 0f, dashDirection.z * speed);
        }
        else
        {
            isDashing = false;
            Vector3 after = dashDirection * currentSpeed;
            rb.velocity = new Vector3(after.x, rb.velocity.y, after.z);
        }
    }

    private void HandleDashCooldown()
    {
        for (int i = 0; i < maxDashCount; i++)
        {
            if (dashCooldownTimers[i] > 0f)
            {
                dashCooldownTimers[i] -= Time.deltaTime;
                if (dashCooldownTimers[i] <= 0f)
                {
                    dashCooldownTimers[i] = 0f;
                    if (currentDashCount < maxDashCount)
                        currentDashCount++;
                }
            }
        }
    }

    // =====================================================================
    // 디버그 시각화 (Scene 뷰 전용, 빌드 미포함)
    // =====================================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPosition = transform.position + Vector3.down * (1f - groundCheckDistance);
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    }
}