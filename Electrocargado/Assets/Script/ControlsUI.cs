using UnityEngine;
using UnityEngine.InputSystem;

public class ControlsUI : MonoBehaviour
{
    private bool visible = false;

    void Update()
    {
        if (Keyboard.current.hKey.wasPressedThisFrame)
            visible = !visible;
    }

    void OnGUI()
    {
        GUIStyle small = new GUIStyle(GUI.skin.label);
        small.fontSize = 11;
        small.normal.textColor = new Color(1f, 1f, 1f, 0.5f);

        GUI.Label(new Rect(Screen.width - 160, Screen.height - 24, 150, 20),
            "[H] Controls", small);

        if (!visible) return;

        float w = 280f;
        float h = 300f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height / 2f - h / 2f;

        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.skin.label.fontSize = 12;
        GUI.skin.label.normal.textColor = Color.white;

        float lx = x + 15;
        float ly = y + 15;
        float lh = 22;

        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(lx, ly, w, lh), "ELECTRO CARGADO — CONTROLS");
        ly += lh + 5;

        GUI.color = Color.white;
        GUI.Label(new Rect(lx, ly, w, lh), "[A/D]  Move"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[SPACE]  Jump"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[Q]  Absorb / Bend mode toggle"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[LMB]  Absorb / Pull yourself"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[RMB]  Expel / Push target"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[SHIFT]  Bubble mode"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[Z]  Melee attack"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[F]  Physics debug"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[L]  Field lines"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[H]  This menu"); ly += lh + 5;
        GUI.Label(new Rect(lx, ly, w, lh), "[R]  Reset level (uses 1 reset)"); ly += lh;
        GUI.color = new Color(1f, 1f, 0.3f);
        GUI.Label(new Rect(lx, ly, w, lh), "BLUE = +    RED = -"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "Same = repel   Opposite = attract");
    }
}