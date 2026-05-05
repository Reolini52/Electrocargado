using UnityEngine;

public class ChargedObject : MonoBehaviour
{
    [Header("Charge")]
    public float charge = 1f;
    public float effectRadius = 10f;
    public float forceStrength = 50f;

    [Header("Conduction")]
    public bool isConductor = true;
    public float conductionRate = 0.3f;
    public bool isFixedSource = true;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        player = Object.FindAnyObjectByType<ChargeResource>();
        if (player != null)
            playerRb = player.GetComponent<Rigidbody2D>();
        UpdateVisual();
    }

    void FixedUpdate()
    {
        if (player == null || playerRb == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        if (!player.IsNeutral() && dist < effectRadius && dist > 0.1f)
        {
            float normalizedDist = dist / effectRadius;
            float forceMag = forceStrength * (1f - normalizedDist)
                / (dist * dist + 0.5f);
            forceMag = Mathf.Min(forceMag, 25f);

            Vector2 dirToPlayer = (player.transform.position
                - transform.position).normalized;
            float chargeProduct = charge * player.GetCharge();

            if (chargeProduct > 0)
                dirToPlayer *= -1;

            playerRb.AddForce(dirToPlayer * forceMag);
        }

       
    }

    public void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }
}