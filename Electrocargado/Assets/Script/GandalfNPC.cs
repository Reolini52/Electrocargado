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

    private GUIStyle titleStyle;
    private GUIStyle optionStyle;
    private GUIStyle selectedStyle;
    private GUIStyle resultStyle;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }
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
            selectedOption = 0;
            answered = false;
            Time.timeScale = 0f;
        }

        if (!isActive) return;

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
                FinishTrial();
        }
    }

    void SubmitAnswer()
    {
        answered = true;
        correct = selectedOption == questions[currentQuestion].correctAnswer;
        resultTimer = resultDisplayTime;
        Time.timeScale = 1f;

        if (!correct)
        {
            playerHealth?.TakeDamage(failDamage);

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
        }
    }

    void FinishTrial()
    {
        if (correct)
        {
            currentQuestion++;

            if (currentQuestion >= questions.Length)
            {
                // All questions passed
                trialComplete = true;
                isActive = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                // Next question
                answered = false;
                selectedOption = 0;
                resultTimer = 0f;
                Time.timeScale = 0f;
            }
        }
        else
        {
            // Restart from question 1
            answered = false;
            selectedOption = 0;
            currentQuestion = 0;
            Time.timeScale = 0f;
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

        // Panel above player
        Vector3 screenPos = Camera.main.WorldToScreenPoint(
            player != null ? player.position : Vector3.zero);
        screenPos.y = Screen.height - screenPos.y;

        float w = 400f;
        float h = 280f;
        float x = Mathf.Clamp(screenPos.x - w / 2f,
            10f, Screen.width - w - 10f);
        float y = Mathf.Clamp(screenPos.y - h - 80f,
            10f, Screen.height - h - 10f);

        // Panel background
        GUI.color = new Color(0.05f, 0.05f, 0.2f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // Top border
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

        // Title with question number
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(x + 15, y + 10, w - 30, 22),
            $"⚡ FIREWALL — Pregunta {currentQuestion + 1}/{questions.Length}",
            titleStyle);

        // Divider
        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(new Rect(x + 15, y + 38, w - 30, 1),
            Texture2D.whiteTexture);

        if (!answered)
        {
            // Question text
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 15, y + 46, w - 30, 70),
                questions[currentQuestion].question, optionStyle);

            // Options
            for (int i = 0;
                i < questions[currentQuestion].options.Length; i++)
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

            // Controls hint
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(new Rect(x + 15, y + h - 24, w - 30, 18),
                "[W/S] Navegar   [SPACE] Confirmar", optionStyle);
        }
        else
        {
            // Result
            string resultText = correct ?
                "✓ CORRECTO! Acceso concedido." :
                "✗ INCORRECTO. El firewall resiste.";

            GUI.color = correct ?
                new Color(0.3f, 1f, 0.3f) :
                new Color(1f, 0.3f, 0.3f);

            GUI.Label(
                new Rect(x + 15, y + h / 2f - 30, w - 30, 40),
                resultText, resultStyle);

            if (!correct && currentQuestion < questions.Length)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(
                    new Rect(x + 15, y + h / 2f + 15, w - 30, 24),
                    "Respuesta correcta: " +
                    questions[currentQuestion]
                        .options[questions[currentQuestion].correctAnswer],
                    optionStyle);
            }
        }
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
        resultStyle.fontSize = 16;
        resultStyle.fontStyle = FontStyle.Bold;
        resultStyle.alignment = TextAnchor.MiddleCenter;
        resultStyle.wordWrap = true;
        resultStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}