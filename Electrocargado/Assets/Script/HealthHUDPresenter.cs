using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Fase 3 — Pulido UI. Capa de PRESENTACIÓN del HUD de vida en TextMeshPro (reemplazo visual de HealthUI/OnGUI).
///
/// NO modifica la lógica de PlayerHealth: solo LEE currentHealth / currentResets y reacciona a los cambios:
///   • Daño     → Screen Shake leve del panel (ruido Perlin con decaimiento) + "punch" de escala + flash de color.
///   • Curación → "pop" de escala suave.
///   • Vida = 1 → pulso de latido continuo (alerta de peligro).
///   • Resets   → pequeño punch en el contador.
///
/// Rendimiento: sin coroutines ni strings por frame. Los textos solo se reconstruyen (StringBuilder cacheado)
/// cuando cambia un valor. Las animaciones usan tiempo no escalado (funcionan durante la pausa del Firewall).
///
/// SETUP:
///   Canvas (Screen Space - Overlay, Canvas Scaler: Scale With Screen Size 1920x1080)
///   └─ HUD_Health (RectTransform + CanvasGroup + Image de fondo opcional) ← shakeTarget / canvasGroup
///       ├─ HeartsText  (TextMeshPro - Text UI)  ← heartsText
///       ├─ ResetsText  (TextMeshPro - Text UI)  ← resetsText
///       └─ HintText    (TextMeshPro - Text UI)  ← hintText (opcional)
///   Asigna este script a HUD_Health y arrastra el componente HealthUI antiguo a "legacyHealthUI" para apagarlo.
/// </summary>
[DisallowMultipleComponent]
public class HealthHUDPresenter : MonoBehaviour
{
    #region Inspector — Referencias

