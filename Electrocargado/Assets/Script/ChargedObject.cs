using UnityEngine;

public class ChargedObject : MonoBehaviour
{
    [Header("Charge")]
    public float charge = 1f;
    public float effectRadius = 10f;
    public float forceStrength = 50f;

    [Header("Settings")]
    public bool isConductor = true;
    public bool isFixedSource = true;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    void Update()
    {
        if (player == null)
        {
            player = Object.FindAnyObjectByType<ChargeResource>();
            if (player != null)
                playerRb = player.GetComponent<Rigidbody2D>();
        }
    }

    void FixedUpdate()
    {
        if (player == null || playerRb == null) return;

        // ALWAYS check distance to player directly
        // No cursor needed — this is passive physics
        float dist = Vector2.Distance(
            transform.position,
            player.transform.position
        );

        if (dist > effectRadius || dist < 0.1f) return;
        if (player.IsNeutral()) return;

        float normalizedDist = dist / effectRadius;
        float forceMag = forceStrength * (1f - normalizedDist)
            / (dist * dist + 0.5f);
        forceMag = Mathf.Clamp(forceMag, 2f, 30f);

        // Direction FROM object TO player
        Vector2 dirToPlayer = (player.transform.position
            - transform.position).normalized;

        float chargeProduct = charge * player.GetCharge();

        // SAME sign = positive product = REPEL = push player away
        // DIFFERENT sign = negative product = ATTRACT = pull player toward
        if (chargeProduct > 0)
        {
            // Same charge = REPEL = push player AWAY
            playerRb.AddForce(dirToPlayer * forceMag);
        }
        else
        {
            // Opposite charge = ATTRACT = pull player TOWARD
            playerRb.AddForce(-dirToPlayer * forceMag);
        }
    }

    public void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }
}