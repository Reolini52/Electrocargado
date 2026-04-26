using UnityEngine;

public class ChargedObject : MonoBehaviour
{
    [Header("Charge Settings")]
    public float charge = 1f; // positive or negative
    public float forceStrength = 10f;
    public float effectRadius = 5f;
    public bool alwaysActive = true;

    private PlayerController player;
    private SpriteRenderer sr;

    public Color positiveColor = new Color(0.3f, 0.6f, 1f, 0.8f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.transform.position);
        if (distance > effectRadius || distance < 0.1f) return;

        // Normalized distance 0-1 for smoother falloff
        float normalizedDist = distance / effectRadius;
        float forceMagnitude = forceStrength * (1f - normalizedDist) / (distance * distance + 0.5f);
        forceMagnitude = Mathf.Min(forceMagnitude, 30f);
        float chargeProduct = charge * player.GetCharge();
        Vector2 direction = (player.transform.position - transform.position).normalized;

        if (chargeProduct < 0)
            direction *= -1;

        player.GetComponent<Rigidbody2D>().AddForce(direction * forceMagnitude);
    }

    void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = charge > 0 ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, effectRadius);
    }
}