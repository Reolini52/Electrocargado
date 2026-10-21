using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5.3 Boss Final — POLAROUS (El Capacitor Viviente).
/// Integra todos los conceptos de Electromagnetismo del proyecto en un examen práctico:
/// 1. Capacitancia y Descarga RC (Q = C*V, Q(t) = Q0*e^(-t/RC)):
///    Al cargar las placas ⊕ (+Q) y ⊖ (-Q) en cualquier orden (pisándolas, expulsando carga [Q]+Click Der
///    o empujando la esfera móvil con Ley de Coulomb), se cierra el circuito por la Barra Conductora (CurrentRod),
///    liberando una descarga RC que daña y sobrecarga a POLAROUS.
/// 2. Ley de Coulomb (Fe = ke * q1*q2 / r^2):
///    Los proyectiles cargados de POLAROUS se repelen de vuelta hacia él si THEIA tiene la MISMA carga
///    o les apunta con el rayo [Click Izq] de igual signo.
/// 3. Campo Eléctrico (Fase 2) y Corriente / Fuerza Magnética (Fase 3):
///    En Fase 2 invierte el campo eléctrico E entre placas; en Fase 3 activa la corriente I y fuerza magnética Fm.
/// </summary>
public class PolarousBossController : MonoBehaviour
{
    [Header("Salud y Fases de POLAROUS")]
    public int maxHealth = 9;
    public int currentHealth = 9;
    public int currentPhase = 1; // 1, 2 o 3

    [Header("Ciclo de Carga de POLAROUS (Capacitor)")]
    [Tooltip("Polaridad actual de POLAROUS (+1 Azul o -1 Rojo).")]
    public float bossCharge = 1f;

    [Tooltip("Segundos entre cada inversión de polaridad del Boss.")]
    public float polaritySwitchInterval = 8.0f;
    private float polarityTimer = 0f;

    [Header("Ataques de Proyectiles Cargados")]
    public float shootInterval = 2.3f;
    public float projectileSpeed = 6.8f;
    private float shootTimer = 1.8f;

    [Header("Movimiento en el Núcleo del Capacitor")]
    public Vector2 arenaCenter = new Vector2(0f, 2.2f);
    public float hoverAmplitudeX = 4.2f;
    public float hoverAmplitudeY = 1.3f;
    public float hoverSpeed = 1.0f;

    [Header("Conexiones del Circuito (Arena)")]
    public CurrentRod centralCurrentRod;
    public FieldZone arenaElectricField;

    // Estado de sobrecarga / aturdimiento tras descarga RC
    private float overloadStunTimer = 0f;
    private float meleeHitCooldown = 0f;

    // Referencias
    private Transform player;
    private ChargeResource playerCharge;
    private Rigidbody2D playerRb;
    private SpriteRenderer sr;
    private readonly List<CapacitorPlateNode> plateNodes = new List<CapacitorPlateNode>();

    // Elementos visuales del Capacitor Viviente y Arco RC
    private LineRenderer dischargeArcRenderer;
    private LineRenderer bossAuraRing;
    private float arcDisplayTimer = 0f;
    private string statusBannerText = "";

    private GUIStyle bossTitleStyle;
    private GUIStyle bossSubStyle;

    public void InitializeBoss(List<CapacitorPlateNode> nodes, Vector2 center, CurrentRod rod = null, FieldZone field = null)
    {
        arenaCenter = center;
        centralCurrentRod = rod;
        arenaElectricField = field;

        plateNodes.Clear();
        plateNodes.AddRange(nodes);

        foreach (var node in plateNodes)
        {
            if (node != null)
            {
                node.OnNodeChargedByPlayer -= HandlePlateNodeCharged;
                node.OnNodeChargedByPlayer += HandlePlateNodeCharged;
            }
        }

        SetupPhaseCircuit(1);
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        polarityTimer = polaritySwitchInterval;

        // Arco eléctrico de descarga RC
        GameObject arcObj = new GameObject("CapacitorDischargeArc");
        arcObj.transform.SetParent(transform);
        dischargeArcRenderer = arcObj.AddComponent<LineRenderer>();
        dischargeArcRenderer.useWorldSpace = true;
        dischargeArcRenderer.positionCount = 6;
        dischargeArcRenderer.startWidth = 0.24f;
        dischargeArcRenderer.endWidth = 0.08f;
        dischargeArcRenderer.material = new Material(Shader.Find("Sprites/Default"));
        dischargeArcRenderer.sortingOrder = 12;
        dischargeArcRenderer.enabled = false;

        // Anillo electromagnético alrededor de POLAROUS
        GameObject ringObj = new GameObject("PolarousFieldRing");
        ringObj.transform.SetParent(transform);
        ringObj.transform.localPosition = Vector3.zero;
        bossAuraRing = ringObj.AddComponent<LineRenderer>();
        bossAuraRing.useWorldSpace = true;
        bossAuraRing.loop = true;
        bossAuraRing.positionCount = 28;
        bossAuraRing.startWidth = 0.08f;
        bossAuraRing.endWidth = 0.08f;
        bossAuraRing.material = new Material(Shader.Find("Sprites/Default"));
        bossAuraRing.sortingOrder = 5;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerCharge = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }

