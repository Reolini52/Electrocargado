using UnityEngine;

public class EnemyEmisor : MonoBehaviour
{
    [Header("Stats")]
    public float detectionRadius = 10f;
    public float retreatRadius = 4f;
    public float moveSpeed = 2f;

    [Header("Shooting")]
    public float shootCooldown = 2f;
    public float projectileSpeed = 6f;
    public int projectileDamage = 1;
    public float charge = 1f;

    [Header("Charge Interaction")]
    public float chargeForceStrength = 30f;
    public float chargeEffectRadius = 8f;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color attractedColor = new Color(1f, 1f, 0f);

    private Transform player;
    private PlayerHealth playerHealth;
    private ChargeResource playerCharge;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Rigidbody2D playerRb;

    private float shootTimer = 0f;
    private bool isAttracted = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            playerCharge = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }

        sr.color = charge > 0 ? positiveColor : negativeColor;
        shootTimer = shootCooldown;
    }

    void FixedUpdate()
    {
        if (player == null || playerCharge == null) return;
        if (playerCharge.IsNeutral()) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > chargeEffectRadius || dist < 0.1f) return;

        float normalizedDist = dist / chargeEffectRadius;
        float forceMag = chargeForceStrength * (1f - normalizedDist)
            / (dist * dist + 0.5f);
        forceMag = Mathf.Clamp(forceMag, 1f, 20f);

        // Direction FROM player TO emisor
        Vector2 dirAwayFromPlayer = (transform.position - player.position).normalized;
        float chargeProduct = charge * playerCharge.GetCharge();

        isAttracted = chargeProduct < 0;

        if (chargeProduct > 0)
        {
            // Same charge = repel emisor AWAY from player
            rb.AddForce(dirAwayFromPlayer * forceMag);
            playerRb.AddForce(-dirAwayFromPlayer * forceMag);
        }
        else
        {
            // Opposite = attract emisor TOWARD player
            rb.AddForce(-dirAwayFromPlayer * forceMag);
            playerRb.AddForce(dirAwayFromPlayer * forceMag);
        }
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        shootTimer -= Time.deltaTime;

        UpdateVisual();

        if (dist <= detectionRadius)
        {
            // Only retreat if player is too close AND same charge
            // Otherwise just hover in place and shoot
            float chargeProduct = charge * playerCharge.GetCharge();
            bool sameCharge = chargeProduct > 0 && !playerCharge.IsNeutral();

            if (dist < retreatRadius && !sameCharge)
                Retreat();
            else
                Hover(); // just float, let Coulomb handle the push/pull

            if (shootTimer <= 0f)
            {
                Shoot();
                shootTimer = dist > detectionRadius * 0.7f ?
                    shootCooldown * 0.6f : shootCooldown;
            }
        }
        else
        {
            Hover();
        }
    }

    void Hover()
    {
        float hover = Mathf.Sin(Time.time * 2f) * 0.5f;
        rb.linearVelocity = new Vector2(
            Mathf.Sin(Time.time * 0.5f) * moveSpeed * 0.3f,
            hover
        );
    }

    void Retreat()
    {
        Vector2 dirAway = (transform.position - player.position).normalized;
        rb.linearVelocity = new Vector2(
            dirAway.x * moveSpeed,
            rb.linearVelocity.y
        );
    }

    void Shoot()
    {
        if (player == null) return;

        GameObject proj = new GameObject("EmisorProjectile");
        proj.transform.position = transform.position;

        SpriteRenderer projSr = proj.AddComponent<SpriteRenderer>();
        projSr.sprite = GetComponent<SpriteRenderer>().sprite;
        projSr.color = charge > 0 ?
            new Color(0.3f, 0.6f, 1f, 0.8f) :
            new Color(1f, 0.3f, 0.3f, 0.8f);
        proj.transform.localScale = Vector3.one * 0.3f;

        Rigidbody2D projRb = proj.AddComponent<Rigidbody2D>();
        projRb.gravityScale = 0.1f;

        CircleCollider2D projCol = proj.AddComponent<CircleCollider2D>();
        projCol.isTrigger = true;
        projCol.radius = 0.15f;

        EmisorProjectile projScript = proj.AddComponent<EmisorProjectile>();
        projScript.damage = projectileDamage;
        projScript.charge = charge;
        projScript.SetOwner(gameObject);

        Vector2 dir = (player.position - transform.position).normalized;
        projRb.linearVelocity = dir * projectileSpeed;

        Destroy(proj, 4f);
    }

    void UpdateVisual()
    {
        if (sr == null) return;
        Color base1 = charge > 0 ? positiveColor : negativeColor;
        if (isAttracted)
            sr.color = Color.Lerp(base1, attractedColor,
                Mathf.PingPong(Time.time * 4f, 1f));
        else
            sr.color = base1;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, retreatRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, chargeEffectRadius);
    }
}