using UnityEngine;

public class EnemyChargeReactor : MonoBehaviour
{
    public float charge = 1f;
    public float effectRadius = 8f;
    public float forceStrength = 30f;

    private ChargeResource player;
    private Rigidbody2D rb;
    private Rigidbody2D playerRb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }
    }

    void FixedUpdate()
    {
        if (player == null || player.IsNeutral()) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist > effectRadius || dist < 0.1f) return;

        float normalizedDist = dist / effectRadius;
        float forceMag = forceStrength * (1f - normalizedDist)
            / (dist * dist + 0.5f);
        forceMag = Mathf.Clamp(forceMag, 1f, 20f);

        Vector2 dirToPlayer = (player.transform.position
            - transform.position).normalized;
        float chargeProduct = charge * player.GetCharge();

        // Same = repel both, opposite = attract both
        if (chargeProduct > 0)
        {
            // Repel
            playerRb.AddForce(-dirToPlayer * forceMag);
            if (rb != null) rb.AddForce(dirToPlayer * forceMag);
        }
        else
        {
            // Attract
            playerRb.AddForce(dirToPlayer * forceMag);
            if (rb != null) rb.AddForce(-dirToPlayer * forceMag);
        }
    }
}