        UpdateBossVisual();
    }

    private void Update()
    {
        if (player == null) return;

        if (meleeHitCooldown > 0f)
            meleeHitCooldown -= Time.deltaTime;

        // 1. Estado de Sobrecarga (Stun tras descarga RC) vs Movimiento normal
        if (overloadStunTimer > 0f)
        {
            overloadStunTimer -= Time.deltaTime;
            // Desciende cerca de la plataforma central mientras está en cortocircuito
            Vector3 stunPos = new Vector3(arenaCenter.x, 1.3f + Mathf.Sin(Time.time * 18f) * 0.08f, 0f);
            transform.position = Vector3.Lerp(transform.position, stunPos, Time.deltaTime * 5f);

            if (overloadStunTimer <= 0f)
            {
                if (centralCurrentRod != null && currentPhase < 3)
                    centralCurrentRod.ActivateCurrent(false, true);
                UpdatePhaseBannerText();
            }
        }
        else
        {
            float speedMult = currentPhase == 1 ? 1f : (currentPhase == 2 ? 1.3f : 1.6f);
            float targetX = arenaCenter.x + Mathf.Sin(Time.time * hoverSpeed * speedMult) * hoverAmplitudeX;
            float targetY = arenaCenter.y + Mathf.Cos(Time.time * hoverSpeed * 1.35f * speedMult) * hoverAmplitudeY;
            transform.position = Vector3.Lerp(transform.position, new Vector3(targetX, targetY, 0f), Time.deltaTime * 3.5f);

            // 2. Ciclo de carga/descarga de POLAROUS
            polarityTimer -= Time.deltaTime;
            if (polarityTimer <= 0f)
            {
                bossCharge = -bossCharge;
                polarityTimer = Mathf.Max(4.2f, polaritySwitchInterval - (currentPhase - 1) * 1.3f);
                UpdateBossVisual();

                // En Fase 2 y 3, el campo eléctrico entre las placas oscila con la polaridad de POLAROUS
                if (arenaElectricField != null && currentPhase >= 2)
                {
                    arenaElectricField.direction = new Vector2(bossCharge > 0f ? 1f : -1f, 0f);
                }
            }

            // 3. Disparo de proyectiles cargados
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                ExecutePhaseAttack();
                float baseCooldown = shootInterval - (currentPhase - 1) * 0.35f;
                shootTimer = Mathf.Max(1.0f, baseCooldown);
            }
        }

        // 4. Actualizar anillo electromagnético de POLAROUS
        UpdateBossAuraRing();

        // 5. Animar rayo zigzagueante de descarga RC cuando recibe daño de las placas
        if (arcDisplayTimer > 0f)
        {
            arcDisplayTimer -= Time.deltaTime;
            if (dischargeArcRenderer != null)
            {
                Vector3 startPos = centralCurrentRod != null ? centralCurrentRod.transform.position : Vector3.zero;
                Vector3 endPos = transform.position;
                int count = dischargeArcRenderer.positionCount;
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / (count - 1);
                    Vector3 pt = Vector3.Lerp(startPos, endPos, t);
                    if (i > 0 && i < count - 1)
                    {
                        pt += new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.35f, 0.35f), 0f);
                    }
                    dischargeArcRenderer.SetPosition(i, pt);
                }

                if (arcDisplayTimer <= 0f)
                    dischargeArcRenderer.enabled = false;
            }
        }
    }

    private void UpdateBossAuraRing()
    {
        if (bossAuraRing == null) return;

        float radius = overloadStunTimer > 0f
            ? 0.9f + Mathf.Sin(Time.time * 20f) * 0.15f
            : 1.15f + Mathf.Sin(Time.time * 4f) * 0.12f;

        int count = bossAuraRing.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)i / count * Mathf.PI * 2f;
            bossAuraRing.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        Color c = overloadStunTimer > 0f
            ? new Color(1f, 0.92f, 0.2f, 0.95f)
            : (bossCharge > 0f ? new Color(0.25f, 0.72f, 1f, 0.85f) : new Color(1f, 0.28f, 0.32f, 0.85f));
        bossAuraRing.startColor = c;
        bossAuraRing.endColor = c;
    }

    private void FixedUpdate()
    {
        if (player == null || playerCharge == null || playerRb == null) return;
        if (playerCharge.IsNeutral()) return;

        // Fuerza de Coulomb directa entre el núcleo de POLAROUS y THEIA
        Vector2 bossPos = transform.position;
        Vector2 playerPos = player.position;
        Vector2 disp = playerPos - bossPos;
        float dist = disp.magnitude;

        if (dist > 14f || dist < 0.2f) return;

        float clampedR = Mathf.Max(dist, 1.1f);
        Vector2 rHat = disp / dist;
        float q1 = bossCharge * 1.4f;
        float q2 = playerCharge.GetCharge();
        float ke = 50f;

        float forceMag = Mathf.Clamp(ke * Mathf.Abs(q1 * q2) / (clampedR * clampedR), 0.5f, 22f);
        bool isAttractive = (q1 * q2) < 0f;
        Vector2 forceVec = isAttractive ? (-rHat * forceMag) : (rHat * forceMag);

        playerRb.AddForce(forceVec, ForceMode2D.Force);

        GameEvents.RaiseForceApplied(new CoulombForceData(
            bossPos, playerPos, q1, q2, clampedR, ke, forceVec, isAttractive
        ));
    }

    private void ExecutePhaseAttack()
    {
        if (player == null) return;

        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;

        if (currentPhase == 1)
        {
            SpawnChargedProjectile(toPlayer, bossCharge, projectileSpeed);
        }
        else if (currentPhase == 2)
        {
            SpawnChargedProjectile(RotateVector(toPlayer, -11f), bossCharge, projectileSpeed * 1.08f);
            SpawnChargedProjectile(RotateVector(toPlayer, 11f), bossCharge, projectileSpeed * 1.08f);
        }
        else
        {
            SpawnChargedProjectile(toPlayer, bossCharge, projectileSpeed * 1.18f);
            SpawnChargedProjectile(RotateVector(toPlayer, -20f), bossCharge, projectileSpeed * 1.1f);
            SpawnChargedProjectile(RotateVector(toPlayer, 20f), -bossCharge, projectileSpeed * 1.05f);
        }
    }

    private void SpawnChargedProjectile(Vector2 dir, float projCharge, float speed)
    {
        GameObject proj = new GameObject("PolarousProjectile");
        proj.transform.position = transform.position;

        SpriteRenderer projSr = proj.AddComponent<SpriteRenderer>();
        if (sr != null) projSr.sprite = sr.sprite;
        projSr.color = projCharge > 0f
            ? new Color(0.25f, 0.75f, 1f, 0.95f)
            : new Color(1f, 0.28f, 0.32f, 0.95f);
        proj.transform.localScale = Vector3.one * 0.42f;
        projSr.sortingOrder = 8;

        Rigidbody2D projRb = proj.AddComponent<Rigidbody2D>();
        projRb.gravityScale = 0f;
        projRb.linearVelocity = dir * speed;

        CircleCollider2D col = proj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.3f;

        EmisorProjectile script = proj.AddComponent<EmisorProjectile>();
        script.damage = 1;
        script.charge = projCharge;
        script.SetOwner(gameObject);

        Destroy(proj, 5.5f);
    }

    private static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }

    /// <summary>
    /// Configura las placas del capacitor según la fase sin exigir orden secuencial.
    /// </summary>
    private void SetupPhaseCircuit(int phase)
    {
        currentPhase = phase;
        Vector3 busPos = centralCurrentRod != null ? centralCurrentRod.transform.position : new Vector3(0f, 0.6f, 0f);

        if (plateNodes.Count < 4) return;

        if (phase == 1)
        {
            // Fase 1: Capacitor simple de 2 placas (Placa Izquierda ⊕ +Q y Placa Derecha ⊖ -Q)
            plateNodes[0].ConfigureNode(0, +1f, true, busPos);
            plateNodes[1].ConfigureNode(1, -1f, true, busPos);
            plateNodes[2].ConfigureNode(2, -1f, false, busPos);
            plateNodes[3].ConfigureNode(3, +1f, false, busPos);

            if (centralCurrentRod != null)
                centralCurrentRod.ActivateCurrent(false, true);
        }
        else if (phase == 2)
        {
            // Fase 2: Inversión de polaridad (Izquierda ⊖ -Q, Derecha ⊕ +Q)
            plateNodes[0].ConfigureNode(0, -1f, true, busPos);
            plateNodes[1].ConfigureNode(1, +1f, true, busPos);
            plateNodes[2].ConfigureNode(2, +1f, false, busPos);
            plateNodes[3].ConfigureNode(3, -1f, false, busPos);

            if (centralCurrentRod != null)
                centralCurrentRod.ActivateCurrent(false, false);
        }
        else
        {
            // Fase 3: Circuito RC en Paralelo (4 terminales: 2 ⊕ y 2 ⊖) + Corriente I activa
            plateNodes[0].ConfigureNode(0, +1f, true, busPos);
            plateNodes[1].ConfigureNode(1, -1f, true, busPos);
            plateNodes[2].ConfigureNode(2, -1f, true, busPos);
            plateNodes[3].ConfigureNode(3, +1f, true, busPos);

            if (centralCurrentRod != null)
            {
                centralCurrentRod.ActivateCurrent(true, true);
            }
        }

        UpdatePhaseBannerText();
    }

    private void UpdatePhaseBannerText()
    {
        int chargedCount = 0;
        int totalActive = 0;
        for (int i = 0; i < plateNodes.Count; i++)
        {
            if (plateNodes[i] != null && plateNodes[i].isActiveInCurrentPhase)
            {
                totalActive++;
                if (plateNodes[i].IsCharged) chargedCount++;
            }
        }

        if (overloadStunTimer > 0f)
        {
            statusBannerText = "¡DESCARGA RC ACTIVA! POLAROUS en cortocircuito — ¡Golpéalo con [Z] o repele cargas!";
            return;
        }

        if (currentPhase == 1)
        {
            statusBannerText = $"FASE 1 (Capacitor Simple): Carga las placas ⊕ y ⊖ ({chargedCount}/{totalActive}) o repele proyectiles con igual carga";
        }
        else if (currentPhase == 2)
        {
            statusBannerText = $"FASE 2 (Campo Eléctrico E + Polaridad Invertida): Carga ⊖ y ⊕ ({chargedCount}/{totalActive}) o usa la esfera móvil";
        }
        else
        {
            statusBannerText = $"FASE 3 (Circuito RC + Fuerza Magnética Fm): Carga las 4 placas ({chargedCount}/{totalActive}) para la descarga final";
        }
    }

    private void HandlePlateNodeCharged(CapacitorPlateNode chargedNode)
    {
        int chargedCount = 0;
        int totalActive = 0;

        for (int i = 0; i < plateNodes.Count; i++)
        {
            if (plateNodes[i] != null && plateNodes[i].isActiveInCurrentPhase)
            {
                totalActive++;
                if (plateNodes[i].IsCharged) chargedCount++;
            }
        }

        if (chargedCount >= totalActive && totalActive > 0)
        {
            TriggerCapacitorDischargeOnBoss();
        }
        else
        {
            UpdatePhaseBannerText();
        }
    }

    private void TriggerCapacitorDischargeOnBoss()
    {
        // Activar la Barra de Corriente central (I = dQ/dt)
        if (centralCurrentRod != null)
        {
            centralCurrentRod.ActivateCurrent(true, currentPhase != 2);
        }

        // Mostrar el rayo de descarga RC desde el bus central hacia POLAROUS
        if (dischargeArcRenderer != null)
        {
            dischargeArcRenderer.enabled = true;
            dischargeArcRenderer.startColor = new Color(0.3f, 1f, 0.6f, 1f);
            dischargeArcRenderer.endColor = new Color(0.3f, 0.9f, 1f, 1f);
            arcDisplayTimer = 1.4f;
        }

        overloadStunTimer = 3.8f;

        // Descarga exponencial RC quita 3 de vida
        ApplyDamageToBoss(3);
    }

    public void TakeReflectedProjectileDamage(int amount)
    {
        ApplyDamageToBoss(amount);
    }

    public void TakeMeleeDamage(int amount)
    {
        if (meleeHitCooldown > 0f) return;
        meleeHitCooldown = 0.45f;

        // Golpear a POLAROUS con [Z] cuando está sobrecargado o cuando THEIA tiene carga opuesta (neutralización)
        bool hasOppositeCharge = playerCharge != null && (playerCharge.GetCharge() * bossCharge) < -0.15f;
        if (overloadStunTimer > 0f || hasOppositeCharge)
        {
            ApplyDamageToBoss(amount);
        }
        else
        {
            ApplyDamageToBoss(1);
        }
    }

    private void ApplyDamageToBoss(int dmg)
    {
        int prevPhase = currentPhase;
        currentHealth = Mathf.Max(0, currentHealth - dmg);

        if (currentHealth <= 0)
        {
            DefeatPolarous();
            return;
        }

        int newPhase = currentHealth > 6 ? 1 : (currentHealth > 3 ? 2 : 3);
        if (newPhase != prevPhase)
        {
            SetupPhaseCircuit(newPhase);
        }
        else
        {
            UpdatePhaseBannerText();
        }
    }

    private void DefeatPolarous()
    {
        int unlocked = PlayerPrefs.GetInt("UnlokedLevel", 1);
        if (unlocked < 4)
        {
            PlayerPrefs.SetInt("UnlokedLevel", 4);
            PlayerPrefs.Save();
        }

        SceneManager.LoadScene("WinScreen");
    }

    private void UpdateBossVisual()
    {
        if (sr == null) return;
        sr.color = bossCharge > 0f
            ? new Color(0.3f, 0.75f, 1f, 1f)
            : new Color(1f, 0.32f, 0.35f, 1f);
    }

    private void InitStyles()
    {
        if (bossTitleStyle != null) return;

        bossTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bossTitleStyle.normal.textColor = Color.white;

        bossSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bossSubStyle.normal.textColor = new Color(0.9f, 0.96f, 1f, 0.95f);
    }

    private void OnGUI()
    {
        if (GameEvents.IsCleanHudHidden) return;
        InitStyles();

        float barW = 420f;
        float barH = 12f;
        float bx = Screen.width / 2f - barW / 2f;
        float by = 8f;

        // Panel superior compacto del Boss
        GUI.color = new Color(0.04f, 0.06f, 0.14f, 0.82f);
        GUI.DrawTexture(new Rect(bx - 12f, by - 4f, barW + 24f, 52f), Texture2D.whiteTexture);

        string polText = overloadStunTimer > 0f
            ? "¡SOBRECARGADO / CORTOCIRCUITO RC!"
            : (bossCharge > 0 ? "POLARIDAD +Q (Azul)" : "POLARIDAD -Q (Roja)");

        GUI.color = overloadStunTimer > 0f
            ? new Color(1f, 0.9f, 0.25f)
            : (bossCharge > 0 ? new Color(0.4f, 0.82f, 1f) : new Color(1f, 0.42f, 0.42f));

        GUI.Label(new Rect(bx, by - 2f, barW, 18f),
            $"POLAROUS — Capacitor Viviente  [{polText}]  ({currentHealth}/{maxHealth} HP)", bossTitleStyle);

        // Barra de vida
        GUI.color = new Color(0.15f, 0.16f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(bx, by + 18f, barW, barH), Texture2D.whiteTexture);

        float hpRatio = Mathf.Clamp01((float)currentHealth / maxHealth);
        GUI.color = overloadStunTimer > 0f
            ? new Color(1f, 0.85f, 0.2f, 0.95f)
            : (bossCharge > 0 ? new Color(0.25f, 0.68f, 1f, 0.95f) : new Color(1f, 0.32f, 0.36f, 0.95f));
        GUI.DrawTexture(new Rect(bx, by + 18f, barW * hpRatio, barH), Texture2D.whiteTexture);

        // Divisiones de las 3 fases en la barra de vida
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(new Rect(bx + barW * 0.333f, by + 18f, 2f, barH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx + barW * 0.666f, by + 18f, 2f, barH), Texture2D.whiteTexture);

        GUI.color = new Color(0.88f, 0.96f, 1f, 0.95f);
        GUI.Label(new Rect(bx - 60f, by + 31f, barW + 120f, 18f), statusBannerText, bossSubStyle);
    }
}
