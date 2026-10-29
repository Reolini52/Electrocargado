using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Sistema de diálogo en el nivel con soporte de accesibilidad (fuente grande configurable,
/// fragmentación automática de textos largos en fragmentos cortos y soporte para Gamepad).
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(3, 6)]
    public string[] lines;
    public float triggerRadius = 3f;
    public bool triggerOnce = true;
    public bool autoAdvance = false;
    public float autoAdvanceTime = 3.5f;

    [Header("Speaker")]
    public string speakerName = "SYSTEM";
    public Color speakerColor = new Color(0.3f, 0.8f, 1f);

    [Header("Accesibilidad y Tipografía (Inspector)")]
    [Tooltip("Tamaño de fuente del texto de diálogo.")]
    [SerializeField, Range(14, 34)]
    private int dialogueFontSize = 18;

    [Tooltip("Tamaño de fuente del nombre del emisor.")]
    [SerializeField, Range(14, 34)]
    private int speakerFontSize = 17;

    [Tooltip("Máximo de caracteres por fragmento. Si una línea es muy larga, se divide automáticamente en varias diapositivas cortas.")]
    [SerializeField, Range(70, 220)]
    private int maxCharsPerSlide = 135;

    [Tooltip("Opcional: Referencia a un componente TextMeshPro si se usa Canvas UI.")]
    [SerializeField]
    private TMP_Text tmpDialogueText;

    [Header("Typewriter")]
    public float typeSpeed = 0.03f;
    public AudioClip typeSound;

    private string[] activeSlides;
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
    private int cachedFontSize = -1;

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

        RebuildShortSlides();
    }

    private void RebuildShortSlides()
    {
        if (lines == null || lines.Length == 0)
        {
            activeSlides = new string[0];
            return;
        }

        List<string> slides = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            if (lines[i].Length <= maxCharsPerSlide)
            {
                slides.Add(lines[i].Trim());
            }
            else
            {
                string[] chunks = Billboard.SplitIntoShortFragments(lines[i], maxCharsPerSlide, asBullets: false);
                slides.AddRange(chunks);
            }
        }

        activeSlides = slides.ToArray();
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

        bool advancePressed =
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (advancePressed)
            AdvanceLine();
    }

    void UpdateTypewriter()
    {
        if (activeSlides == null || currentLine >= activeSlides.Length) return;
        string fullText = activeSlides[currentLine];

        if (displayedChars < fullText.Length)
        {
            typeTimer -= Time.unscaledDeltaTime;
            if (typeTimer <= 0f)
            {
                displayedChars++;
                displayedText = fullText.Substring(0, displayedChars);
                typeTimer = typeSpeed;

                if (tmpDialogueText != null)
                {
                    tmpDialogueText.fontSize = dialogueFontSize;
                    tmpDialogueText.text = displayedText;
                }

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
        RebuildShortSlides();
        if (activeSlides == null || activeSlides.Length == 0) return;

        currentLine = 0;
        isShowing = true;
        displayedText = "";
        displayedChars = 0;
        typeTimer = typeSpeed;
        Time.timeScale = 0.3f;
    }

    void AdvanceLine()
    {
        if (activeSlides == null) return;

        if (currentLine < activeSlides.Length &&
            displayedChars < activeSlides[currentLine].Length)
        {
            displayedChars = activeSlides[currentLine].Length;
            displayedText = activeSlides[currentLine];
            if (tmpDialogueText != null) tmpDialogueText.text = displayedText;
            return;
        }

        currentLine++;
        displayedText = "";
        displayedChars = 0;
        typeTimer = typeSpeed;
        autoTimer = autoAdvanceTime;

        if (currentLine >= activeSlides.Length)
        {
            isShowing = false;
            Time.timeScale = 1f;
        }
    }

    void OnGUI()
    {
        if (!isShowing || activeSlides == null || activeSlides.Length == 0) return;
        if (textStyle == null || cachedFontSize != dialogueFontSize) InitStyles();

        float w = Mathf.Min(Screen.width * 0.68f, 760f);
        float h = 110f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height - h - 110f;

        GUI.color = new Color(0.04f, 0.05f, 0.16f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = speakerColor;
        GUI.DrawTexture(new Rect(x, y, w, 2.5f), Texture2D.whiteTexture);

        GUI.color = speakerColor;
        GUI.Label(new Rect(x + 16f, y + 8f, w - 32f, 24f),
            $"{speakerName}  ({currentLine + 1}/{activeSlides.Length})", nameStyle);

        GUI.color = Color.white;
        if (currentLine < activeSlides.Length)
        {
            GUI.Label(new Rect(x + 16f, y + 34f, w - 32f, h - 54f),
                displayedText, textStyle);
        }

        if (currentLine < activeSlides.Length &&
            displayedChars >= activeSlides[currentLine].Length)
        {
            GUI.color = new Color(0.5f, 0.95f, 1f,
                Mathf.PingPong(Time.unscaledTime * 2f, 1f));
            GUI.Label(new Rect(x + w - 230f, y + h - 26f, 215f, 22f),
                "[ESPACIO / Botón A] Continuar", textStyle);
        }
    }

    void InitStyles()
    {
        cachedFontSize = dialogueFontSize;

        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = dialogueFontSize,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        textStyle.normal.textColor = Color.white;

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = speakerFontSize,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Overflow
        };
        nameStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
