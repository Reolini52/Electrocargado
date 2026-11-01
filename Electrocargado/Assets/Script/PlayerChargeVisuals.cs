using UnityEngine;

/// <summary>
/// Fase 3 — Componente visual modular de polaridad para THEIA (cabello, aura, cuerpo y partículas alrededor del Player).
///
/// CARACTERÍSTICAS TÉCNICAS:
///  • Auto-configuración completa: si el prefab Player no tiene un ParticleSystem hijo asignado,
///    lo construye automáticamente alrededor de THEIA con una textura circular suave procedural
///    y material Sprites/Default compatible con URP 2D (cero cuadros rosas).
///  • Coordina tanto con <see cref="ChargeResource"/> como con <see cref="CargaResidual"/> para que
///    ninguno sobrescriba el color del SpriteRenderer de THEIA.
///  • Aplica el color en <see cref="LateUpdate"/> (después del Animator) y genera cero basura (GC-free) en cada frame.
/// </summary>
[DisallowMultipleComponent]
public class PlayerChargeVisuals : MonoBehaviour
{
    #region 1. Referencias y Parámetros Expuestos

    [Header("Referencias Principales")]
    [Tooltip("SpriteRenderer a colorear (cuerpo de THEIA o un objeto hijo para su cabello/aura).")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Tooltip("Componente ChargeResource del jugador. Si se deja vacío se busca automáticamente en Awake.")]
    [SerializeField] private ChargeResource chargeResource;

