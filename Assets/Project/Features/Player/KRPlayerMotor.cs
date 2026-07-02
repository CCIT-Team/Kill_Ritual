using UnityEngine;

namespace KillRitual
{
    /// <summary>
    /// 살굿 MVP용 플레이어 이동 컨트롤러.
    ///
    /// 핵심 방향:
    /// - 기본 이동은 항상 달리기.
    /// - Shift 달리기 없음.
    /// - Ctrl 미사용.
    /// - 이동은 가속/감속 기반.
    /// - 대시는 개수 기반이며 외부에서 확장 가능.
    /// - 점프는 기존 Rigidbody 컨트롤러의 jumpForce 방식을 참고해,
    ///   Y속도 초기화 후 즉시 상승 속도를 넣는 방식으로 처리.
    ///
    /// 사용 컴포넌트:
    /// - CharacterController 필수.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class KRPlayerMotor : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [Header("References")]
        [Tooltip("플레이어의 CharacterController. 비워두면 자동으로 찾는다.")]
        [SerializeField]
        private CharacterController characterController;

        // ============================================================
        // Ground Check
        // ============================================================

        [Header("Ground Check")]
        [Tooltip("바닥으로 인정할 레이어. Player 레이어는 반드시 제외해야 한다.")]
        [SerializeField]
        private LayerMask groundMask = ~0;

        [Tooltip("바닥 체크용 구체 반지름. CharacterController Radius보다 약간 작게 둔다.")]
        [SerializeField]
        private float groundCheckRadius = 0.32f;

        [Tooltip("바닥 체크 구체를 발바닥보다 살짝 아래로 내리는 값. 접지 판정 안정화용.")]
        [SerializeField]
        private float groundCheckInset = 0.04f;

        [Tooltip("바닥 노멀을 확인하기 위한 탐색 거리.")]
        [SerializeField]
        private float groundProbeDistance = 0.25f;

        // ============================================================
        // Movement
        // ============================================================

        [Header("Run Movement")]
        [Tooltip("기본 최고 이동 속도. 살굿은 달리기가 기본이므로 Walk Speed를 따로 두지 않는다.")]
        [SerializeField]
        private float maxRunSpeed = 11f;

        [Tooltip("지상에서 목표 속도까지 도달하는 가속도.")]
        [SerializeField]
        private float groundAcceleration = 52f;

        [Tooltip("지상에서 입력을 멈췄을 때 감속되는 정도. 높을수록 미끄러짐이 줄어든다.")]
        [SerializeField]
        private float groundDeceleration = 68f;

        [Tooltip("공중에서 방향을 바꿀 수 있는 가속도.")]
        [SerializeField]
        private float airAcceleration = 18f;

        [Tooltip("공중에서 입력을 멈췄을 때 감속되는 정도. 너무 높으면 공중에서 멈춘 느낌이 난다.")]
        [SerializeField]
        private float airDeceleration = 1.5f;

        // ============================================================
        // Jump
        // ============================================================

        [Header("Jump")]
        [Tooltip("점프 시작 속도. 기존 Rigidbody 코드의 jumpForce와 같은 역할.")]
        [SerializeField]
        private float jumpForce = 13.5f;

        [Tooltip("최대 점프 가능 횟수. 1이면 일반 점프, 2면 2단 점프.")]
        [SerializeField]
        private int maxJumpCount = 1;

        [Tooltip("기본 중력. 반드시 음수값이어야 한다.")]
        [SerializeField]
        private float gravity = -34f;

        [Tooltip("낙하 중 추가 중력 배율. 높을수록 최고점 이후 빠르게 떨어진다.")]
        [SerializeField]
        private float fallGravityMultiplier = 2.4f;

        [Tooltip("상승 중 추가 중력 배율. 기본적으로 1에 가깝게 둔다.")]
        [SerializeField]
        private float riseGravityMultiplier = 1f;

        [Tooltip("점프 키를 짧게 뗐을 때 낮은 점프를 허용할지 여부. FPS에서는 일단 false 권장.")]
        [SerializeField]
        private bool useVariableJumpHeight = false;

        [Tooltip("점프 키를 짧게 뗐을 때 상승 속도를 줄이는 배율.")]
        [SerializeField]
        private float jumpCutMultiplier = 0.8f;

        [Tooltip("최대 낙하 속도. 낮으면 깃털처럼 천천히 떨어진다.")]
        [SerializeField]
        private float maxFallSpeed = 70f;

