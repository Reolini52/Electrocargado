using UnityEngine;
using UnityEngine.InputSystem;

public class ActionFeedback : MonoBehaviour
{
    private ChargeResource chargeResource;
    private ChargeAbsorber absorber;
    private PlayerController playerController;
    private Camera cam;

    private string currentAction = "";
    private float actionTimer = 0f;
    private Color actionColor = Color.white;

    private GUIStyle actionStyle;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        absorber = GetComponent<ChargeAbsorber>();
        playerController = GetComponent<PlayerController>();
        cam = Camera.main;
    }

    void Update()
    {
        if (GandalfNPC.isGandalfActive) return;

        if (actionTimer > 0)
            actionTimer -= Time.deltaTime;

        if (absorber.IsAbsorbModeActive())
        {
            if (Mouse.current.leftButton.isPressed)
                SetAction("ABSORBIENDO", new Color(0f, 1f, 0.5f));
            else if (Mouse.current.rightButton.isPressed)
                SetAction("EXPULSANDO", new Color(1f, 0.6f, 0f));
            else
                SetAction("MODO ABSORCIÓN", new Color(0f, 1f, 0.5f, 0.5f));
        }
        else if (!chargeResource.IsNeutral())
        {
            if (Mouse.current.leftButton.isPressed)
                SetAction("GRAPPLING →", new Color(1f, 1f, 0f));
            else if (actionTimer <= 0)
                SetAction(chargeResource.IsPositive() ? "CARGADO +" : "CARGADO -",
                    chargeResource.IsPositive() ?
                    new Color(0.3f, 0.6f, 1f) :
                    new Color(1f, 0.3f, 0.3f));
        }
        else
        {
            if (actionTimer <= 0)
                SetAction("NEUTRO", new Color(0.7f, 0.7f, 0.7f, 0.5f));
        }

        if (chargeResource.IsBubbleActive())
            SetAction("BURBUJA ACTIVA", new Color(0.8f, 0.3f, 1f));
    }

    void SetAction(string text, Color color, float duration = 0.1f)
    {
        currentAction = text;
        actionColor = color;
        actionTimer = duration;
    }

    void OnGUI()
    {
        if (GandalfNPC.isGandalfActive || GameEvents.IsCleanHudHidden) return;

        if (actionStyle == null)
        {
            actionStyle = new GUIStyle(GUI.skin.label);
            actionStyle.fontSize = 16;
            actionStyle.fontStyle = FontStyle.Bold;
            actionStyle.alignment = TextAnchor.MiddleCenter;
        }

        float barWidth = 200f;
        float barHeight = 16f;
        float barX = Screen.width / 2f - barWidth / 2f;
        float barY = Screen.height - 60f;

        // Grace timer warning
        if (chargeResource != null && chargeResource.IsInGrace())
        {
            float grace = chargeResource.GetGraceTimer();
            GUI.color = new Color(1f, 0.8f, 0f, 0.9f);
            GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.fontSize = 14;
            warningStyle.fontStyle = FontStyle.Bold;
            warningStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(barX, barY - 58, barWidth, 20),
                $"¡RECARGA AHORA! {grace:F1}s", warningStyle);
        }

        // Background
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(barX - 5, barY - 5, barWidth + 10, barHeight + 10),
            Texture2D.whiteTexture);

        // Negative side
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        float negWidth = chargeResource.GetCharge() < 0 ?
            Mathf.Abs(chargeResource.GetCharge()) * (barWidth / 2f) : 0f;
        GUI.DrawTexture(new Rect(barX + barWidth / 2f - negWidth, barY,
            negWidth, barHeight), Texture2D.whiteTexture);

        // Positive side
        GUI.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        float posWidth = chargeResource.GetCharge() > 0 ?
            chargeResource.GetCharge() * (barWidth / 2f) : 0f;
        GUI.DrawTexture(new Rect(barX + barWidth / 2f, barY,
            posWidth, barHeight), Texture2D.whiteTexture);

        // Center line
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(barX + barWidth / 2f - 1, barY, 2, barHeight),
            Texture2D.whiteTexture);

        // Labels
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        actionStyle.fontSize = 11;
        GUI.Label(new Rect(barX, barY - 18, barWidth, 20),
            "— CARGA —", actionStyle);
        GUI.Label(new Rect(barX, barY + barHeight + 2, 30, 16),
            "-", actionStyle);
        GUI.Label(new Rect(barX + barWidth - 30, barY + barHeight + 2, 30, 16),
            "+", actionStyle);

        // Action text
        GUI.color = actionColor;
        actionStyle.fontSize = 15;
        GUI.Label(new Rect(barX, barY - 38, barWidth, 24),
            currentAction, actionStyle);

        // Mode indicator
        GUI.color = absorber.IsAbsorbModeActive() ?
            new Color(0f, 1f, 0.5f, 0.8f) :
            new Color(1f, 1f, 0f, 0.8f);
        actionStyle.fontSize = 11;
        GUI.Label(new Rect(barX, barY + barHeight + 18, barWidth, 16),
            absorber.IsAbsorbModeActive() ?
            "[Q / E] MODO ABSORCIÓN ON" : "[Q / E] ABSORBER / [LMB] GRAPPLE",
            actionStyle);

        // Jump counter (Cero GetComponent en OnGUI/Update)
        if (playerController != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            actionStyle.fontSize = 11;
            GUI.Label(new Rect(barX, barY + barHeight + 34, barWidth, 16),
                $"Saltos: {playerController.jumpsRemaining}/{playerController.maxJumps}",
                actionStyle);
        }
    }
}