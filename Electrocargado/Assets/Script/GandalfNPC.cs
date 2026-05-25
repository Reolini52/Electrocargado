using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[System.Serializable]
public class TrialQuestion
{
    [TextArea(2, 4)]
    public string question;
    public string[] options = new string[4];
    public int correctAnswer = 0;
}

public class GandalfNPC : MonoBehaviour
{
    [Header("Settings")]
    public float triggerRadius = 3f;
    public string nextSceneName = "Level2";

    [Header("Questions")]
    public TrialQuestion[] questions;

    [Header("Punishment")]
    public int failDamage = 1;
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;

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
    private float resultDisplayTime = 2f;

    private int correctCount = 0;
    private int requiredToPass = 0;

    // Summary screen
    private bool showingSummary = false;
    private bool[] questionResults;

    private GUIStyle titleStyle;
    private GUIStyle optionStyle;
    private GUIStyle selectedStyle;
    private GUIStyle resultStyle;
    private GUIStyle summaryStyle;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        requiredToPass = Mathf.CeilToInt(questions.Length / 2f);
        questionResults = new bool[questions.Length];
    }

    void Update()
    {
        if (player == null || trialComplete) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist < triggerRadius && !hasTriggered)
        {
            hasTriggered = true;
            isActive = true;
            currentQuestion = 0;
            correctCount = 0;
            selectedOption = 0;
            answered = false;
            showingSummary = false;
            questionResults = new bool[questions.Length];
            Time.timeScale = 0f;
        }

        if (!isActive) return;

        // Summary screen input
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
        correct = selectedOption == questions[currentQuestion].correctAnswer;
        questionResults[currentQuestion] = correct;
        resultTimer = resultDisplayTime;
        Time.timeScale = 1f;

        if (correct)
            correctCount++;
        else
            playerHealth?.TakeDamage(failDamage);
    }

    void AdvanceQuestion()
    {
        currentQuestion++;

        if (currentQuestion >= questions.Length)
        {
            // All questions done — show summary
            showingSummary = true;
            Time.timeScale = 0f;
            return;
        }

        // Next question
        answered = false;
        selectedOption = 0;
        resultTimer = 0f;
        Time.timeScale = 0f;
    }

    void FinishSummary()
    {
        bool passed = correctCount >= requiredToPass;

        if (passed)
        {
            trialComplete = true;
            isActive = false;
            showingSummary = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            // Daño por preguntas falladas
            int missing = requiredToPass - correctCount;
            for (int i = 0; i < missing; i++)
                playerHealth?.TakeDamage(failDamage);

            // Spawn enemigos
            foreach (Transform spawn in spawnPoints)
            {
                if (enemyPrefabs.Length > 0)
                {
                    int idx = Random.Range(0, enemyPrefabs.Length);
                    if (enemyPrefabs[idx] != null)
                        Instantiate(enemyPrefabs[idx],
                            spawn.position, Quaternion.identity);
                }
            }

            // Cerrar cuestionario — vuelve al juego frente a Gandalf
            isActive = false;
            showingSummary = false;
            hasTriggered = false; // permite volver a triggear
            currentQuestion = 0;
            correctCount = 0;
            selectedOption = 0;
            answered = false;
            questionResults = new bool[questions.Length];
            Time.timeScale = 1f; // devuelve control al jugador
        }
    }

    void OnGUI()
    {
        if (!isActive) return;
        if (titleStyle == null) InitStyles();

        // Dim background
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            Texture2D.whiteTexture);

        float w = 420f;
        float h = showingSummary ? 320f : 280f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height / 2f - h / 2f;

        // Panel
        GUI.color = new Color(0.05f, 0.05f, 0.2f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // Top border
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

        if (showingSummary)
        {
            DrawSummary(x, y, w, h);
            return;
        }

        // Title
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(x + 15, y + 10, w - 30, 22),
            $"⚡ FIREWALL — Pregunta {currentQuestion + 1}/{questions.Length}  |  ✓ {correctCount}/{requiredToPass} requeridas",
            titleStyle);

        // Divider
        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(new Rect(x + 15, y + 38, w - 30, 1),
            Texture2D.whiteTexture);

        if (!answered)
        {
            // Question
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 15, y + 46, w - 30, 70),
                questions[currentQuestion].question, optionStyle);

            // Options
            for (int i = 0; i < questions[currentQuestion].options.Length; i++)
            {
                float oy = y + 125f + i * 36f;
                bool isSelected = i == selectedOption;

                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.8f, 1f, 0.15f);
                    GUI.DrawTexture(new Rect(x + 12, oy - 2, w - 24, 30),
                        Texture2D.whiteTexture);
                }

                GUI.color = isSelected ?
                    new Color(0.3f, 1f, 0.8f) : Color.white;
                GUI.Label(new Rect(x + 20, oy, w - 40, 28),
                    (isSelected ? "▶ " : "   ") +
                    questions[currentQuestion].options[i],
                    isSelected ? selectedStyle : optionStyle);
            }

            // Hint
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(new Rect(x + 15, y + h - 24, w - 30, 18),
                "[W/S] Navegar   [SPACE] Confirmar", optionStyle);
        }
        else
        {
            // Result per question
            string resultText = correct ?
                "✓ ¡Correcto!" : "✗ Incorrecto";

            GUI.color = correct ?
                new Color(0.3f, 1f, 0.3f) :
                new Color(1f, 0.3f, 0.3f);

            GUI.Label(new Rect(x + 15, y + h / 2f - 30, w - 30, 40),
                resultText, resultStyle);

            if (!correct)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(
                    new Rect(x + 15, y + h / 2f + 15, w - 30, 24),
                    "Correcta: " +
                    questions[currentQuestion]
                        .options[questions[currentQuestion].correctAnswer],
                    optionStyle);
            }
        }
    }

    void DrawSummary(float x, float y, float w, float h)
    {
        bool passed = correctCount >= requiredToPass;

        // Title
        GUI.color = passed ?
            new Color(0.3f, 1f, 0.3f) :
            new Color(1f, 0.3f, 0.3f);

        GUI.Label(new Rect(x + 15, y + 10, w - 30, 24),
            passed ? "⚡ FIREWALL SUPERADO" : "⚡ ACCESO DENEGADO",
            titleStyle);

        // Score
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 15, y + 40, w - 30, 22),
            $"Resultado: {correctCount}/{questions.Length} correctas  (mínimo {requiredToPass})",
            optionStyle);

        // Divider
        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(new Rect(x + 15, y + 68, w - 30, 1),
            Texture2D.whiteTexture);

        // Question results
        GUI.Label(new Rect(x + 15, y + 78, w - 30, 18),
            "Resumen de respuestas:", optionStyle);

        for (int i = 0; i < questionResults.Length; i++)
        {
            float ry = y + 100f + i * 28f;
            bool wasCorrect = questionResults[i];

            GUI.color = wasCorrect ?
                new Color(0.3f, 1f, 0.3f) :
                new Color(1f, 0.3f, 0.3f);

            // Show short version of question
            string shortQ = questions[i].question.Length > 50 ?
                questions[i].question.Substring(0, 50) + "..." :
                questions[i].question;

            GUI.Label(new Rect(x + 20, ry, w - 40, 24),
                $"{(wasCorrect ? "✓" : "✗")} P{i + 1}: {shortQ}",
                optionStyle);

            // Show correct answer if wrong
            if (!wasCorrect)
            {
                GUI.color = new Color(1f, 1f, 0.5f, 0.8f);
                GUI.Label(new Rect(x + 35, ry + 14, w - 50, 16),
                    $"→ {questions[i].options[questions[i].correctAnswer]}",
                    optionStyle);
            }
        }

        // Continue hint
        GUI.color = passed ?
            new Color(0.3f, 1f, 0.3f, 0.8f) :
            new Color(1f, 0.4f, 0.4f, 0.8f);

        GUI.Label(
            new Rect(x + 15, y + h - 30, w - 30, 24),
            passed ?
                "[SPACE] Continuar al siguiente nivel" :
                "[SPACE] Reintentar — se aplicará penalización",
            optionStyle);
    }

    void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 13;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        optionStyle = new GUIStyle(GUI.skin.label);
        optionStyle.fontSize = 12;
        optionStyle.wordWrap = true;
        optionStyle.normal.textColor = Color.white;

        selectedStyle = new GUIStyle(GUI.skin.label);
        selectedStyle.fontSize = 12;
        selectedStyle.fontStyle = FontStyle.Bold;
        selectedStyle.normal.textColor = Color.white;

        resultStyle = new GUIStyle(GUI.skin.label);
        resultStyle.fontSize = 18;
        resultStyle.fontStyle = FontStyle.Bold;
        resultStyle.alignment = TextAnchor.MiddleCenter;
        resultStyle.wordWrap = true;
        resultStyle.normal.textColor = Color.white;

        summaryStyle = new GUIStyle(GUI.skin.label);
        summaryStyle.fontSize = 13;
        summaryStyle.wordWrap = true;
        summaryStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}