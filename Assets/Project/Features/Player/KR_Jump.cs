using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Jump
{
    private PlayerState state;
    private float jumpForce;
    private float doubleJumpForce;

    private float riseGravityMultiplier;   // 상승 중 추가 중력
    private float fallGravityMultiplier;   // 하강 중 추가 중력
    private float shortHopMultiplier;      // Space 일찍 뗄 때 추가 감속

    private bool jumpRequested = false;
    private float pendingJumpForce = 0f;

    public Jump(PlayerState state,
                float jumpForce, float doubleJumpForce,
                float riseGravityMultiplier,
                float fallGravityMultiplier,
                float shortHopMultiplier)
    {
        this.state = state;
        this.jumpForce = jumpForce;
        this.doubleJumpForce = doubleJumpForce;
        this.riseGravityMultiplier = riseGravityMultiplier;
        this.fallGravityMultiplier = fallGravityMultiplier;
        this.shortHopMultiplier = shortHopMultiplier;
    }

    public void HandleInput()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;

        if (state.isGrounded && state.jumpCount == 0)
        {
            jumpRequested = true;
            pendingJumpForce = jumpForce;
            state.jumpCount = 1;
        }
        else if (!state.isGrounded && state.jumpCount == 1)
        {
            jumpRequested = true;
            pendingJumpForce = doubleJumpForce;
            state.jumpCount = 2;
        }
    }

    public void ApplyJump()
    {
        if (jumpRequested)
        {
            jumpRequested = false;

            state.rb.velocity = new Vector3(
                state.rb.velocity.x,
                0f,
                state.rb.velocity.z
            );
            state.rb.AddForce(Vector3.up * pendingJumpForce, ForceMode.Impulse);
        }

        ApplyVariableGravity();
    }

    private void ApplyVariableGravity()
    {
        float vy = state.rb.velocity.y;

        if (vy > 0f)
        {
            Vector3 extraGravity = Physics.gravity * (riseGravityMultiplier - 1f);
            state.rb.AddForce(extraGravity, ForceMode.Acceleration);

            if (!Input.GetKey(KeyCode.Space))
            {
                Vector3 shortHopGravity = Physics.gravity * (shortHopMultiplier - 1f);
                state.rb.AddForce(shortHopGravity, ForceMode.Acceleration);
            }
        }
        else if (vy < 0f)
        {
            Vector3 extraGravity = Physics.gravity * (fallGravityMultiplier - 1f);
            state.rb.AddForce(extraGravity, ForceMode.Acceleration);
        }
    }
}