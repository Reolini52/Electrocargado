using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueSystem : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(3, 6)]
    public string[] lines;
    public float triggerRadius = 3f;
    public bool triggerOnce = true;
    public bool autoAdvance = false;
    public float autoAdvanceTime = 3f;

    [Header("Speaker")]
    public string speakerName = "SYSTEM";
    public Color speakerColor = new Color(0.3f, 0.8f, 1f);

    [Header("Typewriter")]
    public float typeSpeed = 0.04f;
    public AudioClip typeSound;

    private int currentLine = 0;
    private bool isShowing = false;
    private bool hasTriggered = false;
    private float autoTimer = 0f;

    private string displayedText = "";
    private float typeTimer = 0f;
    private int displayedChars = 0;

    private Transform player;
    private AudioSource audioSource;

    private GUIStyle textStyle;
    private GUIStyle nameStyle;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (typeSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist < triggerRadius && !hasTriggered)
        {
            StartDialogue();
            if (triggerOnce) hasTriggered = true;
        }

        if (!isShowing) return;

        UpdateTypewriter();

        if (autoAdvance)
        {
            autoTimer -= Time.unscaledDeltaTime;
            if (autoTimer <= 0f) AdvanceLine();
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            AdvanceLine();
    }

    void UpdateTypewriter()
    {
        if (currentLine >= lines.Length) return;
        string fullText = lines[currentLine];

        if (displayedChars < fullText.Length)
        {
            typeTimer -= Time.unscaledDeltaTime;
            if (typeTimer <= 0f)
            {
                displayedChars++;
                displayedText = fullText.Substring(0, displayedChars);
                typeTimer = typeSpeed;

                if (typeSound != null && audioSource != null)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(typeSound, 0.3f);
                }
            }
        }
    }

    void StartDialogue()
    {
        currentLine = 0;
        isShowing = true;
        displayedText = "";
        displayedChars = 0;
        typeTimer = typeSpeed;
        Time.timeScale = 0.3f;
    }

    void AdvanceLine()
    {
        if (currentLine < lines.Length &&
            displayedChars < lines[currentLine].Length)
        {
            displayedChars = lines[currentLine].Length;
            displayedText = lines[currentLine];
            return;
        }

        currentLine++;
        displayedText = "";
        displayedChars = 0;
        typeTimer = typeSpeed;
        autoTimer = autoAdvanceTime;

        if (currentLine >= lines.Length)
        {
            isShowing = false;
            Time.timeScale = 1f;
        }
    }

    void OnGUI()
    {
        if (!isShowing) return;
        if (textStyle == null) InitStyles();

        float w = Screen.width * 0.55f;
        float h = 70f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height - h - 150f;

        GUI.color = new Color(0.05f, 0.05f, 0.15f, 0.92f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = speakerColor;
        GUI.DrawTexture(new Rect(x, y, w, 2), Texture2D.whiteTexture);

        GUI.color = speakerColor;
        GUI.Label(new Rect(x + 12, y + 6, w, 18), speakerName, nameStyle);

        GUI.color = Color.white;
        if (currentLine < lines.Length)
            GUI.Label(new Rect(x + 12, y + 26, w - 24, h - 32),
                displayedText, textStyle);

        if (currentLine < lines.Length &&
            displayedChars >= lines[currentLine].Length)
        {
            GUI.color = new Color(1f, 1f, 1f,
                Mathf.PingPong(Time.unscaledTime * 2f, 1f));
            GUI.Label(new Rect(x + w - 130, y + h - 20, 120, 16),
                "[SPACE] Continue", textStyle);
        }
    }

    void InitStyles()
    {
        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 13;
        textStyle.wordWrap = true;
        textStyle.normal.textColor = Color.white;

        nameStyle = new GUIStyle(GUI.skin.label);
        nameStyle.fontSize = 13;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}