using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Objeto de carga estática/fuente electrostática (Ley de Coulomb).
/// Refactorizado con registro estático libre de basura, cero GetComponent/Find en Update(),
/// clamping de seguridad r_min y F_max en FixedUpdate(), y emisión en vivo a GameEvents.cs.
/// </summary>
public class ChargedObject : MonoBehaviour
{
    public static readonly List<ChargedObject> ActiveChargedObjects = new List<ChargedObject>(32);

    [Header("Parámetros Electrostáticos (Ley de Coulomb)")]
    [Tooltip("Carga eléctrica q_1 de esta fuente.")]
    [Range(-5f, 5f)]
    public float charge = 1f;

    [Tooltip("Radio máximo de alcance del campo eléctrico.")]
    [Range(1f, 30f)]
    public float effectRadius = 10f;

    [Tooltip("Constante electrostática efectiva k_e para la Ley de Coulomb.")]
    [Range(5f, 200f)]
    public float forceStrength = 50f;

    [Header("Clamping de Seguridad Física")]
    [Tooltip("Distancia mínima r_min para evitar división por cero o fuerzas infinitas.")]
    [SerializeField, Range(0.25f, 2.5f)]
    private float minSafeDistance = 0.7f;

    [Tooltip("Magnitud máxima de fuerza electrostática aplicable al Rigidbody2D.")]
    [SerializeField, Range(5f, 100f)]
    private float maxForceClamp = 30f;

    [Header("Propiedades del Material")]
    [Tooltip("Si es conductor, permite absorber/transferir carga con THEIA.")]
    public bool isConductor = true;

    [Tooltip("Si es fuente fija, mantiene su carga constante al absorber.")]
    public bool isFixedSource = true;

    [Header("Feedback Visual de Polaridad")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);

    [Header("Anillos Equipotenciales / Campo")]
    public bool showRings = true;
    [Range(1, 6)]
    public int ringCount = 3;

    // Referencias cacheadas (Cero GetComponent/Find en Update)
    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;
    private readonly List<LineRenderer> fieldRings = new List<LineRenderer>();

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (GetComponent<CoulombForce2D>() == null)
        {
            CoulombForce2D cf = gameObject.AddComponent<CoulombForce2D>();
            cf.CurrentCharge = charge;
        }
    }

    void OnEnable()
    {
        if (!ActiveChargedObjects.Contains(this))
            ActiveChargedObjects.Add(this);
    }

    void OnDisable()
    {
        ActiveChargedObjects.Remove(this);
    }

    void Start()
    {
        CachePlayerReferencesOnce();
        if (showRings) CreateFieldRings();
        UpdateVisual();
    }

    private void CachePlayerReferencesOnce()
    {
        player = Object.FindAnyObjectByType<ChargeResource>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }
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
        if (!showRings) return;

        float dist = player != null
            ? Vector2.Distance(transform.position, player.transform.position)
            : float.MaxValue;

        bool inRange = player != null && dist < effectRadius && !player.IsNeutral();

        for (int i = 0; i < fieldRings.Count; i++)
        {
            float pulse = Mathf.PingPong(Time.time * 0.8f + i * 0.5f, 1f);
            float alpha = inRange ? pulse * 0.6f : pulse * 0.25f;

            Color ringColor = charge > 0
                ? new Color(0.3f, 0.6f, 1f, alpha)
                : new Color(1f, 0.3f, 0.3f, alpha);

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
        if (player == null || playerRb == null) return;
        if (player.IsNeutral()) return;

        Vector2 sourcePos = transform.position;
        Vector2 targetPos = player.transform.position;
        Vector2 displacement = targetPos - sourcePos;
        float rawDist = displacement.magnitude;

        if (rawDist > effectRadius || rawDist < 0.05f) return;

        // Clamping de seguridad en distancia mínima r_min para evitar singularidad 1/r^2
        float clampedR = Mathf.Max(rawDist, minSafeDistance);
        Vector2 rHat = displacement / rawDist;

        float q1 = charge;
        float q2 = player.GetCharge();
        float chargeProduct = q1 * q2;

        // Modelo Matemático Oficial: Ley de Coulomb F_e = k_e * |q1 * q2| / r^2
        float rawCoulombMag = forceStrength * Mathf.Abs(chargeProduct) / (clampedR * clampedR);
        float forceMag = Mathf.Clamp(rawCoulombMag, 0.5f, maxForceClamp);

        // Si q1 * q2 > 0 -> Repulsión (+rHat saliente); si q1 * q2 < 0 -> Atracción (-rHat entrante)
        bool isAttractive = chargeProduct < 0f;
        Vector2 forceVector = isAttractive ? (-rHat * forceMag) : (rHat * forceMag);

        playerRb.AddForce(forceVector, ForceMode2D.Force);

        // Emitir telemetría en tiempo real al Bus de Eventos para el Debug Overlay (Tecla V)
        GameEvents.RaiseForceApplied(new CoulombForceData(
            sourcePos,
            targetPos,
            q1,
            q2,
            clampedR,
            forceStrength,
            forceVector,
            isAttractive
        ));
    }

    public void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }
}