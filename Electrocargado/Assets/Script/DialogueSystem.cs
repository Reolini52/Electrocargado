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
    public float triggerRadius = 3.5f;
    public bool triggerOnce = false;
    public bool autoAdvance = true;
    public float autoAdvanceTime = 4.5f;

    [Header("Floating Bubble Mode (Level 0 Tutorial / Arcade Style)")]
    [Tooltip("Si está activo, muestra una burbuja flotante en el espacio sobre el nodo sin frenar el juego (Time.timeScale = 1).")]
    public bool floatingWorldBubble = true;

    [Tooltip("Desplazamiento vertical de la burbuja flotante sobre la posición del GameObject.")]
    public Vector2 bubbleWorldOffset = new Vector2(0f, 1.35f);

    [Header("Speaker")]
    public string speakerName = "SISTEMA";
    public Color speakerColor = new Color(0.3f, 0.85f, 1f);

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
    public float typeSpeed = 0.025f;
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
            hasTriggered = true;
        }
        else if (floatingWorldBubble && dist > triggerRadius * 1.35f)
        {
            // En modo burbuja flotante, se oculta al alejarse y permite volver a activarse al acercarse de nuevo
            isShowing = false;
            hasTriggered = false;
            return;
        }

        if (!isShowing) return;

        UpdateTypewriter();

        if (autoAdvance)
        {
            autoTimer -= Time.unscaledDeltaTime;
            if (autoTimer <= 0f)
            {
                // Si es burbuja flotante de 1 sola línea y el jugador sigue cerca, mantenerla visible mientras esté en el radio
                if (floatingWorldBubble && activeSlides != null && currentLine >= activeSlides.Length - 1 && dist <= triggerRadius * 1.15f)
                {
                    autoTimer = 1.0f;
                }
                else
                {
                    AdvanceLine();
                }
            }
        }

        bool advancePressed =
            !floatingWorldBubble &&
            ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
             (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame));

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
        autoTimer = autoAdvanceTime;
        if (!floatingWorldBubble)
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
        if (GandalfNPC.isGandalfActive || GameEvents.IsCleanHudHidden) return;
        if (textStyle == null || cachedFontSize != dialogueFontSize) InitStyles();

        if (floatingWorldBubble && Camera.main != null)
        {
            Vector3 worldTarget = transform.position + new Vector3(bubbleWorldOffset.x, bubbleWorldOffset.y, 0f);
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldTarget);
            if (screenPos.z > 0f)
            {
                float bw = 410f;
                string fullCurrentSlide = (currentLine < activeSlides.Length) ? activeSlides[currentLine] : displayedText;
                float textHeight = textStyle.CalcHeight(new GUIContent(fullCurrentSlide), bw - 24f);
                float bh = Mathf.Max(78f, textHeight + 38f);
                float bx = Mathf.Clamp(screenPos.x - bw * 0.5f, 12f, Screen.width - bw - 12f);
                float by = Mathf.Clamp((Screen.height - screenPos.y) - bh * 0.5f, 12f, Screen.height - bh - 12f);

                GUI.color = new Color(0.03f, 0.05f, 0.15f, 0.94f);
                GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);

                GUI.color = speakerColor;
                GUI.DrawTexture(new Rect(bx, by, bw, 3f), Texture2D.whiteTexture);
                GUI.Label(new Rect(bx + 12f, by + 5f, bw - 24f, 20f), $"⚡ {speakerName}", nameStyle);

                GUI.color = Color.white;
                if (currentLine < activeSlides.Length)
                {
                    GUI.Label(new Rect(bx + 12f, by + 26f, bw - 24f, bh - 30f), displayedText, textStyle);
                }
                return;
            }
        }

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
