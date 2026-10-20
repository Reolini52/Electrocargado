using UnityEngine;

/// <summary>
/// Gestiona la capacitancia, acumulación y disipación exponencial RC de THEIA:
/// Q(t) = Q_0 * e^(-t / (R * C))
/// Cumple estrictamente con cero GetComponent en Update() y comunicación vía GameEvents.
/// </summary>
[DisallowMultipleComponent]
public class CargaResidual : MonoBehaviour
{
    #region Parámetros del Modelo Físico RC

    [Header("Modelo de Capacitancia de THEIA (Circuito RC)")]
    [Tooltip("Carga eléctrica máxima que puede almacenar THEIA (Q_max en Coulombs escalados).")]
    [SerializeField, Range(0.5f, 10f)]
    private float maxChargeCapacity = 1.0f;

    [Tooltip("Carga eléctrica actual Q(t) almacenada en THEIA.")]
    [SerializeField, Range(-10f, 10f)]
    private float currentCharge = 0f;

    [Tooltip("Resistencia eléctrica equivalente (R en Ohmios escalados) durante contacto con suelo conductor.")]
    [SerializeField, Range(0.1f, 50f)]
    private float groundResistanceR = 2.5f;

    [Tooltip("Capacitancia intrínseca de THEIA (C en Faradios escalados). Tau = R * C.")]
    [SerializeField, Range(0.1f, 10f)]
    private float capacitanceC = 1.2f;

    [Tooltip("Resistencia de fuga ambiental en el aire (R_air >> R_ground) cuando no toca conductores.")]
    [SerializeField, Range(5f, 200f)]
    private float airLeakageResistance = 45f;

    [Tooltip("Umbral por debajo del cual la carga se considera neutralizada (|Q| <= umbral).")]
    [SerializeField, Range(0.01f, 0.2f)]
    private float neutralThreshold = 0.05f;

    #endregion

    #region Detección de Tierra / Superficie Conductora

    [Header("Contacto con Tierra / Conductores")]
    [Tooltip("Punto de chequeo inferior para detectar contacto con suelo conductor.")]
    [SerializeField]
    private Transform groundCheckPoint;

    [Tooltip("Radio de detección de contacto con superficies conductoras.")]
    [SerializeField, Range(0.05f, 1f)]
    private float contactRadius = 0.22f;

    [Tooltip("Capas consideradas como superficies conductoras (Tierra) que activan la descarga RC rápida.")]
    [SerializeField]
    private LayerMask conductorGroundMask = ~0;

    #endregion

    #region Efectos Físicos e Inerciales en THEIA

    [Header("Acoplamiento Carga-Masa (Agilidad de THEIA)")]
    [Tooltip("Masa base del Rigidbody2D cuando THEIA está descargada (neutra).")]
    [SerializeField, Range(0.5f, 5f)]
    private float baseMass = 1.0f;

    [Tooltip("Variación adicional de masa a carga máxima (simula inercia electrostática / sobrecarga).")]
    [SerializeField, Range(-0.5f, 2f)]
    private float fullChargeMassDelta = 0.25f;

    [Header("Feedback Visual de Polaridad")]
    [Tooltip("Color representativo de carga positiva (+Q).")]
    [SerializeField]
    private Color positivePolarityColor = new Color(0.25f, 0.65f, 1f, 1f);

    [Tooltip("Color representativo de carga negativa (-Q).")]
    [SerializeField]
    private Color negativePolarityColor = new Color(1f, 0.28f, 0.32f, 1f);

    [Tooltip("Color en estado neutro (Q ≈ 0).")]
    [SerializeField]
    private Color neutralColor = new Color(0.85f, 0.85f, 0.9f, 1f);

    #endregion

    #region Estado Interno y Referencias Cacheadas (Cero GetComponent en Update)

    private Rigidbody2D cachedRb;
    private SpriteRenderer cachedSpriteRenderer;
    private ChargeResource legacyChargeResource;

    // Variables del proceso transitorio exponencial Q(t) = Q_0 * e^(-t / RC)
    private float initialDischargeChargeQ0;
    private float dischargeElapsedTime;
    private bool isContactingConductor;
    private bool wasContactingConductor;

    #endregion

