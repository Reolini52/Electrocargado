using UnityEngine;

public class DynamicChargedObject : MonoBehaviour
{
    [Header("Charge")]
    public float charge = 1f;
    public float effectRadius = 10f;
    public float forceStrength = 40f;
    public float maxSpeed = 8f;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.8f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        player = Object.FindAnyObjectByType<ChargeResource>();
        playerRb = player.GetComponent<Rigidbody2D>();
        UpdateVisual();
    }

    void FixedUpdate()
    {
        if (player == null) return;
        if (player.IsNeutral()) return;

        ApplyForceBetween(player.transform.position, player.GetCharge(), playerRb);

        // Cap speed
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    void ApplyForceBetween(Vector3 otherPos, float otherCharge, Rigidbody2D otherRb)
    {
        float dist = Vector2.Distance(transform.position, otherPos);
        if (dist > effectRadius || dist < 0.1f) return;

        float normalizedDist = dist / effectRadius;
        float forceMag = forceStrength * (1f - normalizedDist) / (dist * dist + 0.5f);
        forceMag = Mathf.Min(forceMag, 20f);

        Vector2 dirAway = (transform.position - otherPos).normalized;
        float chargeProduct = charge * otherCharge;

        // Same = repel (push away), opposite = attract (pull toward)
        if (chargeProduct < 0)
            dirAway *= -1;

        rb.AddForce(dirAway * forceMag);
    }

    public void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }
}