        [Tooltip("바닥에 붙어 있도록 유지하는 작은 하강 속도. 양수로 입력한다.")]
        [SerializeField]
        private float groundedStickVelocity = 5f;

        [Tooltip("바닥에서 살짝 떨어진 직후에도 점프를 허용하는 시간.")]
        [SerializeField]
        private float coyoteTime = 0.08f;

        [Tooltip("점프 키를 살짝 먼저 눌러도 착지 직후 점프되게 하는 시간.")]
        [SerializeField]
        private float jumpBufferTime = 0.08f;

        [Tooltip("점프 직후 바닥 체크를 잠깐 무시하는 시간. 점프 직후 접지 판정이 남는 문제를 막는다.")]
        [SerializeField]
        private float groundCheckDisableAfterJump = 0.12f;

        // ============================================================
        // Dash
        // ============================================================

        [Header("Dash")]
        [Tooltip("대시 입력 키. Ctrl은 사용하지 않는다.")]
        [SerializeField]
        private KeyCode dashKey = KeyCode.LeftAlt;

        [Tooltip("대시 속도.")]
        [SerializeField]
        private float dashSpeed = 24f;

        [Tooltip("대시 지속 시간.")]
        [SerializeField]
        private float dashDuration = 0.12f;

        [Tooltip("대시를 연속 입력할 때 최소 간격.")]
        [SerializeField]
        private float minTimeBetweenDashes = 0.12f;

        [Tooltip("최대 대시 개수. 인스펙터에서 조절 가능하며 외부에서 증가 가능하다.")]
        [SerializeField]
        private int maxDashCharges = 1;

        [Tooltip("현재 대시 개수. 시작 시 maxDashCharges로 보정된다.")]
        [SerializeField]
        private int currentDashCharges = 1;

        [Tooltip("대시 1개가 회복되는 시간.")]
        [SerializeField]
        private float dashRechargeTime = 1.1f;

        [Tooltip("대시 종료 후 유지할 속도 비율. 1이면 기본 달리기 속도 정도로 이어진다.")]
        [SerializeField]
        private float dashExitSpeedRatio = 1f;

        [Tooltip("대시 시작 시 하강 속도를 완화할지 여부.")]
        [SerializeField]
        private bool softenFallingVelocityOnDash = true;

        [Header("Dash UI")]
        [Tooltip("대시 충전 상태를 표시하는 UI입니다.")]
        [SerializeField]
        private KRDashChargeUI dashChargeUI;

        // ============================================================
        // Debug
        // ============================================================

        [Header("Debug")]
        [SerializeField]
        private bool showDebugLog = false;

        [SerializeField]
        private bool drawGroundCheckGizmo = true;

        // ============================================================
        // Runtime State
        // ============================================================

        /// <summary>
        /// WASD 입력 방향.
        /// </summary>
        private Vector3 moveInput;

        /// <summary>
        /// 현재 수평 이동 속도.
        /// </summary>
        private Vector3 horizontalVelocity;

        /// <summary>
        /// 현재 수직 속도.
        /// CharacterController는 Rigidbody가 아니므로 직접 관리한다.
        /// </summary>
        private float verticalVelocity;

        /// <summary>
        /// 현재 바닥에 닿아 있는지 여부.
        /// </summary>
        private bool isGrounded;

        /// <summary>
        /// 이전 프레임의 바닥 접지 여부.
        /// 착지 감지에 사용한다.
        /// </summary>
        private bool wasGrounded;

        /// <summary>
        /// 현재 바닥의 노멀.
        /// 경사면 이동 보정에 사용한다.
        /// </summary>
        private Vector3 groundNormal = Vector3.up;

        /// <summary>
        /// 마지막으로 바닥에 닿았던 시간.
        /// Coyote Time 계산에 사용한다.
        /// </summary>
        private float lastGroundedTime = -999f;

        /// <summary>
        /// 마지막으로 점프 입력이 들어온 시간.
        /// Jump Buffer 계산에 사용한다.
        /// </summary>
        private float lastJumpPressedTime = -999f;

        /// <summary>
        /// 마지막 점프 실행 시간.
        /// 점프 직후 접지 판정을 잠깐 무시하기 위해 사용한다.
        /// </summary>
        private float lastJumpTime = -999f;

        /// <summary>
        /// 남은 점프 횟수.
        /// maxJumpCount가 1이면 일반 점프만 가능.
        /// </summary>
        private int remainingJumps;

