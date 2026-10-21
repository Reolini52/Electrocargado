using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelMenu : MonoBehaviour
{
    public Button[] buttons;

    public void Awake()
    {
        PlayerPrefs.SetInt("UnlokedLevel", 4);

        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            // El botón sigue visible en el menú, pero el Nivel 3 (índice 2) queda bloqueado/inaccesible
            buttons[i].gameObject.SetActive(true);
            buttons[i].interactable = (i != 2);
        }
    }

    public void OpenLevel(int levelId)
    {
        // Bloquear acceso a Nivel 3 de forma segura sin causar errores ni crashes
        if (levelId == 3)
        {
            return;
        }

        Time.timeScale = 1f;
        string levelName = "Level " + levelId;
        SceneManager.LoadScene(levelName);
    }
}
