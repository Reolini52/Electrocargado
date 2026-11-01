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
    private EnemyHealth enemyHealth;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Rigidbody2D playerRb;

    private float shootTimer = 0f;
    private bool isAttracted = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            enemyHealth = gameObject.AddComponent<EnemyHealth>();
            enemyHealth.maxHealth = 3;
            enemyHealth.currentHealth = 3;
        }
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            playerCharge = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }

        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
        shootTimer = shootCooldown;
    }

    /// <summary>
    /// Recibe daño de un proyectil reflejado por THEIA y delega la muerte a EnemyHealth.
    /// </summary>
    public void TakeReflectedDamage(int amount)
    {
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(amount);
        }
        else
        {
            EnemyHealth.SpawnBurstParticles(transform.position, sr != null ? sr.color : Color.cyan, 18, 4.5f);
            Destroy(gameObject);
        }
    }

    void FixedUpdate()
    {
        if (player == null || playerCharge == null) return;
        if (playerCharge.IsNeutral()) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > chargeEffectRadius || dist < 0.1f) return;

        float sqrClampedR = ElectroPhysicsUtility.SafeSqrDistance(dist, 0.7f, chargeEffectRadius);
        float clampedR = Mathf.Sqrt(sqrClampedR);
        float q1 = charge;
        float q2 = playerCharge.GetCharge();
        float chargeProduct = q1 * q2;

        float forceMag = chargeForceStrength * Mathf.Abs(chargeProduct) / sqrClampedR;
        forceMag = Mathf.Clamp(forceMag, 0.5f, 40f);

        Vector2 dirAwayFromPlayer = (transform.position - player.position).normalized;
        isAttracted = chargeProduct < 0;

        if (chargeProduct > 0)
        {
            Vector2 f = Vector2.ClampMagnitude(dirAwayFromPlayer * forceMag, 40f);
            rb.AddForce(f);
            EMForceAccumulator2D.ApplyForce(playerRb, -f);
            GameEvents.RaiseForceApplied(new CoulombForceData(
                transform.position, player.position, q1, q2, clampedR, chargeForceStrength, -f, false));
        }
        else
        {
            Vector2 f = Vector2.ClampMagnitude(dirAwayFromPlayer * forceMag, 40f);
            rb.AddForce(-f);
            EMForceAccumulator2D.ApplyForce(playerRb, f);
            GameEvents.RaiseForceApplied(new CoulombForceData(
                transform.position, player.position, q1, q2, clampedR, chargeForceStrength, f, true));
        }
    }

    void Update()
    {
        if (player == null || playerCharge == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        shootTimer -= Time.deltaTime;

        UpdateVisual();

        if (dist <= detectionRadius)
        {
            float chargeProduct = charge * playerCharge.GetCharge();
            bool sameCharge = chargeProduct > 0 && !playerCharge.IsNeutral();

            if (dist < retreatRadius && !sameCharge)
                Retreat();
            else
                Hover();

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
        if (rb == null) return;
        float hover = Mathf.Sin(Time.time * 2f) * 0.5f;
        rb.linearVelocity = new Vector2(
            Mathf.Sin(Time.time * 0.5f) * moveSpeed * 0.3f,
            hover
        );
    }

    void Retreat()
    {
        if (rb == null || player == null) return;
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
        proj.layer = gameObject.layer;

        SpriteRenderer projSr = proj.AddComponent<SpriteRenderer>();
        projSr.sprite = sr != null ? sr.sprite : null;
        projSr.color = charge > 0 ?
            new Color(0.3f, 0.6f, 1f, 0.9f) :
            new Color(1f, 0.3f, 0.3f, 0.9f);
        if (sr != null)
        {
            projSr.sortingLayerID = sr.sortingLayerID;
            projSr.sortingOrder = sr.sortingOrder + 2;
        }
        proj.transform.localScale = Vector3.one * 0.32f;

        Rigidbody2D projRb = proj.AddComponent<Rigidbody2D>();
        projRb.gravityScale = 0.05f;
        projRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D projCol = proj.AddComponent<CircleCollider2D>();
        projCol.isTrigger = true;
        projCol.radius = 0.42f;

        EmisorProjectile projScript = proj.AddComponent<EmisorProjectile>();
        projScript.damage = Mathf.Max(1, projectileDamage);
        projScript.charge = charge;
        projScript.lifetime = 6f;
        projScript.reflectedLifetime = 7f;
        projScript.SetOwner(gameObject);

        Vector2 dir = (player.position - transform.position).normalized;
        projRb.linearVelocity = dir * projectileSpeed;
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