        /// <summary>
        /// CharacterController의 원래 Step Offset.
        /// 공중에서는 0으로 낮추고, 지상에서는 복구한다.
        /// </summary>
        private float defaultStepOffset;

        /// <summary>
        /// 현재 대시 중인지 여부.
        /// </summary>
        private bool isDashing;

        /// <summary>
        /// 대시 방향.
        /// </summary>
        private Vector3 dashDirection;

        /// <summary>
        /// 대시가 끝나는 시간.
        /// </summary>
        private float dashEndTime;

        /// <summary>
        /// 마지막 대시 시작 시간.
        /// </summary>
        private float lastDashStartTime = -999f;

        /// <summary>
        /// 다음 대시 충전 완료 시간.
        /// </summary>
        private float nextDashRechargeTime = -999f;

        public int CurrentDashCharges => currentDashCharges;
        public int MaxDashCharges => maxDashCharges;
        public bool IsGrounded => isGrounded;
        public Vector3 HorizontalVelocity => horizontalVelocity;
        public float VerticalVelocity => verticalVelocity;

        // ============================================================
        // Unity Life Cycle
        // ============================================================

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            defaultStepOffset = characterController.stepOffset;
            characterController.minMoveDistance = 0f;

            maxJumpCount = Mathf.Max(1, maxJumpCount);
            remainingJumps = maxJumpCount;

            maxDashCharges = Mathf.Max(0, maxDashCharges);
            currentDashCharges = Mathf.Clamp(currentDashCharges, 0, maxDashCharges);

            if (currentDashCharges <= 0 && maxDashCharges > 0)
                currentDashCharges = maxDashCharges;

