using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menú de selección de niveles para la Demo de Feria (5 niveles desbloqueados con acceso libre):
///  1. Tutorial ("Tutorial")
///  2. Nivel 1  ("Level 1")
///  3. Nivel 2  ("Level 2")
///  4. Nivel 3  ("Level 3")
///  5. Boss     ("Level 4")
/// Incluye soporte completo para Mouse/Touch y navegación fluida con Gamepad/Teclado (EventSystem).
/// </summary>
public class LevelMenu : MonoBehaviour
{
    [Header("Nombres de Escenas (Build Settings)")]
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private string level1SceneName = "Level 1";
    [SerializeField] private string level2SceneName = "Level 2";
    [SerializeField] private string level3SceneName = "Level 3";
    [SerializeField] private string bossSceneName = "Level 4";

    [Header("Referencias de los 5 Botones (Orden: Tutorial, Nivel 1, Nivel 2, Nivel 3, Boss)")]
    public Button[] buttons;

    [Header("Game Feel (Navegación UI / Gamepad)")]
    [Tooltip("Escala aplicada al botón actualmente seleccionado con Gamepad o Teclado.")]
    [SerializeField] private float selectedButtonScale = 1.14f;

    [Tooltip("Velocidad de interpolación de escala para el botón activo.")]
    [SerializeField] private float scaleTransitionSpeed = 12f;

    [Tooltip("Si la escena solo tenía 4 botones (Nivel 1..Boss), genera automáticamente el botón Tutorial al inicio.")]
    [SerializeField] private bool autoCreateTutorialButtonIfMissing = true;

    public void Awake()
    {
        // Desbloquear todos los niveles para la feria
        PlayerPrefs.SetInt("UnlokedLevel", 5);

        EnsureFiveButtonsSetup();
        UnlockAndConfigureAllButtons();
    }

    private void OnEnable()
    {
        UnlockAndConfigureAllButtons();
        SelectFirstButtonForGamepad();
    }

    private void Update()
    {
        UpdateGamepadSelectionAndFeel();
    }

    // =========================================================================
    // MÉTODOS PÚBLICOS PARA EL EVENTO OnClick() DE CADA BOTÓN EN EL INSPECTOR
    // =========================================================================

    /// <summary>Botón 1: Carga la escena del Tutorial ("Tutorial").</summary>
    public void LoadTutorial()
    {
        LoadSceneSafe(tutorialSceneName);
    }

    /// <summary>Botón 2: Carga el Nivel 1 ("Level 1").</summary>
    public void LoadLevel1()
    {
        LoadSceneSafe(level1SceneName);
    }

    /// <summary>Botón 3: Carga el Nivel 2 ("Level 2").</summary>
    public void LoadLevel2()
    {
        LoadSceneSafe(level2SceneName);
    }

    /// <summary>Botón 4: Carga el Nivel 3 ("Level 3").</summary>
    public void LoadLevel3()
    {
        LoadSceneSafe(level3SceneName);
    }

    /// <summary>Botón 5: Carga la batalla contra el Jefe Final POLAROUS ("Level 4").</summary>
    public void LoadBoss()
    {
        LoadSceneSafe(bossSceneName);
    }

    /// <summary>
    /// Método compatible con llamadas por índice entero desde el Inspector:
    /// 0 = Tutorial, 1 = Nivel 1, 2 = Nivel 2, 3 = Nivel 3, 4 = Boss (Level 4).
    /// </summary>
    public void OpenLevel(int levelId)
    {
        switch (levelId)
        {
            case 0:
                LoadTutorial();
                break;
            case 1:
                LoadLevel1();
                break;
            case 2:
                LoadLevel2();
                break;
            case 3:
                LoadLevel3();
                break;
            case 4:
            case 5:
                LoadBoss();
                break;
            default:
                LoadSceneSafe("Level " + levelId);
                break;
        }
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        Time.timeScale = 1f;
        GandalfNPC.isGandalfActive = false;
        SceneManager.LoadScene(sceneName);
    }

    // =========================================================================
    // CONFIGURACIÓN AUTOMÁTICA DE LOS 5 BOTONES Y NAVEGACIÓN GAMEPAD
    // =========================================================================

    private void EnsureFiveButtonsSetup()
    {
        if (!autoCreateTutorialButtonIfMissing || buttons == null || buttons.Length != 4)
            return;

        // Si el arreglo tiene los 4 botones originales (Level1, Level2, Level3, boss),
        // verificamos si falta el botón de Tutorial al inicio y lo creamos dinámicamente.
        if (buttons[0] != null && buttons[0].name != "Tutorial")
        {
            Transform parentGrid = buttons[0].transform.parent;
            if (parentGrid != null)
            {
                GameObject tutBtnObj = Instantiate(buttons[0].gameObject, parentGrid);
                tutBtnObj.name = "Tutorial";
                tutBtnObj.transform.SetAsFirstSibling();

                TMP_Text label = tutBtnObj.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "0";
                }

                Button tutBtn = tutBtnObj.GetComponent<Button>();
                if (tutBtn != null)
                {
                    tutBtn.onClick = new Button.ButtonClickedEvent();
                    tutBtn.onClick.AddListener(LoadTutorial);

                    List<Button> updatedList = new List<Button>(5) { tutBtn };
                    updatedList.AddRange(buttons);
                    buttons = updatedList.ToArray();
                }

                // Ajustar espaciado del GridLayoutGroup para que quepan los 5 botones cómodamente
                GridLayoutGroup grid = parentGrid.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(64f, 64f);
                    grid.spacing = new Vector2(14f, 20f);
                }
            }
        }
    }

    private void UnlockAndConfigureAllButtons()
    {
        if (buttons == null || buttons.Length == 0) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (btn == null) continue;

            // Todos los botones activos y desbloqueados para la demo
            btn.gameObject.SetActive(true);
            btn.interactable = true;

            // Configurar navegación explícita izquierda/derecha con wrap-around para Gamepad
            Navigation nav = btn.navigation;
            nav.mode = Navigation.Mode.Explicit;

            int prevIndex = (i - 1 + buttons.Length) % buttons.Length;
            int nextIndex = (i + 1) % buttons.Length;

            nav.selectOnLeft = buttons[prevIndex];
            nav.selectOnRight = buttons[nextIndex];
            btn.navigation = nav;
        }
    }

    private void SelectFirstButtonForGamepad()
    {
        if (buttons == null || buttons.Length == 0 || buttons[0] == null) return;
        if (EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    private void UpdateGamepadSelectionAndFeel()
    {
        if (buttons == null || buttons.Length == 0) return;

        // Si se usa un gamepad o teclado y ningún botón está seleccionado en el EventSystem, recuperar el foco
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            bool gamepadOrKeyInput =
                (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f ||
                                             Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f)) ||
                (Keyboard.current != null && (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                                              Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                                              Keyboard.current.aKey.wasPressedThisFrame ||
                                              Keyboard.current.dKey.wasPressedThisFrame));

            if (gamepadOrKeyInput && buttons[0] != null)
            {
                EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
            }
        }

        GameObject selectedObj = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        // Animar suavemente la escala del botón seleccionado para dar Game Feel arcade
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            bool isSelected = (selectedObj == buttons[i].gameObject);
            Vector3 targetScale = isSelected ? Vector3.one * selectedButtonScale : Vector3.one;
            buttons[i].transform.localScale = Vector3.Lerp(
                buttons[i].transform.localScale,
                targetScale,
                Time.unscaledDeltaTime * scaleTransitionSpeed
            );
        }
    }
}
