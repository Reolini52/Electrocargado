using UnityEngine;

public class EnemyCorruptor : MonoBehaviour
{
    [Header("Stats")]
    public float moveSpeed = 3f;
    public float attractedSpeedMultiplier = 2f;
    public float detectionRadius = 8f;
    public float detonationRadius = 0.8f;
    public int detonationDamage = 1;
    public float detonationForce = 15f;

    [Header("Charge")]
    public float charge = 1f;

    [Header("AI")]
    public float groundCheckDistance = 1f;
    public float edgeCheckDistance = 0.8f;
    public LayerMask groundLayer;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color attractedColor = new Color(1f, 1f, 0f);
    public Color repelledColor = new Color(0.5f, 0.5f, 0.5f);

    private Transform player;
    private PlayerHealth playerHealth;
    private ChargeResource playerCharge;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private EnemyHealth health;

    private bool isDead = false;
    private bool isAttracted = false;
    private bool isFacingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();

        isFacingRight = Random.value > 0.5f;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            playerCharge = playerObj.GetComponent<ChargeResource>();
        }

        UpdateVisual();
    }

    void Update()
    {
        if (isDead || player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (playerCharge != null && !playerCharge.IsNeutral())
        {
            float chargeProduct = charge * playerCharge.GetCharge();
            isAttracted = chargeProduct < 0;
        }
        else
        {
            isAttracted = false;
        }

        UpdateVisual();

        if (dist < detonationRadius)
        {
            Detonate();
            return;
        }

        if (dist <= detectionRadius)
            Chase();
        else
            Patrol();
    }

    void Chase()
    {
        Vector2 dirToPlayer = (player.position - transform.position).normalized;

        if (!isAttracted && playerCharge != null && !playerCharge.IsNeutral())
        {
            // Same charge = flee
            bool edgeAhead = CheckEdgeAhead();
            if (!edgeAhead)
            {
                rb.linearVelocity = new Vector2(
                    -dirToPlayer.x * moveSpeed,
                    rb.linearVelocity.y
                );
                isFacingRight = -dirToPlayer.x > 0;
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            return;
        }

        bool edge = CheckEdgeAhead();
        if (edge)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float speed = isAttracted ?
            moveSpeed * attractedSpeedMultiplier : moveSpeed;

        rb.linearVelocity = new Vector2(
            dirToPlayer.x * speed,
            rb.linearVelocity.y
        );
        isFacingRight = dirToPlayer.x > 0;
    }

    void Patrol()
    {
        bool edgeAhead = CheckEdgeAhead();
        bool wallAhead = CheckWallAhead();

        if (edgeAhead || wallAhead)
            isFacingRight = !isFacingRight;

        float dir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(
            dir * moveSpeed * 0.4f,
            rb.linearVelocity.y
        );
    }

    bool CheckEdgeAhead()
    {
        Vector2 ahead = transform.position + new Vector3(
            isFacingRight ? edgeCheckDistance : -edgeCheckDistance,
            0f, 0f);

        RaycastHit2D hit = Physics2D.Raycast(
            ahead,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        return hit.collider == null;
    }

    bool CheckWallAhead()
    {
        Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            dir,
            0.6f,
            groundLayer
        );

        return hit.collider != null;
    }

    void Detonate()
    {
        if (isDead) return;
        isDead = true;

        playerHealth?.TakeDamage(detonationDamage);

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 knockDir = (player.position - transform.position).normalized;
            playerRb.AddForce(knockDir * detonationForce, ForceMode2D.Impulse);
        }

        Destroy(gameObject);
    }

    void UpdateVisual()
    {
        if (sr == null) return;
        Color baseColor = charge > 0 ? positiveColor : negativeColor;

        if (isAttracted)
            sr.color = Color.Lerp(baseColor, attractedColor,
                Mathf.PingPong(Time.time * 5f, 1f));
        else if (playerCharge != null && !playerCharge.IsNeutral())
            sr.color = Color.Lerp(baseColor, repelledColor, 0.3f);
        else
            sr.color = baseColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detonationRadius);
    }
}