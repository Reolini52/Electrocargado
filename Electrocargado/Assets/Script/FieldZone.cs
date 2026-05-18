using UnityEngine;

public class FieldZone : MonoBehaviour
{
    [Header("Field Settings")]
    public float charge = 1f;
    public float forceStrength = 15f;
    public bool directional = false;
    public Vector2 direction = Vector2.right;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f, 0.2f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f, 0.2f);

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;
    private bool playerInside = false;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }
        UpdateVisual();
    }

    void FixedUpdate()
    {
        if (!playerInside || player == null) return;
        if (player.IsNeutral()) return;

        float chargeProduct = charge * player.GetCharge();

        if (directional)
        {
            // Directional current — same charge flows with it
            // opposite charge flows against it
            Vector2 force = direction.normalized * forceStrength;
            if (chargeProduct < 0) force *= -1;
            playerRb.AddForce(force);
        }
        else
        {
            // Radial zone — pushes or pulls from center
            Vector2 dirFromCenter = (player.transform.position
                - transform.position).normalized;
            if (chargeProduct > 0)
                playerRb.AddForce(dirFromCenter * forceStrength); // repel
            else
                playerRb.AddForce(-dirFromCenter * forceStrength); // attract
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = charge > 0 ? Color.blue : Color.red;
        if (directional)
            Gizmos.DrawRay(transform.position, direction * 2f);
        else
            Gizmos.DrawWireSphere(transform.position, 1f);
    }
}