    #region Propiedades Públicas de Lectura Científica

    /// <summary>Carga instantánea actual Q(t).</summary>
    public float CurrentCharge => currentCharge;

    /// <summary>Capacidad máxima de carga Q_max.</summary>
    public float MaxChargeCapacity => maxChargeCapacity;

    /// <summary>Carga inicial Q_0 registrada al inicio del tramo actual de descarga exponencial.</summary>
    public float InitialDischargeChargeQ0 => initialDischargeChargeQ0;

    /// <summary>Tiempo transcurrido t (s) desde que inició el régimen actual de descarga RC.</summary>
    public float DischargeElapsedTime => dischargeElapsedTime;

    /// <summary>Constante de tiempo activa tau = R * C (en segundos).</summary>
    public float ActiveTimeConstantTau => (isContactingConductor ? groundResistanceR : airLeakageResistance) * capacitanceC;

    /// <summary>Indica si THEIA se encuentra en estado eléctricamente neutro.</summary>
    public bool IsNeutral => Mathf.Abs(currentCharge) <= neutralThreshold;

    /// <summary>Indica si THEIA tiene carga neta positiva (+Q).</summary>
    public bool IsPositive => currentCharge > neutralThreshold;

    /// <summary>Indica si THEIA tiene carga neta negativa (-Q).</summary>
    public bool IsNegative => currentCharge < -neutralThreshold;

    /// <summary>Indica si THEIA está haciendo contacto físico con tierra o material conductor.</summary>
    public bool IsContactingConductor => isContactingConductor;

    #endregion

    #region Ciclo de Vida de Unity

    private void Awake()
    {
        cachedRb = GetComponent<Rigidbody2D>();
        cachedSpriteRenderer = GetComponent<SpriteRenderer>();
        legacyChargeResource = GetComponent<ChargeResource>();

        if (groundCheckPoint == null)
        {
            Transform foundCheck = transform.Find("groundcheker");
            if (foundCheck != null)
            {
                groundCheckPoint = foundCheck;
            }
        }

        initialDischargeChargeQ0 = currentCharge;
        dischargeElapsedTime = 0f;
    }

    private void Start()
    {
        SyncMassAndVisuals();
        GameEvents.RaiseChargeChanged(currentCharge, maxChargeCapacity);
    }

    private void Update()
    {
        // Timers y evolución analítica exponencial RC en Update()
        UpdateExponentialRCDischarge(Time.deltaTime);
        UpdateVisualFeedback();
    }

    private void FixedUpdate()
    {
        // Chequeo físico de contacto con tierra/conductores en FixedUpdate()
        CheckConductorGroundContact();
    }

    #endregion

    #region Modelo Matemático de Descarga RC: Q(t) = Q_0 * e^(-t / RC)

    private void CheckConductorGroundContact()
    {
        Vector2 checkPos = groundCheckPoint != null
            ? (Vector2)groundCheckPoint.position
            : (Vector2)transform.position + Vector2.down * 0.5f;

        Collider2D hit = Physics2D.OverlapCircle(checkPos, contactRadius, conductorGroundMask);
        isContactingConductor = hit != null && hit.gameObject != gameObject;

        if (isContactingConductor != wasContactingConductor)
        {
            // Al cambiar el régimen (Aire <-> Conductor), re-anclamos Q_0 y t = 0
            // para garantizar continuidad analítica en la curva exponencial Q(t) = Q_0 * e^(-t / tau).
            ResetExponentialReferenceState();
            wasContactingConductor = isContactingConductor;
            GameEvents.RaiseConductorContactChanged(isContactingConductor);
        }
    }

