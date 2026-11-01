using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fase 3 — Pulido UI. Vista TextMeshPro del Firewall Gandalf (reemplazo visual del panel OnGUI).
///
/// PRINCIPIO: esta clase SOLO PINTA. Lee el estado de <see cref="GandalfNPC"/> mediante su API de solo lectura
/// y nunca llama a métodos que alteren el examen. Input, regla de aprobado (≥ mitad, p. ej. 2/3), daño por fallo,
/// apertura de puerta y carga de escena siguen 100% en GandalfNPC.
///
/// EFECTOS:
///   • Panel: fade-in + leve escala (0.94 → 1) al abrirse; fade-out al cerrarse.
///   • Enunciado / notas intro / resultado: efecto Typewriter (TMPTypewriter, tiempo no escalado).
///   • Opciones: aparecen escalonadas con Fade In + deslizamiento lateral cuando termina de escribirse la pregunta.
///   • Selección: resaltado suave (lerp de alpha y escala), sin saltos.
///   • Resultado: la opción correcta se tiñe de verde y la elegida (si falló) de rojo + barra de tiempo opcional.
///
/// Fuentes personalizadas: titleFont / bodyFont / optionFont (TMP_FontAsset) + materiales preset opcionales.
///
/// Si este componente se desactiva o falta una referencia obligatoria, GandalfNPC vuelve automáticamente
/// a su panel IMGUI original (fallback seguro).
/// </summary>
[DisallowMultipleComponent]
public class GandalfQuizPresenter : MonoBehaviour
{
    #region Tipos auxiliares

    /// <summary>Vista de una opción de respuesta.</summary>
    [System.Serializable]
    public class OptionView
    {
        [Tooltip("Raíz de la opción (RectTransform con CanvasGroup).")]
        public CanvasGroup group;
        [Tooltip("Texto TMP de la opción.")]
        public TMP_Text label;
        [Tooltip("Imagen de fondo/resaltado opcional (se ilumina al seleccionar).")]
        public Image highlight;

        [System.NonSerialized] public Vector2 basePosition;
        [System.NonSerialized] public float currentHighlight;
    }

    private enum ViewMode { Hidden, Intro, Question, Result, Summary }

    #endregion

    #region Inspector — Referencias

    [Header("Fuente de datos (se busca automáticamente si se deja vacío)")]
    [SerializeField] private GandalfNPC gandalf;

    [Header("Panel (obligatorio)")]
    [Tooltip("CanvasGroup raíz del modal (incluye el fondo oscuro a pantalla completa).")]
    [SerializeField] private CanvasGroup panelGroup;
    [Tooltip("RectTransform de la 'tarjeta' central que hace el efecto de escala al abrir.")]
    [SerializeField] private RectTransform cardTransform;

