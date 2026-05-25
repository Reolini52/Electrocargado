using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    private bool isPaused = false;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    public bool IsPaused() => isPaused;
    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    void OnGUI()
    {
        if (!isPaused) return;
        if (titleStyle == null) InitStyles();

        // Dim background
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            Texture2D.whiteTexture);

        float w = 200f;
        float h = 220f;
        float x = Screen.width / 2f - w / 2f;
        float y = Screen.height / 2f - h / 2f;

        GUI.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.Label(new Rect(x, y + 15, w, 30), "PAUSED", titleStyle);

        GUI.color = Color.white;

        if (GUI.Button(new Rect(x + 20, y + 60, w - 40, 35),
            "Resume", buttonStyle))
            TogglePause();

        if (GUI.Button(new Rect(x + 20, y + 105, w - 40, 35),
            "Restart Level", buttonStyle))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if (GUI.Button(new Rect(x + 20, y + 150, w - 40, 35),
            "Main Menu", buttonStyle))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main Menu");
        }
    }

    void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 22;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.fontStyle = FontStyle.Bold;
    }

    void OnDestroy()
    {
        // Make sure time scale resets if scene changes
        Time.timeScale = 1f;
    }
}