    private void UpdateExponentialRCDischarge(float deltaTime)
    {
        // Si el escudo/burbuja del script legacy está activo, respetamos su retención
        if (legacyChargeResource != null && legacyChargeResource.IsBubbleActive())
        {
            currentCharge = legacyChargeResource.GetCharge();
            ResetExponentialReferenceState();
            return;
        }

        if (Mathf.Abs(currentCharge) <= 0.0001f)
        {
            if (currentCharge != 0f)
            {
                currentCharge = 0f;
                SyncLegacyChargeComponent();
                GameEvents.RaiseChargeChanged(currentCharge, maxChargeCapacity);
            }
            return;
        }

        dischargeElapsedTime += deltaTime;

        float tau = ActiveTimeConstantTau;
        if (tau <= 0.0001f) tau = 0.0001f;

        // Ecuación exacta del circuito RC: Q(t) = Q_0 * e^(-t / (R * C))
        float decayFactor = Mathf.Exp(-dischargeElapsedTime / tau);
        currentCharge = initialDischargeChargeQ0 * decayFactor;

        if (Mathf.Abs(currentCharge) < neutralThreshold * 0.25f)
        {
            currentCharge = 0f;
            initialDischargeChargeQ0 = 0f;
            dischargeElapsedTime = 0f;
        }

        SyncMassAndVisuals();
        SyncLegacyChargeComponent();

        // Notificar al bus de eventos desacoplado (Capa 1 y Capa 2)
        GameEvents.RaiseChargeChanged(currentCharge, maxChargeCapacity);
        GameEvents.RaiseRCDischargeStep(currentCharge, initialDischargeChargeQ0, dischargeElapsedTime, tau);
    }

    /// <summary>
    /// Fija la condición de frontera inicial Q_0 = Q_actual y reinicia el tiempo t = 0
    /// tras cualquier inyección/expulsión de carga o cambio de resistencia R.
    /// </summary>
    private void ResetExponentialReferenceState()
    {
        initialDischargeChargeQ0 = currentCharge;
        dischargeElapsedTime = 0f;
    }

    #endregion

    #region API Pública para Manipulación de Carga (Bending / Absorber / Expulsar)

    /// <summary>
    /// Inyecta o sustrae una cantidad de carga (\Delta Q) al capacitor de THEIA.
    /// </summary>
    /// <param name="deltaCharge">Cantidad de carga a sumar (positiva o negativa).</param>
    public void AddCharge(float deltaCharge)
    {
        currentCharge = Mathf.Clamp(currentCharge + deltaCharge, -maxChargeCapacity, maxChargeCapacity);
        ResetExponentialReferenceState();
        SyncMassAndVisuals();
        SyncLegacyChargeComponent();

        GameEvents.RaiseChargeChanged(currentCharge, maxChargeCapacity);
    }

    /// <summary>
    /// Establece instantáneamente la carga de THEIA a un valor específico.
    /// </summary>
    public void SetCharge(float newCharge)
    {
        currentCharge = Mathf.Clamp(newCharge, -maxChargeCapacity, maxChargeCapacity);
        ResetExponentialReferenceState();
        SyncMassAndVisuals();
        SyncLegacyChargeComponent();

        GameEvents.RaiseChargeChanged(currentCharge, maxChargeCapacity);
    }

    #endregion

    #region Sincronización Física y Visual

    private void SyncMassAndVisuals()
    {
        if (cachedRb != null)
        {
            float normalizedChargeMagnitude = Mathf.Clamp01(Mathf.Abs(currentCharge) / maxChargeCapacity);
            cachedRb.mass = Mathf.Max(0.1f, baseMass + fullChargeMassDelta * normalizedChargeMagnitude);
        }
    }

    private void UpdateVisualFeedback()
    {
        if (cachedSpriteRenderer == null) return;

        float normalizedMagnitude = Mathf.Clamp01(Mathf.Abs(currentCharge) / maxChargeCapacity);

        if (IsPositive)
        {
            cachedSpriteRenderer.color = Color.Lerp(neutralColor, positivePolarityColor, normalizedMagnitude);
        }
        else if (IsNegative)
        {
            cachedSpriteRenderer.color = Color.Lerp(neutralColor, negativePolarityColor, normalizedMagnitude);
        }
        else
        {
            cachedSpriteRenderer.color = neutralColor;
        }
    }

    private void SyncLegacyChargeComponent()
    {
        if (legacyChargeResource != null)
        {
            legacyChargeResource.charge = Mathf.Clamp(currentCharge / maxChargeCapacity, -1f, 1f);
        }
    }

    #endregion

    #region Gizmos de Depuración Científica

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPos = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.5f;

        Gizmos.color = isContactingConductor ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(checkPos, contactRadius);
    }

    #endregion
}