    [Header("Fuente de datos (se busca automáticamente si se deja vacío)")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Textos TextMeshPro (obligatorio: heartsText)")]
    [SerializeField] private TMP_Text heartsText;
    [SerializeField] private TMP_Text resetsText;
    [SerializeField] private TMP_Text hintText;

    [Header("Tipografía personalizada (opcional)")]
    [Tooltip("Fuente aplicada a corazones/contadores. Para glifos como ♥ asegúrate de que la fuente los contenga.")]
    [SerializeField] private TMP_FontAsset hudFont;
    [Tooltip("Material preset opcional (Outline / Glow / Underlay) para dar acabado 'arcade'.")]
    [SerializeField] private Material hudFontMaterialPreset;

    [Header("Contenedor y visibilidad")]
    [Tooltip("RectTransform que tiembla al recibir daño (normalmente el panel del HUD).")]
    [SerializeField] private RectTransform shakeTarget;
    [Tooltip("CanvasGroup para ocultar el HUD con fade cuando Gandalf está activo o el HUD limpio [H] está activo.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(1f, 20f)] private float visibilityFadeSpeed = 8f;

    [Tooltip("Componente HealthUI (OnGUI) antiguo. Si se asigna, se desactiva para no dibujar el HUD dos veces.")]
    [SerializeField] private HealthUI legacyHealthUI;

    #endregion

    #region Inspector — Estilo

    [Header("Iconografía (rich text TMP)")]
    [Tooltip("Marcado de corazón lleno. Puedes usar un Sprite Asset: <sprite name=\"heart_full\">")]
    [SerializeField] private string filledHeartMarkup = "<color=#FF3B4E>♥</color>";
    [Tooltip("Marcado de corazón vacío. Ej: <sprite name=\"heart_empty\">")]
    [SerializeField] private string emptyHeartMarkup = "<color=#3A3F4F>♥</color>";
    [SerializeField] private string heartSeparator = " ";

    [Tooltip("Formato del contador de resets. {0} = actuales, {1} = máximos.")]
    [SerializeField] private string resetsFormat = "<color=#FFC93C>RESETS</color>  <b>{0}</b><color=#FFFFFF66>/{1}</color>";

    [SerializeField] private string hintContent = "<color=#FFFFFF66>[R] Reiniciar nivel</color>";

    #endregion

    #region Inspector — Animación

    [Header("Screen Shake (daño)")]
    [SerializeField, Range(0f, 40f)] private float shakeMagnitudePixels = 12f;
    [SerializeField, Range(0.05f, 1f)] private float shakeDuration = 0.35f;
    [SerializeField, Range(5f, 60f)] private float shakeFrequency = 28f;

    [Header("Punch de escala")]
    [Tooltip("Escala máxima al recibir daño (se interpola con Mathf.Lerp de vuelta a 1).")]
    [SerializeField, Range(1f, 2f)] private float damagePunchScale = 1.35f;
    [Tooltip("Escala máxima al curarse.")]
    [SerializeField, Range(1f, 2f)] private float healPunchScale = 1.2f;
    [SerializeField, Range(0.05f, 1f)] private float punchDuration = 0.3f;

    [Header("Flash de color (daño)")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField, Range(0.05f, 1f)] private float flashDuration = 0.25f;

    [Header("Latido de peligro (vida = 1)")]
    [SerializeField] private bool lowHealthHeartbeat = true;
    [SerializeField, Range(0f, 0.3f)] private float heartbeatAmplitude = 0.08f;
    [SerializeField, Range(0.5f, 5f)] private float heartbeatSpeed = 2.4f;

    #endregion

    #region Estado runtime

    private readonly StringBuilder sb = new StringBuilder(128);

    private int lastHealth = int.MinValue;
    private int lastMaxHealth = int.MinValue;
    private int lastResets = int.MinValue;
    private int lastMaxResets = int.MinValue;

    private Vector2 shakeBasePosition;
    private float shakeTimer;
    private float shakeSeed;

    private float heartsPunchTimer;
    private float heartsPunchPeak = 1f;
    private float resetsPunchTimer;
    private float flashTimer;

    #endregion

    private void Awake()
    {
        if (shakeTarget == null) shakeTarget = transform as RectTransform;
        if (shakeTarget != null) shakeBasePosition = shakeTarget.anchoredPosition;
        if (legacyHealthUI != null) legacyHealthUI.enabled = false;

        ApplyTypography();
        if (hintText != null) hintText.text = hintContent;
    }

    private void OnValidate()
    {
        ApplyTypography();
    }

    private void Start()
    {
        // Búsqueda única (nunca en Update).
        if (playerHealth == null) playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
    }

    /// <summary>Aplica fuente y material personalizados a todos los textos del HUD.</summary>
    private void ApplyTypography()
    {
        ApplyFontTo(heartsText);
        ApplyFontTo(resetsText);
        ApplyFontTo(hintText);
    }

    private void ApplyFontTo(TMP_Text t)
    {
        if (t == null) return;
        if (hudFont != null) t.font = hudFont;
        if (hudFontMaterialPreset != null) t.fontSharedMaterial = hudFontMaterialPreset;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        UpdateVisibility(dt);
        if (playerHealth == null || heartsText == null) return;

        DetectChanges();
        AnimateShake(dt);
        AnimateHearts(dt);
        AnimateResets(dt);
    }

    #region Detección de cambios (lectura pasiva de PlayerHealth)

    private void DetectChanges()
    {
        int hp = playerHealth.currentHealth;
        int maxHp = playerHealth.maxHealth;

        if (hp != lastHealth || maxHp != lastMaxHealth)
        {
            // Primera lectura: sincronizar sin animar (PlayerHealth.Start puede ejecutarse después que este Start).
            bool firstSync = lastHealth == int.MinValue;

            if (!firstSync)
            {
                if (hp < lastHealth) PlayDamageFeedback();
                else if (hp > lastHealth) PlayHealFeedback();
            }

            lastHealth = hp;
            lastMaxHealth = maxHp;
            RebuildHeartsText(hp, maxHp);
        }

        if (resetsText != null)
        {
            int resets = playerHealth.currentResets;
            int maxResets = playerHealth.maxResets;
            if (resets != lastResets || maxResets != lastMaxResets)
            {
                if (lastResets != int.MinValue) resetsPunchTimer = punchDuration;
                lastResets = resets;
                lastMaxResets = maxResets;
                resetsText.SetText(resetsFormat, resets, maxResets); // SetText con args numéricos: sin GC.
            }
        }
    }

    private void RebuildHeartsText(int hp, int maxHp)
    {
        sb.Clear();
        for (int i = 0; i < maxHp; i++)
        {
            if (i > 0) sb.Append(heartSeparator);
            sb.Append(i < hp ? filledHeartMarkup : emptyHeartMarkup);
        }
        heartsText.SetText(sb);
    }

    private void PlayDamageFeedback()
    {
        shakeTimer = shakeDuration;
        shakeSeed = Random.value * 100f;
        heartsPunchTimer = punchDuration;
        heartsPunchPeak = damagePunchScale;
        flashTimer = flashDuration;
    }

    private void PlayHealFeedback()
    {
        heartsPunchTimer = punchDuration;
        heartsPunchPeak = healPunchScale;
    }

    #endregion

    #region Animaciones

    private void AnimateShake(float dt)
    {
        if (shakeTarget == null) return;

        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float decay = Mathf.Clamp01(shakeTimer / shakeDuration); // 1 → 0
            decay *= decay;                                          // Ease-out cuadrático: golpe fuerte, cola suave.

            float t = Time.unscaledTime * shakeFrequency;
            // Ruido Perlin (−1..1) → movimiento orgánico, no aleatorio "a saltos".
            float x = (Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f) * shakeMagnitudePixels * decay;
            float y = (Mathf.PerlinNoise(shakeSeed + 37.1f, t) * 2f - 1f) * shakeMagnitudePixels * decay;

            shakeTarget.anchoredPosition = shakeBasePosition + new Vector2(x, y);

            if (shakeTimer <= 0f) shakeTarget.anchoredPosition = shakeBasePosition;
        }
    }

    private void AnimateHearts(float dt)
    {
        float scale = 1f;

        // Punch: Mathf.Lerp desde el pico hasta 1 con curva ease-out.
        if (heartsPunchTimer > 0f)
        {
            heartsPunchTimer -= dt;
            float t = 1f - Mathf.Clamp01(heartsPunchTimer / punchDuration); // 0 → 1
            float eased = 1f - (1f - t) * (1f - t);                         // EaseOutQuad
            scale = Mathf.Lerp(heartsPunchPeak, 1f, eased);
        }
        else if (lowHealthHeartbeat && lastHealth == 1)
        {
            // Latido "doble" (lub-dub) con |sin|^4 para que sea seco y legible.
            float s = Mathf.Abs(Mathf.Sin(Time.unscaledTime * heartbeatSpeed * Mathf.PI));
            scale = 1f + heartbeatAmplitude * s * s * s * s;
        }

        heartsText.rectTransform.localScale = new Vector3(scale, scale, 1f);

        // Flash: el color de vértice multiplica el color de los tags <color> → tiñe todos los corazones.
        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            float t = Mathf.Clamp01(flashTimer / flashDuration);
            heartsText.color = Color.Lerp(Color.white, damageFlashColor, t);
        }
        else if (heartsText.color != Color.white)
        {
            heartsText.color = Color.white;
        }
    }

    private void AnimateResets(float dt)
    {
        if (resetsText == null) return;

        float scale = 1f;
        if (resetsPunchTimer > 0f)
        {
            resetsPunchTimer -= dt;
            float t = 1f - Mathf.Clamp01(resetsPunchTimer / punchDuration);
            scale = Mathf.Lerp(healPunchScale, 1f, t);
        }
        resetsText.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private void UpdateVisibility(float dt)
    {
        if (canvasGroup == null) return;

        // Misma regla que el HealthUI original: oculto durante Gandalf o con HUD limpio [H].
        bool hidden = GandalfNPC.isGandalfActive || GameEvents.IsCleanHudHidden;
        float target = hidden ? 0f : 1f;

        if (!Mathf.Approximately(canvasGroup.alpha, target))
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, visibilityFadeSpeed * dt);
    }

    #endregion

    /// <summary>Permite disparar el shake desde otros sistemas (explosiones, descarga RC del jefe...).</summary>
    public void TriggerShake()
    {
        shakeTimer = shakeDuration;
        shakeSeed = Random.value * 100f;
    }
}