    [Header("Textos TMP")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Obligatorio. Enunciado / nota / resultado. Se le añade TMPTypewriter si no lo tiene.")]
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Pistas pedagógicas / resumen.")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private TMP_Text footerText;

    [Header("Opciones (normalmente 4)")]
    [SerializeField] private OptionView[] optionViews = new OptionView[4];

    [Header("Barra de tiempo tras responder (opcional, Image tipo Filled)")]
    [SerializeField] private Image resultTimerFill;

    #endregion

    #region Inspector — Tipografía

    [Header("Tipografía personalizada (opcional)")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private TMP_FontAsset optionFont;
    [Tooltip("Material preset opcional (Glow / Outline) para el título.")]
    [SerializeField] private Material titleMaterialPreset;

    #endregion

    #region Inspector — Estilo y textos

    [Header("Colores")]
    [SerializeField] private Color accentColor = new Color(0.30f, 0.85f, 1f);
    [SerializeField] private Color optionColor = new Color(0.85f, 0.90f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.35f, 1f, 0.82f);
    [SerializeField] private Color correctColor = new Color(0.35f, 1f, 0.45f);
    [SerializeField] private Color wrongColor = new Color(1f, 0.38f, 0.38f);
    [SerializeField] private Color hintColor = new Color(1f, 0.92f, 0.40f);
    [SerializeField] private Color highlightColor = new Color(0.30f, 0.85f, 1f, 0.18f);

    [Header("Marcadores (verifica que tu fuente tenga los glifos o usa <sprite>)")]
    [SerializeField] private string titlePrefix = "<b>FIREWALL GANDALF</b>";
    [SerializeField] private string selectedPrefix = "<b>></b> ";
    [SerializeField] private string unselectedPrefix = "   ";
    [SerializeField] private string correctMark = "[+]";
    [SerializeField] private string wrongMark = "[x]";

    [Header("Pies de página")]
    [SerializeField] private string footerIntro = "[ESPACIO / Botón A] Continuar";
    [SerializeField] private string footerQuestion = "[W/S o D-Pad] Elegir   |   [ESPACIO / Botón A] Confirmar";
    [SerializeField] private string footerResult = "[ESPACIO / Botón A] Siguiente";
    [SerializeField] private string footerSummaryPass = "[ESPACIO / Botón A] Cruzar Puerta al Siguiente Sector";
    [SerializeField] private string footerSummaryFail = "[ESPACIO / Botón A] Re-experimentar en el Nivel";

    #endregion

    #region Inspector — Animación

    [Header("Panel")]
    [SerializeField, Range(1f, 20f)] private float panelFadeSpeed = 6f;
    [SerializeField, Range(0.8f, 1f)] private float panelStartScale = 0.94f;

    [Header("Opciones")]
    [Tooltip("Retraso entre la aparición de cada opción.")]
    [SerializeField, Range(0f, 0.5f)] private float optionStagger = 0.08f;
    [SerializeField, Range(0.05f, 1f)] private float optionFadeDuration = 0.25f;
    [Tooltip("Desplazamiento horizontal inicial (px) desde el que entran las opciones.")]
    [SerializeField, Range(0f, 80f)] private float optionSlideOffset = 24f;
    [Tooltip("Si es true, las opciones esperan a que termine de escribirse la pregunta.")]
    [SerializeField] private bool waitForTypewriterBeforeOptions = true;
    [SerializeField, Range(1f, 1.15f)] private float selectedOptionScale = 1.04f;
    [SerializeField, Range(1f, 30f)] private float selectionLerpSpeed = 14f;

    [Header("Typewriter")]
    [Tooltip("Multiplicador de velocidad del typewriter en la pantalla de resultado (más rápido que el enunciado).")]
    [SerializeField, Range(1f, 5f)] private float resultTypeSpeedMultiplier = 2f;

    #endregion

    #region Estado runtime

    private readonly StringBuilder sb = new StringBuilder(512);
    private TMPTypewriter typewriter;
    private float baseTypeSpeed;

    private ViewMode currentMode = ViewMode.Hidden;
    private int currentModeIndex = -1;     // índice de pregunta / nota del modo actual
    private int lastSelectedOption = -1;

    private bool optionsRevealing;
    private float optionsRevealClock;
    private int visibleOptionCount;

    private float panelAlpha;

    #endregion

    #region Ciclo de vida

    private void Awake()
    {
        if (bodyText != null)
        {
            typewriter = bodyText.GetComponent<TMPTypewriter>();
            if (typewriter == null) typewriter = bodyText.gameObject.AddComponent<TMPTypewriter>();
            baseTypeSpeed = typewriter.CharactersPerSecond;
        }

        for (int i = 0; i < optionViews.Length; i++)
        {
            OptionView ov = optionViews[i];
            if (ov?.group != null)
                ov.basePosition = ((RectTransform)ov.group.transform).anchoredPosition;
        }

        ApplyTypography();
        SetPanelImmediate(0f);
    }

    private void OnValidate()
    {
        ApplyTypography();
    }

    private void Start()
    {
        if (gandalf == null) gandalf = Object.FindAnyObjectByType<GandalfNPC>(); // Búsqueda única.
        RegisterWithGandalf(true);
    }

    private void OnEnable() => RegisterWithGandalf(true);
    private void OnDisable() => RegisterWithGandalf(false);

    /// <summary>Activa la vista TMP en GandalfNPC solo si la configuración mínima es válida (fallback a IMGUI si no).</summary>
    private void RegisterWithGandalf(bool enable)
    {
        if (gandalf == null) return;
        bool valid = panelGroup != null && bodyText != null;
        gandalf.UseExternalPresenter = enable && valid;

        if (enable && !valid)
            Debug.LogWarning("[GandalfQuizPresenter] Falta panelGroup o bodyText: se usa el panel IMGUI original.", this);
    }

    private void Update()
    {
        if (gandalf == null || panelGroup == null || bodyText == null) return;

        float dt = Time.unscaledDeltaTime;

        ViewMode mode;
        int index;
        ResolveMode(out mode, out index);

        // Reconstruir solo cuando cambia el "estado lógico" (no cada frame).
        if (mode != currentMode || index != currentModeIndex)
        {
            EnterMode(mode, index);
        }

        AnimatePanel(dt, mode != ViewMode.Hidden);
        if (mode == ViewMode.Hidden) return;

        if (mode == ViewMode.Question && gandalf.SelectedOption != lastSelectedOption)
        {
            lastSelectedOption = gandalf.SelectedOption;
            RefreshOptionLabels(ViewMode.Question);
        }

        AnimateOptions(dt);

        if (resultTimerFill != null)
            resultTimerFill.fillAmount = mode == ViewMode.Result ? gandalf.ResultTimeRemaining01 : 0f;
    }

    #endregion

    #region Máquina de vistas

    private void ResolveMode(out ViewMode mode, out int index)
    {
        if (!gandalf.IsTrialActive) { mode = ViewMode.Hidden; index = -1; return; }
        if (gandalf.IsShowingIntro) { mode = ViewMode.Intro; index = gandalf.CurrentIntroIndex; return; }
        if (gandalf.IsShowingSummary) { mode = ViewMode.Summary; index = 0; return; }

        index = gandalf.CurrentQuestionIndex;
        mode = gandalf.IsAnswered ? ViewMode.Result : ViewMode.Question;
    }

    private void EnterMode(ViewMode mode, int index)
    {
        ViewMode previous = currentMode;
        currentMode = mode;
        currentModeIndex = index;
        lastSelectedOption = gandalf.SelectedOption;

        if (mode == ViewMode.Hidden) return;

        // Al abrir el panel desde cero: arrancar animación de entrada.
        if (previous == ViewMode.Hidden) SetPanelImmediate(0f);

        switch (mode)
        {
            case ViewMode.Intro: BuildIntro(); break;
            case ViewMode.Question: BuildQuestion(); break;
            case ViewMode.Result: BuildResult(); break;
            case ViewMode.Summary: BuildSummary(); break;
        }
    }

    private void BuildIntro()
    {
        SetTitle(sb.Clear().Append(titlePrefix).Append("  <size=80%><alpha=#AA>Nota ")
            .Append(gandalf.CurrentIntroIndex + 1).Append('/').Append(gandalf.IntroChunkCount).Append("</size>"));

        string chunk = gandalf.CurrentIntroChunk;
        if (gandalf.FormatHintsAsBulletsEnabled) chunk = GandalfNPC.FormatExplanationAsBullets(chunk);
        PlayBody(chunk, 1f);

        SetHint(null);
        HideOptionsImmediate();
        SetFooter(footerIntro);
    }

    private void BuildQuestion()
    {
        TrialQuestion q = gandalf.CurrentTrialQuestion;
        if (q == null) return;

        SetTitle(sb.Clear().Append(titlePrefix).Append("  <size=80%><alpha=#AA>Pregunta ")
            .Append(gandalf.CurrentQuestionIndex + 1).Append('/').Append(gandalf.QuestionCount)
            .Append("   <color=#").Append(ColorUtility.ToHtmlStringRGB(correctColor)).Append('>')
            .Append(gandalf.CorrectCount).Append('/').Append(gandalf.RequiredToPass).Append(" requeridas</color></size>"));

        PlayBody(q.question, 1f);
        SetHint(null);

        // Preparar opciones ocultas y programar su aparición escalonada.
        visibleOptionCount = q.options != null ? Mathf.Min(q.options.Length, optionViews.Length) : 0;
        HideOptionsImmediate();
        RefreshOptionLabels(ViewMode.Question);
        optionsRevealing = true;
        optionsRevealClock = 0f;

        SetFooter(footerQuestion);
    }

    private void BuildResult()
    {
        TrialQuestion q = gandalf.CurrentTrialQuestion;
        if (q == null) return;

        bool ok = gandalf.LastAnswerCorrect;
        Color c = ok ? correctColor : wrongColor;

        sb.Clear().Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append("><b>")
          .Append(ok ? correctMark + "  ¡Correcto! Comprensión verificada" : wrongMark + "  Incorrecto — Revisa los puntos clave")
          .Append("</b></color>");
        PlayBody(sb.ToString(), resultTypeSpeedMultiplier);

        if (!ok)
        {
            string rawHint = string.IsNullOrEmpty(q.pedagogicalHint)
                ? "Usa [V] para observar cómo F = k·|q·Q|/r² depende inversamente de r²."
                : q.pedagogicalHint;
            string hint = gandalf.FormatHintsAsBulletsEnabled ? GandalfNPC.FormatExplanationAsBullets(rawHint) : rawHint;

            sb.Clear().Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(hintColor)).Append("><b>Puntos clave</b>\n")
              .Append(hint).Append("</color>");
            SetHint(sb.ToString());
        }
        else
        {
            SetHint(null);
        }

        // Las opciones siguen visibles pero recoloreadas (correcta en verde, elegida errónea en rojo).
        // Si el jugador respondió antes de que terminaran de aparecer, se muestran completas de inmediato.
        ShowOptionsImmediate();
        RefreshOptionLabels(ViewMode.Result);
        SetFooter(footerResult);
    }

    private void BuildSummary()
    {
        bool passed = gandalf.HasPassed;
        Color c = passed ? correctColor : wrongColor;

        SetTitle(sb.Clear().Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>')
            .Append(passed ? "<b>FIREWALL SUPERADO</b> — Puerta desbloqueada" : "<b>ACCESO DENEGADO</b> — Re-experimenta con [V]")
            .Append("</color>"));

        sb.Clear().Append("Resultado: <b>").Append(gandalf.CorrectCount).Append('/').Append(gandalf.QuestionCount)
          .Append("</b> correctas  <alpha=#AA>(mínimo ").Append(gandalf.RequiredToPass).Append(')');
        PlayBody(sb.ToString(), 1.5f);

        // Desglose por pregunta en el bloque de pistas.
        sb.Clear();
        for (int i = 0; i < gandalf.QuestionCount; i++)
        {
            TrialQuestion q = gandalf.GetQuestion(i);
            if (q == null) continue;
            bool ok = gandalf.GetQuestionResult(i);

            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(ok ? correctColor : wrongColor)).Append('>')
              .Append(ok ? correctMark : wrongMark).Append(" P").Append(i + 1).Append(": </color>")
              .Append(Truncate(q.question, 64));

            if (!ok && q.options != null && q.correctAnswer >= 0 && q.correctAnswer < q.options.Length)
            {
                sb.Append("\n   <color=#").Append(ColorUtility.ToHtmlStringRGB(hintColor)).Append(">→ ")
                  .Append(q.options[q.correctAnswer]).Append("</color>");
            }
        }
        SetHint(sb.ToString());

        HideOptionsImmediate();
        SetFooter(passed ? footerSummaryPass : footerSummaryFail);
    }

