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
    private static CameraFollow activeInstance;
    private float shakeTimer = 0f;
    private float shakeDuration = 0f;
    private float shakeIntensity = 0f;

    void Awake()
    {
        activeInstance = this;
    }

    /// <summary>
    /// Dispara un Screen Shake ligero en la cámara activa (ideal para explosiones o impactos).
    /// </summary>
    public static void ShakeActiveCamera(float intensity = 0.22f, float duration = 0.2f)
    {
        if (activeInstance == null && Camera.main != null)
            activeInstance = Camera.main.GetComponent<CameraFollow>();

        if (activeInstance != null)
            activeInstance.TriggerShake(intensity, duration);
    }

    public void TriggerShake(float intensity, float duration)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = Mathf.Max(shakeTimer, duration);
    }

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

        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position, desired, ref velocity, 1f / smoothSpeed);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float damper = Mathf.Clamp01(shakeTimer / shakeDuration);
            Vector2 shakeOffset = Random.insideUnitCircle * (shakeIntensity * damper);
            smoothed.x += shakeOffset.x;
            smoothed.y += shakeOffset.y;
            if (shakeTimer <= 0f)
                shakeIntensity = 0f;
        }

        transform.position = smoothed;
    }
}
