using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[System.Serializable]
public class TrialQuestion
{
    [Tooltip("Enunciado conceptual de electromagnetismo para evaluar al jugador.")]
    [TextArea(2, 4)]
    public string question;

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
/// Conectado a GameEvents.cs y equipado con pistas matemáticas pedagógicas al fallar.
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

    [Header("Banco de Preguntas Conceptuales")]
    public TrialQuestion[] questions;

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

    private int currentQuestion = 0;
    private int selectedOption = 0;
    private bool answered = false;
    private bool correct = false;
    private float resultTimer = 0f;
    private float resultDisplayTime = 2.6f;

    private int correctCount = 0;
    private int requiredToPass = 0;

    private bool showingSummary = false;
    private bool[] questionResults;

    private GUIStyle titleStyle;
    private GUIStyle optionStyle;
    private GUIStyle selectedStyle;
    private GUIStyle resultStyle;
    private GUIStyle hintStyle;

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
    }

    protected virtual void Update()
    {
        if (player == null || trialComplete) return;

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
            questionResults = new bool[questions.Length];
            Time.timeScale = 0f;

            GameEvents.RaiseFirewallTriggered(true);
        }

        if (!isActive || Keyboard.current == null) return;

        if (showingSummary)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
                FinishSummary();
            return;
        }

        if (!answered)
        {
            if (Keyboard.current.wKey.wasPressedThisFrame ||
                Keyboard.current.upArrowKey.wasPressedThisFrame)
                selectedOption = Mathf.Max(0, selectedOption - 1);

            if (Keyboard.current.sKey.wasPressedThisFrame ||
                Keyboard.current.downArrowKey.wasPressedThisFrame)
                selectedOption = Mathf.Min(
                    questions[currentQuestion].options.Length - 1,
                    selectedOption + 1);

            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
                SubmitAnswer();
        }
        else
        {
            resultTimer -= Time.unscaledDeltaTime;
            if (resultTimer <= 0f)
                AdvanceQuestion();
        }
    }

    void SubmitAnswer()
    {
        answered = true;
        TrialQuestion q = questions[currentQuestion];
        correct = selectedOption == q.correctAnswer;
        questionResults[currentQuestion] = correct;
        resultTimer = resultDisplayTime;
        Time.timeScale = 1f;

        string hintMsg = string.IsNullOrEmpty(q.pedagogicalHint)
            ? $"En Ley de Coulomb (F = k·|q1·q2|/r²) y descarga RC (Q(t)=Q0·e^(-t/RC)), revisa cómo varían las magnitudes."
            : q.pedagogicalHint;

        if (correct)
            correctCount++;
        else
            playerHealth?.TakeDamage(failDamage);

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
                string targetScene = nextSceneName == "Level2" ? "Level 2" :
                                     (nextSceneName == "Level3" || nextSceneName == "Level 3") ? "Level 4" :
                                     nextSceneName == "Level4" ? "Level 4" : nextSceneName;
                SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            int missing = requiredToPass - correctCount;
            for (int i = 0; i < missing; i++)
                playerHealth?.TakeDamage(failDamage);

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
        if (!isActive) return;
        if (titleStyle == null) InitStyles();

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float w = 460f;
        float h = showingSummary ? 340f : 310f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height / 2f - h / 2f;

        GUI.color = new Color(0.05f, 0.05f, 0.2f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

        if (showingSummary)
        {
            DrawSummary(x, y, w, h);
            return;
        }

        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(x + 15, y + 10, w - 30, 22),
            $"⚡ FIREWALL GANDALF — Pregunta {currentQuestion + 1}/{questions.Length}  |  ✓ {correctCount}/{requiredToPass} requeridas",
            titleStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(new Rect(x + 15, y + 38, w - 30, 1), Texture2D.whiteTexture);

        TrialQuestion q = questions[currentQuestion];

        if (!answered)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 15, y + 46, w - 30, 70), q.question, optionStyle);

            for (int i = 0; i < q.options.Length; i++)
            {
                float oy = y + 125f + i * 36f;
                bool isSelected = i == selectedOption;

                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.8f, 1f, 0.15f);
                    GUI.DrawTexture(new Rect(x + 12, oy - 2, w - 24, 30), Texture2D.whiteTexture);
                }

                GUI.color = isSelected ? new Color(0.3f, 1f, 0.8f) : Color.white;
                GUI.Label(new Rect(x + 20, oy, w - 40, 28),
                    (isSelected ? "▶ " : "   ") + q.options[i],
                    isSelected ? selectedStyle : optionStyle);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(x + 15, y + h - 26, w - 30, 18),
                "[W/S o ↑/↓] Navegar   [SPACE / ENTER] Confirmar", optionStyle);
        }
        else
        {
            string resultText = correct ? "✓ ¡Correcto! Comprensión Verificada" : "✗ Incorrecto — Revisa el Modelo Físico";

            GUI.color = correct ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
            GUI.Label(new Rect(x + 15, y + 70f, w - 30, 36), resultText, resultStyle);

            if (!correct)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.Label(
                    new Rect(x + 15, y + 120f, w - 30, 28),
                    "Respuesta Correcta: " + q.options[q.correctAnswer],
                    selectedStyle);

                string hint = string.IsNullOrEmpty(q.pedagogicalHint)
                    ? "Pista Matemática: Usa [V] (Debug Overlay) para observar cómo F = k·|q·Q|/r² depende inversamente de r²."
                    : "💡 Pista Matemática: " + q.pedagogicalHint;

                GUI.color = new Color(1f, 0.9f, 0.35f, 0.95f);
                GUI.Label(new Rect(x + 15, y + 160f, w - 30, 70), hint, hintStyle);
            }
        }
    }

    void DrawSummary(float x, float y, float w, float h)
    {
        bool passed = correctCount >= requiredToPass;

        GUI.color = passed ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
        GUI.Label(new Rect(x + 15, y + 10, w - 30, 24),
            passed ? "⚡ FIREWALL SUPERADO — PUERTA DESBLOQUEADA" : "⚡ ACCESO DENEGADO — RE-EXPERIMENTA CON [V]",
            titleStyle);

        GUI.color = Color.white;
        GUI.Label(new Rect(x + 15, y + 40, w - 30, 22),
            $"Resultado: {correctCount}/{questions.Length} correctas  (mínimo {requiredToPass})",
            optionStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(new Rect(x + 15, y + 68, w - 30, 1), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(x + 15, y + 78, w - 30, 18), "Resumen de evaluación conceptual:", optionStyle);

        for (int i = 0; i < questionResults.Length; i++)
        {
            float ry = y + 100f + i * 30f;
            bool wasCorrect = questionResults[i];

            GUI.color = wasCorrect ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);

            string shortQ = questions[i].question.Length > 52
                ? questions[i].question.Substring(0, 52) + "..."
                : questions[i].question;

            GUI.Label(new Rect(x + 20, ry, w - 40, 24),
                $"{(wasCorrect ? "✓" : "✗")} P{i + 1}: {shortQ}",
                optionStyle);

            if (!wasCorrect)
            {
                GUI.color = new Color(1f, 1f, 0.5f, 0.85f);
                GUI.Label(new Rect(x + 35, ry + 15, w - 50, 18),
                    $"→ {questions[i].options[questions[i].correctAnswer]}",
                    optionStyle);
            }
        }

        GUI.color = passed ? new Color(0.3f, 1f, 0.3f, 0.9f) : new Color(1f, 0.4f, 0.4f, 0.9f);
        GUI.Label(
            new Rect(x + 15, y + h - 32, w - 30, 24),
            passed
                ? "[SPACE] Cruzar Puerta al Siguiente Sector"
                : "[SPACE] Re-experimentar en el Nivel (Usa [V] para ver las ecuaciones)",
            optionStyle);
    }

    void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;

        optionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true
        };
        optionStyle.normal.textColor = Color.white;

        selectedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        selectedStyle.normal.textColor = Color.white;

        resultStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        resultStyle.normal.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
        hintStyle.normal.textColor = new Color(1f, 0.92f, 0.4f);
    }

    private static TrialQuestion[] GetDefaultElectromagnetismQuestions()
    {
        return new TrialQuestion[]
        {
            new TrialQuestion
            {
                question = "Según la Ley de Coulomb, si duplicas la distancia (r -> 2r) entre THEIA y una carga fuente, ¿qué ocurre con la magnitud de la fuerza eléctrica?",
                options = new string[]
                {
                    "Se reduce a la cuarta parte (F / 4)",
                    "Se reduce a la mitad (F / 2)",
                    "Se duplica (2F)",
                    "Permanece constante"
                },
                correctAnswer = 0,
                pedagogicalHint = "Como F = k·|q1·q2| / r², al sustituir (2r)² = 4r² en el denominador, la fuerza se divide entre 4."
            },
            new TrialQuestion
            {
                question = "Cuando THEIA toca una plataforma conductora conectada a tierra, su carga Q(t) disminuye siguiendo Q(t) = Q₀·e^(-t/RC). ¿Qué representa τ = R·C?",
                options = new string[]
                {
                    "La fuerza máxima de repulsión magnética",
                    "La constante de tiempo capacitiva (tiempo en caer al 36.8% de Q₀)",
                    "La velocidad máxima de salto de THEIA",
                    "La distancia mínima entre dos cargas"
                },
                correctAnswer = 1,
                pedagogicalHint = "En un circuito RC, τ = R·C es la constante de tiempo de relajación exponencial del capacitor."
            }
        };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}