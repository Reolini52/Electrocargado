using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo terrestre "Corruptor" refactorizado con diseño de combate justo y Game Feel:
/// 1. Telegraphing / Fuse Time (explosionDelay): Entra en estado de Sobrecarga antes de estallar,
///    frenando su avance, parpadeando en rojo/blanco y vibrando para dar tiempo a esquivar o repeler con Coulomb.
/// 2. Separación de Rangos: detectionRadius (persecución), triggerRadius (inicio de mecha) y explosionRadius (AoE final).
/// 3. Feedback Visual y Partículas: Reproduce explosionParticles, aplica Screen Shake, oculta SpriteRenderer/Collider2D
///    y espera a que terminen las partículas antes de destruir el GameObject.
/// 4. IA Terrestre Limpia: Movimiento físico en FixedUpdate usando rb.linearVelocityX con anti-atasco en bordes y muros.
/// 5. Empuje Electrostático y Caída al Vacío (Ley de Coulomb): Cuando THEIA y el Corruptor tienen CARGAS IGUALES,
///    recibe AddForce horizontal + levemente vertical y activa ignoreEdgeCheck = true (0.8s - 1.2s), permitiendo
///    empujarlo fuera del borde de la plataforma hacia la zona de muerte (killYZone / VoidKill).
/// </summary>
public class EnemyCorruptor : MonoBehaviour
{
    [Header("1. Rangos y Hitbox (Calibrables con Gizmos)")]
    [Tooltip("Rango visual para empezar a perseguir o huir de THEIA en el suelo.")]
    public float detectionRadius = 7.5f;

    [Tooltip("Rango cercano donde el Corruptor inicia la cuenta regresiva de sobrecarga.")]
    public float triggerRadius = 1.15f;

    [Tooltip("Radio del daño de área (AoE) al terminar la sobrecarga.")]
    public float explosionRadius = 1.65f;

    [Tooltip("Proporción interna del explosionRadius donde se aplica el 100% del daño (en el borde exterior el daño se reduce).")]
    [Range(0.4f, 0.9f)]
    [SerializeField] private float innerFullDamageRatio = 0.68f;

    // Compatibilidad con escenas previas que serializaban detonationRadius
    [HideInInspector] public float detonationRadius = 1.15f;

    [Header("2. Mecánica de Detonación con Aviso (Fuse / Telegraph)")]
    [Tooltip("Tiempo de sobrecarga crítica antes de que ocurra la explosión real.")]
    [SerializeField] private float explosionDelay = 0.8f;

    [Tooltip("Daño máximo en el núcleo de la explosión.")]
    public int detonationDamage = 1;

    [Tooltip("Impulso físico aplicado a THEIA si queda dentro de la onda expansiva.")]
    public float detonationForce = 14f;

