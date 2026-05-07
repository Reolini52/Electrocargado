using UnityEngine;

public class HealthUI : MonoBehaviour
{
    private PlayerHealth playerHealth;
    private GUIStyle heartStyle;
    private GUIStyle resetStyle;

    void Start()
    {
        playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
    }

    void OnGUI()
    {
        if (playerHealth == null) return;
        if (heartStyle == null) InitStyles();

        // Health hearts top left
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(8, 8, 120, 36), Texture2D.whiteTexture);

        for (int i = 0; i < playerHealth.maxHealth; i++)
        {
            bool filled = i < playerHealth.currentHealth;
            GUI.color = filled ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
            GUI.Label(new Rect(15 + i * 35, 12, 30, 30), "♥", heartStyle);
        }

        // Resets counter
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(8, 48, 120, 24), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.8f, 0.2f);
        GUI.Label(new Rect(15, 50, 200, 20),
            $"Resets: {playerHealth.currentResets}/{playerHealth.maxResets}",
            resetStyle);
        GUI.color = new Color(1f, 1f, 1f, 0.4f);
        GUI.Label(new Rect(15, 72, 200, 20), "[R] Reset level", resetStyle);
    }

    void InitStyles()
    {
        heartStyle = new GUIStyle(GUI.skin.label);
        heartStyle.fontSize = 22;
        heartStyle.fontStyle = FontStyle.Bold;

        resetStyle = new GUIStyle(GUI.skin.label);
        resetStyle.fontSize = 11;
        resetStyle.fontStyle = FontStyle.Bold;
        resetStyle.normal.textColor = Color.white;
    }
}