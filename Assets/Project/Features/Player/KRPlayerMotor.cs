using UnityEngine;
using KillRitual.Player.Combat;

namespace KillRitual
{
    [RequireComponent(typeof(CharacterController))]
    public class KRPlayerMotor : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [Header("References")]
        [Tooltip("플레이어의 CharacterController로, 비워두면 자동으로 찾습니다.")]
        [SerializeField]
        private CharacterController characterController;

        // ============================================================
        // Ground Check
        // ============================================================

        [Header("Ground Check")]
        [Tooltip("바닥으로 판정할 레이어로, Player 레이어와는 반드시 분리해야 합니다.")]
        [SerializeField]
        private LayerMask groundMask = ~0;

        [Tooltip("바닥 체크용 구체 반지름으로, CharacterController Radius보다 약간 작게 둡니다.")]
        [SerializeField]
        private float groundCheckRadius = 0.32f;

        [Tooltip("바닥 체크 구체를 발바닥보다 살짝 아래로 내리는 값으로, 착지 판정 안정화용입니다.")]
        [SerializeField]
        private float groundCheckInset = 0.04f;

        [Tooltip("바닥 노멀을 확인하기 위한 탐지 거리입니다.")]
        [SerializeField]
        private float groundProbeDistance = 0.25f;

        // ============================================================
        // Movement
        // ============================================================

        [Header("Run Movement")]
        [Tooltip("기본 달리기 이동 속도로, 달리기가 기본이므로 Walk Speed는 따로 두지 않습니다.")]
        [SerializeField]
        private float maxRunSpeed = 11f;

        [Header("slow Movement")]
        [Tooltip("이동 속도 감속 배율입니다.")]
        [SerializeField]
        private float slowRunSpeed = 0.5f;

        [Tooltip("작두(KRJakduSystem) 참조. 작두 발동 중(IsActing)에만 slowRunSpeed 배율로 이동 속도를 " +
                 "감속시키는 데 사용합니다. 비워두면 같은 오브젝트에서 자동 탐색합니다.")]
        [SerializeField]
        private KRJakduSystem jakduSystem;

        [Tooltip("지상에서 목표 속도까지 도달하는 가속도입니다.")]
        [SerializeField]
        private float groundAcceleration = 52f;

        [Tooltip("지상에서 입력이 없을 때 감속되는 정도로, 클수록 미끄러지지 않고 빨리 멈춥니다.")]
        [SerializeField]
        private float groundDeceleration = 68f;

        [Tooltip("공중에서 방향을 바꿀 수 있는 가속도입니다.")]
        [SerializeField]
        private float airAcceleration = 18f;

        [Tooltip("공중에서 입력이 없을 때 감속되는 정도로, 너무 크면 공중 관성 느낌이 사라집니다.")]
        [SerializeField]
        private float airDeceleration = 1.5f;

        // ============================================================
        // Jump
        // ============================================================

        [Header("Jump")]
        [Tooltip("점프 시작 속도로, 기존 Rigidbody 코드의 jumpForce와 같은 개념입니다.")]
        [SerializeField]
        private float jumpForce = 13.5f;

        [Tooltip("최대 점프 가능 횟수로, 1이면 일반 점프, 2면 2단 점프입니다.")]
        [SerializeField]
        private int maxJumpCount = 1;

        [Tooltip("기본 중력으로, 반드시 음수여야 합니다.")]
        [SerializeField]
        private float gravity = -34f;

        [Tooltip("낙하 중 추가 중력 배율로, 클수록 빠르게 떨어지고 낙하감이 무거워집니다.")]
        [SerializeField]
        private float fallGravityMultiplier = 2.4f;

        [Tooltip("상승 중 추가 중력 배율로, 기본적으로 1로 두고 사용합니다.")]
        [SerializeField]
        private float riseGravityMultiplier = 1f;

        [Tooltip("점프 키를 짧게 뗐을 때 점프 높이를 줄일지 여부로, FPS 게임에서는 보통 false로 둡니다.")]
        [SerializeField]
        private bool useVariableJumpHeight = false;

        [Tooltip("점프 키를 짧게 뗐을 때 상승 속도를 줄이는 배율입니다.")]
        [SerializeField]
        private float jumpCutMultiplier = 0.8f;

        [Tooltip("최대 낙하 속도로, 초과하지 않도록 클램프합니다.")]
        [SerializeField]
        private float maxFallSpeed = 70f;

        [Tooltip("바닥에 붙어 있도록 유지하는 최소 하강 속도로, 음수로 입력합니다.")]
        [SerializeField]
        private float groundedStickVelocity = 5f;

        [Tooltip("바닥에서 살짝 벗어난 직후에도 점프를 허용하는 시간입니다.")]
        [SerializeField]
        private float coyoteTime = 0.08f;

        [Tooltip("점프 키를 살짝 미리 눌러도 착지 시점에 점프가 인정되게 하는 시간입니다.")]
        [SerializeField]
        private float jumpBufferTime = 0.08f;

