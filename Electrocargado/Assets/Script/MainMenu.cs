using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Opcional: Primer botón a enfocar con Gamepad")]
    [SerializeField] private Button defaultSelectedButton;

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;
        GandalfNPC.isGandalfActive = false;

        FocusFirstAvailableButton();
    }

    void OnEnable()
    {
        FocusFirstAvailableButton();
    }

    void Update()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            bool gamepadOrKeyUsed =
                (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f ||
                                             Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f)) ||
                (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame ||
                                              Keyboard.current.downArrowKey.wasPressedThisFrame ||
                                              Keyboard.current.wKey.wasPressedThisFrame ||
                                              Keyboard.current.sKey.wasPressedThisFrame));

            if (gamepadOrKeyUsed)
            {
                FocusFirstAvailableButton();
            }
        }
    }

    public void FocusFirstAvailableButton()
    {
        if (EventSystem.current == null) return;

        if (defaultSelectedButton != null && defaultSelectedButton.gameObject.activeInHierarchy && defaultSelectedButton.interactable)
        {
            EventSystem.current.SetSelectedGameObject(defaultSelectedButton.gameObject);
            return;
        }

        Button firstBtn = GetComponentInChildren<Button>(false);
        if (firstBtn != null && firstBtn.interactable)
        {
            EventSystem.current.SetSelectedGameObject(firstBtn.gameObject);
        }
    }

    public void Sandbox()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync("SceneTesting");
    }

    public void LevelMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync("Main Menu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
