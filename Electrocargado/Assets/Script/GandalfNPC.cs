using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class TrialQuestion
{
    [Tooltip("Enunciado conceptual de electromagnetismo para evaluar al jugador.")]
    [TextArea(2, 4)]
    public string question;

    [Tooltip("Fragmentos cortos opcionales si el enunciado requiere dividirse en pasos cortos antes de responder.")]
    [TextArea(1, 3)]
    public string[] questionFragments;

    [Tooltip("Opciones de respuesta múltiple.")]
    public string[] options = new string[4];

    [Tooltip("Índice (0 a 3) de la respuesta correcta.")]
    [Range(0, 3)]
    public int correctAnswer = 0;

    [Tooltip("Pista pedagógica / explicación matemática que se muestra si el jugador falla.")]
    [TextArea(1, 3)]
    public string pedagogicalHint = "Recuerda la Ley de Coulomb: Fe = k·|q1·q2| / r² (inversamente proporcional al cuadrado de la distancia).";
}

/// <summary>
/// Capa 3 (Verificar): NPC Firewall "Gandalf" que custodia la puerta al final de cada sector.
/// Refactorizado en Fase 1 con:
/// - Accesibilidad visual: tamaños de fuente dinámicos expuestos en el Inspector (TextMeshPro e IMGUI).
/// - Formateador automático de textos largos en viñetas cortas y fragmentos secuenciales (arrays de strings).
/// - Soporte nativo para Gamepad (D-Pad / Stick Izquierdo + Botón Sur [A / Cruz]) además de Teclado.
/// </summary>
public class GandalfNPC : MonoBehaviour
{
    // Estado accesible globalmente
    public static bool isGandalfActive = false;

    [Header("Configuración de Puerta / Nivel (Capa 3: Verificar)")]
    [Tooltip("Distancia a la cual Gandalf intercepta a THEIA para verificar su comprensión.")]
    [Range(1f, 10f)]
    public float triggerRadius = 3f;

    [Tooltip("Nombre de la siguiente escena a cargar al superar el Firewall.")]
    public string nextSceneName = "Level2";

    [Tooltip("Puerta física opcional en la escena que se desbloquea al aprobar.")]
    public GameObject firewallDoorToUnlock;

    [Header("Diálogo Introductorio en Fragmentos Cortos (Opcional)")]
    [Tooltip("Fragmentos cortos que Gandalf muestra uno por uno antes de iniciar el cuestionario.")]
    [TextArea(2, 3)]
    public string[] introDialogueChunks;

    [Header("Banco de Preguntas Conceptuales")]
    public TrialQuestion[] questions;

    [Header("Accesibilidad Visual y Tamaño de Fuente (Inspector)")]
    [Tooltip("Tamaño de fuente del encabezado del Firewall.")]
    [SerializeField, Range(14, 34)]
    private int titleFontSize = 18;

    [Tooltip("Tamaño de fuente del enunciado de la pregunta / explicación.")]
    [SerializeField, Range(14, 36)]
    private int questionFontSize = 18;

    [Tooltip("Tamaño de fuente de las opciones de respuesta.")]
    [SerializeField, Range(13, 32)]
    private int optionFontSize = 16;

    [Tooltip("Tamaño de fuente para las pistas pedagógicas en viñetas.")]
    [SerializeField, Range(13, 32)]
    private int hintFontSize = 16;

    [Tooltip("Formatear automáticamente las explicaciones y pistas en viñetas cortas (•) para lectura rápida.")]
    [SerializeField]
    private bool formatHintsAsBullets = true;

    [Header("Referencias Opcionales TextMeshPro (UI)")]
    [Tooltip("Fuente TMP personalizada opcional aplicada a todos los textos TMP del examen.")]
    [SerializeField] private TMP_FontAsset customTmpFont;
    [SerializeField] private TMP_Text tmpTitleText;
    [SerializeField] private TMP_Text tmpQuestionText;
    [SerializeField] private TMP_Text tmpHintText;
    [SerializeField] private TMP_Text[] tmpOptionTexts;