        [Tooltip("점프 직후 바닥 체크를 잠깐 무시하는 시간으로, 점프 즉시 재접지 판정되는 것을 막습니다.")]
        [SerializeField]
        private float groundCheckDisableAfterJump = 0.12f;

        // ============================================================
        // Dash
        // ============================================================

        [Header("Dash")]
        [Tooltip("대시 입력 키로, Ctrl은 사용하지 않습니다.")]
        [SerializeField]
        private KeyCode dashKey = KeyCode.LeftAlt;

        [Tooltip("대시 속도입니다.")]
        [SerializeField]
        private float dashSpeed = 24f;

        [Tooltip("대시 지속 시간입니다.")]
        [SerializeField]
        private float dashDuration = 0.12f;

        [Tooltip("연속으로 대시를 입력할 때 최소 간격입니다.")]
        [SerializeField]
        private float minTimeBetweenDashes = 0.12f;

        [Tooltip("최대 대시 충전 수로, 인스펙터 값을 바꾸면 외부에서도 같이 적용됩니다.")]
        [SerializeField]
        private int maxDashCharges = 1;

        [Tooltip("현재 대시 충전 수로, 시작 시 maxDashCharges 범위로 보정됩니다.")]
        [SerializeField]
        private int currentDashCharges = 1;

        [Tooltip("대시 1회가 충전되는 시간입니다.")]
        [SerializeField]
        private float dashRechargeTime = 1.1f;

        [Tooltip("대시 종료 시 적용되는 속도 배율로, 1이면 기본 달리기 속도 그대로 이어집니다.")]
        [SerializeField]
        private float dashExitSpeedRatio = 1f;

        [Tooltip("대시 시작 시 하강 속도를 완화할지 여부입니다.")]
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

        private Vector3 moveInput;

        private Vector3 horizontalVelocity;

        private float verticalVelocity;

        private bool isGrounded;

        private bool wasGrounded;

        private Vector3 groundNormal = Vector3.up;

        private float lastGroundedTime = -999f;

        private float lastJumpPressedTime = -999f;

        private float lastJumpTime = -999f;

        private int remainingJumps;

        private float defaultStepOffset;

        private bool isDashing;

        private Vector3 dashDirection;

        private float dashEndTime;

        private float lastDashStartTime = -999f;

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

            if (jakduSystem == null)
                jakduSystem = GetComponent<KRJakduSystem>();

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

            // gravity는 항상 음수로 유지합니다.
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

        private void ReadJumpInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                lastJumpPressedTime = Time.time;
            }

            // FPS게임에서는 짧게 눌러도 완전 점프가 기본이므로, 필요할 때만 켭니다.
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

            // CharacterController.isGrounded는 Move 직후 갱신되어 프레임이 어긋날 수 있으므로 sphereGrounded와 함께 사용합니다.
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

        private void OnLanded()
        {
            remainingJumps = maxJumpCount;

            //if (showDebugLog)
            //    Debug.Log("Player Landed");
        }

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

                // 자기 자신이나 자기 자신의 콜라이더는 바닥으로 취급하지 않습니다.
                if (hit.transform.root == transform.root)
                    continue;

                return true;
            }

            return false;
        }

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

        private void PerformJump()
        {
            verticalVelocity = 0f;
            verticalVelocity = jumpForce;

            remainingJumps = Mathf.Max(0, remainingJumps - 1);

            isGrounded = false;
            lastJumpTime = Time.time;

            characterController.stepOffset = 0f;

            //if (showDebugLog)
            //{
            //    Debug.Log(
            //        $"Player Jump. verticalVelocity: {verticalVelocity}, remainingJumps: {remainingJumps}"
            //    );
            //}
        }

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

        private void UpdateHorizontalVelocity()
        {
            if (isDashing)
            {
                UpdateDashVelocity();
                return;
            }

            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;

            float effectiveRunSpeed = (jakduSystem != null && jakduSystem.IsActing)
                ? maxRunSpeed * slowRunSpeed
                : maxRunSpeed;

            if (hasMoveInput)
            {
                Vector3 targetVelocity = moveInput * effectiveRunSpeed;

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

            //if (showDebugLog)
            //    Debug.Log($"Dash Start. Charges: {currentDashCharges}/{maxDashCharges}");
        }

        private void UpdateDashVelocity()
        {
            if (Time.time >= dashEndTime)
            {
                EndDash();
                return;
            }

            horizontalVelocity = dashDirection * dashSpeed;
        }

        private void EndDash()
        {
            isDashing = false;

            float exitSpeed = maxRunSpeed * dashExitSpeedRatio;
            horizontalVelocity = dashDirection * exitSpeed;

            //if (showDebugLog)
            //    Debug.Log("Dash End");
        }

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

            //if (showDebugLog)
            //    Debug.Log($"Dash Recharged. Charges: {currentDashCharges}/{maxDashCharges}");
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
