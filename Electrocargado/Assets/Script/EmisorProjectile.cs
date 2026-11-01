using UnityEngine;

/// <summary>
/// Proyectil con carga eléctrica disparado por EnemyEmisor o por el Boss POLAROUS.
/// Implementa interacción electrostática (Ley de Coulomb) con THEIA (ChargeResource):
///  - Cargas OPUESTAS (q_proj * Q_THEIA < 0): Se atrae hacia THEIA y le causa daño al impactar.
///  - Cargas IGUALES  (q_proj * Q_THEIA > 0): Se repele al entrar en el radio de THEIA,
///    invierte su velocidad (rb.linearVelocity = -rb.linearVelocity * repelForce) y activa isReflected = true.
///  - Mientras isReflected == true: Ya NO daña al jugador y se vuelve letal para EnemyEmisor / EnemyHealth.
/// </summary>
public class EmisorProjectile : MonoBehaviour
{
    [Header("Projectile Stats")]
    public int damage = 1;
    public float charge = 1f;
    public float lifetime = 5f;
    public float reflectedLifetime = 6f;

    [Header("Coulomb Polarity Physics")]
    [Tooltip("Intensidad base de la fuerza de Coulomb (atracción/repulsión).")]
    public float coulombDeflectionStrength = 32f;

    [Tooltip("Radio externo donde el campo eléctrico de THEIA empieza a curvar el proyectil.")]
    public float deflectionRadius = 5.0f;

    [Tooltip("Radio interno de repulsión donde cargas iguales invierten la dirección del proyectil (rebote).")]
    public float repelRadius = 2.4f;

    [Tooltip("Multiplicador aplicado al invertir la velocidad (-rb.linearVelocity * repelForce).")]
    public float repelForce = 1.65f;

    [Tooltip("Velocidad mínima garantizada tras ser reflejado para alcanzar al emisor.")]
    public float minReflectedSpeed = 11f;

    [Tooltip("Velocidad máxima permitida para mantener estabilidad física.")]
    public float maxSpeed = 18f;

    [Tooltip("Si está activo, al rebotar corrige ligeramente la trayectoria hacia el Emisor que disparó.")]
    [Range(0f, 1f)]
    public float aimAssistTowardOwner = 0.85f;

    [Header("State Flag")]
    [Tooltip("True cuando el proyectil fue repelido por THEIA. No daña al jugador y daña a enemigos.")]
    public bool isReflected = false;

    /// <summary>
    /// Propiedad de compatibilidad con scripts existentes.
    /// </summary>
    public bool wasDeflected => isReflected;

