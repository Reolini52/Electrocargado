using UnityEngine;

public class DynamicChargedObject : MonoBehaviour
{
    [Header("Charge")]
    public float charge = 1f;
    public float effectRadius = 10f;
    public float forceStrength = 40f;
    public float maxSpeed = 8f;

    [Header("Conduction")]
    public bool isConductor = true;
    public float conductionRate = 0.4f;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.8f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color neutralColor = new Color(0.8f, 0.8f, 0.8f);

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        player = Object.FindAnyObjectByType<ChargeResource>();
        if (player != null)
            playerRb = player.GetComponent<Rigidbody2D>();
        UpdateVisual();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        if (!player.IsNeutral() && dist < effectRadius && dist > 0.1f)
        {
            float normalizedDist = dist / effectRadius;
            float forceMag = forceStrength * (1f - normalizedDist)
                / (dist * dist + 0.5f);
            forceMag = Mathf.Min(forceMag, 20f);

            Vector2 dirAway = (transform.position
                - player.transform.position).normalized;
            float chargeProduct = charge * player.GetCharge();

            if (chargeProduct < 0)
                dirAway *= -1;

            rb.AddForce(dirAway * forceMag);
        }

        

        if (!player.IsNeutral() && Mathf.Abs(charge) > 0.1f && dist < effectRadius)
        {
            if (playerRb != null)
            {
                Vector2 playerVel = playerRb.linearVelocity;
                if (playerVel.magnitude > 0.5f)
                {
                    Vector2 perpendicular = new Vector2(
                        -playerVel.normalized.y,
                        playerVel.normalized.x
                    );
                    float magneticStrength = player.GetCharge() * charge
                        * playerVel.magnitude * 2f;
                    rb.AddForce(perpendicular * magneticStrength);
                }
            }
        }

        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    public void UpdateVisual()
    {
        if (sr == null) return;
        sr.color = charge > 0.1f ? positiveColor :
                   charge < -0.1f ? negativeColor : neutralColor;
    }
}