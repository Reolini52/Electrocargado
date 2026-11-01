using UnityEngine;
using TMPro;

/// <summary>
/// HUD de Salud y Resets de THEIA.
/// [Fase 3] Mantiene intacta la lectura pasiva de PlayerHealth (cero cambios en la lógica de vida),
/// pero mejora la capa de presentación con:
///  1. Screen Shake orgánico (Perlin Noise con decaimiento cuadrático) al perder vida.
///  2. Escala interpolada (Mathf.Lerp) en los corazones al recibir daño/curación y latido cuando queda 1 HP.
///  3. Soporte dual: si asignas referencias TextMeshPro (o usas HealthHUDPresenter), usa TMP con fuente personalizada;
///     si no hay Canvas configurado aún, renderiza el HUD pulido y animado directamente en OnGUI como fallback inmediato.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [Header("Referencias TextMeshPro Opcionales (Fase 3)")]
    [Tooltip("Fuente TMP personalizada opcional para el HUD.")]
    [SerializeField] private TMP_FontAsset customTmpFont;
    [Tooltip("Texto TMP opcional para los corazones (si se asigna, sincroniza el texto en TMP).")]
    [SerializeField] private TMP_Text tmpHeartsText;
    [Tooltip("Texto TMP opcional para el contador de Resets.")]
    [SerializeField] private TMP_Text tmpResetsText;
    [Tooltip("RectTransform opcional del contenedor TMP para aplicar Screen Shake.")]
    [SerializeField] private RectTransform tmpShakeContainer;

    [Header("Game Feel — Animaciones de Daño y Latido (Fase 3)")]
    [Tooltip("Magnitud del temblor (Screen Shake) en píxeles cuando THEIA pierde vida.")]
    [SerializeField, Range(0f, 25f)] private float shakeIntensity = 9f;
    [Tooltip("Duración del temblor en segundos.")]
    [SerializeField, Range(0.05f, 0.8f)] private float shakeDuration = 0.32f;
    [Tooltip("Escala pico del texto de vida al recibir impacto (se interpola con Mathf.Lerp de vuelta a 1).")]
    [SerializeField, Range(1f, 1.8f)] private float damageScalePunch = 1.35f;

    private PlayerHealth playerHealth;
    private GUIStyle heartStyle;
    private GUIStyle resetStyle;
    private GUIStyle badgeStyle;

    // Estado de animación (solo lectura pasiva de currentHealth)
    private int lastRecordedHealth = -1;
    private float shakeTimer = 0f;
    private float punchTimer = 0f;
    private float flashTimer = 0f;
    private float shakeSeed = 0f;
    private Vector2 tmpBaseAnchoredPos;

    private const int BaseHeartFontSize = 23;

    void Start()
    {
        playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
            lastRecordedHealth = playerHealth.currentHealth;

        if (tmpShakeContainer != null)
            tmpBaseAnchoredPos = tmpShakeContainer.anchoredPosition;

        ApplyCustomFontIfAssigned();
    }

    private void OnValidate()
    {
        ApplyCustomFontIfAssigned();
    }

    private void ApplyCustomFontIfAssigned()
    {
        if (customTmpFont == null) return;
        if (tmpHeartsText != null) tmpHeartsText.font = customTmpFont;
        if (tmpResetsText != null) tmpResetsText.font = customTmpFont;
    }

    void Update()
    {
        if (playerHealth == null) return;

        // Detectar cambio de vida sin tocar PlayerHealth.TakeDamage
        if (lastRecordedHealth != -1 && playerHealth.currentHealth != lastRecordedHealth)
        {
            if (playerHealth.currentHealth < lastRecordedHealth)
            {
                // Recibió daño -> activar Screen Shake + Punch de escala + Flash
                shakeTimer = shakeDuration;
                punchTimer = 0.28f;
                flashTimer = 0.25f;
                shakeSeed = Random.value * 100f;
            }
            else if (playerHealth.currentHealth > lastRecordedHealth)
            {
                punchTimer = 0.25f;
            }
            lastRecordedHealth = playerHealth.currentHealth;
        }
        else if (lastRecordedHealth == -1)
        {
            lastRecordedHealth = playerHealth.currentHealth;
        }

        float dt = Time.unscaledDeltaTime;
        if (shakeTimer > 0f) shakeTimer = Mathf.Max(0f, shakeTimer - dt);
        if (punchTimer > 0f) punchTimer = Mathf.Max(0f, punchTimer - dt);
        if (flashTimer > 0f) flashTimer = Mathf.Max(0f, flashTimer - dt);

        UpdateOptionalTMPReferences();
    }

    private void UpdateOptionalTMPReferences()
    {
        if (tmpHeartsText == null && tmpResetsText == null) return;

        // Escala interpolada con Mathf.Lerp
        float scale = EvaluateHeartScale();
        if (tmpHeartsText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < playerHealth.maxHealth; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i < playerHealth.currentHealth ? "<color=#FF3B4E>♥</color>" : "<color=#3A3F4F>♥</color>");
            }
            tmpHeartsText.SetText(sb);
            tmpHeartsText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (tmpResetsText != null)
        {
            tmpResetsText.SetText("RESETS: {0}/{1}", playerHealth.currentResets, playerHealth.maxResets);
        }

        if (tmpShakeContainer != null)
        {
            tmpShakeContainer.anchoredPosition = tmpBaseAnchoredPos + EvaluateShakeOffset();
        }
    }

    private Vector2 EvaluateShakeOffset()
    {
        if (shakeTimer <= 0f || shakeDuration <= 0.001f) return Vector2.zero;

        float decay = Mathf.Clamp01(shakeTimer / shakeDuration);
        decay *= decay; // Ease-out cuadrático
        float t = Time.unscaledTime * 28f;
        float ox = (Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f) * shakeIntensity * decay;
        float oy = (Mathf.PerlinNoise(shakeSeed + 41.7f, t) * 2f - 1f) * shakeIntensity * decay;
        return new Vector2(ox, oy);
    }

    private float EvaluateHeartScale()
    {
        if (punchTimer > 0f)
        {
            float t = 1f - Mathf.Clamp01(punchTimer / 0.28f);
            float eased = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(damageScalePunch, 1f, eased);
        }

        // Pulso sutil de peligro cuando a THEIA le queda 1 sola vida
        if (playerHealth != null && playerHealth.currentHealth == 1)
        {
            float beat = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));
            return 1f + beat * beat * 0.12f;
        }

        return 1f;
    }

    void OnGUI()
    {
        if (GandalfNPC.isGandalfActive || GameEvents.IsCleanHudHidden) return;
        if (playerHealth == null) return;
        if (heartStyle == null) InitStyles();

        Vector2 shake = EvaluateShakeOffset();
        float scale = EvaluateHeartScale();
        heartStyle.fontSize = Mathf.RoundToInt(BaseHeartFontSize * scale);

        float panelX = 10f + shake.x;
        float panelY = 10f + shake.y;
        float panelW = 148f;
        float panelH = 38f;

        // Fondo estilizado sci-fi con borde de acento (cambia a rojo en flash de daño)
        Color borderColor = flashTimer > 0f
            ? Color.Lerp(new Color(0.25f, 0.8f, 1f, 0.9f), new Color(1f, 0.25f, 0.3f, 1f), flashTimer / 0.25f)
            : new Color(0.25f, 0.8f, 1f, 0.85f);

        GUI.color = new Color(0.04f, 0.06f, 0.14f, 0.86f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

        // Barra lateral de acento eléctrico
        GUI.color = borderColor;
        GUI.DrawTexture(new Rect(panelX, panelY, 3f, panelH), Texture2D.whiteTexture);

        for (int i = 0; i < playerHealth.maxHealth; i++)
        {
            bool filled = i < playerHealth.currentHealth;
            if (filled)
            {
                GUI.color = flashTimer > 0f
                    ? Color.Lerp(new Color(1f, 0.22f, 0.28f), Color.white, flashTimer / 0.25f)
                    : new Color(1f, 0.22f, 0.28f);
            }
            else
            {
                GUI.color = new Color(0.28f, 0.30f, 0.38f, 0.85f);
            }

            GUI.Label(new Rect(panelX + 12f + i * 36f, panelY + 4f, 34f, 32f), "♥", heartStyle);
        }

        // Contador de Resets con barra inferior estilizada
        float resetY = panelY + panelH + 4f;
        GUI.color = new Color(0.04f, 0.06f, 0.14f, 0.82f);
        GUI.DrawTexture(new Rect(panelX, resetY, panelW, 24f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.82f, 0.25f, 0.9f);
        GUI.DrawTexture(new Rect(panelX, resetY, 3f, 24f), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 0.86f, 0.3f);
        GUI.Label(new Rect(panelX + 10f, resetY + 3f, 190f, 20f),
            $"⚡ Resets: {playerHealth.currentResets}/{playerHealth.maxResets}",
            resetStyle);

        GUI.color = new Color(0.85f, 0.92f, 1f, 0.55f);
        GUI.Label(new Rect(panelX + 4f, resetY + 26f, 200f, 20f), "[R] Reiniciar nivel", badgeStyle);
    }

    void InitStyles()
    {
        heartStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = BaseHeartFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        resetStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };
        resetStyle.normal.textColor = Color.white;

        badgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Normal
        };
        badgeStyle.normal.textColor = new Color(0.85f, 0.92f, 1f, 0.6f);
    }
}