    #endregion

    #region Opciones

    private void RefreshOptionLabels(ViewMode mode)
    {
        TrialQuestion q = gandalf.CurrentTrialQuestion;
        if (q == null || q.options == null) return;

        int selected = gandalf.SelectedOption;

        for (int i = 0; i < optionViews.Length; i++)
        {
            OptionView ov = optionViews[i];
            if (ov == null || ov.label == null) continue;

            bool exists = i < q.options.Length;
            if (ov.group != null) ov.group.gameObject.SetActive(exists);
            if (!exists) continue;

            bool isSelected = i == selected;
            Color c = optionColor;

            if (mode == ViewMode.Question)
            {
                c = isSelected ? selectedColor : optionColor;
            }
            else if (mode == ViewMode.Result)
            {
                if (i == q.correctAnswer) c = correctColor;
                else if (isSelected) c = wrongColor;
                else c = new Color(optionColor.r, optionColor.g, optionColor.b, 0.45f); // Atenuar el resto.
            }

            ov.label.color = c;
            sb.Clear().Append(isSelected ? selectedPrefix : unselectedPrefix).Append(q.options[i]);
            ov.label.SetText(sb);
        }
    }

    private void HideOptionsImmediate()
    {
        optionsRevealing = false;
        for (int i = 0; i < optionViews.Length; i++)
        {
            OptionView ov = optionViews[i];
            if (ov?.group == null) continue;
            ov.group.alpha = 0f;
            ((RectTransform)ov.group.transform).anchoredPosition = ov.basePosition + new Vector2(optionSlideOffset, 0f);
        }
    }