    [Tooltip("Multiplicador de velocidad mientras está sobrecargándose (0.1 = casi detenido).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float overloadSpeedMultiplier = 0.1f;

    [Tooltip("Amplitud de vibración visual del sprite durante la sobrecarga.")]
    [SerializeField] private float overloadVibrationAmplitude = 0.06f;

    [Header("3. Movimiento e IA Terrestre (FixedUpdate)")]
    public float moveSpeed = 3f;
    public float attractedSpeedMultiplier = 1.65f;
    [SerializeField] private float groundAcceleration = 22f;
    [SerializeField] private float chaseDeadzoneX = 0.28f;
    [SerializeField] private float turnCooldown = 0.35f;
    public float groundCheckDistance = 1.05f;
    public float edgeCheckDistance = 0.65f;
    public float wallCheckDistance = 0.55f;
    public LayerMask groundLayer;

    [Header("4. Empuje Electrostático y Caída al Vacío (Ley de Coulomb)")]
    public float charge = 1f;
    public float coulombForceStrength = 34f;
    public float coulombEffectRadius = 6.5f;

    [Tooltip("Elevación vertical ligera al ser repelido por cargas iguales para despegarlo de la fricción del suelo.")]
    [SerializeField] private float repulsionUpwardForce = 3.2f;

    [Tooltip("Tiempo (0.8s a 1.2s) durante el cual ignoreEdgeCheck permanece activo tras recibir repulsión por cargas iguales.")]
    [SerializeField, Range(0.5f, 2.0f)]
    private float edgeIgnoreDuration = 1.0f;

    [Tooltip("Estado activo cuando la repulsión de Coulomb desactiva el freno de bordes para permitir que caiga al vacío.")]
    public bool ignoreEdgeCheck = false;

    [Tooltip("Altura Y absoluta bajo la cual el Corruptor muere al caer al vacío.")]
    public float killYZone = -15f;

    [Tooltip("Distancia máxima de caída respecto a su altura inicial (para niveles construidos en coordenadas Y altas).")]
    [SerializeField] private float maxFallDistanceFromSpawn = 18f;

    [Header("5. Feedback Visual y Partículas (Juice)")]
    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private float screenShakeIntensity = 0.24f;
    [SerializeField] private float screenShakeDuration = 0.22f;
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color attractedColor = new Color(1f, 1f, 0f);
    public Color repelledColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color overloadFlashColorA = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color overloadFlashColorB = Color.white;

    private Transform player;
    private PlayerHealth playerHealth;
    private ChargeResource playerCharge;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Collider2D col;
    private EnemyHealth health;

    private bool isDead = false;
    private bool isOverloading = false;
    private bool isAttracted = false;
    private bool isSamePolarity = false;
    private bool isFacingRight = true;

    private float desiredVelocityX = 0f;
    private float lastTurnTime = -10f;
    private float ignoreEdgeTimer = 0f;
    private float initialSpawnY;
    private float baseLinearDamping = 2f;
    private Vector3 baseLocalScale;
    private Coroutine overloadCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        health = GetComponent<EnemyHealth>();
        baseLocalScale = transform.localScale;
        initialSpawnY = transform.position.y;

        if (rb != null)
            baseLinearDamping = rb.linearDamping;

        // Ajustar el BoxCollider2D si era excesivamente grande para evitar golpes injustos
        if (col is BoxCollider2D box)
        {
            if (box.size.x > 0.8f || box.size.y > 0.85f)
            {
                box.size = new Vector2(Mathf.Min(box.size.x, 0.72f), Mathf.Min(box.size.y, 0.78f));
                box.offset = new Vector2(box.offset.x, Mathf.Clamp(box.offset.y, -0.05f, 0.08f));
            }
        }

        EnsureExplosionParticles();
    }

    void Start()
    {
        initialSpawnY = transform.position.y;
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
        if (isDead) return;

        // 0. Verificar caída al vacío (killYZone absoluto o caída profunda desde su plataforma inicial)
        if (transform.position.y < killYZone || transform.position.y < (initialSpawnY - maxFallDistanceFromSpawn))
        {
            DieFromFall();
            return;
        }

        // Actualizar temporizador de ignoreEdgeCheck (repulsión de Coulomb)
        if (ignoreEdgeTimer > 0f)
        {
            ignoreEdgeTimer -= Time.deltaTime;
            ignoreEdgeCheck = ignoreEdgeTimer > 0f;
            if (!ignoreEdgeCheck && rb != null)
            {
                rb.linearDamping = baseLinearDamping;
            }
        }

        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        // Evaluar polaridad relativa con THEIA
        if (playerCharge != null && !playerCharge.IsNeutral())
        {
            float chargeProduct = charge * playerCharge.GetCharge();
            isAttracted = chargeProduct < -0.05f;
            isSamePolarity = chargeProduct > 0.05f;
        }
        else
        {
            isAttracted = false;
            isSamePolarity = false;
        }

        // 1. Iniciar cuenta regresiva de Sobrecarga si entra en triggerRadius
        if (!isOverloading && dist <= triggerRadius)
        {
            overloadCoroutine = StartCoroutine(OverloadAndDetonateRoutine());
        }

        // 2. Decidir intención de movimiento horizontal (ejecutada limpiamente en FixedUpdate)
        if (isOverloading)
        {
            float dx = player.position.x - transform.position.x;
            float dirSign = Mathf.Abs(dx) > chaseDeadzoneX ? Mathf.Sign(dx) : 0f;
            desiredVelocityX = dirSign * moveSpeed * overloadSpeedMultiplier;
            return;
        }

        UpdateVisual();

        if (dist <= detectionRadius)
        {
            ComputeChaseVelocity();
        }
        else
        {
            ComputePatrolVelocity();
        }
    }

    void FixedUpdate()
    {
        if (isDead || rb == null) return;

        // Aplicar repulsión/atracción electrostática de Coulomb
        ApplyCoulombInteraction();

        // Si está siendo empujado por repulsión de cargas iguales (ignoreEdgeCheck == true),
        // permitir que la inercia del AddForce lo desplace fuera del borde sin sobreescribir bruscamente su velocidad
        if (ignoreEdgeCheck)
        {
            rb.linearVelocityX = Mathf.MoveTowards(
                rb.linearVelocityX,
                desiredVelocityX,
                (groundAcceleration * 0.25f) * Time.fixedDeltaTime
            );
        }
        else
        {
            float nextVx = Mathf.MoveTowards(rb.linearVelocityX, desiredVelocityX, groundAcceleration * Time.fixedDeltaTime);
            rb.linearVelocityX = nextVx;
        }

        if (sr != null && Mathf.Abs(rb.linearVelocityX) > 0.05f)
        {
            sr.flipX = rb.linearVelocityX < 0f;
        }
    }

    private void ComputeChaseVelocity()
    {
        float dx = player.position.x - transform.position.x;

        // Zona muerta horizontal para que no vibre debajo/encima del jugador
        if (Mathf.Abs(dx) <= chaseDeadzoneX)
        {
            desiredVelocityX = 0f;
            return;
        }

        float dirToPlayerX = Mathf.Sign(dx);

        // Si tienen la MISMA carga, el Corruptor es repelido hacia el lado opuesto de THEIA
        if (isSamePolarity)
        {
            float fleeDir = -dirToPlayerX;
            isFacingRight = fleeDir > 0f;

            // Si ignoreEdgeCheck está activo por repulsión de Coulomb, NO se detiene en el borde de la plataforma
            if ((!ignoreEdgeCheck && CheckEdgeAhead(fleeDir)) || CheckWallAhead(fleeDir))
            {
                desiredVelocityX = 0f;
            }
            else
            {
                desiredVelocityX = fleeDir * moveSpeed * 0.95f;
            }
            return;
        }

        isFacingRight = dirToPlayerX > 0f;

        // No caer por precipicios (salvo que ignoreEdgeCheck esté activo) ni empujar muros infinitamente
        if ((!ignoreEdgeCheck && CheckEdgeAhead(dirToPlayerX)) || CheckWallAhead(dirToPlayerX))
        {
            desiredVelocityX = 0f;
            return;
        }

        float speed = isAttracted ? moveSpeed * attractedSpeedMultiplier : moveSpeed;
        desiredVelocityX = dirToPlayerX * speed;
    }

    private void ComputePatrolVelocity()
    {
        float dirSign = isFacingRight ? 1f : -1f;
        bool edgeAhead = !ignoreEdgeCheck && CheckEdgeAhead(dirSign);
        bool wallAhead = CheckWallAhead(dirSign);

        if ((edgeAhead || wallAhead) && Time.time - lastTurnTime >= turnCooldown)
        {
            isFacingRight = !isFacingRight;
            dirSign = isFacingRight ? 1f : -1f;
            lastTurnTime = Time.time;
        }

        // Si ambos lados están bloqueados, quedarse quieto sin vibrar
        if ((!ignoreEdgeCheck && CheckEdgeAhead(dirSign)) || CheckWallAhead(dirSign))
        {
            desiredVelocityX = 0f;
            return;
        }

        desiredVelocityX = dirSign * (moveSpeed * 0.45f);
    }

    /// <summary>
    /// Aplica la fuerza electrostática de Coulomb en FixedUpdate:
    /// Si THEIA está cerca con la MISMA polaridad (qProd > 0), aplica AddForce horizontal y levemente vertical
    /// y activa ignoreEdgeCheck = true durante edgeIgnoreDuration para poder lanzarlo fuera de la plataforma.
    /// </summary>
    private void ApplyCoulombInteraction()
    {
        if (player == null || playerCharge == null || playerCharge.IsNeutral()) return;

        Vector2 toCorruptor = (Vector2)transform.position - (Vector2)player.position;
        float dist = toCorruptor.magnitude;
        if (dist > coulombEffectRadius || dist < 0.1f) return;

        float qProd = charge * playerCharge.GetCharge();
        if (Mathf.Abs(qProd) < 0.05f) return;

        float sqrR = ElectroPhysicsUtility.SafeSqrDistance(dist, 0.75f, coulombEffectRadius);
        float forceMag = Mathf.Clamp((coulombForceStrength * Mathf.Abs(qProd)) / sqrR, 0f, 38f);
        float dirFromPlayerX = Mathf.Abs(toCorruptor.x) > 0.05f ? Mathf.Sign(toCorruptor.x) : (isFacingRight ? 1f : -1f);

        if (qProd > 0.05f)
        {
            // REPULSIÓN POR CARGAS IGUALES:
            // 1. Activar ignoreEdgeCheck temporalmente (0.8s a 1.2s) para desactivar el bloqueo de bordes
            ActivateEdgeIgnoreWindow(edgeIgnoreDuration);

            // 2. Aplicar AddForce horizontal y levemente vertical para vencer la fricción y sacarlo de la plataforma
            float verticalLift = rb.linearVelocityY <= 1.2f ? repulsionUpwardForce : 0f;
            Vector2 repulsionForce = new Vector2(dirFromPlayerX * forceMag, verticalLift);
            rb.AddForce(repulsionForce, ForceMode2D.Force);
        }
        else if (qProd < -0.05f)
        {
            // Atracción por cargas opuestas (mantiene el bloqueo de bordes normal)
            rb.AddForce(new Vector2(-dirFromPlayerX * (forceMag * 0.45f), 0f), ForceMode2D.Force);
        }
    }

    /// <summary>
    /// Activa temporalmente ignoreEdgeCheck = true y reduce el amortiguamiento lineal
    /// para que el empuje electrostático desplace al Corruptor fuera de la plataforma.
    /// También puede ser llamado directamente por el rayo de THEIA (ChargeInteractor).
    /// </summary>
    public void ActivateEdgeIgnoreWindow(float duration = -1f)
    {
        float targetDuration = duration > 0f ? duration : edgeIgnoreDuration;
        ignoreEdgeTimer = Mathf.Max(ignoreEdgeTimer, targetDuration);
        ignoreEdgeCheck = true;

        if (rb != null)
        {
            rb.linearDamping = 0.25f;
        }
    }

    private bool CheckEdgeAhead(float dirSign)
    {
        if (ignoreEdgeCheck) return false;
        if (groundLayer.value == 0) return false;

        Vector2 origin = (Vector2)transform.position + new Vector2(dirSign * edgeCheckDistance, 0.1f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        return hit.collider == null;
    }

    private bool CheckWallAhead(float dirSign)
    {
        if (groundLayer.value == 0) return false;

        Vector2 dir = dirSign > 0f ? Vector2.right : Vector2.left;
        Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.15f);
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, wallCheckDistance, groundLayer);
        return hit.collider != null && hit.collider != col && !hit.collider.isTrigger;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CheckKillZoneCollision(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
        {
            CheckKillZoneCollision(collision.collider);
        }
    }

    private void CheckKillZoneCollision(Collider2D other)
    {
        if (isDead || other == null) return;

        // Si cae en una zona de muerte (VoidKill, Spikes o tag de peligro), muere definitivamente
        bool isVoidOrSpikes =
            other.GetComponent<Spikes>() != null ||
            other.name.IndexOf("VoidKill", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            other.name.IndexOf("KillZone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            other.CompareTag("Respawn");

        if (isVoidOrSpikes)
        {
            DieFromFall();
        }
    }

    /// <summary>
    /// Mata definitivamente al Corruptor cuando cae fuera de la plataforma hacia el vacío o VoidKill.
    /// </summary>
    public void DieFromFall()
    {
        if (isDead) return;
        isDead = true;

        if (overloadCoroutine != null)
        {
            StopCoroutine(overloadCoroutine);
            overloadCoroutine = null;
        }

        Color burstCol = charge > 0 ? positiveColor : negativeColor;
        EnemyHealth.SpawnBurstParticles(transform.position, burstCol, 18, 4.5f);

        if (health != null)
        {
            health.Die();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Corrutina de Sobrecarga Crítica ("Telegraphing / Fuse Time"):
    /// Frena al Corruptor, parpadea cada vez más rápido entre rojo y blanco, vibra
    /// y finalmente ejecuta la explosión por área (Physics2D.OverlapCircleAll).
    /// </summary>
    private IEnumerator OverloadAndDetonateRoutine()
    {
        isOverloading = true;
        float elapsed = 0f;
        float duration = Mathf.Max(0.15f, explosionDelay);

        while (elapsed < duration)
        {
            if (isDead) yield break;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // Frecuencia de parpadeo acelerada conforme se acerca la detonación
            float flashFreq = Mathf.Lerp(10f, 32f, progress);
            float tFlash = Mathf.PingPong(elapsed * flashFreq, 1f);
            if (sr != null)
            {
                sr.color = Color.Lerp(overloadFlashColorA, overloadFlashColorB, tFlash);
            }

            // Vibración / hinchazón progresiva para telegrafiar el radio inminente
            float scaleInflation = 1f + (progress * 0.22f);
            float shakeX = Mathf.Sin(elapsed * 55f) * overloadVibrationAmplitude * progress;
            float shakeY = Mathf.Cos(elapsed * 48f) * overloadVibrationAmplitude * 0.5f * progress;
            transform.localScale = baseLocalScale * scaleInflation + new Vector3(shakeX, shakeY, 0f);

            yield return null;
        }

        transform.localScale = baseLocalScale;
        ExecuteExplosion();
    }

    /// <summary>
    /// Ejecuta la detonación real usando Physics2D.OverlapCircleAll(transform.position, explosionRadius),
    /// permitiendo esquivar o recibir daño reducido en el borde exterior.
    /// </summary>
    private void ExecuteExplosion()
    {
        if (isDead) return;
        isDead = true;
        desiredVelocityX = 0f;

        // 1. Evaluar objetivos dentro de explosionRadius con Physics2D.OverlapCircleAll
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        bool damagedPlayer = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject == gameObject) continue;

            PlayerHealth targetPlayerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (targetPlayerHealth != null && !damagedPlayer)
            {
                damagedPlayer = true;
                float distToPlayer = Vector2.Distance(transform.position, targetPlayerHealth.transform.position);
                float coreRadius = explosionRadius * innerFullDamageRatio;

                // Si el jugador está en el núcleo recibe el daño completo; si se alejó al borde recibe daño reducido
                int finalDamage = distToPlayer <= coreRadius
                    ? detonationDamage
                    : Mathf.Max(0, detonationDamage - 1);

                if (finalDamage > 0)
                {
                    targetPlayerHealth.TakeDamage(finalDamage);
                }

                Rigidbody2D targetRb = targetPlayerHealth.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 knockDir = ((Vector2)targetPlayerHealth.transform.position - (Vector2)transform.position).normalized;
                    if (knockDir.y < 0.25f) knockDir.y = 0.35f;
                    knockDir.Normalize();

                    float distFactor = Mathf.Clamp01(1f - (distToPlayer / (explosionRadius * 1.15f)));
                    float impulse = detonationForce * Mathf.Lerp(0.45f, 1f, distFactor);
                    targetRb.AddForce(knockDir * impulse, ForceMode2D.Impulse);
                }
            }
        }

        // 2. Feedback Visual: Partículas + Screen Shake
        CameraFollow.ShakeActiveCamera(screenShakeIntensity, screenShakeDuration);

        float destroyWait = 0.55f;
        if (explosionParticles != null)
        {
            explosionParticles.transform.SetParent(null, true);
            explosionParticles.Play(true);
            destroyWait = Mathf.Max(0.45f, explosionParticles.main.duration + explosionParticles.main.startLifetime.constantMax);
            Destroy(explosionParticles.gameObject, destroyWait + 0.1f);
        }
        else
        {
            Color burstCol = charge > 0 ? positiveColor : negativeColor;
            EnemyHealth.SpawnBurstParticles(transform.position, burstCol, 22, 5.2f);
        }

        // 3. Desactivar SpriteRenderer, Collider2D y físicas antes de destruir el GameObject
        if (sr != null) sr.enabled = false;
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Destroy(gameObject, destroyWait);
    }

    private void EnsureExplosionParticles()
    {
        if (explosionParticles != null) return;

        Transform existing = transform.Find("CorruptorExplosionVFX");
        if (existing != null)
        {
            explosionParticles = existing.GetComponent<ParticleSystem>();
            if (explosionParticles != null) return;
        }

        GameObject vfxObj = new GameObject("CorruptorExplosionVFX");
        vfxObj.transform.SetParent(transform, false);
        vfxObj.transform.localPosition = Vector3.zero;

        explosionParticles = vfxObj.AddComponent<ParticleSystem>();
        explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = explosionParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(overloadFlashColorA, new Color(1f, 0.75f, 0.2f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.15f;
        main.maxParticles = 36;

        var emission = explosionParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 26) });

        var shape = explosionParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        var sizeOverLifetime = explosionParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.6f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                renderer.sharedMaterial = new Material(shader);
            renderer.sortingOrder = 25;
        }
    }