    [Header("Efectos de Presentación (Typewriter & Fade-In — Fase 3)")]
    [Tooltip("Velocidad del efecto máquina de escribir (caracteres por segundo en tiempo no escalado).")]
    [SerializeField, Range(15f, 180f)] private float typewriterCharsPerSec = 55f;
    [Tooltip("Duración del Fade-In escalonado de cada opción de respuesta.")]
    [SerializeField, Range(0.05f, 0.8f)] private float optionFadeInDuration = 0.25f;
    [Tooltip("Retraso entre la aparición de una opción y la siguiente.")]
    [SerializeField, Range(0f, 0.35f)] private float optionStaggerDelay = 0.08f;

    [Header("Capa de Presentación Externa (Fase 3)")]
    [Tooltip("Si está activo, el panel IMGUI (OnGUI) no se dibuja y la vista la pinta GandalfQuizPresenter (TextMeshPro). " +
             "La lógica del examen (input, 2/3 correctas, daño, puerta) NO cambia.")]
    [SerializeField] private bool useExternalTMPPresenter = false;

    [Header("Consecuencia de Fallo (Re-experimentación)")]
    [Range(0, 5)]
    public int failDamage = 1;
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;

    // Referencias cacheadas en Start (Cero GetComponent en Update)
    private Transform player;
    private PlayerHealth playerHealth;

    private bool isActive = false;
    private bool hasTriggered = false;
    private bool trialComplete = false;

    private bool showingIntroFragments = false;
    private int currentIntroIndex = 0;

    private int currentQuestion = 0;
    private int selectedOption = 0;
    private bool answered = false;
    private bool correct = false;
    private float resultTimer = 0f;
    private float resultDisplayTime = 3.0f;

    private int correctCount = 0;
    private int requiredToPass = 0;

    private bool showingSummary = false;
    private bool[] questionResults;

    // Cooldown para navegación suave con el Stick analógico del Gamepad
    private float gamepadNavCooldown = 0f;
    private float retryCooldownTimer = 0f;

    // Reloj de presentación (Typewriter + Fade-In en tiempo no escalado, no afecta la lógica)
    private float questionPresentationElapsed = 0f;
    private int lastPresentedStateKey = -999;

    private GUIStyle titleStyle;
    private GUIStyle questionStyle;
    private GUIStyle optionStyle;
    private GUIStyle selectedStyle;
    private GUIStyle resultStyle;
    private GUIStyle hintStyle;
    private int cachedFontHash = -1;

    protected virtual void Start()
    {
        isGandalfActive = false;
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (questions == null || questions.Length == 0)
        {
            questions = GetDefaultElectromagnetismQuestions();
        }

        requiredToPass = Mathf.CeilToInt(questions.Length / 2f);
        questionResults = new bool[questions.Length];
        ApplyFontSizesToTMP();
    }

    private void OnValidate()
    {
        ApplyFontSizesToTMP();
    }

    private void OnDestroy()
    {
        if (isActive)
        {
            isGandalfActive = false;
            Time.timeScale = 1f;
        }
    }

    protected virtual void Update()
    {
        if (player == null || trialComplete) return;

        if (retryCooldownTimer > 0f)
        {
            retryCooldownTimer -= Time.unscaledDeltaTime;
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist < triggerRadius && !hasTriggered)
        {
            hasTriggered = true;
            isActive = true;
            isGandalfActive = true;
            currentQuestion = 0;
            correctCount = 0;
            selectedOption = 0;
            answered = false;
            showingSummary = false;
            showingIntroFragments = introDialogueChunks != null && introDialogueChunks.Length > 0;
            currentIntroIndex = 0;
            questionResults = new bool[questions.Length];
            Time.timeScale = 0f;

            ApplyFontSizesToTMP();
            GameEvents.RaiseFirewallTriggered(true);
        }

        if (!isActive) return;

        // Actualizar reloj de presentación (Typewriter y Fade-In) cuando cambia la pregunta o nota
        int stateKey = showingIntroFragments ? (1000 + currentIntroIndex) : (showingSummary ? 2000 : currentQuestion);
        if (stateKey != lastPresentedStateKey)
        {
            lastPresentedStateKey = stateKey;
            questionPresentationElapsed = 0f;
        }
        questionPresentationElapsed += Time.unscaledDeltaTime;

        if (gamepadNavCooldown > 0f)
            gamepadNavCooldown -= Time.unscaledDeltaTime;

        bool confirmPressed = WasConfirmPressedThisFrame();
        bool moveUpPressed = WasNavigateUpPressedThisFrame();
        bool moveDownPressed = WasNavigateDownPressedThisFrame();

        // Paso 0: Si hay fragmentos introductorios cortos, mostrarlos uno por uno antes de las preguntas
        if (showingIntroFragments)
        {
            if (confirmPressed)
            {
                currentIntroIndex++;
                if (currentIntroIndex >= introDialogueChunks.Length)
                {
                    showingIntroFragments = false;
                }
            }
            return;
        }

        if (showingSummary)
        {
            if (confirmPressed)
                FinishSummary();
            return;
        }

        if (!answered)
        {
            if (moveUpPressed)
                selectedOption = Mathf.Max(0, selectedOption - 1);

            if (moveDownPressed)
                selectedOption = Mathf.Min(
                    questions[currentQuestion].options.Length - 1,
                    selectedOption + 1);

            if (confirmPressed)
                SubmitAnswer();
        }
        else
        {
            resultTimer -= Time.unscaledDeltaTime;
            if (resultTimer <= 0f || confirmPressed)
                AdvanceQuestion();
        }
    }

