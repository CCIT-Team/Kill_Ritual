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
        [Tooltip("�÷��̾��� CharacterController. ����θ� �ڵ����� ã�´�.")]
        [SerializeField]
        private CharacterController characterController;

        // ============================================================
        // Ground Check
        // ============================================================

        [Header("Ground Check")]
        [Tooltip("�ٴ����� ������ ���̾�. Player ���̾�� �ݵ�� �����ؾ� �Ѵ�.")]
        [SerializeField]
        private LayerMask groundMask = ~0;

        [Tooltip("�ٴ� üũ�� ��ü ������. CharacterController Radius���� �ణ �۰� �д�.")]
        [SerializeField]
        private float groundCheckRadius = 0.32f;

        [Tooltip("�ٴ� üũ ��ü�� �߹ٴں��� ��¦ �Ʒ��� ������ ��. ���� ���� ����ȭ��.")]
        [SerializeField]
        private float groundCheckInset = 0.04f;

        [Tooltip("�ٴ� ����� Ȯ���ϱ� ���� Ž�� �Ÿ�.")]
        [SerializeField]
        private float groundProbeDistance = 0.25f;

        // ============================================================
        // Movement
        // ============================================================

        [Header("Run Movement")]
        [Tooltip("�⺻ �ְ� �̵� �ӵ�. ����� �޸��Ⱑ �⺻�̹Ƿ� Walk Speed�� ���� ���� �ʴ´�.")]
        [SerializeField]
        private float maxRunSpeed = 11f;

        [Header("slow Movement")]
        [Tooltip("�̵� �ӵ� ���� ����")]
        [SerializeField]
        private float slowRunSpeed = 0.5f;

        [Tooltip("작두(KRJakduSystem) 참조. 작두 발동 중(IsActing)에만 slowRunSpeed 배율로 이동 속도를 " +
                 "감속시키는 데 사용합니다. 비워두면 같은 오브젝트에서 자동 탐색합니다.")]
        [SerializeField]
        private KRJakduSystem jakduSystem;

        [Tooltip("���󿡼� ��ǥ �ӵ����� �����ϴ� ���ӵ�.")]
        [SerializeField]
        private float groundAcceleration = 52f;

        [Tooltip("���󿡼� �Է��� ������ �� ���ӵǴ� ����. �������� �̲������� �پ���.")]
        [SerializeField]
        private float groundDeceleration = 68f;

        [Tooltip("���߿��� ������ �ٲ� �� �ִ� ���ӵ�.")]
        [SerializeField]
        private float airAcceleration = 18f;

        [Tooltip("���߿��� �Է��� ������ �� ���ӵǴ� ����. �ʹ� ������ ���߿��� ���� ������ ����.")]
        [SerializeField]
        private float airDeceleration = 1.5f;

        // ============================================================
        // Jump
        // ============================================================

        [Header("Jump")]
        [Tooltip("���� ���� �ӵ�. ���� Rigidbody �ڵ��� jumpForce�� ���� ����.")]
        [SerializeField]
        private float jumpForce = 13.5f;

        [Tooltip("�ִ� ���� ���� Ƚ��. 1�̸� �Ϲ� ����, 2�� 2�� ����.")]
        [SerializeField]
        private int maxJumpCount = 1;

        [Tooltip("�⺻ �߷�. �ݵ�� �������̾�� �Ѵ�.")]
        [SerializeField]
        private float gravity = -34f;

        [Tooltip("���� �� �߰� �߷� ����. �������� �ְ��� ���� ������ ��������.")]
        [SerializeField]
        private float fallGravityMultiplier = 2.4f;

        [Tooltip("��� �� �߰� �߷� ����. �⺻������ 1�� ������ �д�.")]
        [SerializeField]
        private float riseGravityMultiplier = 1f;

        [Tooltip("���� Ű�� ª�� ���� �� ���� ������ ������� ����. FPS������ �ϴ� false ����.")]
        [SerializeField]
        private bool useVariableJumpHeight = false;

        [Tooltip("���� Ű�� ª�� ���� �� ��� �ӵ��� ���̴� ����.")]
        [SerializeField]
        private float jumpCutMultiplier = 0.8f;

        [Tooltip("�ִ� ���� �ӵ�. ������ ����ó�� õõ�� ��������.")]
        [SerializeField]
        private float maxFallSpeed = 70f;

        [Tooltip("�ٴڿ� �پ� �ֵ��� �����ϴ� ���� �ϰ� �ӵ�. ����� �Է��Ѵ�.")]
        [SerializeField]
        private float groundedStickVelocity = 5f;

        [Tooltip("�ٴڿ��� ��¦ ������ ���Ŀ��� ������ ����ϴ� �ð�.")]
        [SerializeField]
        private float coyoteTime = 0.08f;

        [Tooltip("���� Ű�� ��¦ ���� ������ ���� ���� �����ǰ� �ϴ� �ð�.")]
        [SerializeField]
        private float jumpBufferTime = 0.08f;

        [Tooltip("���� ���� �ٴ� üũ�� ��� �����ϴ� �ð�. ���� ���� ���� ������ ���� ������ ���´�.")]
        [SerializeField]
        private float groundCheckDisableAfterJump = 0.12f;

        // ============================================================
        // Dash
        // ============================================================

        [Header("Dash")]
        [Tooltip("��� �Է� Ű. Ctrl�� ������� �ʴ´�.")]
        [SerializeField]
        private KeyCode dashKey = KeyCode.LeftAlt;

        [Tooltip("��� �ӵ�.")]
        [SerializeField]
        private float dashSpeed = 24f;

        [Tooltip("��� ���� �ð�.")]
        [SerializeField]
        private float dashDuration = 0.12f;

        [Tooltip("��ø� ���� �Է��� �� �ּ� ����.")]
        [SerializeField]
        private float minTimeBetweenDashes = 0.12f;

        [Tooltip("�ִ� ��� ����. �ν����Ϳ��� ���� �����ϸ� �ܺο��� ���� �����ϴ�.")]
        [SerializeField]
        private int maxDashCharges = 1;

        [Tooltip("���� ��� ����. ���� �� maxDashCharges�� �����ȴ�.")]
        [SerializeField]
        private int currentDashCharges = 1;

        [Tooltip("��� 1���� ȸ���Ǵ� �ð�.")]
        [SerializeField]
        private float dashRechargeTime = 1.1f;

        [Tooltip("��� ���� �� ������ �ӵ� ����. 1�̸� �⺻ �޸��� �ӵ� ������ �̾�����.")]
        [SerializeField]
        private float dashExitSpeedRatio = 1f;

        [Tooltip("��� ���� �� �ϰ� �ӵ��� ��ȭ���� ����.")]
        [SerializeField]
        private bool softenFallingVelocityOnDash = true;

        [Header("Dash UI")]
        [Tooltip("��� ���� ���¸� ǥ���ϴ� UI�Դϴ�.")]
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

            // gravity�� �������� �Ѵ�.
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

            // FPS������ ������ �� ����� ���۰��� ��������.
            // �⺻���� false�� �ΰ�, ���� ������ �ʿ��� ���� �Ҵ�.
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

            // CharacterController.isGrounded�� Move ���� ���ŵǴ� ���̶� �ܵ� ����� �Ҿ����ϴ�.
            // �׷��� ���� �����δ� �����ϹǷ� sphereGrounded�� �Բ� ����Ѵ�.
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

                // �ڱ� �ڽ��̳� �ڱ� �ڽ� �ݶ��̴��� �ٴ����� ���� �ʴ´�.
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