    void UpdateVisual()
    {
        if (sr == null || isOverloading) return;
        Color baseColor = charge > 0 ? positiveColor : negativeColor;

        if (isAttracted)
        {
            sr.color = Color.Lerp(baseColor, attractedColor, Mathf.PingPong(Time.time * 5f, 1f));
        }
        else if (isSamePolarity)
        {
            sr.color = Color.Lerp(baseColor, repelledColor, 0.35f);
        }
        else
        {
            sr.color = baseColor;
        }
    }

    void OnDrawGizmosSelected()
    {
        // 1. detectionRadius (Cian): Rango de persecución
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 2. triggerRadius (Amarillo): Rango donde inicia la mecha / sobrecarga
        Gizmos.color = new Color(1f, 0.9f, 0.15f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        // 3. explosionRadius (Rojo): Radio final de la explosión AoE
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        // 4. Rayos de detección de borde y pared
        float dirSign = isFacingRight ? 1f : -1f;
        Gizmos.color = ignoreEdgeCheck ? Color.magenta : Color.green;
        Vector3 edgeOrigin = transform.position + new Vector3(dirSign * edgeCheckDistance, 0.1f, 0f);
        Gizmos.DrawLine(edgeOrigin, edgeOrigin + Vector3.down * groundCheckDistance);

        Vector3 wallOrigin = transform.position + new Vector3(0f, 0.15f, 0f);
        Gizmos.DrawLine(wallOrigin, wallOrigin + new Vector3(dirSign * wallCheckDistance, 0f, 0f));
    }
}
