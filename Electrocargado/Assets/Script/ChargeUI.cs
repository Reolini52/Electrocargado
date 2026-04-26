using UnityEngine;
using UnityEngine.InputSystem;

public class ChargeUI : MonoBehaviour
{
    private PlayerController player;
    private ChargedObject[] chargedObjects;
    private bool debugVisible = false;

    private GUIStyle bigStyle;
    private GUIStyle infoStyle;
    private GUIStyle equationStyle;

    void Start()
    {
        player = Object.FindAnyObjectByType<PlayerController>();
        chargedObjects = FindObjectsByType<ChargedObject>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            debugVisible = !debugVisible;
            chargedObjects = FindObjectsByType<ChargedObject>(FindObjectsSortMode.None);
        }
    }

    void OnGUI()
    {
        if (player == null) return;

        InitStyles();

        bool isPositive = player.GetCharge() > 0;
        Color chargeColor = isPositive ?
            new Color(0.3f, 0.6f, 1f) :
            new Color(1f, 0.3f, 0.3f);

        // Charge indicator bottom left
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(10, Screen.height - 90, 120, 80), Texture2D.whiteTexture);
        GUI.color = chargeColor;
        GUI.Label(new Rect(20, Screen.height - 85, 100, 50),
            isPositive ? "⊕" : "⊖", bigStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(20, Screen.height - 45, 100, 30),
            isPositive ? "POSITIVE" : "NEGATIVE", infoStyle);

        // Nearest object interaction info
        ChargedObject nearest = GetNearest();
        if (nearest != null)
        {
            float r = Vector2.Distance(
                player.transform.position,
                nearest.transform.position);

            if (r < nearest.effectRadius)
            {
                bool attracting = (player.GetCharge() * nearest.charge) < 0;
                float k = 8.99f;
                float F = Mathf.Min(
                    k * Mathf.Abs(player.GetCharge() * nearest.charge) / (r * r),
                    30f);

                Color interactionColor = attracting ?
                    new Color(0.3f, 1f, 0.3f) :
                    new Color(1f, 0.8f, 0.2f);

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.DrawTexture(new Rect(10, 10, 280, 110), Texture2D.whiteTexture);
                GUI.color = interactionColor;
                GUI.Label(new Rect(20, 15, 260, 25),
                    attracting ? "▼ ATTRACTION" : "▲ REPULSION", infoStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(20, 40, 260, 20),
                    $"F = k|qQ| / r²", equationStyle);
                GUI.Label(new Rect(20, 62, 260, 20),
                    $"F = 8.99 × |{player.GetCharge():F0} × {nearest.charge:F0}| / {r:F1}²", equationStyle);
                GUI.Label(new Rect(20, 84, 260, 20),
                    $"F = {F:F2} N", equationStyle);
            }
        }

        // Debug panel
        if (debugVisible && nearest != null)
        {
            float r = Vector2.Distance(player.transform.position, nearest.transform.position);
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(10, 130, 280, 100), Texture2D.whiteTexture);
            GUI.color = Color.green;
            GUI.Label(new Rect(20, 135, 260, 20), "=== DEBUG [F] ===", infoStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 155, 260, 20), $"Player charge: {player.GetCharge():F0}", equationStyle);
            GUI.Label(new Rect(20, 172, 260, 20), $"Nearest Q: {nearest.charge:F0}", equationStyle);
            GUI.Label(new Rect(20, 189, 260, 20), $"Distance: {r:F2} units", equationStyle);
            GUI.Label(new Rect(20, 206, 260, 20), $"Effect radius: {nearest.effectRadius:F1}", equationStyle);
        }

        // Hints
        GUI.color = new Color(1f, 1f, 1f, 0.4f);
        GUI.Label(new Rect(10, Screen.height - 115, 300, 20),
            "[E] Switch charge   [F] Debug   [L] Field lines", infoStyle);
        GUI.color = Color.white;
    }

    void InitStyles()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label);
            bigStyle.fontSize = 36;
            bigStyle.fontStyle = FontStyle.Bold;
        }
        if (infoStyle == null)
        {
            infoStyle = new GUIStyle(GUI.skin.label);
            infoStyle.fontSize = 12;
            infoStyle.fontStyle = FontStyle.Bold;
        }
        if (equationStyle == null)
        {
            equationStyle = new GUIStyle(GUI.skin.label);
            equationStyle.fontSize = 11;
            equationStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }
    }

    ChargedObject GetNearest()
    {
        ChargedObject nearest = null;
        float minDist = float.MaxValue;
        if (chargedObjects == null) return null;
        foreach (var obj in chargedObjects)
        {
            if (obj == null) continue;
            float d = Vector2.Distance(player.transform.position, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj; }
        }
        return nearest;
    }
}