            UpdateDashUI();
        }

        private void Update()
        {
            ReadMoveInput();
            ReadJumpInput();

            UpdateGroundState();
            UpdateDashRecharge();

            TryStartDash();
            TryConsumeBufferedJump();

            UpdateHorizontalVelocity();
            UpdateVerticalVelocity();

            MoveCharacter();

            UpdateDashUI();
        }

        private void OnValidate()
        {
            maxJumpCount = Mathf.Max(1, maxJumpCount);
            jumpForce = Mathf.Max(0f, jumpForce);

            // gravity는 음수여야 한다.
            if (gravity > 0f)
                gravity *= -1f;

            fallGravityMultiplier = Mathf.Max(0.1f, fallGravityMultiplier);
            riseGravityMultiplier = Mathf.Max(0.1f, riseGravityMultiplier);
            maxFallSpeed = Mathf.Max(1f, maxFallSpeed);
            groundedStickVelocity = Mathf.Max(0f, groundedStickVelocity);

            maxDashCharges = Mathf.Max(0, maxDashCharges);
            currentDashCharges = Mathf.Clamp(currentDashCharges, 0, maxDashCharges);
        }

        // ============================================================
        // Input
        // ============================================================

        /// <summary>
        /// WASD 입력을 읽는다.
        /// 입력 방향은 플레이어가 바라보는 방향 기준이다.
        /// </summary>
        private void ReadMoveInput()
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputZ = Input.GetAxisRaw("Vertical");

            moveInput =
                transform.right * inputX +
                transform.forward * inputZ;

            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();
        }

        /// <summary>
        /// 점프 입력을 저장한다.
        ///
        /// 즉시 점프하지 않고 시간을 저장하는 이유:
        /// - 착지 직전에 Space를 눌러도 착지 후 점프되게 하기 위함.
        /// - 이를 Jump Buffer라고 한다.
        /// </summary>
        private void ReadJumpInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                lastJumpPressedTime = Time.time;
            }

            // FPS에서는 점프가 뚝 끊기면 조작감이 나빠진다.
            // 기본값은 false로 두고, 낮은 점프가 필요할 때만 켠다.
            if (!useVariableJumpHeight)
                return;

            if (Input.GetKeyUp(KeyCode.Space) && verticalVelocity > 0f)
            {
                verticalVelocity *= jumpCutMultiplier;
            }
        }

        // ============================================================
        // Ground
        // ============================================================

        /// <summary>
        /// 바닥 상태를 갱신한다.
        ///
        /// 기존 문제:
        /// - groundMask가 모든 레이어일 경우 Player 자신을 바닥으로 오인할 수 있음.
        /// - 점프 직후 CheckSphere가 아직 바닥과 겹쳐서 isGrounded가 true로 남을 수 있음.
        ///
        /// 해결:
        /// - OverlapSphere로 바닥 후보를 직접 검사하되, 자기 자신은 무시.
        /// - 점프 직후에는 일정 시간 접지 판정 무시.
        /// </summary>
        private void UpdateGroundState()
        {
            wasGrounded = isGrounded;

            bool shouldIgnoreGroundCheck =
                Time.time < lastJumpTime + groundCheckDisableAfterJump;

            if (shouldIgnoreGroundCheck)
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                characterController.stepOffset = 0f;
                return;
            }

            bool sphereGrounded = CheckGroundByOverlapSphere();

            // CharacterController.isGrounded는 Move 이후 갱신되는 값이라 단독 사용은 불안정하다.
            // 그래도 보조 정보로는 유용하므로 sphereGrounded와 함께 사용한다.
            bool controllerGrounded = characterController.isGrounded;

            isGrounded = sphereGrounded || controllerGrounded;

            if (isGrounded)
            {
                lastGroundedTime = Time.time;
                groundNormal = ProbeGroundNormal();

                characterController.stepOffset = defaultStepOffset;

                if (!wasGrounded)
                {
                    OnLanded();
                }
            }
            else
            {
                groundNormal = Vector3.up;
                characterController.stepOffset = 0f;
            }
        }

        /// <summary>
        /// 착지 시 처리.
        /// 점프 횟수를 회복한다.
        /// </summary>
        private void OnLanded()
        {
            remainingJumps = maxJumpCount;

            if (showDebugLog)
                Debug.Log("Player Landed");
        }

        /// <summary>
        /// OverlapSphere로 바닥을 검사한다.
        /// 자기 자신의 CharacterController를 바닥으로 오인하지 않도록 root가 같은 콜라이더는 무시한다.
        /// </summary>
        private bool CheckGroundByOverlapSphere()
        {
            Vector3 checkPosition = GetGroundCheckPosition();

            Collider[] hits = Physics.OverlapSphere(
                checkPosition,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];

                if (hit == null)
                    continue;

                // 자기 자신이나 자기 자식 콜라이더는 바닥으로 보지 않는다.
                if (hit.transform.root == transform.root)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 발밑 바닥 체크 위치를 계산한다.
        ///
        /// CharacterController 기준:
        /// - worldCenter.y - height / 2 가 발바닥 높이에 가깝다.
        /// - CheckSphere의 중심은 발바닥보다 groundCheckRadius만큼 위에 둔다.
        /// - groundCheckInset만큼 아래로 내려 바닥과 살짝 겹치게 한다.
        /// </summary>
        private Vector3 GetGroundCheckPosition()
        {
            Vector3 worldCenter = transform.TransformPoint(characterController.center);

            float bottomY =
                worldCenter.y -
                characterController.height * 0.5f;

            return new Vector3(
                worldCenter.x,
                bottomY + groundCheckRadius - groundCheckInset,
                worldCenter.z
            );
        }

        /// <summary>
        /// 바닥 노멀을 구한다.
        /// 경사면 이동 보정에 사용한다.
        /// </summary>
        private Vector3 ProbeGroundNormal()
        {
            Vector3 worldCenter = transform.TransformPoint(characterController.center);

            float castDistance =
                characterController.height * 0.5f +
                groundProbeDistance;

            RaycastHit[] hits = Physics.SphereCastAll(
                worldCenter,
                characterController.radius * 0.85f,
                Vector3.down,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

            RaycastHit closestHit = default;
            bool hasValidHit = false;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                    hasValidHit = true;
                }
            }

            if (!hasValidHit)
                return Vector3.up;

            return closestHit.normal;
        }

        // ============================================================
        // Jump
        // ============================================================

        /// <summary>
        /// 저장된 점프 입력을 실제 점프로 소비한다.
        ///
        /// 조건:
        /// - 최근에 점프 입력이 있었고
        /// - 지상 점프 가능 상태이거나
        /// - 남은 공중 점프 횟수가 있을 것.
        /// </summary>
        private void TryConsumeBufferedJump()
        {
            bool hasBufferedJump =
                Time.time <= lastJumpPressedTime + jumpBufferTime;

            if (!hasBufferedJump)
                return;

            bool canUseGroundJump =
                Time.time <= lastGroundedTime + coyoteTime;

            bool canUseAirJump =
                !canUseGroundJump && remainingJumps > 0;

            if (!canUseGroundJump && !canUseAirJump)
                return;

            PerformJump();

            lastJumpPressedTime = -999f;
        }

        /// <summary>
        /// 실제 점프 실행.
        ///
        /// 기존 Rigidbody 코드의 PerformJump 구조를 CharacterController 방식으로 옮긴 것이다.
        /// - 기존 수직 속도를 제거.
        /// - jumpForce를 즉시 수직 속도로 넣음.
        /// - 점프 직후 접지 판정을 잠깐 무시.
        /// </summary>
        private void PerformJump()
        {
            verticalVelocity = 0f;
            verticalVelocity = jumpForce;

            remainingJumps = Mathf.Max(0, remainingJumps - 1);

            isGrounded = false;
            lastJumpTime = Time.time;

            characterController.stepOffset = 0f;

            if (showDebugLog)
            {
                Debug.Log(
                    $"Player Jump. verticalVelocity: {verticalVelocity}, remainingJumps: {remainingJumps}"
                );
            }
        }

        /// <summary>
        /// 수직 속도를 갱신한다.
        ///
        /// 지상:
        /// - 작은 하강 속도로 바닥에 붙여둔다.
        ///
        /// 공중:
        /// - 상승 중에는 기본 중력 또는 상승 배율 적용.
        /// - 낙하 중에는 fallGravityMultiplier를 적용해 빠르게 떨어지게 한다.
        /// </summary>
        private void UpdateVerticalVelocity()
        {
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickVelocity;
                return;
            }

            bool isFalling = verticalVelocity < 0f;

            float gravityMultiplier = isFalling
                ? fallGravityMultiplier
                : riseGravityMultiplier;

            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;

            if (verticalVelocity < -maxFallSpeed)
                verticalVelocity = -maxFallSpeed;
        }

        // ============================================================
        // Horizontal Movement
        // ============================================================

        /// <summary>
        /// 수평 이동 속도를 갱신한다.
        /// </summary>
        private void UpdateHorizontalVelocity()
        {
            if (isDashing)
            {
                UpdateDashVelocity();
                return;
            }

            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;

            if (hasMoveInput)
            {
                Vector3 targetVelocity = moveInput * maxRunSpeed;

                float acceleration = isGrounded
                    ? groundAcceleration
                    : airAcceleration;

                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    targetVelocity,
                    acceleration * Time.deltaTime
                );
            }
            else
            {
                float deceleration = isGrounded
                    ? groundDeceleration
                    : airDeceleration;

                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    Vector3.zero,
                    deceleration * Time.deltaTime
                );
            }
        }

        // ============================================================
        // Dash
        // ============================================================

        /// <summary>
        /// 대시 시작을 시도한다.
        /// Ctrl은 사용하지 않는다.
        /// </summary>
        private void TryStartDash()
        {
            if (!Input.GetKeyDown(dashKey))
                return;

            if (isDashing)
                return;

            if (currentDashCharges <= 0)
                return;

            if (Time.time < lastDashStartTime + minTimeBetweenDashes)
                return;

            StartDash();
        }

        /// <summary>
        /// 대시 시작.
        /// 입력 방향이 있으면 입력 방향으로, 없으면 정면으로 대시한다.
        /// </summary>
        private void StartDash()
        {
            dashDirection = moveInput.sqrMagnitude > 0.01f
                ? moveInput.normalized
                : transform.forward;

            isDashing = true;
            dashEndTime = Time.time + dashDuration;
            lastDashStartTime = Time.time;

            currentDashCharges--;

            if (currentDashCharges < maxDashCharges && nextDashRechargeTime < Time.time)
                nextDashRechargeTime = Time.time + dashRechargeTime;

            if (softenFallingVelocityOnDash && verticalVelocity < 0f)
                verticalVelocity = 0f;

            if (showDebugLog)
                Debug.Log($"Dash Start. Charges: {currentDashCharges}/{maxDashCharges}");
        }

        /// <summary>
        /// 대시 중 속도 갱신.
        /// </summary>
        private void UpdateDashVelocity()
        {
            if (Time.time >= dashEndTime)
            {
                EndDash();
                return;
            }

            horizontalVelocity = dashDirection * dashSpeed;
        }

        /// <summary>
        /// 대시 종료 처리.
        /// </summary>
        private void EndDash()
        {
            isDashing = false;

            float exitSpeed = maxRunSpeed * dashExitSpeedRatio;
            horizontalVelocity = dashDirection * exitSpeed;

            if (showDebugLog)
                Debug.Log("Dash End");
        }

        /// <summary>
        /// 대시 충전 갱신.
        /// </summary>
        private void UpdateDashRecharge()
        {
            if (currentDashCharges >= maxDashCharges)
                return;

            if (Time.time < nextDashRechargeTime)
                return;

            currentDashCharges++;

            if (currentDashCharges < maxDashCharges)
                nextDashRechargeTime = Time.time + dashRechargeTime;
            else
                nextDashRechargeTime = -999f;

            if (showDebugLog)
                Debug.Log($"Dash Recharged. Charges: {currentDashCharges}/{maxDashCharges}");
        }

        public float DashRechargeProgress01
        {
            get
            {
                if (maxDashCharges <= 0)
                    return 0f;

                if (currentDashCharges >= maxDashCharges)
                    return 1f;

                if (nextDashRechargeTime < 0f)
                    return 0f;

                float remainingTime = nextDashRechargeTime - Time.time;
                float progress = 1f - remainingTime / dashRechargeTime;

                return Mathf.Clamp01(progress);
            }
        }

        private void UpdateDashUI()
        {
            if (dashChargeUI == null)
            {
                return;
            }

            dashChargeUI.SetDashState(
                currentDashCharges,
                maxDashCharges,
                DashRechargeProgress01
            );
        }

        // ============================================================
        // Character Move
        // ============================================================

        /// <summary>
        /// 최종 이동을 CharacterController에 적용한다.
        ///
        /// CharacterController.Move는 프레임당 한 번만 호출하는 것이 안정적이다.
        /// 수평 이동과 수직 이동을 합쳐 한 번에 처리한다.
        /// </summary>
        private void MoveCharacter()
        {
            Vector3 finalHorizontalVelocity = horizontalVelocity;

            if (isGrounded && verticalVelocity <= 0f)
            {
                finalHorizontalVelocity = Vector3.ProjectOnPlane(
                    horizontalVelocity,
                    groundNormal
                );
            }

            Vector3 finalVelocity =
                finalHorizontalVelocity +
                Vector3.up * verticalVelocity;

            CollisionFlags flags =
                characterController.Move(finalVelocity * Time.deltaTime);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                bool landedThisFrame = !isGrounded;

                isGrounded = true;
                lastGroundedTime = Time.time;

                if (landedThisFrame)
                    OnLanded();
            }
        }

        // ============================================================
        // External Extension Methods
        // ============================================================

        /// <summary>
        /// 외부 시스템에서 최대 대시 개수를 직접 설정할 때 사용한다.
        /// 예: 강화 시스템에서 대시 최대 개수를 2로 변경.
        /// </summary>
        public void SetMaxDashCharges(int newMaxDashCharges, bool refill)
        {
            maxDashCharges = Mathf.Max(0, newMaxDashCharges);

            if (refill)
            {
                currentDashCharges = maxDashCharges;
            }
            else
            {
                currentDashCharges = Mathf.Clamp(
                    currentDashCharges,
                    0,
                    maxDashCharges
                );
            }

            if (currentDashCharges >= maxDashCharges)
                nextDashRechargeTime = -999f;
            else
                nextDashRechargeTime = Time.time + dashRechargeTime;
        }

        /// <summary>
        /// 외부 시스템에서 최대 대시 개수를 증가시킬 때 사용한다.
        /// 예: 대시 강화 아이템 획득.
        /// </summary>
        public void AddMaxDashCharges(int amount, bool refillAddedCharge)
        {
            if (amount <= 0)
                return;

            maxDashCharges += amount;

            if (refillAddedCharge)
                currentDashCharges += amount;

            currentDashCharges = Mathf.Clamp(
                currentDashCharges,
                0,
                maxDashCharges
            );
        }

        /// <summary>
        /// 현재 대시 개수를 즉시 회복한다.
        /// 예: 파살 성공 시 대시 1개 회복.
        /// </summary>
        public void AddDashCharges(int amount)
        {
            if (amount <= 0)
                return;

            currentDashCharges += amount;
            currentDashCharges = Mathf.Clamp(
                currentDashCharges,
                0,
                maxDashCharges
            );

            if (currentDashCharges >= maxDashCharges)
                nextDashRechargeTime = -999f;
        }

        /// <summary>
        /// 대시 개수를 최대치까지 회복한다.
        /// </summary>
        public void RefillDashCharges()
        {
            currentDashCharges = maxDashCharges;
            nextDashRechargeTime = -999f;
        }

        // ============================================================
        // Gizmos
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            if (!drawGroundCheckGizmo)
                return;

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (characterController == null)
                return;

            Vector3 checkPosition = GetGroundCheckPosition();

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
        }
    }
}