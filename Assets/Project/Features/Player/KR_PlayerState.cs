using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    
    public Rigidbody rb;
    public Collider col;
    public Transform cameraTransform;
    public bool isGrounded = false;

    public LayerMask groundLayer;

    public int jumpCount = 0;

    public bool isDashing = false;
    public int dashCharges;
    public float[] dashCooldownTimers;
    public Vector3 dashDirection = Vector3.zero;
    public float dashTimer = 0f;
}