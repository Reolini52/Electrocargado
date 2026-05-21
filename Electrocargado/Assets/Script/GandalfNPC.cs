using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GandalfNPC : MonoBehaviour
{
    [Header("Settings")]
    public float triggerRadius = 3f;
    public string nextSceneName = "Level2";

    [Header("Questions")]
    [TextArea(2, 4)]
    public string question = "Two charges with the same sign are placed near each other. What happens?";
    public string[] options = {
        "A) They attract",
        "B) They repel",
        "C) Nothing happens",
        "D) They neutralize"
    };
    public int correctAnswer = 1; // index of correct option (B = index 1)

    [Header("Punishment")]
    public int failDamage = 1;
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;

    private Transform player;
    private PlayerHealth playerHealth;
    private bool isActive = false;
    private bool hasTriggered = false;
    private bool trialComplete = false;

    private int selectedOption = 0;
    private bool answered = false;
    private bool correct = false;
    private float resultTimer = 0f;
    private float resultDisplayTime = 2f;

    private GUIStyle titleStyle;
    private GUIStyle optionStyle;
    private GUIStyle selectedStyle;
    private GUIStyle correctStyle;
    private GUIStyle wrongStyle;
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
            Time.timeScale = 0f;
        }

        if (!isActive || answered) return;

        // Navigate options
        if (Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame)
            selectedOption = Mathf.Max(0, selectedOption - 1);

        if (Keyboard.current.sKey.wasPressedThisFrame ||
            Keyboard.current.downArrowKey.wasPressedThisFrame)
            selectedOption = Mathf.Min(options.Length - 1, selectedOption + 1);

        if (Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame)
            SubmitAnswer();

        // Result timer
        if (answered)
        {
            resultTimer -= Time.unscaledDeltaTime;
            if (resultTimer <= 0f)
                FinishTrial();
        }
    }

    void SubmitAnswer()
    {
        answered = true;
        correct = selectedOption == correctAnswer;
        resultTimer = resultDisplayTime;
        Time.timeScale = 1f;

        if (!correct)
        {
            // Punishment
            playerHealth?.TakeDamage(failDamage);

            // Spawn enemies if assigned
            foreach (Transform spawn in spawnPoints)
            {
                if (enemyPrefabs.Length > 0)
                {
                    int idx = Random.Range(0, enemyPrefabs.Length);
                    if (enemyPrefabs[idx] != null)
                        Instantiate(enemyPrefabs[idx], spawn.position,
                            Quaternion.identity);
                }
            }
        }
    }

    void FinishTrial()
    {
        if (correct)
        {
            trialComplete = true;
            isActive = false;
            // Load next level
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            // Reset for retry
            answered = false;
            selectedOption = 0;
            hasTriggered = false;
            isActive = false;
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

        float w = 500f;
        float h = 320f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height / 2f - h / 2f;

        // Panel
        GUI.color = new Color(0.05f, 0.05f, 0.2f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // Top border
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

        // Title
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(x + 20, y + 12, w - 40, 24),
            "⚡ FIREWALL CHECKPOINT", titleStyle);

        // Divider
        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(new Rect(x + 20, y + 42, w - 40, 1),
            Texture2D.whiteTexture);

        if (!answered)
        {
            // Question
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 20, y + 52, w - 40, 60),
                question, optionStyle);

            // Options
            for (int i = 0; i < options.Length; i++)
            {
                float oy = y + 125f + i * 40f;
                bool isSelected = i == selectedOption;

                // Highlight selected
                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.8f, 1f, 0.2f);
                    GUI.DrawTexture(new Rect(x + 15, oy - 2, w - 30, 34),
                        Texture2D.whiteTexture);
                }

                GUI.color = isSelected ?
                    new Color(0.3f, 1f, 0.8f) : Color.white;
                GUI.Label(new Rect(x + 25, oy, w - 50, 30),
                    (isSelected ? "▶ " : "   ") + options[i],
                    isSelected ? selectedStyle : optionStyle);
            }

            // Controls hint
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(new Rect(x + 20, y + h - 28, w - 40, 20),
                "[W/S] Navigate   [SPACE] Confirm", optionStyle);
        }
        else
        {
            // Result screen
            string resultText = correct ?
                "✓ CORRECT! Access granted." :
                "✗ WRONG! The firewall holds.";

            GUI.color = correct ?
                new Color(0.3f, 1f, 0.3f) :
                new Color(1f, 0.3f, 0.3f);

            GUI.Label(new Rect(x + 20, y + h / 2f - 20, w - 40, 40),
                resultText, resultStyle);

            if (!correct)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(x + 20, y + h / 2f + 20, w - 40, 24),
                    $"Correct answer: {options[correctAnswer]}", optionStyle);
            }
        }
    }

    void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        optionStyle = new GUIStyle(GUI.skin.label);
        optionStyle.fontSize = 13;
        optionStyle.wordWrap = true;
        optionStyle.normal.textColor = Color.white;

        selectedStyle = new GUIStyle(GUI.skin.label);
        selectedStyle.fontSize = 13;
        selectedStyle.fontStyle = FontStyle.Bold;
        selectedStyle.normal.textColor = Color.white;

        resultStyle = new GUIStyle(GUI.skin.label);
        resultStyle.fontSize = 18;
        resultStyle.fontStyle = FontStyle.Bold;
        resultStyle.alignment = TextAnchor.MiddleCenter;
        resultStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}