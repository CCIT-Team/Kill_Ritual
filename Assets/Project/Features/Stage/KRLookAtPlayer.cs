using UnityEngine;

public class KRLookAtPlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Options")]
    [SerializeField] private bool lockYAxis = true;
    [SerializeField] private bool reverseDirection = false;

    private void Awake()
    {
        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer != null)
                player = foundPlayer.transform;
        }
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;

        if (lockYAxis)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        if (reverseDirection)
            direction = -direction;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}