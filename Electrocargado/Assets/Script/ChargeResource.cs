using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona el recurso de carga de THEIA con modelo matemático de Descarga Exponencial RC:
/// Q(t) = Q_0 * e^(-t / (R * C))
/// Incluye Feedback Visual Dinámico ("Show, Don't Tell") mediante interpolación suave de color
/// en el SpriteRenderer del personaje y soporte para Teclado + Gamepad.
/// </summary>
public class ChargeResource : MonoBehaviour
{
    [Header("Carga y Modelo Exponencial RC")]
    [Tooltip("Carga eléctrica instantánea Q(t) normalizada en [-1, 1].")]
    [Range(-1f, 1f)]
    public float charge = 0f;

    [Tooltip("Tasa base de disipación (usada para escalar la constante de tiempo RC en suelo conductor).")]
    [Range(0.001f, 0.5f)]
    public float dissipationRate = 0.01f;

    [Tooltip("Resistencia equivalente R en contacto con suelo conductor (Ohmios escalados).")]
    [SerializeField, Range(0.5f, 50f)]
    private float groundResistanceR = 8.0f;

    [Tooltip("Resistencia equivalente R en el aire (aislante relativo, descarga más lenta).")]
    [SerializeField, Range(5f, 200f)]
    private float airResistanceR = 35.0f;

    [Tooltip("Capacitancia intrínseca C de THEIA (Faradios escalados). Tau = R * C.")]
    [SerializeField, Range(0.2f, 10f)]
    private float capacitanceC = 1.5f;

    [Header("Escudo Dieléctrico (Bubble)")]
    [Tooltip("Indica si el aislamiento dieléctrico (Shift / LB en Gamepad) está activo.")]
    public bool bubbleActive = false;

    [Tooltip("Costo de carga por segundo al mantener la burbuja activa.")]
    [Range(0.01f, 1f)]
    public float bubbleChargeCost = 0.2f;

    [Header("Penalización por Descarga Total")]
    [Tooltip("Tiempo de gracia antes de recibir daño por estar completamente descargado (neutro).")]
    [Range(0.5f, 10f)]
    public float graceTime = 3f;

    [Tooltip("Intervalo en segundos entre pulsos de daño por neutralidad.")]
    [Range(0.5f, 10f)]
    public float dischargeDamageInterval = 2f;

    [Header("Feedback Visual de Carga (Show, Don't Tell)")]
    [Tooltip("Referencia al SpriteRenderer de THEIA. Si se deja vacío se asigna automáticamente en Awake().")]
    [SerializeField]
    private SpriteRenderer playerSpriteRenderer;

    [Tooltip("Color objetivo para Carga Positiva (+Q): Tinte Rojo / Cálido.")]
    [SerializeField]
    public Color positiveColor = new Color(1f, 0.32f, 0.28f, 1f);

    [Tooltip("Color objetivo para Carga Negativa (-Q): Tinte Azul / Frío.")]
    [SerializeField]
    public Color negativeColor = new Color(0.25f, 0.65f, 1f, 1f);

    [Tooltip("Color objetivo para Carga Neutra (0): Color original (Blanco).")]
    [SerializeField]
    public Color neutralColor = Color.white;

    [Tooltip("Velocidad de transición suave (Color.Lerp) entre estados de carga.")]
    [SerializeField, Range(1f, 25f)]
    private float colorTransitionSpeed = 10f;

    // Referencias cacheadas en Awake/Start (Cero GetComponent en Update)
    private PlayerHealth playerHealth;
    private PlayerController playerController;

    // Estado interno del modelo exponencial Q(t) = Q_0 * e^(-t / RC)
    private float initialChargeQ0;
    private float elapsedTimeInRegime;
    private bool wasGroundedOnConductor;

    private float graceTimer = 0f;
    private float damageTimer = 0f;
    private bool wasNeutral = false;

    void Awake()
    {
        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
            if (playerSpriteRenderer == null)
                playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<PlayerController>();

        // Asegurar que CargaResidual y CoulombForce2D estén presentes en THEIA para telemetría completa
        if (GetComponent<CargaResidual>() == null)
        {
            gameObject.AddComponent<CargaResidual>();
        }
        if (GetComponent<CoulombForce2D>() == null)
        {
            gameObject.AddComponent<CoulombForce2D>();
        }
    }

    void Start()
    {
        graceTimer = graceTime;
        damageTimer = dischargeDamageInterval;
        AnchorExponentialCurve();
        UpdateVisual(instant: true);
        GameEvents.RaiseChargeChanged(charge, 1f);
    }

    void Update()
    {
        if (GandalfNPC.isGandalfActive) return;

        HandleBubble();
        DissipateChargeExponentialRC();
        HandleDischargePenalty();
        UpdateVisual(instant: false);
    }

    /// <summary>
    /// Fija la condición inicial Q_0 = charge y t = 0 cuando cambia el estado de carga o el contacto con tierra.
    /// </summary>
    private void AnchorExponentialCurve()
    {
        initialChargeQ0 = charge;
        elapsedTimeInRegime = 0f;
    }