    [Header("Paleta de Polaridad Electromagnética")]
    [Tooltip("Color objetivo cuando la carga es positiva (+Q).")]
    [SerializeField] private Color positiveColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("Color objetivo cuando la carga es neutra (0).")]
    [SerializeField] private Color neutralColor = Color.white;

    [Tooltip("Color objetivo cuando la carga es negativa (-Q).")]
    [SerializeField] private Color negativeColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Tooltip("Velocidad de interpolación (Color.Lerp) hacia el color objetivo.")]
    [SerializeField, Range(0.5f, 25f)] private float colorTransitionSpeed = 8f;

    [Tooltip("Umbral mínimo |q| por debajo del cual se considera carga neutra (0).")]
    [SerializeField, Range(0.001f, 0.2f)] private float neutralDeadzone = 0.05f;

    #endregion

    #region 2. Partículas Alrededor del Player y Capas Adicionales

    [Header("Partículas de Chispas Electrostáticas Alrededor de THEIA")]
    [Tooltip("ParticleSystem para emitir chispas alrededor de THEIA. Si está vacío, se crea automáticamente en el Player.")]
    [SerializeField] private ParticleSystem chargeSparksParticles;

    [Tooltip("Crear automáticamente el sistema de partículas alrededor del Player si no hay ninguno asignado.")]
    [SerializeField] private bool autoCreateParticlesIfMissing = true;

    [Tooltip("Emitir partículas sutiles incluso cuando THEIA está neutra (para ver siempre el aura de partículas).")]
    [SerializeField] private bool emitSubtleIdleParticlesWhenNeutral = true;

    [Tooltip("Umbral de magnitud de carga |q| ∈ [0, 1] a partir del cual se activan las chispas intensas de carga.")]
    [SerializeField, Range(0.02f, 0.95f)] private float sparkChargeThreshold = 0.1f;

    [Tooltip("Tasa máxima de emisión (partículas/segundo) cuando THEIA alcanza carga máxima (|q| = 1).")]
    [SerializeField, Range(5f, 100f)] private float maxSparkEmissionRate = 38f;

    [Tooltip("Radio del anillo de emisión alrededor del cuerpo de THEIA.")]
    [SerializeField, Range(0.15f, 1.5f)] private float auraRadius = 0.48f;

    [Header("Capas Adicionales Opcionales (Cabello / Aura / Brillo)")]
    [Tooltip("Renderers extra opcionales (ej. si quieres teñir simultáneamente cabello + aura).")]
    [SerializeField] private SpriteRenderer[] additionalRenderers;

    #endregion

    #region 3. Estado Interno Cacheado (Cero GC en Update / LateUpdate)

    private Color currentColor = Color.white;
    private Color targetColor = Color.white;
    private float cachedNormalizedCharge = -999f;

    private bool hasParticleSystem = false;
    private ParticleSystem.MainModule sparksMainModule;
    private ParticleSystem.EmissionModule sparksEmissionModule;
    private ParticleSystem.MinMaxCurve emissionRateCurve;
    private ParticleSystem.MinMaxGradient sparkColorGradient;

    // Textura y material estáticos compartidos para partículas 2D (cero fugas de memoria y compatible con URP 2D)
    private static Texture2D sharedSoftSparkTexture;
    private static Material sharedParticleMaterial;

    #endregion

    #region 4. Ciclo de Vida y Auto-Construcción de Partículas

    private void Awake()
    {
        if (chargeResource == null)
        {
            chargeResource = GetComponent<ChargeResource>();
            if (chargeResource == null)
                chargeResource = GetComponentInParent<ChargeResource>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Buscar si ya existe un hijo "ChargeSparksAura" en el prefab o crearlo automáticamente
        if (chargeSparksParticles == null)
        {
            Transform existingAura = transform.Find("ChargeSparksAura");
            if (existingAura != null)
                chargeSparksParticles = existingAura.GetComponent<ParticleSystem>();

            if (chargeSparksParticles == null && autoCreateParticlesIfMissing)
                chargeSparksParticles = CreateProceduralSparksAroundPlayer();
        }

        if (chargeSparksParticles != null)
        {
            ConfigureParticleSystemModules(chargeSparksParticles);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnChargeChanged += HandleChargeChangedEvent;

        if (chargeResource != null)
        {
            chargeResource.overrideBuiltInVisuals = true;
        }
    }

    private void OnDisable()
    {
        GameEvents.OnChargeChanged -= HandleChargeChangedEvent;

        if (chargeResource != null)
        {
            chargeResource.overrideBuiltInVisuals = false;
        }
    }

    private void Start()
    {
        float initialCharge = chargeResource != null ? chargeResource.GetCharge() : 0f;
        cachedNormalizedCharge = initialCharge;
        EvaluateTargetColorAndSparks(initialCharge);

        currentColor = targetColor;
        if (targetRenderer != null)
            targetRenderer.color = currentColor;

        ApplyToAdditionalRenderers(currentColor);
    }

    private void HandleChargeChangedEvent(float currentCharge, float maxCharge, float normalizedRatio)
    {
        // Preferimos leer directamente de chargeResource para evitar que CargaResidual pise el valor
        float effectiveCharge = chargeResource != null ? chargeResource.GetCharge() : normalizedRatio;
        cachedNormalizedCharge = effectiveCharge;
        EvaluateTargetColorAndSparks(effectiveCharge);
    }

    /// <summary>
    /// Se ejecuta en LateUpdate() (después del Animator) para que los clips de animación nunca pisen el color.
    /// </summary>
    private void LateUpdate()
    {
        if (chargeResource != null)
        {
            // Asegurar que la bandera siga activa
            chargeResource.overrideBuiltInVisuals = true;

            float liveCharge = chargeResource.GetCharge();
            if (Mathf.Abs(liveCharge - cachedNormalizedCharge) > 0.001f)
            {
                cachedNormalizedCharge = liveCharge;
                EvaluateTargetColorAndSparks(liveCharge);
            }
        }

        if (targetRenderer == null) return;

        targetRenderer.color = Color.Lerp(
            targetRenderer.color,
            targetColor,
            Time.deltaTime * colorTransitionSpeed
        );

        currentColor = targetRenderer.color;
        ApplyToAdditionalRenderers(currentColor);
    }

    #endregion

    #region 5. Cálculo de Color y Emisión de Partículas (GC-Free)

    private void EvaluateTargetColorAndSparks(float chargeValue)
    {
        float absIntensity = Mathf.Clamp01(Mathf.Abs(chargeValue));

        if (chargeValue > neutralDeadzone)
        {
            targetColor = Color.Lerp(neutralColor, positiveColor, absIntensity);
        }
        else if (chargeValue < -neutralDeadzone)
        {
            targetColor = Color.Lerp(neutralColor, negativeColor, absIntensity);
        }
        else
        {
            targetColor = neutralColor;
        }

        UpdateSparksParticles(chargeValue, absIntensity);
    }

    private void UpdateSparksParticles(float chargeValue, float absIntensity)
    {
        if (!hasParticleSystem || chargeSparksParticles == null) return;

        if (absIntensity >= sparkChargeThreshold)
        {
            float t = Mathf.InverseLerp(sparkChargeThreshold, 1f, absIntensity);
            emissionRateCurve.constant = Mathf.Lerp(10f, maxSparkEmissionRate, t);
            sparksEmissionModule.rateOverTime = emissionRateCurve;

            Color sparkColor = chargeValue > 0f ? positiveColor : negativeColor;
            sparkColor.a = 0.92f;
            sparkColorGradient.color = sparkColor;
            sparksMainModule.startColor = sparkColorGradient;
            sparksMainModule.startSpeed = Mathf.Lerp(0.6f, 1.8f, t);
            sparksMainModule.startSize = Mathf.Lerp(0.09f, 0.17f, t);

            if (!chargeSparksParticles.isPlaying)
                chargeSparksParticles.Play();
        }
        else if (emitSubtleIdleParticlesWhenNeutral)
        {
            // Emisión sutil en estado neutro para que siempre se perciba el aura alrededor del Player
            emissionRateCurve.constant = 5f;
            sparksEmissionModule.rateOverTime = emissionRateCurve;

            Color idleColor = new Color(neutralColor.r, neutralColor.g, neutralColor.b, 0.45f);
            sparkColorGradient.color = idleColor;
            sparksMainModule.startColor = sparkColorGradient;
            sparksMainModule.startSpeed = 0.35f;
            sparksMainModule.startSize = 0.07f;

            if (!chargeSparksParticles.isPlaying)
                chargeSparksParticles.Play();
        }
        else
        {
            emissionRateCurve.constant = 0f;
            sparksEmissionModule.rateOverTime = emissionRateCurve;

            if (chargeSparksParticles.isPlaying && chargeSparksParticles.particleCount == 0)
                chargeSparksParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void ApplyToAdditionalRenderers(Color c)
    {
        if (additionalRenderers == null) return;

        int count = additionalRenderers.Length;
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = additionalRenderers[i];
            if (sr != null)
            {
                Color copy = c;
                copy.a = sr.color.a;
                sr.color = copy;
            }
        }
    }

    #endregion

    #region 6. Generador Procedural del ParticleSystem 2D Alrededor del Player

    private ParticleSystem CreateProceduralSparksAroundPlayer()
    {
        GameObject sparksObj = new GameObject("ChargeSparksAura");
        sparksObj.transform.SetParent(transform, false);
        sparksObj.transform.localPosition = Vector3.zero;
        sparksObj.layer = gameObject.layer;

        ParticleSystem ps = sparksObj.AddComponent<ParticleSystem>();
        return ps;
    }

    private void ConfigureParticleSystemModules(ParticleSystem ps)
    {
        hasParticleSystem = true;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        sparksMainModule = ps.main;
        sparksMainModule.loop = true;
        sparksMainModule.playOnAwake = true;
        sparksMainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        sparksMainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        sparksMainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        sparksMainModule.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        sparksMainModule.gravityModifier = -0.08f; // Flotan ligeramente hacia arriba como chispas de plasma
        sparksMainModule.maxParticles = 120;

        sparksEmissionModule = ps.emission;
        sparksEmissionModule.enabled = true;
        emissionRateCurve = new ParticleSystem.MinMaxCurve(6f);
        sparksEmissionModule.rateOverTime = emissionRateCurve;

        // Forma circular alrededor del cuerpo de THEIA
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = auraRadius;
        shape.radiusThickness = 0.65f; // Emitir principalmente en el borde alrededor de THEIA

        // Desvanecimiento suave de tamaño a lo largo de la vida de cada chispa
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Renderer 2D compatible con URP 2D (evita el cuadro magenta por falta de material)
        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.sharedMaterial = GetOrCreateSoftSparkMaterial();
            if (targetRenderer != null)
            {
                psRenderer.sortingLayerID = targetRenderer.sortingLayerID;
                psRenderer.sortingOrder = targetRenderer.sortingOrder + 1;
            }
            else
            {
                psRenderer.sortingOrder = 5;
            }
        }

        sparkColorGradient = new ParticleSystem.MinMaxGradient(neutralColor);
        ps.Play();
    }

    private static Material GetOrCreateSoftSparkMaterial()
    {
        if (sharedParticleMaterial != null) return sharedParticleMaterial;

        if (sharedSoftSparkTexture == null)
        {
            const int size = 32;
            sharedSoftSparkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralSparkGlowTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha; // Brillo radial suave
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedSoftSparkTexture.SetPixels(pixels);
            sharedSoftSparkTexture.Apply(false, false);
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        sharedParticleMaterial = new Material(shader)
        {
            name = "ProceduralSparkGlowMat",
            mainTexture = sharedSoftSparkTexture
        };

        return sharedParticleMaterial;
    }

    #endregion
}
