using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class WinScreen : MonoBehaviour
{
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;

    [TextArea(3, 6)]
    public string[] conceptsSummary = {
        "⚡ Ley de Coulomb: F = kqQ/r²",
        "Cargas iguales se repelen, opuestas se atraen.",
        "⚡ Campo Eléctrico: E = kQ/r²",
        "Las líneas de campo van de + a -.",
        "⚡ Experimento de Oersted",
        "Una corriente eléctrica genera un campo magnético.",
        "⚡ Fuerza de Lorentz: F = qv×B",
        "La fuerza magnética actúa perpendicular al movimiento."
    };

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame)
            SceneManager.LoadScene("MainMenu");
    }

    void OnGUI()
    {
        if (titleStyle == null) InitStyles();

        // Background
        GUI.color = new Color(0.05f, 0.05f, 0.15f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            Texture2D.whiteTexture);

        float w = 600f;
        float x = Screen.width / 2f - w / 2f;
        float y = 40f;

        // Title
        GUI.color = new Color(0.3f, 1f, 0.5f);
        GUI.Label(new Rect(x, y, w, 50),
            "⚡ SISTEMA NEUTRALIZADO", titleStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        GUI.Label(new Rect(x, y + 55, w, 24),
            "POLAROUS ha sido derrotado. THEIA ha salvado la red.",
            textStyle);

        // Divider
        GUI.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        GUI.DrawTexture(new Rect(x, y + 88, w, 2), Texture2D.whiteTexture);

        // Concepts learned
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(x, y + 100, w, 24),
            "Conceptos dominados:", textStyle);

        for (int i = 0; i < conceptsSummary.Length; i++)
        {
            bool isHeader = conceptsSummary[i].StartsWith("⚡");
            GUI.color = isHeader ?
                new Color(1f, 0.9f, 0.2f) :
                new Color(0.9f, 0.9f, 0.9f, 0.8f);

            GUI.Label(new Rect(x + (isHeader ? 0 : 20),
                y + 130f + i * 28f, w, 26),
                conceptsSummary[i], textStyle);
        }

        // Continue hint
        float hintY = y + 130f + conceptsSummary.Length * 28f + 20f;
        GUI.color = new Color(1f, 1f, 1f,
            Mathf.PingPong(Time.time * 1.5f, 1f));
        GUI.Label(new Rect(x, hintY, w, 30),
            "[SPACE] Volver al menú", textStyle);
    }

    void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 14;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.wordWrap = true;
        textStyle.normal.textColor = Color.white;
    }
}