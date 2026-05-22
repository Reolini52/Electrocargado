using UnityEngine;
using UnityEngine.InputSystem;

public class Billboard : MonoBehaviour
{
    [Header("Content")]
    public Sprite fullImage;
    public string caption = "";
    public float interactRadius = 3f;

    private Transform player;
    private bool isViewing = false;
    private bool playerNearby = false;
    private Texture2D displayTexture;

    private GUIStyle captionStyle;
    private GUIStyle hintStyle;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (fullImage != null)
            displayTexture = fullImage.texture;
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        playerNearby = dist < interactRadius;

        if (playerNearby && Keyboard.current.fKey.wasPressedThisFrame)
        {
            isViewing = !isViewing;
            Time.timeScale = isViewing ? 0f : 1f;
        }

        if (isViewing && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            isViewing = false;
            Time.timeScale = 1f;
        }
    }

    void OnGUI()
    {
        if (captionStyle == null) InitStyles();

        // Show interact hint when nearby
        if (playerNearby && !isViewing)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            screenPos.y = Screen.height - screenPos.y;

            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(screenPos.x - 50, screenPos.y - 40, 100, 20),
                "[F] Inspect", hintStyle);
        }

        if (!isViewing || displayTexture == null) return;

        // Fullscreen dim
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            Texture2D.whiteTexture);

        // Image centered
        float imgW = Screen.width * 0.7f;
        float imgH = Screen.height * 0.7f;
        float imgX = Screen.width / 2f - imgW / 2f;
        float imgY = Screen.height / 2f - imgH / 2f;

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(imgX, imgY, imgW, imgH),
            displayTexture, ScaleMode.ScaleToFit);

        // Caption
        if (!string.IsNullOrEmpty(caption))
        {
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(imgX, imgY + imgH + 10, imgW, 30),
                caption, captionStyle);
        }

        // Close hint
        GUI.color = new Color(1f, 1f, 1f, 0.4f);
        GUI.Label(new Rect(Screen.width / 2f - 100, imgY + imgH + 40, 200, 20),
            "[F] or [ESC] Close", hintStyle);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void InitStyles()
    {
        captionStyle = new GUIStyle(GUI.skin.label);
        captionStyle.fontSize = 13;
        captionStyle.alignment = TextAnchor.MiddleCenter;
        captionStyle.normal.textColor = Color.white;
        captionStyle.wordWrap = true;

        hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = 11;
        hintStyle.alignment = TextAnchor.MiddleCenter;
        hintStyle.normal.textColor = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}