using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Objeto dinámico con carga eléctrica que responde a la Ley de Coulomb y Fuerza de Lorentz (Magnética).
/// Refactorizado con registro estático libre de basura, cero GetComponent/Find en Update() y telemetría vía GameEvents.
/// </summary>
public class DynamicChargedObject : MonoBehaviour
{
    public static readonly List<DynamicChargedObject> ActiveDynamicObjects = new List<DynamicChargedObject>(16);

    [Header("Parámetros Electrostáticos (Ley de Coulomb)")]
    [Tooltip("Carga eléctrica q_1 almacenada en el objeto dinámico.")]
    [Range(-5f, 5f)]
    public float charge = 1f;

    [Tooltip("Radio máximo de interacción electrostática.")]
    [Range(1f, 30f)]
    public float effectRadius = 10f;

    [Tooltip("Constante electrostática efectiva k_e.")]
    [Range(5f, 200f)]
    public float forceStrength = 40f;

    [Tooltip("Velocidad lineal máxima permitida (Clamping de seguridad en Rigidbody2D).")]
    [Range(1f, 30f)]
    public float maxSpeed = 8f;

    [Header("Clamping de Seguridad Física")]
    [Tooltip("Distancia mínima r_min para evitar división por cero o fuerzas infinitas.")]
    [SerializeField, Range(0.25f, 2.5f)]
    private float minSafeDistance = 0.7f;

    [Tooltip("Magnitud máxima de fuerza de Coulomb por paso físico.")]
    [SerializeField, Range(5f, 100f)]
    private float maxForceClamp = 25f;

    [Header("Propiedades del Material")]
    public bool isConductor = true;

    [Header("Feedback Visual de Polaridad")]
    public Color positiveColor = new Color(0.3f, 0.8f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color neutralColor = new Color(0.8f, 0.8f, 0.8f);

    [Header("Anillos Equipotenciales")]
    public bool showRings = true;
    [Range(1, 5)]
    public int ringCount = 2;

    // Referencias cacheadas en Awake/Start (Cero GetComponent/Find en Update)
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private ChargeResource player;
    private Rigidbody2D playerRb;
    private readonly List<LineRenderer> fieldRings = new List<LineRenderer>();

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (!ActiveDynamicObjects.Contains(this))
            ActiveDynamicObjects.Add(this);
    }

    void OnDisable()
    {
        ActiveDynamicObjects.Remove(this);
    }

    void Start()
    {
        player = Object.FindAnyObjectByType<ChargeResource>();
        if (player != null)
            playerRb = player.GetComponent<Rigidbody2D>();

        if (showRings) CreateFieldRings();
        UpdateVisual();
    }

    void CreateFieldRings()
    {
        float scaleX = transform.localScale.x * 0.5f;
        float scaleY = transform.localScale.y * 0.5f;

        for (int i = 0; i < ringCount; i++)
        {
            GameObject ringObj = new GameObject($"FieldRing_{i}");
            ringObj.transform.parent = transform;
            ringObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = 0.04f;
            lr.endWidth = 0.04f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 1;

            int segments = 32;
            lr.positionCount = segments;

            float offsetX = scaleX + 0.3f + i * 0.4f;
            float offsetY = scaleY + 0.3f + i * 0.4f;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f;
                lr.SetPosition(j, transform.position + new Vector3(
                    Mathf.Cos(angle) * offsetX,
                    Mathf.Sin(angle) * offsetY,
                    0f));
            }

            Color c = new Color(0.3f, 0.6f, 1f, 0f);
            lr.startColor = c;
            lr.endColor = c;
            fieldRings.Add(lr);
        }
    }

    void Update()
    {
        UpdateRings();
    }

    void UpdateRings()
    {
        if (!showRings || Mathf.Abs(charge) < 0.05f) return;

        for (int i = 0; i < fieldRings.Count; i++)
        {
            float pulse = Mathf.PingPong(Time.time * 1f + i * 0.4f, 1f);
            float alpha = pulse * 0.5f;

            Color ringColor = charge > 0.1f ?
                new Color(0.3f, 0.6f, 1f, alpha) :
                charge < -0.1f ?
                new Color(1f, 0.3f, 0.3f, alpha) :
                new Color(0.5f, 0.5f, 0.5f, 0f);

            fieldRings[i].startColor = ringColor;
            fieldRings[i].endColor = ringColor;

            int segments = fieldRings[i].positionCount;
            float offsetX = transform.localScale.x * 0.5f + 0.3f + i * 0.4f;
            float offsetY = transform.localScale.y * 0.5f + 0.3f + i * 0.4f;
            float rotOffset = Time.time * (charge > 0 ? 0.3f : -0.3f) + i * 0.5f;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f + rotOffset;
                fieldRings[i].SetPosition(j,
                    transform.position + new Vector3(
                        Mathf.Cos(angle) * offsetX,
                        Mathf.Sin(angle) * offsetY,
                        0f));
            }
        }
    }

    void FixedUpdate()
    {
        if (player == null || rb == null) return;
        if (player.IsNeutral() || Mathf.Abs(charge) < 0.05f) return;

        Vector2 myPos = transform.position;
        Vector2 playerPos = player.transform.position;
        Vector2 displacementToPlayer = playerPos - myPos;
        float rawDist = displacementToPlayer.magnitude;

        if (rawDist > effectRadius || rawDist < 0.05f) return;

        // Clamping de seguridad en distancia mínima r_min
        float clampedR = Mathf.Max(rawDist, minSafeDistance);
        Vector2 dirToPlayer = displacementToPlayer / rawDist;

        float q1 = charge;
        float q2 = player.GetCharge();
        float chargeProduct = q1 * q2;

        // Ley de Coulomb: F = k_e * |q1 * q2| / r^2
        float rawCoulombMag = forceStrength * Mathf.Abs(chargeProduct) / (clampedR * clampedR);
        float forceMag = Mathf.Clamp(rawCoulombMag, 0.5f, maxForceClamp);

        bool isAttractive = chargeProduct < 0f;
        Vector2 forceOnDynamicObj = isAttractive ? (dirToPlayer * forceMag) : (-dirToPlayer * forceMag);

        rb.AddForce(forceOnDynamicObj, ForceMode2D.Force);

        // Componente Magnética (Lorentz inducida por movimiento relativo de carga)
        if (playerRb != null && playerRb.linearVelocity.sqrMagnitude > 0.25f)
        {
            Vector2 playerVel = playerRb.linearVelocity;
            Vector2 perp = new Vector2(-playerVel.normalized.y, playerVel.normalized.x);
            float magForce = q2 * q1 * playerVel.magnitude * 1.5f;
            rb.AddForce(perp * magForce, ForceMode2D.Force);
        }

        // Clamping de velocidad máxima en Rigidbody2D
        if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        // Emitir telemetría en vivo a la Capa 2 (DebugOverlayUI)
        GameEvents.RaiseForceApplied(new CoulombForceData(
            myPos,
            playerPos,
            q1,
            q2,
            clampedR,
            forceStrength,
            -forceOnDynamicObj, // Fuerza recíproca (3ra Ley de Newton) sobre THEIA
            isAttractive
        ));
    }

    public void UpdateVisual()
    {
        if (sr == null) return;
        sr.color = charge > 0.1f ? positiveColor :
                   charge < -0.1f ? negativeColor : neutralColor;
    }
}