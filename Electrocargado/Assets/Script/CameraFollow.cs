using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 2, -10);

    [Header("Feel")]
    public float smoothSpeed = 6f;
    public float lookAheadX = 1.5f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        float lookAhead = rb != null ? rb.linearVelocity.x * lookAheadX * 0.1f : 0f;

        Vector3 desired = new Vector3(
            target.position.x + lookAhead,
            target.position.y,
            -10f
        ) + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref velocity, 1f / smoothSpeed);
    }
}