    /// <summary>
    /// Disipación exponencial de circuito RC: Q(t) = Q_0 * e^(-t / (R * C))
    /// </summary>
    void DissipateChargeExponentialRC()
    {
        if (bubbleActive)
        {
            AnchorExponentialCurve();
            return;
        }

        bool isGrounded = playerController != null && playerController.IsGrounded();
        if (isGrounded != wasGroundedOnConductor)
        {
            AnchorExponentialCurve();
            wasGroundedOnConductor = isGrounded;
            GameEvents.RaiseConductorContactChanged(isGrounded);
        }

        if (Mathf.Abs(charge) <= 0.0001f)
        {
            charge = 0f;
            return;
        }

        elapsedTimeInRegime += Time.deltaTime;

        // Constante de tiempo efectiva tau = R * C modulada por dissipationRate configurado en el Inspector
        float baseR = isGrounded ? groundResistanceR : airResistanceR;
        float rateScale = Mathf.Max(0.001f, dissipationRate / 0.01f);
        float tauRC = Mathf.Max(0.05f, (baseR * capacitanceC) / rateScale);

        // Solución analítica exacta: Q(t) = Q_0 * e^(-t / tau)
        charge = initialChargeQ0 * Mathf.Exp(-elapsedTimeInRegime / tauRC);

        if (Mathf.Abs(charge) < 0.005f)
        {
            charge = 0f;
        }

        GameEvents.RaiseChargeChanged(charge, 1f);
        GameEvents.RaiseRCDischargeStep(charge, initialChargeQ0, elapsedTimeInRegime, tauRC);
    }

    void HandleBubble()
    {
        bool toggleBubblePressed =
            (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame);

        if (toggleBubblePressed)
        {
            if (!bubbleActive && Mathf.Abs(charge) > 0.1f)
                bubbleActive = true;
            else
                bubbleActive = false;
        }

        if (bubbleActive)
        {
            charge = Mathf.MoveTowards(charge, 0f, bubbleChargeCost * Time.deltaTime);
            AnchorExponentialCurve();
            GameEvents.RaiseChargeChanged(charge, 1f);

            if (Mathf.Abs(charge) < 0.05f)
                bubbleActive = false;
        }
    }

    void HandleDischargePenalty()
    {
        if (IsNeutral())
        {
            if (!wasNeutral)
            {
                graceTimer = graceTime;
                wasNeutral = true;
                damageTimer = dischargeDamageInterval;
            }

            if (graceTimer > 0f)
            {
                graceTimer -= Time.deltaTime;
                return;
            }

            damageTimer -= Time.deltaTime;
            if (damageTimer <= 0f)
            {
                playerHealth?.TakeDamage(1);
                damageTimer = dischargeDamageInterval;
            }
        }
        else
        {
            wasNeutral = false;
            graceTimer = graceTime;
            damageTimer = dischargeDamageInterval;
        }
    }

    /// <summary>
    /// Interpola dinámicamente el color del SpriteRenderer de THEIA según su carga:
    /// +Q -> Rojo/Cálido | -Q -> Azul/Frío | 0 -> Blanco original.
    /// </summary>
    void UpdateVisual(bool instant = false)
    {
        if (playerSpriteRenderer == null) return;

        Color targetColor;
        float normalizedIntensity = Mathf.Clamp01(Mathf.Abs(charge));

        if (IsPositive())
        {
            targetColor = Color.Lerp(neutralColor, positiveColor, normalizedIntensity);
        }
        else if (IsNegative())
        {
            targetColor = Color.Lerp(neutralColor, negativeColor, normalizedIntensity);
        }
        else
        {
            targetColor = neutralColor;
        }

        if (instant)
        {
            playerSpriteRenderer.color = targetColor;
        }
        else
        {
            playerSpriteRenderer.color = Color.Lerp(
                playerSpriteRenderer.color,
                targetColor,
                colorTransitionSpeed * Time.deltaTime
            );
        }
    }

    public float GetGraceTimer() => graceTimer;
    public bool IsInGrace() => wasNeutral && graceTimer > 0f;
    public float GetInitialChargeQ0() => initialChargeQ0;
    public float GetElapsedTimeRC() => elapsedTimeInRegime;
    public float GetActiveTauRC()
    {
        bool isGrounded = playerController != null && playerController.IsGrounded();
        float baseR = isGrounded ? groundResistanceR : airResistanceR;
        float rateScale = Mathf.Max(0.001f, dissipationRate / 0.01f);
        return Mathf.Max(0.05f, (baseR * capacitanceC) / rateScale);
    }

    public void AddCharge(float amount)
    {
        charge = Mathf.Clamp(charge + amount, -1f, 1f);
        AnchorExponentialCurve();
        GameEvents.RaiseChargeChanged(charge, 1f);
    }

    public float GetCharge() => charge;
    public bool IsPositive() => charge > 0.1f;
    public bool IsNegative() => charge < -0.1f;
    public bool IsNeutral() => Mathf.Abs(charge) <= 0.1f;
    public bool IsBubbleActive() => bubbleActive;
}