    private void ShowOptionsImmediate()
    {
        optionsRevealing = false;
        for (int i = 0; i < optionViews.Length; i++)
        {
            OptionView ov = optionViews[i];
            if (ov?.group == null) continue;
            ov.group.alpha = 1f;
            ((RectTransform)ov.group.transform).anchoredPosition = ov.basePosition;
        }
    }

    private void AnimateOptions(float dt)
    {
        bool showOptions = currentMode == ViewMode.Question || currentMode == ViewMode.Result;
        if (!showOptions) return;

        // 1) Fade In escalonado + deslizamiento (solo al presentar una pregunta nueva).
        if (optionsRevealing)
        {
            bool canStart = !waitForTypewriterBeforeOptions || typewriter == null || !typewriter.IsPlaying;
            if (canStart)
            {
                optionsRevealClock += dt;
                bool allDone = true;

                for (int i = 0; i < visibleOptionCount; i++)
                {
                    OptionView ov = optionViews[i];
                    if (ov?.group == null) continue;

                    float localT = Mathf.Clamp01((optionsRevealClock - i * optionStagger) / optionFadeDuration);
                    float eased = 1f - (1f - localT) * (1f - localT) * (1f - localT); // EaseOutCubic

                    ov.group.alpha = eased;
                    ((RectTransform)ov.group.transform).anchoredPosition =
                        ov.basePosition + new Vector2(Mathf.Lerp(optionSlideOffset, 0f, eased), 0f);

                    if (localT < 1f) allDone = false;
                }

                if (allDone) optionsRevealing = false;
            }
        }

        // 2) Resaltado de selección suave (alpha del fondo + escala del texto).
        int selected = gandalf.SelectedOption;
        float k = 1f - Mathf.Exp(-selectionLerpSpeed * dt); // Lerp independiente del framerate.

        for (int i = 0; i < optionViews.Length; i++)
        {
            OptionView ov = optionViews[i];
            if (ov == null) continue;

            float target = (i == selected) ? 1f : 0f;
            ov.currentHighlight = Mathf.Lerp(ov.currentHighlight, target, k);

            if (ov.highlight != null)
            {
                Color hc = highlightColor;
                hc.a *= ov.currentHighlight;
                ov.highlight.color = hc;
            }

            if (ov.label != null)
            {
                float s = Mathf.Lerp(1f, selectedOptionScale, ov.currentHighlight);
                ov.label.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    #endregion

    #region Panel y helpers de texto

    private void AnimatePanel(float dt, bool visible)
    {
        float target = visible ? 1f : 0f;
        panelAlpha = Mathf.MoveTowards(panelAlpha, target, panelFadeSpeed * dt);

        panelGroup.alpha = panelAlpha;
        panelGroup.blocksRaycasts = panelAlpha > 0.01f;
        panelGroup.interactable = visible;

        if (cardTransform != null)
        {
            // Escala con ease-out ligada al alpha: "pop" sutil al abrir.
            float eased = 1f - (1f - panelAlpha) * (1f - panelAlpha);
            float s = Mathf.Lerp(panelStartScale, 1f, eased);
            cardTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    private void SetPanelImmediate(float alpha)
    {
        panelAlpha = alpha;
        if (panelGroup != null)
        {
            panelGroup.alpha = alpha;
            panelGroup.blocksRaycasts = alpha > 0.01f;
        }
        if (cardTransform != null)
            cardTransform.localScale = Vector3.one * (alpha > 0f ? 1f : panelStartScale);
    }

    private void PlayBody(string content, float speedMultiplier)
    {
        if (typewriter != null)
        {
            typewriter.CharactersPerSecond = baseTypeSpeed * speedMultiplier;
            typewriter.Play(content);
        }
        else
        {
            bodyText.text = content;
        }
    }

    private void SetTitle(StringBuilder content)
    {
        if (titleText != null)
        {
            titleText.color = accentColor;
            titleText.SetText(content);
        }
    }

    private void SetHint(string content)
    {
        bool has = !string.IsNullOrEmpty(content);
        if (hintText != null) hintText.text = has ? content : string.Empty;
        if (hintGroup != null) hintGroup.alpha = has ? 1f : 0f;
    }

    private void SetFooter(string content)
    {
        if (footerText != null) footerText.text = content;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max) + "...";
    }

    private void ApplyTypography()
    {
        if (titleText != null)
        {
            if (titleFont != null) titleText.font = titleFont;
            if (titleMaterialPreset != null) titleText.fontSharedMaterial = titleMaterialPreset;
        }
        if (bodyFont != null)
        {
            if (bodyText != null) bodyText.font = bodyFont;
            if (hintText != null) hintText.font = bodyFont;
            if (footerText != null) footerText.font = bodyFont;
        }
        if (optionFont != null && optionViews != null)
        {
            for (int i = 0; i < optionViews.Length; i++)
                if (optionViews[i]?.label != null) optionViews[i].label.font = optionFont;
        }
    }

    #endregion
}
