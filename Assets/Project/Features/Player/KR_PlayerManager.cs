using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // UnityEvent 사용을 위한 선언

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerManager : MonoBehaviour
{
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

    [HideInInspector] 
    public PlayerState state;

    private PlayerGroundChecker groundChecker;
    private BasicMove basicMove;
    private Jump jump;
    private Dash dash;

    private PlayerStats playerStats;

    private bool isDead = false;

    void Awake()
    {
        state = new PlayerState();
        state.rb = GetComponent<Rigidbody>();
        state.col = GetComponent<Collider>();
        state.groundLayer = groundLayer;
        state.rb.constraints = RigidbodyConstraints.FreezeRotationX
                             | RigidbodyConstraints.FreezeRotationZ;

        if (cameraHolder != null)
        {
            state.cameraTransform = cameraHolder;
        }
        else
        {
            Camera mainCam = Camera.main;
            state.cameraTransform = mainCam != null ? mainCam.transform : this.transform;
            Debug.LogWarning("[PlayerManager] CameraHolder가 연결되지 않았습니다. " +
                             "Inspector에서 CameraHolder를 연결하세요.");
        }

        state.dashCharges = maxDashCharges;
        state.dashCooldownTimers = new float[maxDashCharges];

        groundChecker = new PlayerGroundChecker(state, groundCheckDistance);

        basicMove = new BasicMove(state, walkSpeed, runSpeed,
                                  airControlMultiplier, airAcceleration,
                                  groundAcceleration, groundDeceleration);

        jump = new Jump(state, jumpForce, doubleJumpForce,
                       riseGravityMultiplier, fallGravityMultiplier, shortHopMultiplier);

        state.rb.drag = 0f;

        dash = new Dash(state, dashForce, dashDuration, dashCooldown, maxDashCharges);

        playerStats = GetComponent<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.OnPlayerDied.AddListener(OnPlayerDied);
        }
        else
        {
            Debug.LogWarning("[PlayerManager] PlayerStats 컴포넌트를 찾지 못했습니다. " +
                             "Player 오브젝트에 PlayerStats를 Add Component 하세요.");
        }
    }

    void Update()
    {
        if (isDead) return;

        groundChecker.CheckGrounded();  // 바닥 감지 (isGrounded 갱신)
        jump.HandleInput();             // Space 입력 감지
        dash.HandleInput();             // Ctrl 입력 감지
        dash.UpdateCooldowns();         // 대쉬 쿨다운 차감 및 충전 회복
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            state.rb.velocity = Vector3.zero;
            return;
        }

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
    private void OnPlayerDied()
    {
        isDead = true;
        // 사망 직후 물리 속도를 즉시 0으로 → 미끄러지며 쓰러지는 것 방지
        if (state?.rb != null)
        {
            state.rb.velocity = Vector3.zero;
            state.rb.angularVelocity = Vector3.zero;
        }
        Debug.Log("[PlayerManager] 사망 — 이동 입력 차단됨");
    }

    private void OnDestroy()
    {
        if (playerStats != null)
            playerStats.OnPlayerDied.RemoveListener(OnPlayerDied);
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

    /// <summary>현재 체력</summary>
    public float CurrentHP => playerStats != null ? playerStats.CurrentHP : 0f;

    /// <summary>최대 체력</summary>
    public float MaxHP => playerStats != null ? playerStats.MaxHP : 0f;

    /// <summary>현재 방어도</summary>
    public float CurrentArmor => playerStats != null ? playerStats.CurrentArmor : 0f;

    /// <summary>최대 방어도</summary>
    public float MaxArmor => playerStats != null ? playerStats.MaxArmor : 0f;

    /// <summary>사망 여부</summary>
    public bool IsDead => isDead;

    /// <summary>
    /// 외부(몬스터, 함정 등)에서 플레이어에게 피해를 줄 때 호출하는 진입점.
    /// PlayerStats.TakeDamage()를 직접 호출해도 되지만,
    /// PlayerManager를 통하면 나중에 무적 프레임 등을 여기서 한 번에 처리할 수 있음.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        playerStats?.TakeDamage(damage);
    }
}