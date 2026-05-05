using UnityEngine;
using UnityEngine.InputSystem;

public class ActionFeedback : MonoBehaviour
{
    private ChargeResource chargeResource;
    private ChargeAbsorber absorber;
    private Camera cam;

    private string currentAction = "";
    private float actionTimer = 0f;
    private Color actionColor = Color.white;

    private GUIStyle actionStyle;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        absorber = GetComponent<ChargeAbsorber>();
        cam = Camera.main;
    }

    void Update()
    {
        if (actionTimer > 0)
            actionTimer -= Time.deltaTime;

        // Detect current mode for persistent label
        if (absorber.IsAbsorbModeActive())
        {
            Vector2 mouseWorld = cam.ScreenToWorldPoint(
                Mouse.current.position.ReadValue()
            );

            if (Mouse.current.leftButton.isPressed)
                SetAction("ABSORBING", new Color(0f, 1f, 0.5f));
            else if (Mouse.current.rightButton.isPressed)
                SetAction("EXPELLING", new Color(1f, 0.6f, 0f));
            else
                SetAction("ABSORB MODE", new Color(0f, 1f, 0.5f, 0.5f));
        }
        else if (!chargeResource.IsNeutral())
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                SetAction("BENDING →", new Color(1f, 1f, 0f));
            else if (Mouse.current.rightButton.wasPressedThisFrame)
                SetAction("PUSHING →", new Color(1f, 0.4f, 0.1f));
            else if (actionTimer <= 0)
                SetAction(chargeResource.IsPositive() ? "CHARGED +" : "CHARGED -",
                    chargeResource.IsPositive() ?
                    new Color(0.3f, 0.6f, 1f) :
                    new Color(1f, 0.3f, 0.3f));
        }
        else
        {
            if (actionTimer <= 0)
                SetAction("NEUTRAL", new Color(0.7f, 0.7f, 0.7f, 0.5f));
        }

        if (chargeResource.IsBubbleActive())
            SetAction("BUBBLE ACTIVE", new Color(0.8f, 0.3f, 1f));
    }

    void SetAction(string text, Color color, float duration = 0.1f)
    {
        currentAction = text;
        actionColor = color;
        actionTimer = duration;
    }

    void OnGUI()
    {
        if (actionStyle == null)
        {
            actionStyle = new GUIStyle(GUI.skin.label);
            actionStyle.fontSize = 16;
            actionStyle.fontStyle = FontStyle.Bold;
            actionStyle.alignment = TextAnchor.MiddleCenter;
        }

        // Show charge meter
        float charge = chargeResource.GetCharge();
        float barWidth = 200f;
        float barHeight = 16f;
        float barX = Screen.width / 2f - barWidth / 2f;
        float barY = Screen.height - 60f;

        // Background
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(barX - 5, barY - 5, barWidth + 10, barHeight + 10),
            Texture2D.whiteTexture);

        // Negative side (left)
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        float negWidth = charge < 0 ? Mathf.Abs(charge) * (barWidth / 2f) : 0f;
        GUI.DrawTexture(new Rect(barX + barWidth / 2f - negWidth, barY, negWidth, barHeight),
            Texture2D.whiteTexture);

        // Positive side (right)
        GUI.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        float posWidth = charge > 0 ? charge * (barWidth / 2f) : 0f;
        GUI.DrawTexture(new Rect(barX + barWidth / 2f, barY, posWidth, barHeight),
            Texture2D.whiteTexture);

        // Center line
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(barX + barWidth / 2f - 1, barY, 2, barHeight),
            Texture2D.whiteTexture);

        // Labels
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        actionStyle.fontSize = 11;
        GUI.Label(new Rect(barX, barY - 18, barWidth, 20), "— CHARGE —", actionStyle);
        GUI.Label(new Rect(barX, barY + barHeight + 2, 30, 16), "-", actionStyle);
        GUI.Label(new Rect(barX + barWidth - 30, barY + barHeight + 2, 30, 16), "+", actionStyle);

        // Action text above bar
        GUI.color = actionColor;
        actionStyle.fontSize = 15;
        GUI.Label(new Rect(barX, barY - 38, barWidth, 24), currentAction, actionStyle);

        // Mode indicator
        GUI.color = absorber.IsAbsorbModeActive() ?
            new Color(0f, 1f, 0.5f, 0.8f) :
            new Color(1f, 1f, 0f, 0.8f);
        actionStyle.fontSize = 11;
        GUI.Label(new Rect(barX, barY + barHeight + 18, barWidth, 16),
            absorber.IsAbsorbModeActive() ? "[Q] ABSORB MODE ON" : "[Q] BEND MODE",
            actionStyle);
    }
}