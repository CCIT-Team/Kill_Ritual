using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGroundChecker
{
    private PlayerState state;
    private float groundCheckDistance;

    public PlayerGroundChecker(PlayerState state, float groundCheckDistance)
    {
        this.state = state;
        this.groundCheckDistance = groundCheckDistance;
    }

    public void CheckGrounded()
    {
        bool wasGrounded = state.isGrounded;

        Vector3 origin = new Vector3(
            state.col.bounds.center.x,
            state.col.bounds.min.y + 0.05f,
            state.col.bounds.center.z
        );

        state.isGrounded = Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance + 0.05f,
            state.groundLayer
        );

        if (!wasGrounded && state.isGrounded)
            state.jumpCount = 0;
    }

    public void DrawGizmo()
    {
        if (state.col == null) return;
        Vector3 origin = new Vector3(
            state.col.bounds.center.x,
            state.col.bounds.min.y + 0.05f,
            state.col.bounds.center.z
        );
        Gizmos.color = state.isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + Vector3.down * (groundCheckDistance + 0.05f));
    }
}

public class BasicMove
{
    private PlayerState state;

    private float walkSpeed;
    private float runSpeed;

    private float groundAcceleration;

    private float groundDeceleration;

    private float airAcceleration;

    private float airControlMultiplier;

    public BasicMove(PlayerState state,
                     float walkSpeed, float runSpeed,
                     float airControlMultiplier, float airAcceleration,
                     float groundAcceleration, float groundDeceleration)
    {
        this.state = state;
        this.walkSpeed = walkSpeed;
        this.runSpeed = runSpeed;
        this.airControlMultiplier = airControlMultiplier;
        this.airAcceleration = airAcceleration;
        this.groundAcceleration = groundAcceleration;
        this.groundDeceleration = groundDeceleration;
    }

    public void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = state.cameraTransform.forward;
        Vector3 camRight = state.cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 inputDir = camForward * v + camRight * h;
        if (inputDir.magnitude > 1f)
            inputDir.Normalize();

        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        if (state.isGrounded)
        {
            ApplyGroundMovement(inputDir, targetSpeed);
        }
        else
        {
            ApplyAirMovement(inputDir, targetSpeed);
        }
    }

    private void ApplyGroundMovement(Vector3 inputDir, float targetSpeed)
    {
        if (inputDir.magnitude > 0.1f)
        {
            Vector3 targetVel = inputDir * targetSpeed;
            Vector3 currentHorizVel = new Vector3(state.rb.velocity.x, 0f, state.rb.velocity.z);
            Vector3 velDiff = targetVel - currentHorizVel;

            state.rb.AddForce(velDiff * groundAcceleration, ForceMode.Force);

            state.rb.drag = 4f;
        }
        else
        {
            state.rb.drag = 10f;
        }
    }

    private void ApplyAirMovement(Vector3 inputDir, float targetSpeed)
    {
        state.rb.drag = 0.5f;

        if (inputDir.magnitude < 0.1f) return;

        Vector3 targetVel = inputDir * targetSpeed * airControlMultiplier;
        Vector3 currentHorizVel = new Vector3(state.rb.velocity.x, 0f, state.rb.velocity.z);

        if (currentHorizVel.magnitude < targetVel.magnitude)
        {
            Vector3 velDiff = targetVel - currentHorizVel;
            state.rb.AddForce(velDiff * airAcceleration, ForceMode.Force);
        }
    }
}