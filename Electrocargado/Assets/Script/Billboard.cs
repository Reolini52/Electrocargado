using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Panel / Cartel inspectable de explicación electromagnética con Accesibilidad Visual (Fase 1):
/// - Tamaño de fuente dinámico configurable desde el Inspector (compatible con TextMeshPro e IMGUI).
/// - Fragmentación automática de textos largos en viñetas cortas y páginas digeribles.
/// - Soporte completo para Teclado ([F]/[SPACE]/[ESC]) y Gamepad (Botón Y/Triángulo o A/Cruz).
/// </summary>
public class Billboard : MonoBehaviour
{
    [Header("Contenido del Cartel")]
    public Sprite fullImage;

    [Tooltip("Texto explicativo principal (si es largo, se divide automáticamente en fragmentos/viñetas).")]
    [TextArea(2, 5)]
    public string caption = "";

    [Tooltip("Fragmentos cortos opcionales. Si se llena este arreglo, el jugador avanza página por página con una fuente grande.")]
    [TextArea(2, 4)]
    public string[] captionFragments;

    [Range(1f, 8f)]
    public float interactRadius = 3f;

    [Header("Accesibilidad Visual y Tipografía (Inspector)")]
    [Tooltip("Tamaño de fuente principal para la explicación (TextMeshPro e IMGUI).")]
    [SerializeField, Range(14, 42)]
    private int captionFontSize = 20;

    [Tooltip("Tamaño de fuente para el indicador de interacción ([F] / Botón Gamepad).")]
    [SerializeField, Range(12, 32)]
    private int hintFontSize = 15;

    [Tooltip("Máximo de caracteres por fragmento antes de dividir un texto largo en páginas cortas.")]
    [SerializeField, Range(60, 220)]
    private int maxCharsPerFragment = 125;

    [Tooltip("Formatear automáticamente las oraciones como viñetas cortas (•) para facilitar la lectura rápida.")]
    [SerializeField]
    private bool autoFormatBulletPoints = true;

    [Header("Referencias Opcionales TextMeshPro (UI)")]
    [Tooltip("Opcional: Si usas un Canvas con TextMeshPro para el texto del cartel, asígnalo aquí.")]
    [SerializeField]
    private TMP_Text tmpCaptionText;

    [Tooltip("Opcional: Si usas un TextMeshPro en el mundo para el aviso de interacción.")]
    [SerializeField]
    private TMP_Text tmpHintText;

    private Transform player;
    private bool isViewing = false;
    private bool playerNearby = false;
    private Texture2D displayTexture;

    private string[] preparedPages;
    private int currentPageIndex = 0;

    private GUIStyle captionStyle;
    private GUIStyle hintStyle;
    private int cachedCaptionFontSize = -1;
    private int cachedHintFontSize = -1;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (fullImage != null)
            displayTexture = fullImage.texture;