    private GameObject owner;
    private Rigidbody2D rb;
    private Collider2D myCol;
    private SpriteRenderer sr;
    private ChargeResource playerCharge;
    private float lifeTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myCol = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        lifeTimer = lifetime;
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (myCol == null) myCol = GetComponent<Collider2D>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerCharge = playerObj.GetComponent<ChargeResource>();
        }
    }

    void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Registra quién disparó el proyectil e ignora inicialmente su colisión al salir del cañón.
    /// </summary>
    public void SetOwner(GameObject ownerObj)
    {
        owner = ownerObj;
        if (ownerObj == null) return;

        if (myCol == null) myCol = GetComponent<Collider2D>();
        Collider2D[] ownerCols = ownerObj.GetComponentsInChildren<Collider2D>();
        if (myCol != null && ownerCols != null)
        {
            for (int i = 0; i < ownerCols.Length; i++)
            {
                if (ownerCols[i] != null)
                    Physics2D.IgnoreCollision(myCol, ownerCols[i], true);
            }
        }
    }

    /// <summary>
    /// Invierte la velocidad del proyectil por repulsión electrostática y activa isReflected = true.
    /// También puede ser invocado por el rayo de THEIA (ChargeInteractor).
    /// </summary>
    public void ForceDeflectByBeam(Vector2 repulsionOrigin)
    {
        ReflectProjectile(repulsionOrigin);
    }

    /// <summary>
    /// Ejecuta el rebote electrostático:
    /// 1. isReflected = true
    /// 2. rb.linearVelocity = -rb.linearVelocity * repelForce
    /// 3. Reactiva las colisiones con el Emisor de origen para poder destruirlo.
    /// </summary>
    public void ReflectProjectile(Vector2 repulsionOrigin)
    {
        if (isReflected || rb == null) return;

        isReflected = true;
        lifeTimer = reflectedLifetime;

        // 1. Invertir velocidad según la regla solicitada: rb.linearVelocity = -rb.linearVelocity * repelForce
        Vector2 invertedVelocity = -rb.linearVelocity * repelForce;
        float targetSpeed = Mathf.Clamp(invertedVelocity.magnitude, minReflectedSpeed, maxSpeed);

        Vector2 reflectedDir = invertedVelocity.sqrMagnitude > 0.001f
            ? invertedVelocity.normalized
            : ((Vector2)transform.position - repulsionOrigin).normalized;

        // 2. Reactivar colisiones con el Emisor/Boss dueño para que el proyectil reflejado no lo ignore
        if (owner != null)
        {
            ReenableOwnerCollisions();

            Vector2 toOwner = ((Vector2)owner.transform.position - (Vector2)transform.position).normalized;
            if (toOwner.sqrMagnitude > 0.001f && aimAssistTowardOwner > 0f)
            {
                reflectedDir = Vector2.Lerp(reflectedDir, toOwner, aimAssistTowardOwner).normalized;
            }
        }

        // Asegurar que las máscaras de capa del Collider2D no excluyan al Emisor
        if (myCol != null)
        {
            myCol.includeLayers = ~0;
            myCol.excludeLayers = 0;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = reflectedDir * targetSpeed;

        // Feedback visual de proyectil reflejado (núcleo brillante sobrecargado)
        if (sr != null)
        {
            sr.color = Color.Lerp(sr.color, Color.white, 0.55f);
            transform.localScale *= 1.25f;
        }

        EnemyHealth.SpawnBurstParticles(transform.position, Color.cyan, 8, 2.8f);
    }

    private void ReenableOwnerCollisions()
    {
        if (owner == null) return;
        if (myCol == null) myCol = GetComponent<Collider2D>();
        if (myCol == null) return;

        Collider2D[] ownerCols = owner.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < ownerCols.Length; i++)
        {
            if (ownerCols[i] != null)
                Physics2D.IgnoreCollision(myCol, ownerCols[i], false);
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Si ya fue reflejado y su emisor sigue vivo, mantener guiado ligero para compensar el movimiento del Emisor
        if (isReflected)
        {
            if (owner != null && aimAssistTowardOwner > 0f)
            {
                float currentSpeed = Mathf.Clamp(rb.linearVelocity.magnitude, minReflectedSpeed, maxSpeed);
                Vector2 toOwner = ((Vector2)owner.transform.position - (Vector2)transform.position).normalized;
                if (toOwner.sqrMagnitude > 0.001f)
                {
                    Vector2 newDir = Vector2.Lerp(rb.linearVelocity.normalized, toOwner, Time.fixedDeltaTime * 6f).normalized;
                    rb.linearVelocity = newDir * currentSpeed;
                }
            }
            return;
        }

        if (playerCharge == null || playerCharge.IsNeutral()) return;

        Vector2 playerPos = playerCharge.transform.position;
        Vector2 toProjectile = (Vector2)transform.position - playerPos;
        float dist = toProjectile.magnitude;
        if (dist > deflectionRadius || dist < 0.05f) return;

        float qProd = charge * playerCharge.GetCharge();
        float sqrClampedR = ElectroPhysicsUtility.SafeSqrDistance(dist, 0.65f, deflectionRadius);
        Vector2 dirFromPlayer = toProjectile / dist;

        if (qProd > 0.05f)
        {
            // b) CARGAS IGUALES: Repulsión electrostática
            // Si entra en el radio de repulsión de THEIA con la misma polaridad, rebota e invierte dirección
            if (dist <= repelRadius)
            {
                ReflectProjectile(playerPos);
                return;
            }

            float repelAccel = (coulombDeflectionStrength * 1.8f * Mathf.Abs(qProd)) / sqrClampedR;
            rb.AddForce(dirFromPlayer * repelAccel, ForceMode2D.Force);
        }
        else if (qProd < -0.05f)
        {
            // a) CARGAS OPUESTAS: Atracción electrostática hacia THEIA (acelera hacia el jugador)
            Vector2 dirToPlayer = -dirFromPlayer;
            float attractAccel = (coulombDeflectionStrength * 0.85f * Mathf.Abs(qProd)) / sqrClampedR;
            rb.AddForce(dirToPlayer * attractAccel, ForceMode2D.Force);
            rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleImpact(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
        {
            HandleImpact(collision.collider);
        }
    }

    private void HandleImpact(Collider2D other)
    {
        if (other == null) return;

        // Ignorar colisión entre proyectiles
        if (other.GetComponent<EmisorProjectile>() != null) return;

        // Si aún NO ha sido reflejado, ignorar a su propio emisor al salir disparado
        if (!isReflected && owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            return;

        // 1. Interacción con el Jugador (THEIA)
        PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            // c) Mientras isReflected == true, la bala YA NO daña al jugador
            if (isReflected) return;

            // Si tienen la misma polaridad al tocar el collider de THEIA, la repulsión la rebota sin dañarla
            if (playerCharge != null && !playerCharge.IsNeutral() && (charge * playerCharge.GetCharge()) > 0.05f)
            {
                ReflectProjectile(playerCharge.transform.position);
                return;
            }

            // Cargas opuestas o neutra: causa daño al jugador y se destruye
            ph.TakeDamage(damage);
            EnemyHealth.SpawnBurstParticles(transform.position, Color.red, 10, 3f);
            Destroy(gameObject);
            return;
        }

        // 2. Interacción con Enemigos y Jefes cuando isReflected == true
        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null) enemyHealth = other.GetComponent<EnemyHealth>();

        EnemyEmisor emisor = other.GetComponentInParent<EnemyEmisor>();
        if (emisor == null) emisor = other.GetComponent<EnemyEmisor>();

        PolarousBossController bossController = other.GetComponentInParent<PolarousBossController>();
        PolarousBoss polarousBoss = other.GetComponentInParent<PolarousBoss>();
        bool hasEnemyTag = other.CompareTag("Enemy");

        bool isEnemyTarget = enemyHealth != null || emisor != null || bossController != null || polarousBoss != null || hasEnemyTag;

        if (isEnemyTarget)
        {
            // Si aún no está reflejado, no daña a los enemigos
            if (!isReflected) return;

            if (bossController != null)
            {
                bossController.TakeReflectedProjectileDamage(damage);
            }
            else if (polarousBoss != null)
            {
                polarousBoss.TakeReflectedProjectileDamage(damage);
            }
            else if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
            else if (emisor != null)
            {
                emisor.TakeReflectedDamage(damage);
            }
            else if (hasEnemyTag)
            {
                Destroy(other.gameObject);
            }

            EnemyHealth.SpawnBurstParticles(
                transform.position,
                sr != null ? sr.color : Color.yellow,
                14,
                4.0f
            );
            Destroy(gameObject);
            return;
        }

        // Ignorar triggers ambientales (zonas de diálogo, campos, detectores) para que no destruyan la bala en vuelo
        if (other.isTrigger) return;

        // Chocó contra pared / piso sólido
        EnemyHealth.SpawnBurstParticles(transform.position, sr != null ? sr.color : Color.white, 6, 2f);
        Destroy(gameObject);
    }
}
