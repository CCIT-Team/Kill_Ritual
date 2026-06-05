using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dash
{
    private PlayerState state;
    private float dashForce;
    private float dashDuration;
    private float dashCooldown;
    private int maxDashCharges;

    public Dash(PlayerState state, float dashForce, float dashDuration,
                float dashCooldown, int maxDashCharges)
    {
        this.state = state;
        this.dashForce = dashForce;
        this.dashDuration = dashDuration;
        this.dashCooldown = dashCooldown;
        this.maxDashCharges = maxDashCharges;
    }

    public void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl)
            && state.dashCharges > 0
            && !state.isDashing)
        {
            StartDash();
        }
    }

    private void StartDash()
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

        if (inputDir.magnitude > 0.1f)
        {
            state.dashDirection = inputDir.normalized;
        }
        else
        {
            state.dashDirection = camForward;
        }

        state.isDashing = true;
        state.dashTimer = 0f;

        state.dashCharges--;
        state.dashCooldownTimers[state.dashCharges] = dashCooldown;
    }

    public void HandleMovement()
    {
        state.dashTimer += Time.fixedDeltaTime;

        float t = state.dashTimer / dashDuration;
        float currentSpeed = Mathf.Lerp(dashForce, 0f, t);

        Vector3 dashVel = state.dashDirection * currentSpeed;
        state.rb.velocity = new Vector3(dashVel.x, state.rb.velocity.y, dashVel.z);

        if (state.dashTimer >= dashDuration)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        state.isDashing = false;
    }

    public void UpdateCooldowns()
    {
        for (int i = 0; i < maxDashCharges; i++)
        {
            if (state.dashCooldownTimers[i] > 0f)
            {
                state.dashCooldownTimers[i] -= Time.deltaTime;

                if (state.dashCooldownTimers[i] <= 0f)
                {
                    state.dashCooldownTimers[i] = 0f;
                    if (state.dashCharges < maxDashCharges)
                    {
                        state.dashCharges++;
                    }
                }
            }
        }
    }
}