        RebuildAccessiblePages();
        ApplyDynamicFontSizeToTMP();
    }

    void OnValidate()
    {
        RebuildAccessiblePages();
        ApplyDynamicFontSizeToTMP();
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        playerNearby = dist < interactRadius;

        if (tmpHintText != null)
        {
            tmpHintText.fontSize = hintFontSize;
            tmpHintText.gameObject.SetActive(playerNearby && !isViewing);
        }

        bool interactPressed =
            (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        bool nextOrConfirmPressed =
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        bool closePressed =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (playerNearby && !isViewing && interactPressed)
        {
            OpenBillboard();
            return;
        }

        if (!isViewing) return;

        if (closePressed)
        {
            CloseBillboard();
            return;
        }

        if (interactPressed || nextOrConfirmPressed)
        {
            if (preparedPages != null && currentPageIndex < preparedPages.Length - 1)
            {
                currentPageIndex++;
                SyncTMPPageText();
            }
            else
            {
                CloseBillboard();
            }
        }
    }

    private void OpenBillboard()
    {
        RebuildAccessiblePages();
        currentPageIndex = 0;
        isViewing = true;
        Time.timeScale = 0f;
        SyncTMPPageText();
    }

    private void CloseBillboard()
    {
        isViewing = false;
        Time.timeScale = 1f;
        if (tmpCaptionText != null)
            tmpCaptionText.gameObject.SetActive(false);
    }

    private void ApplyDynamicFontSizeToTMP()
    {
        if (tmpCaptionText != null)
            tmpCaptionText.fontSize = captionFontSize;

        if (tmpHintText != null)
            tmpHintText.fontSize = hintFontSize;
    }

    private void SyncTMPPageText()
    {
        if (tmpCaptionText == null) return;
        tmpCaptionText.gameObject.SetActive(isViewing);
        tmpCaptionText.fontSize = captionFontSize;

        if (preparedPages != null && preparedPages.Length > 0)
            tmpCaptionText.text = preparedPages[Mathf.Clamp(currentPageIndex, 0, preparedPages.Length - 1)];
        else
            tmpCaptionText.text = string.Empty;
    }

    /// <summary>
    /// Construye el arreglo de páginas cortas a partir de captionFragments o dividiendo caption automáticamente.
    /// </summary>
    public void RebuildAccessiblePages()
    {
        List<string> pages = new List<string>();

        if (captionFragments != null && captionFragments.Length > 0)
        {
            for (int i = 0; i < captionFragments.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(captionFragments[i]))
                {
                    string formatted = autoFormatBulletPoints
                        ? FormatTextAsShortBullets(captionFragments[i])
                        : captionFragments[i].Trim();
                    pages.Add(formatted);
                }
            }
        }

        if (pages.Count == 0 && !string.IsNullOrWhiteSpace(caption))
        {
            string[] autoChunks = SplitIntoShortFragments(caption, maxCharsPerFragment, autoFormatBulletPoints);
            pages.AddRange(autoChunks);
        }

        preparedPages = pages.ToArray();
    }

    /// <summary>
    /// Función auxiliar de accesibilidad: convierte un bloque denso de texto en viñetas cortas y limpias (•)
    /// separando por punto y seguido o saltos de línea para evitar muros de texto.
    /// </summary>
    public static string FormatTextAsShortBullets(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        string cleaned = rawText.Replace("\r\n", "\n").Trim();
        string[] sentences = cleaned.Split(new char[] { '\n', '.' }, System.StringSplitOptions.RemoveEmptyEntries);

        if (sentences.Length <= 1)
            return "• " + cleaned.TrimStart('•', '-', ' ');

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < sentences.Length; i++)
        {
            string line = sentences[i].Trim().TrimStart('•', '-', ' ');
            if (string.IsNullOrEmpty(line)) continue;

            if (sb.Length > 0) sb.Append('\n');
            sb.Append("• ").Append(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Divide un texto largo en un arreglo de fragmentos cortos (string[]) para que quepan en una fuente grande.
    /// </summary>
    public static string[] SplitIntoShortFragments(string rawText, int maxChars = 125, bool asBullets = true)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new string[0];

        string[] rawSentences = rawText
            .Replace("\r\n", "\n")
            .Split(new char[] { '\n', '.' }, System.StringSplitOptions.RemoveEmptyEntries);

        List<string> result = new List<string>();
        StringBuilder currentChunk = new StringBuilder();

        for (int i = 0; i < rawSentences.Length; i++)
        {
            string sentence = rawSentences[i].Trim().TrimStart('•', '-', ' ');
            if (string.IsNullOrEmpty(sentence)) continue;

            string formattedLine = asBullets ? $"• {sentence}" : sentence + ".";

            if (currentChunk.Length > 0 && currentChunk.Length + formattedLine.Length + 1 > maxChars)
            {
                result.Add(currentChunk.ToString());
                currentChunk.Clear();
            }

            if (currentChunk.Length > 0)
                currentChunk.Append('\n');

            currentChunk.Append(formattedLine);
        }

        if (currentChunk.Length > 0)
            result.Add(currentChunk.ToString());

        return result.ToArray();
    }

    void OnGUI()
    {
        if (captionStyle == null || cachedCaptionFontSize != captionFontSize || cachedHintFontSize != hintFontSize)
            InitStyles();

        // Mostrar indicador flotante de interacción
        if (playerNearby && !isViewing && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            screenPos.y = Screen.height - screenPos.y;

            GUI.color = new Color(0.04f, 0.06f, 0.14f, 0.82f);
            GUI.DrawTexture(new Rect(screenPos.x - 95f, screenPos.y - 46f, 190f, 28f), Texture2D.whiteTexture);

            GUI.color = new Color(0.4f, 0.92f, 1f, 0.98f);
            GUI.Label(new Rect(screenPos.x - 95f, screenPos.y - 46f, 190f, 28f),
                "[F / Y] Inspeccionar", hintStyle);
        }

        if (!isViewing || displayTexture == null) return;

        // Fondo oscuro de alto contraste para accesibilidad
        GUI.color = new Color(0f, 0f, 0f, 0.92f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Imagen centrada dejando espacio generoso en la parte inferior para la fuente grande
        bool hasText = preparedPages != null && preparedPages.Length > 0;
        float imgW = Screen.width * 0.68f;
        float imgH = Screen.height * (hasText ? 0.58f : 0.72f);
        float imgX = Screen.width / 2f - imgW / 2f;
        float imgY = Screen.height * 0.06f;

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(imgX, imgY, imgW, imgH), displayTexture, ScaleMode.ScaleToFit);

        // Caja de texto accesible con fuente grande
        if (hasText)
        {
            string activePageText = preparedPages[Mathf.Clamp(currentPageIndex, 0, preparedPages.Length - 1)];
            float boxW = Mathf.Min(Screen.width * 0.82f, 820f);
            float boxH = 120f;
            float boxX = Screen.width / 2f - boxW / 2f;
            float boxY = imgY + imgH + 14f;

            GUI.color = new Color(0.05f, 0.08f, 0.18f, 0.95f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.85f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(boxX + 18f, boxY + 10f, boxW - 36f, boxH - 20f), activePageText, captionStyle);
        }

        // Indicador de paginación y cierre
        string pageIndicator = (preparedPages != null && preparedPages.Length > 1)
            ? $"Página {currentPageIndex + 1}/{preparedPages.Length}  —  [ESPACIO / A] Siguiente   |   [ESC / B] Cerrar"
            : "[F / A / ESC] Cerrar panel";

        GUI.color = new Color(0.8f, 0.92f, 1f, 0.85f);
        GUI.Label(new Rect(Screen.width / 2f - 260f, Screen.height - 42f, 520f, 28f), pageIndicator, hintStyle);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void InitStyles()
    {
        cachedCaptionFontSize = captionFontSize;
        cachedHintFontSize = hintFontSize;

        captionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = captionFontSize,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(4, 4, 4, 4)
        };
        captionStyle.normal.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = hintFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        hintStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