    #region Lectura Unificada de Input (Teclado + Gamepad)

    private bool WasConfirmPressedThisFrame()
    {
        bool kb = Keyboard.current != null &&
                  (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
        bool gp = Gamepad.current != null &&
                  (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame);
        return kb || gp;
    }

    private bool WasNavigateUpPressedThisFrame()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame))
            return true;

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.up.wasPressedThisFrame)
                return true;

            if (gamepadNavCooldown <= 0f && Gamepad.current.leftStick.ReadValue().y > 0.55f)
            {
                gamepadNavCooldown = 0.22f;
                return true;
            }
        }
        return false;
    }

    private bool WasNavigateDownPressedThisFrame()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame))
            return true;

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.down.wasPressedThisFrame)
                return true;

            if (gamepadNavCooldown <= 0f && Gamepad.current.leftStick.ReadValue().y < -0.55f)
            {
                gamepadNavCooldown = 0.22f;
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Funciones Auxiliares de Accesibilidad de Texto (Viñetas y Fragmentación)

    /// <summary>
    /// Convierte explicaciones electromagnéticas largas en viñetas cortas (•) fáciles de escanear visualmente.
    /// </summary>
    public static string FormatExplanationAsBullets(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        string cleaned = rawText.Replace("\r\n", "\n").Trim();
        string[] parts = cleaned.Split(new char[] { '\n', '.', ';' }, System.StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1)
            return "• " + cleaned.TrimStart('•', '-', ' ');

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            string line = parts[i].Trim().TrimStart('•', '-', ' ');
            if (string.IsNullOrEmpty(line)) continue;

            if (sb.Length > 0) sb.Append('\n');
            sb.Append("• ").Append(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Divide un párrafo largo en un array de fragmentos cortos (string[]) para mostrarlos secuencialmente en fuente grande.
    /// </summary>
    public static string[] SplitIntoDigestibleArray(string longText, int maxCharsPerFragment = 115)
    {
        if (string.IsNullOrWhiteSpace(longText))
            return new string[0];

        return Billboard.SplitIntoShortFragments(longText, maxCharsPerFragment, asBullets: true);
    }

    private void ApplyFontSizesToTMP()
    {
        if (tmpTitleText != null)
        {
            tmpTitleText.fontSize = titleFontSize;
            if (customTmpFont != null) tmpTitleText.font = customTmpFont;
        }
        if (tmpQuestionText != null)
        {
            tmpQuestionText.fontSize = questionFontSize;
            if (customTmpFont != null) tmpQuestionText.font = customTmpFont;
        }
        if (tmpHintText != null)
        {
            tmpHintText.fontSize = hintFontSize;
            if (customTmpFont != null) tmpHintText.font = customTmpFont;
        }

        if (tmpOptionTexts != null)
        {
            for (int i = 0; i < tmpOptionTexts.Length; i++)
            {
                if (tmpOptionTexts[i] != null)
                {
                    tmpOptionTexts[i].fontSize = optionFontSize;
                    if (customTmpFont != null) tmpOptionTexts[i].font = customTmpFont;
                }
            }
        }
    }

    #endregion

    #region API de Presentación de Solo Lectura (Fase 3 — consumida por GandalfQuizPresenter)

    // Estas propiedades SOLO exponen el estado interno para que una vista externa (TMP) lo pinte.
    // Ninguna modifica el flujo del examen.

    /// <summary>Si es true, OnGUI no dibuja el panel IMGUI (lo pinta una vista TMP externa).</summary>
    public bool UseExternalPresenter
    {
        get => useExternalTMPPresenter;
        set => useExternalTMPPresenter = value;
    }

    public bool IsTrialActive => isActive;
    public bool IsShowingIntro => showingIntroFragments && introDialogueChunks != null && currentIntroIndex < introDialogueChunks.Length;
    public int CurrentIntroIndex => currentIntroIndex;
    public int IntroChunkCount => introDialogueChunks != null ? introDialogueChunks.Length : 0;
    public string CurrentIntroChunk => IsShowingIntro ? introDialogueChunks[currentIntroIndex] : string.Empty;

    public bool IsShowingSummary => showingSummary;
    public int CurrentQuestionIndex => currentQuestion;
    public int QuestionCount => questions != null ? questions.Length : 0;
    public TrialQuestion CurrentTrialQuestion =>
        questions != null && currentQuestion >= 0 && currentQuestion < questions.Length ? questions[currentQuestion] : null;
    public TrialQuestion GetQuestion(int index) =>
        questions != null && index >= 0 && index < questions.Length ? questions[index] : null;

    public int SelectedOption => selectedOption;
    public bool IsAnswered => answered;
    public bool LastAnswerCorrect => correct;
    public int CorrectCount => correctCount;
    public int RequiredToPass => requiredToPass;
    public bool HasPassed => correctCount >= requiredToPass;
    public bool GetQuestionResult(int index) =>
        questionResults != null && index >= 0 && index < questionResults.Length && questionResults[index];

    /// <summary>Tiempo restante (0..1) antes de avanzar automáticamente tras responder. Útil para una barra de progreso.</summary>
    public float ResultTimeRemaining01 => resultDisplayTime > 0f ? Mathf.Clamp01(resultTimer / resultDisplayTime) : 0f;

    public bool FormatHintsAsBulletsEnabled => formatHintsAsBullets;

    #endregion

    void SubmitAnswer()
    {
        answered = true;
        TrialQuestion q = questions[currentQuestion];
        correct = selectedOption == q.correctAnswer;
        questionResults[currentQuestion] = correct;
        resultTimer = resultDisplayTime;
        Time.timeScale = 1f;

        string rawHint = string.IsNullOrEmpty(q.pedagogicalHint)
            ? "Ley de Coulomb: F = k·|q1·q2|/r². Descarga RC: Q(t) = Q0·e^(-t/RC)."
            : q.pedagogicalHint;

        string hintMsg = formatHintsAsBullets ? FormatExplanationAsBullets(rawHint) : rawHint;

        if (correct)
            correctCount++;
        else if (playerHealth != null && playerHealth.currentHealth > failDamage)
            playerHealth.TakeDamage(failDamage);

        GameEvents.RaiseQuizQuestionAnswered(currentQuestion, correct, hintMsg);
    }

    void AdvanceQuestion()
    {
        currentQuestion++;

        if (currentQuestion >= questions.Length)
        {
            showingSummary = true;
            Time.timeScale = 0f;
            return;
        }

        answered = false;
        selectedOption = 0;
        resultTimer = 0f;
        Time.timeScale = 0f;
    }

    void FinishSummary()
    {
        bool passed = correctCount >= requiredToPass;
        GameEvents.RaiseFirewallEvaluationCompleted(passed);
        GameEvents.RaiseFirewallTriggered(false);

        if (passed)
        {
            trialComplete = true;
            isActive = false;
            showingSummary = false;
            isGandalfActive = false;
            Time.timeScale = 1f;

            if (firewallDoorToUnlock != null)
            {
                firewallDoorToUnlock.SetActive(false);
            }

            if (!string.IsNullOrEmpty(nextSceneName))
            {
                string trimmed = nextSceneName.Trim();
                string targetScene =
                    (trimmed == "Level1" || trimmed == "Nivel 1") ? "Level 1" :
                    (trimmed == "Level2" || trimmed == "Nivel 2") ? "Level 2" :
                    (trimmed == "Level3" || trimmed == "Nivel 3") ? "Level 3" :
                    (trimmed == "Level4" || trimmed == "Nivel 4" || trimmed == "Boss") ? "Level 4" :
                    trimmed;
                SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            // No aplicar daño letal duplicado al cerrar el resumen para evitar recargar la escena innecesariamente;
            // permitir que el jugador re-experimente en el mismo nivel y vuelva a intentar el examen.
            if (spawnPoints != null && enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                foreach (Transform spawn in spawnPoints)
                {
                    if (spawn == null) continue;
                    int idx = Random.Range(0, enemyPrefabs.Length);
                    if (enemyPrefabs[idx] != null)
                        Instantiate(enemyPrefabs[idx], spawn.position, Quaternion.identity);
                }
            }

            isActive = false;
            showingSummary = false;
            isGandalfActive = false;
            hasTriggered = false;
            retryCooldownTimer = 3.0f;
            currentQuestion = 0;
            correctCount = 0;
            selectedOption = 0;
            answered = false;
            questionResults = new bool[questions.Length];
            Time.timeScale = 1f;
        }
    }

    void OnGUI()
    {
        if (!isActive || useExternalTMPPresenter) return;

        int currentHash = (titleFontSize * 397) ^ (questionFontSize * 17) ^ (optionFontSize * 31) ^ hintFontSize;
        if (titleStyle == null || cachedFontHash != currentHash)
            InitStyles(currentHash);

        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Panel más amplio para alojar cómodamente fuentes grandes de accesibilidad
        float w = Mathf.Min(Screen.width * 0.88f, 640f);
        float h = showingSummary ? 420f : 400f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height / 2f - h / 2f;

        GUI.color = new Color(0.04f, 0.05f, 0.18f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.85f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);

        // Si hay fragmentos introductorios cortos, mostrarlos primero con fuente grande
        if (showingIntroFragments && introDialogueChunks != null && currentIntroIndex < introDialogueChunks.Length)
        {
            GUI.color = new Color(0.3f, 0.85f, 1f);
            GUI.Label(new Rect(x + 20f, y + 14f, w - 40f, 30f),
                $"⚡ FIREWALL GANDALF — Nota {currentIntroIndex + 1}/{introDialogueChunks.Length}",
                titleStyle);

            string formattedIntro = formatHintsAsBullets
                ? FormatExplanationAsBullets(introDialogueChunks[currentIntroIndex])
                : introDialogueChunks[currentIntroIndex];

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 24f, y + 70f, w - 48f, h - 140f), formattedIntro, questionStyle);

            GUI.color = new Color(0.4f, 0.95f, 1f, 0.9f);
            GUI.Label(new Rect(x + 20f, y + h - 42f, w - 40f, 28f),
                "[ESPACIO / Botón A] Continuar", optionStyle);
            return;
        }

        if (showingSummary)
        {
            DrawSummary(x, y, w, h);
            return;
        }

        GUI.color = new Color(0.3f, 0.85f, 1f);
        GUI.Label(new Rect(x + 20f, y + 12f, w - 40f, 28f),
            $"⚡ FIREWALL GANDALF — Pregunta {currentQuestion + 1}/{questions.Length}  |  ✓ {correctCount}/{requiredToPass} requeridas",
            titleStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        GUI.DrawTexture(new Rect(x + 20f, y + 46f, w - 40f, 1f), Texture2D.whiteTexture);

        TrialQuestion q = questions[currentQuestion];

        if (!answered)
        {
            // [Fase 3] Efecto Typewriter sobre el enunciado + Fade-In escalonado en las opciones
            int totalChars = q.question != null ? q.question.Length : 0;
            int visibleChars = Mathf.Clamp(Mathf.FloorToInt(questionPresentationElapsed * typewriterCharsPerSec), 0, totalChars);
            string typedQuestion = (q.question != null && visibleChars < totalChars)
                ? q.question.Substring(0, visibleChars) + "▌"
                : q.question;

            float typeDuration = totalChars / Mathf.Max(1f, typewriterCharsPerSec);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 20f, y + 54f, w - 40f, 92f), typedQuestion, questionStyle);

            for (int i = 0; i < q.options.Length; i++)
            {
                // Aparecen escalonadas con Fade-In suave y ligero desplazamiento lateral
                float optStart = Mathf.Min(typeDuration * 0.45f, 0.4f) + i * optionStaggerDelay;
                float optAlpha = Mathf.Clamp01((questionPresentationElapsed - optStart) / Mathf.Max(0.05f, optionFadeInDuration));
                float slideX = (1f - optAlpha) * 18f;

                float oy = y + 156f + i * 46f;
                bool isSelected = i == selectedOption;

                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.85f, 1f, 0.18f * optAlpha);
                    GUI.DrawTexture(new Rect(x + 16f + slideX, oy - 4f, w - 32f, 38f), Texture2D.whiteTexture);
                }

                Color baseOptColor = isSelected ? new Color(0.35f, 1f, 0.82f, optAlpha) : new Color(1f, 1f, 1f, optAlpha);
                GUI.color = baseOptColor;
                GUI.Label(new Rect(x + 24f + slideX, oy, w - 48f, 36f),
                    (isSelected ? "▶ " : "   ") + q.options[i],
                    isSelected ? selectedStyle : optionStyle);
            }

            GUI.color = new Color(0.8f, 0.9f, 1f, 0.65f);
            GUI.Label(new Rect(x + 20f, y + h - 34f, w - 40f, 24f),
                "[W/S o D-Pad/Stick] Elegir   |   [ESPACIO / Botón A] Confirmar", optionStyle);
        }
        else
        {
            string resultText = correct ? "✓ ¡Correcto! Comprensión Verificada" : "✗ Incorrecto — Revisa los Puntos Clave";

            GUI.color = correct ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.38f, 0.38f);
            GUI.Label(new Rect(x + 20f, y + 66f, w - 40f, 40f), resultText, resultStyle);

            if (!correct)
            {
                GUI.color = new Color(0.4f, 1f, 0.85f, 0.95f);
                GUI.Label(
                    new Rect(x + 20f, y + 120f, w - 40f, 40f),
                    "✓ Respuesta Correcta: " + q.options[q.correctAnswer],
                    selectedStyle);

                string rawHint = string.IsNullOrEmpty(q.pedagogicalHint)
                    ? "Usa [V] para observar cómo F = k·|q·Q|/r² depende inversamente de r²."
                    : q.pedagogicalHint;

                string formattedHint = formatHintsAsBullets
                    ? FormatExplanationAsBullets(rawHint)
                    : rawHint;

                GUI.color = new Color(1f, 0.92f, 0.4f, 0.98f);
                GUI.Label(new Rect(x + 20f, y + 172f, w - 40f, 160f), "💡 Puntos clave:\n" + formattedHint, hintStyle);
            }
        }
    }

    void DrawSummary(float x, float y, float w, float h)
    {
        bool passed = correctCount >= requiredToPass;

        GUI.color = passed ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.38f, 0.38f);
        GUI.Label(new Rect(x + 20f, y + 12f, w - 40f, 30f),
            passed ? "⚡ FIREWALL SUPERADO — PUERTA DESBLOQUEADA" : "⚡ ACCESO DENEGADO — RE-EXPERIMENTA CON [V]",
            titleStyle);

        GUI.color = Color.white;
        GUI.Label(new Rect(x + 20f, y + 48f, w - 40f, 28f),
            $"Resultado: {correctCount}/{questions.Length} correctas  (mínimo {requiredToPass})",
            questionStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        GUI.DrawTexture(new Rect(x + 20f, y + 82f, w - 40f, 1f), Texture2D.whiteTexture);

        for (int i = 0; i < questionResults.Length; i++)
        {
            float ry = y + 98f + i * 54f;
            bool wasCorrect = questionResults[i];

            GUI.color = wasCorrect ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.4f, 0.4f);

            string shortQ = questions[i].question.Length > 58
                ? questions[i].question.Substring(0, 58) + "..."
                : questions[i].question;

            GUI.Label(new Rect(x + 22f, ry, w - 44f, 28f),
                $"{(wasCorrect ? "✓" : "✗")} P{i + 1}: {shortQ}",
                optionStyle);

            if (!wasCorrect)
            {
                GUI.color = new Color(1f, 0.95f, 0.5f, 0.92f);
                GUI.Label(new Rect(x + 38f, ry + 24f, w - 58f, 26f),
                    $"→ {questions[i].options[questions[i].correctAnswer]}",
                    optionStyle);
            }
        }

        GUI.color = passed ? new Color(0.35f, 1f, 0.45f, 0.95f) : new Color(1f, 0.45f, 0.45f, 0.95f);
        GUI.Label(
            new Rect(x + 20f, y + h - 40f, w - 40f, 28f),
            passed
                ? "[ESPACIO / Botón A] Cruzar Puerta al Siguiente Sector"
                : "[ESPACIO / Botón A] Re-experimentar en el Nivel",
            selectedStyle);
    }

    void InitStyles(int fontHash)
    {
        cachedFontHash = fontHash;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = titleFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        titleStyle.normal.textColor = Color.white;

        questionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = questionFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        questionStyle.normal.textColor = Color.white;

        optionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = optionFontSize,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        optionStyle.normal.textColor = Color.white;

        selectedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = optionFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        selectedStyle.normal.textColor = Color.white;

        resultStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = titleFontSize + 2,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        resultStyle.normal.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = hintFontSize,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        hintStyle.normal.textColor = new Color(1f, 0.92f, 0.4f);
    }

    [ContextMenu("Load Level 0 Tutorial Diagnostic Preset")]
    public void ApplyTutorialBootSequencePreset()
    {
        questions = GetTutorialBootSequenceQuestions();
        requiredToPass = Mathf.CeilToInt(questions.Length / 2f);
        questionResults = new bool[questions.Length];
    }

    public static TrialQuestion[] GetTutorialBootSequenceQuestions()
    {
        return new TrialQuestion[]
        {
            new TrialQuestion
            {
                question = "How do you restore leaking charge before running out?",
                options = new string[]
                {
                    "A) Stand still",
                    "B) Hold [Q], aim, and click a power node",
                    "C) Take damage from spikes"
                },
                correctAnswer = 1,
                pedagogicalHint = "CHARGE DEPLETION: Charge leaks over time. Hold [Q] + Click a power node to absorb energy by conduction."
            },
            new TrialQuestion
            {
                question = "How can you instantly tell THEIA has enough energy to double jump?",
                options = new string[]
                {
                    "A) She stops moving",
                    "B) Character tint/hair glow and charge bar",
                    "C) Game sound turns off"
                },
                correctAnswer = 1,
                pedagogicalHint = "CAPACITOR BOOST: Watch THEIA's hair/aura glow and the bottom charge bar to know when your capacitor is energized."
            },
            new TrialQuestion
            {
                question = "What opens the sector gate at the end of this circuit?",
                options = new string[]
                {
                    "A) Waiting for the timer",
                    "B) Transferring charge to the terminal",
                    "C) Defeating Gandalf"
                },
                correctAnswer = 1,
                pedagogicalHint = "CIRCUIT OVERRIDE: Charge is conserved (Q_total = constant). Transfer your charge into the gate console to unseal."
            }
        };
    }

    private static TrialQuestion[] GetDefaultElectromagnetismQuestions()
    {
        return new TrialQuestion[]
        {
            new TrialQuestion
            {
                question = "Según la Ley de Coulomb, si duplicas la distancia (r -> 2r) entre THEIA y una carga, ¿qué pasa con la fuerza eléctrica?",
                options = new string[]
                {
                    "Se reduce a la cuarta parte (F / 4)",
                    "Se reduce a la mitad (F / 2)",
                    "Se duplica (2F)",
                    "Permanece constante"
                },
                correctAnswer = 0,
                pedagogicalHint = "Fórmula: F = k·|q1·q2| / r². Al duplicar la distancia: (2r)² = 4r². Por tanto, la fuerza cae a F / 4."
            },
            new TrialQuestion
            {
                question = "Al tocar suelo conductor, la carga de THEIA cae según Q(t) = Q₀·e^(-t/RC). ¿Qué mide τ = R·C?",
                options = new string[]
                {
                    "La fuerza máxima de repulsión magnética",
                    "La constante de tiempo capacitiva (caída al 36.8% de Q₀)",
                    "La velocidad máxima de salto de THEIA",
                    "La distancia mínima entre dos cargas"
                },
                correctAnswer = 1,
                pedagogicalHint = "τ = R·C es la constante de tiempo del circuito. Cuanto mayor sea R o C, más lento se descarga THEIA."
            }
        };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
