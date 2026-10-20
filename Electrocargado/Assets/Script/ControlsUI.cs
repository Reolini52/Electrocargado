using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Menú de ayuda de controles secundarios (Tecla F1), dejando la tecla [H] dedicada
/// a ocultar/mostrar absolutamente todo el HUD (Modo Limpio) sin pausar el juego.
/// </summary>
public class ControlsUI : MonoBehaviour
{
    private bool visible = false;
    public bool IsVisible() => visible;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            visible = !visible;
            Time.timeScale = visible ? 0f : 1f;
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void OnGUI()
    {
        if (GandalfNPC.isGandalfActive || GameEvents.IsCleanHudHidden) return;

        GUIStyle small = new GUIStyle(GUI.skin.label);
        small.fontSize = 10;
        small.normal.textColor = new Color(1f, 1f, 1f, 0.45f);

        GUI.Label(new Rect(Screen.width - 235, Screen.height - 22, 225, 18),
            "[H] Ocultar HUD  |  [F1] Controles", small);

        if (!visible) return;

        float w = 310f;
        float h = 320f;
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
        GUI.Label(new Rect(lx, ly, w, lh), "ELECTROCARGADO — CONTROLES");
        ly += lh + 5;

        GUI.color = Color.white;
        GUI.Label(new Rect(lx, ly, w, lh), "[A / D]  Mover a THEIA"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[SPACE]  Saltar / Doble Salto con Carga"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[Q / E]  Alternar Modo Absorber / Bending"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[Click Izq]  Absorber / Grapple"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[Click Der]  Expulsar Carga"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[SHIFT]  Escudo Burbuja Dieléctrica"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[V]  Ver/Ocultar Vectores (Fe, v, Campo E)"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[B o T]  Ver/Ocultar Tablas y Fórmulas Live"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[H]  Modo Limpio (Ocultar todo el HUD)"); ly += lh;
        GUI.Label(new Rect(lx, ly, w, lh), "[R]  Reiniciar nivel"); ly += lh + 4;
        GUI.color = new Color(1f, 1f, 0.3f);
        GUI.Label(new Rect(lx, ly, w, lh), "AZUL = +Q    ROJO = -Q");
    }
}