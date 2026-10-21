using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Overlay de vectores, telemetría matemática y herramienta de Teletransporte para Demo.
/// [V]: Vectores (Fuerza Fe, Fuerza Magnética Fm, Velocidad v y Campo E)
/// [B]: Cuadro compacto de ecuaciones en esquina superior derecha
/// [P]: Menú de Teletransporte rápido para la presentación/demo
/// [H]: Ocultar todo el HUD
/// </summary>
[DisallowMultipleComponent]
public class DebugOverlayUI : MonoBehaviour
{
    private static DebugOverlayUI instance;

    private struct TeleportEntry
    {
        public string label;
        public Vector2 position;
    }

    // Puntos guardados manualmente en tiempo de ejecución por escena
    private static readonly Dictionary<string, List<TeleportEntry>> runtimeSavedPoints =
        new Dictionary<string, List<TeleportEntry>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOverlaySystem()
    {
        if (Object.FindAnyObjectByType<DebugOverlayUI>() == null)
        {
            GameObject overlayHost = new GameObject("[Electrocargado_DebugOverlay]");
            overlayHost.AddComponent<DebugOverlayUI>();
            overlayHost.AddComponent<ElectricFieldVisualizer>();
            DontDestroyOnLoad(overlayHost);
        }
    }

    [Header("Visibilidad")]
    [SerializeField] private bool showVectorGraphics = false;
    [SerializeField] private bool showMathTables = false;
    [SerializeField] private bool showTeleportMenu = false;
    [SerializeField] private bool hideAllHudCleanMode = false;

    [Header("Escalas y Colores de Vectores ([V])")]
    [SerializeField, Range(0.04f, 0.4f)] private float forceVectorScale = 0.11f;
    [SerializeField, Range(0.04f, 0.4f)] private float velocityVectorScale = 0.14f;
    [SerializeField] private Color attractiveForceColor = new Color(0.2f, 1f, 0.45f, 0.92f);
    [SerializeField] private Color repulsiveForceColor = new Color(1f, 0.85f, 0.2f, 0.92f);
    [SerializeField] private Color velocityVectorColor = new Color(0.25f, 0.95f, 1f, 0.85f);
    [SerializeField] private Color magneticForceColor = new Color(0.95f, 0.4f, 1f, 0.92f);

    // Datos en vivo
    private float currentPlayerChargeQ;
    private float rcInitialChargeQ0;
    private float rcElapsedTimeT;
    private float rcTimeConstantTau = 1f;
    private bool isGroundedOnConductor;

    private CoulombForceData latestForceData;
    private float lastForceTimestamp;

    private CurrentMagneticData latestMagData;
    private float lastMagTimestamp;

    private Vector2 latestNetElectricField;

    private LineRenderer forceVectorRenderer;
    private LineRenderer velocityVectorRenderer;
    private LineRenderer magneticVectorRenderer;
    private Rigidbody2D cachedPlayerRb;

    private readonly List<TeleportEntry> sceneTeleportPoints = new List<TeleportEntry>();

    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle btnActiveStyle;
    private GUIStyle btnIdleStyle;
    private GUIStyle tpButtonStyle;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        CreateWorldVectorRenderers();
    }

    private void OnEnable()
    {
        GameEvents.OnChargeChanged += HandleChargeChanged;
        GameEvents.OnRCDischargeStep += HandleRCDischargeStep;
        GameEvents.OnConductorContactChanged += HandleConductorContact;
        GameEvents.OnForceApplied += HandleForceApplied;
        GameEvents.OnMagneticForceApplied += HandleMagneticForceApplied;
        GameEvents.OnElectricFieldUpdated += HandleElectricFieldUpdated;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        GameEvents.OnChargeChanged -= HandleChargeChanged;
        GameEvents.OnRCDischargeStep -= HandleRCDischargeStep;
        GameEvents.OnConductorContactChanged -= HandleConductorContact;
        GameEvents.OnForceApplied -= HandleForceApplied;
        GameEvents.OnMagneticForceApplied -= HandleMagneticForceApplied;
        GameEvents.OnElectricFieldUpdated -= HandleElectricFieldUpdated;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        LocatePlayerRigidbodyOnce();
        RebuildSceneTeleportPoints();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LocatePlayerRigidbodyOnce();
        RebuildSceneTeleportPoints();
    }

    private void LocatePlayerRigidbodyOnce()
    {
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc != null)
            cachedPlayerRb = pc.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();
        UpdateWorldVectorsRendering();
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            hideAllHudCleanMode = !hideAllHudCleanMode;
            GameEvents.RaiseHudCleanModeToggled(hideAllHudCleanMode);
        }

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            ToggleVectorGraphicsLayer();
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleMathTableLayer();
        }

        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            showTeleportMenu = !showTeleportMenu;
            if (showTeleportMenu)
                RebuildSceneTeleportPoints();
        }
    }

    public void ToggleVectorGraphicsLayer()
    {
        showVectorGraphics = !showVectorGraphics;
        GameEvents.RaiseDebugOverlayToggled(showVectorGraphics);

        if (!showVectorGraphics)
        {
            if (forceVectorRenderer != null) forceVectorRenderer.enabled = false;
            if (velocityVectorRenderer != null) velocityVectorRenderer.enabled = false;
            if (magneticVectorRenderer != null) magneticVectorRenderer.enabled = false;
        }
    }

    public void ToggleMathTableLayer()
    {
        showMathTables = !showMathTables;
        GameEvents.RaiseMathTableToggled(showMathTables);
    }

    private void RebuildSceneTeleportPoints()
    {
        sceneTeleportPoints.Clear();
        string sceneName = SceneManager.GetActiveScene().name;

        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return;

        // 1. Puntos manuales (DemoTeleportPoint) si el usuario puso alguno en el editor
        DemoTeleportPoint[] manualPoints = Object.FindObjectsByType<DemoTeleportPoint>(FindObjectsSortMode.None);
        System.Array.Sort(manualPoints, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < manualPoints.Length; i++)
        {
            string lbl = string.IsNullOrWhiteSpace(manualPoints[i].pointLabel)
                ? $"Punto Manual #{i + 1}"
                : manualPoints[i].pointLabel;
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = "★ " + lbl,
                position = manualPoints[i].transform.position
            });
        }

        // 2. Si es la pelea contra el jefe (Level 4), agregar puntos clave de la arena
        PolarousBossController boss = Object.FindAnyObjectByType<PolarousBossController>();
        if (boss != null || sceneName == "Level 4")
        {
            sceneTeleportPoints.Add(new TeleportEntry { label = "Arena: Centro Inferior", position = new Vector2(0f, -2.5f) });
            sceneTeleportPoints.Add(new TeleportEntry { label = "Arena: Placa Superior", position = new Vector2(0f, 2.6f) });
            sceneTeleportPoints.Add(new TeleportEntry { label = "Arena: Costado Izquierdo", position = new Vector2(-9f, 0f) });
            sceneTeleportPoints.Add(new TeleportEntry { label = "Arena: Costado Derecho", position = new Vector2(9f, 0f) });
            return;
        }

        // 3. Inicio del nivel / Zonas de diálogo ordenadas de izquierda a derecha
        DialogueSystem[] dialogues = Object.FindObjectsByType<DialogueSystem>(FindObjectsSortMode.None);
        System.Array.Sort(dialogues, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < dialogues.Length; i++)
        {
            Vector2 pos = dialogues[i].transform.position;
            string tag = (i == 0) ? "Inicio / Zona 1" : $"Zona {i + 1} (x={pos.x:F0})";
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = tag,
                position = pos + new Vector2(0f, 0.5f)
            });
        }

        // Si no había diálogos, guardar la posición actual del jugador al cargar
        if (dialogues.Length == 0)
        {
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = "Inicio del Nivel",
                position = pc.transform.position
            });
        }

        // 4. Campos Magnéticos (FieldZone)
        FieldZone[] fields = Object.FindObjectsByType<FieldZone>(FindObjectsSortMode.None);
        System.Array.Sort(fields, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < fields.Length; i++)
        {
            Vector2 fPos = fields[i].transform.position;
            float offsetX = 2.5f;
            Collider2D col = fields[i].GetComponent<Collider2D>();
            if (col != null) offsetX = col.bounds.extents.x + 1.4f;

            string lbl = fields.Length == 1 ? "Campo Magnético (Lorentz)" : $"Campo Magnético #{i + 1}";
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = lbl,
                position = new Vector2(fPos.x - offsetX, fPos.y)
            });
        }

        // 5. Barras de Corriente (CurrentRod)
        CurrentRod[] rods = Object.FindObjectsByType<CurrentRod>(FindObjectsSortMode.None);
        System.Array.Sort(rods, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < rods.Length; i++)
        {
            Vector2 rPos = rods[i].transform.position;
            string lbl = rods.Length == 1 ? "Barra Corriente (I)" : $"Barra Corriente #{i + 1}";
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = lbl,
                position = rPos + new Vector2(-2.2f, 0.5f)
            });
        }

        // 6. Receptores / Puertas (ChargeDetector)
        ChargeDetector[] detectors = Object.FindObjectsByType<ChargeDetector>(FindObjectsSortMode.None);
        System.Array.Sort(detectors, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < detectors.Length; i++)
        {
            Vector2 dPos = detectors[i].transform.position;
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = $"Receptor / Puerta #{i + 1}",
                position = dPos + new Vector2(-2.2f, 0.5f)
            });
        }

        // 7. Firewall Gandalf (Final del Nivel)
        GandalfNPC[] gandalfs = Object.FindObjectsByType<GandalfNPC>(FindObjectsSortMode.None);
        for (int i = 0; i < gandalfs.Length; i++)
        {
            Vector2 gPos = gandalfs[i].transform.position;
            sceneTeleportPoints.Add(new TeleportEntry
            {
                label = "Firewall Gandalf (Final)",
                position = gPos + new Vector2(-2.6f, 0.4f)
            });
        }
    }

    private void TeleportPlayerTo(Vector2 targetPos)
    {
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return;

        pc.transform.position = new Vector3(targetPos.x, targetPos.y, pc.transform.position.z);
        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            cachedPlayerRb = rb;
        }
    }

    private void SaveCurrentPlayerPosition()
    {
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (!runtimeSavedPoints.TryGetValue(sceneName, out List<TeleportEntry> list))
        {
            list = new List<TeleportEntry>();
            runtimeSavedPoints[sceneName] = list;
        }

        Vector2 pos = pc.transform.position;
        list.Add(new TeleportEntry
        {
            label = $"Guardado #{list.Count + 1} ({pos.x:F0}, {pos.y:F0})",
            position = pos
        });
    }

    private void HealPlayerFull()
    {
        PlayerHealth ph = Object.FindAnyObjectByType<PlayerHealth>();
        if (ph != null)
        {
            ph.currentHealth = ph.maxHealth;
        }
    }

    private void HandleChargeChanged(float currentQ, float maxQ, float ratio)
    {
        currentPlayerChargeQ = currentQ;
    }

    private void HandleRCDischargeStep(float currentQ, float q0, float elapsedT, float tau)
    {
        currentPlayerChargeQ = currentQ;
        rcInitialChargeQ0 = q0;
        rcElapsedTimeT = elapsedT;
        rcTimeConstantTau = tau;
    }

    private void HandleConductorContact(bool isContacting)
    {
        isGroundedOnConductor = isContacting;
    }

    private void HandleForceApplied(CoulombForceData data)
    {
        latestForceData = data;
        lastForceTimestamp = Time.time;
    }

    private void HandleMagneticForceApplied(CurrentMagneticData data)
    {
        latestMagData = data;
        lastMagTimestamp = Time.time;
    }

    private void HandleElectricFieldUpdated(Vector2 pos, Vector2 netField)
    {
        latestNetElectricField = netField;
    }

    private void CreateWorldVectorRenderers()
    {
        Shader spriteShader = Shader.Find("Sprites/Default");

        // Vector de Fuerza Electrostática Fe
        GameObject forceObj = new GameObject("Vector_Fe");
        forceObj.transform.SetParent(transform);
        forceVectorRenderer = forceObj.AddComponent<LineRenderer>();
        forceVectorRenderer.useWorldSpace = true;
        forceVectorRenderer.positionCount = 5;
        forceVectorRenderer.startWidth = 0.065f;
        forceVectorRenderer.endWidth = 0.065f;
        forceVectorRenderer.material = new Material(spriteShader);
        forceVectorRenderer.sortingOrder = 15;
        forceVectorRenderer.enabled = false;

        // Vector de Velocidad v
        GameObject velObj = new GameObject("Vector_v");
        velObj.transform.SetParent(transform);
        velocityVectorRenderer = velObj.AddComponent<LineRenderer>();
        velocityVectorRenderer.useWorldSpace = true;
        velocityVectorRenderer.positionCount = 5;
        velocityVectorRenderer.startWidth = 0.05f;
        velocityVectorRenderer.endWidth = 0.05f;
        velocityVectorRenderer.material = new Material(spriteShader);
        velocityVectorRenderer.sortingOrder = 14;
        velocityVectorRenderer.enabled = false;

        // Vector de Fuerza Magnética Fm (Corriente / Lorentz)
        GameObject magObj = new GameObject("Vector_Fm");
        magObj.transform.SetParent(transform);
        magneticVectorRenderer = magObj.AddComponent<LineRenderer>();
        magneticVectorRenderer.useWorldSpace = true;
        magneticVectorRenderer.positionCount = 5;
        magneticVectorRenderer.startWidth = 0.06f;
        magneticVectorRenderer.endWidth = 0.06f;
        magneticVectorRenderer.material = new Material(spriteShader);
        magneticVectorRenderer.sortingOrder = 15;
        magneticVectorRenderer.enabled = false;
    }

    private void UpdateWorldVectorsRendering()
    {
        if (!showVectorGraphics || GandalfNPC.isGandalfActive)
        {
            if (forceVectorRenderer != null) forceVectorRenderer.enabled = false;
            if (velocityVectorRenderer != null) velocityVectorRenderer.enabled = false;
            if (magneticVectorRenderer != null) magneticVectorRenderer.enabled = false;
            return;
        }

        // 1. Vector Fe (Coulomb)
        if (forceVectorRenderer != null)
        {
            bool hasRecentForce = (Time.time - lastForceTimestamp) < 0.15f && latestForceData.ForceMagnitude > 0.05f;
            if (hasRecentForce)
            {
                forceVectorRenderer.enabled = true;
                Color c = latestForceData.IsAttractive ? attractiveForceColor : repulsiveForceColor;
                DrawArrow(forceVectorRenderer, latestForceData.TargetPosition, latestForceData.ForceVector, forceVectorScale, 0.45f, 2.4f, c);
            }
            else
            {
                forceVectorRenderer.enabled = false;
            }
        }

        // 2. Vector v (Velocidad)
        if (velocityVectorRenderer != null)
        {
            if (cachedPlayerRb == null) LocatePlayerRigidbodyOnce();

            if (cachedPlayerRb != null && cachedPlayerRb.linearVelocity.sqrMagnitude > 0.15f)
            {
                velocityVectorRenderer.enabled = true;
                DrawArrow(velocityVectorRenderer, cachedPlayerRb.position, cachedPlayerRb.linearVelocity, velocityVectorScale, 0.35f, 1.8f, velocityVectorColor);
            }
            else
            {
                velocityVectorRenderer.enabled = false;
            }
        }

        // 3. Vector Fm (Fuerza Magnética por Corriente / Lorentz)
        if (magneticVectorRenderer != null)
        {
            bool hasRecentMag = (Time.time - lastMagTimestamp) < 0.15f && latestMagData.MagneticForceMag > 0.05f;
            if (hasRecentMag)
            {
                magneticVectorRenderer.enabled = true;
                DrawArrow(magneticVectorRenderer, latestMagData.TargetPosition, latestMagData.MagneticForceVec, forceVectorScale, 0.45f, 2.2f, magneticForceColor);
            }
            else
            {
                magneticVectorRenderer.enabled = false;
            }
        }
    }

    private static void DrawArrow(LineRenderer lr, Vector2 origin, Vector2 vec, float scale, float minLen, float maxLen, Color color)
    {
        Vector2 dir = vec.normalized;
        float length = Mathf.Clamp(vec.magnitude * scale, minLen, maxLen);
        Vector2 tip = origin + dir * length;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 headLeft = tip - dir * 0.22f + perp * 0.12f;
        Vector2 headRight = tip - dir * 0.22f - perp * 0.12f;

        lr.SetPosition(0, origin);
        lr.SetPosition(1, tip);
        lr.SetPosition(2, headLeft);
        lr.SetPosition(3, tip);
        lr.SetPosition(4, headRight);

        lr.startColor = color;
        lr.endColor = color;
    }

    private void InitStyles()
    {
        if (labelStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };
        titleStyle.normal.textColor = new Color(0.4f, 0.85f, 1f, 0.98f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };
        labelStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 0.95f);

        btnActiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(2, 2, 1, 1)
        };
        btnActiveStyle.normal.textColor = new Color(0.3f, 1f, 0.75f);

        btnIdleStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(2, 2, 1, 1)
        };
        btnIdleStyle.normal.textColor = new Color(0.85f, 0.92f, 1f, 0.85f);

        tpButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 6, 2, 2)
        };
        tpButtonStyle.normal.textColor = new Color(0.9f, 0.96f, 1f, 0.95f);
    }

    private void OnGUI()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Main Menu" || sceneName == "WinScreen") return;
        if (GandalfNPC.isGandalfActive || hideAllHudCleanMode) return;

        InitStyles();
        DrawFloatingTopRightButtons();

        float nextPanelY = 34f;

        if (showMathTables)
        {
            nextPanelY = DrawCompactCornerMathHUD(nextPanelY) + 6f;
        }

        if (showTeleportMenu)
        {
            DrawTeleportMenuPanel(nextPanelY);
        }
    }

    private void DrawFloatingTopRightButtons()
    {
        float btnW = 92f;
        float btnH = 22f;
        float gap = 5f;
        float rightMargin = 10f;
        float topMargin = 8f;

        // Botón [P] TP Demo
        Rect tpRect = new Rect(Screen.width - (btnW * 3f + gap * 2f + rightMargin), topMargin, btnW, btnH);
        GUI.backgroundColor = showTeleportMenu
            ? new Color(0.15f, 0.55f, 0.38f, 0.9f)
            : new Color(0.08f, 0.1f, 0.18f, 0.65f);

        if (GUI.Button(tpRect, "[P] TP Demo", showTeleportMenu ? btnActiveStyle : btnIdleStyle))
        {
            showTeleportMenu = !showTeleportMenu;
            if (showTeleportMenu)
                RebuildSceneTeleportPoints();
        }

        // Botón [V] Vectores
        Rect vecRect = new Rect(Screen.width - (btnW * 2f + gap + rightMargin), topMargin, btnW, btnH);
        GUI.backgroundColor = showVectorGraphics
            ? new Color(0.1f, 0.45f, 0.55f, 0.85f)
            : new Color(0.08f, 0.1f, 0.18f, 0.65f);

        if (GUI.Button(vecRect, "[V] Vectores", showVectorGraphics ? btnActiveStyle : btnIdleStyle))
        {
            ToggleVectorGraphicsLayer();
        }

        // Botón [B] Datos
        Rect mathRect = new Rect(Screen.width - (btnW + rightMargin), topMargin, btnW, btnH);
        GUI.backgroundColor = showMathTables
            ? new Color(0.1f, 0.45f, 0.55f, 0.85f)
            : new Color(0.08f, 0.1f, 0.18f, 0.65f);

        if (GUI.Button(mathRect, "[B] Datos", showMathTables ? btnActiveStyle : btnIdleStyle))
        {
            ToggleMathTableLayer();
        }

        GUI.backgroundColor = Color.white;
    }

    private float DrawCompactCornerMathHUD(float startY)
    {
        float panelW = 325f;
        float panelH = 182f;
        float px = Screen.width - panelW - 10f;
        float py = startY;

        // Fondo oscuro translúcido con suficiente ancho y alto para no recortar texto
        GUI.color = new Color(0.03f, 0.05f, 0.12f, 0.78f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.8f, 1f, 0.75f);
        GUI.DrawTexture(new Rect(px, py, panelW, 2f), Texture2D.whiteTexture);

        float x = px + 10f;
        float w = panelW - 20f;
        float y = py + 8f;
        float rowH = 18f;

        // 1. Fuerza de Coulomb
        GUI.color = new Color(0.4f, 0.85f, 1f, 0.98f);
        GUI.Label(new Rect(x, y, w, rowH), "Fuerza Eléctrica  F = k·|q·Q| / r²", titleStyle);
        y += rowH;

        bool hasCoulomb = (Time.time - lastForceTimestamp) < 0.2f && latestForceData.ForceMagnitude > 0.01f;
        if (hasCoulomb)
        {
            float k = latestForceData.CoulombConstant;
            float q1 = latestForceData.ChargeSource;
            float q2 = latestForceData.ChargeTarget;
            float r = latestForceData.Distance;
            float f = latestForceData.ForceMagnitude;
            string tipo = latestForceData.IsAttractive ? "Atrae" : "Repele";

            GUI.color = new Color(0.75f, 1f, 0.85f, 0.98f);
            GUI.Label(new Rect(x, y, w, rowH), $"F = {k:F0}·|{q1:F1}·{q2:F2}| / {r:F1}² = {f:F1} N ({tipo})", labelStyle);
        }
        else
        {
            GUI.color = new Color(0.78f, 0.84f, 0.92f, 0.78f);
            GUI.Label(new Rect(x, y, w, rowH), "F = 0.0 N  (lejos de cargas)", labelStyle);
        }
        y += rowH + 5f;

        // 2. Descarga RC (THEIA)
        GUI.color = new Color(0.4f, 0.85f, 1f, 0.98f);
        GUI.Label(new Rect(x, y, w, rowH), "Carga THEIA  Q(t) = Q₀·e^(-t/RC)", titleStyle);
        y += rowH;

        string suelo = isGroundedOnConductor ? "Suelo" : "Aire";
        GUI.color = isGroundedOnConductor ? new Color(1f, 0.88f, 0.35f, 0.98f) : new Color(0.88f, 0.94f, 1f, 0.95f);
        GUI.Label(new Rect(x, y, w, rowH), $"Q = {currentPlayerChargeQ:F2} C   |   RC = {rcTimeConstantTau:F1} s ({suelo})", labelStyle);
        y += rowH + 5f;

        // 3. Corriente Eléctrica y Magnetismo (Lorentz / CurrentRod / FieldZone)
        GUI.color = new Color(0.4f, 0.85f, 1f, 0.98f);
        GUI.Label(new Rect(x, y, w, rowH), "Magnetismo y Lorentz  Fm = q·(v×B)", titleStyle);
        y += rowH;

        bool hasMag = (Time.time - lastMagTimestamp) < 0.25f && latestMagData.MagneticForceMag > 0.01f;
        if (hasMag)
        {
            GUI.color = new Color(0.95f, 0.7f, 1f, 0.98f);
            GUI.Label(new Rect(x, y, w, rowH),
                $"I = {latestMagData.CurrentIntensityI:F1} A  |  B = {latestMagData.MagneticFieldB:F1} T  |  Fm = {latestMagData.MagneticForceMag:F1} N",
                labelStyle);
        }
        else
        {
            float activeRodCurrent = GetAnyActiveRodCurrent();
            GUI.color = new Color(0.78f, 0.84f, 0.92f, 0.78f);
            GUI.Label(new Rect(x, y, w, rowH),
                activeRodCurrent > 0.01f
                    ? $"I = {activeRodCurrent:F1} A (Barra activa)  |  Fm = 0.0 N"
                    : "I = 0.0 A   |   Fm = 0.0 N",
                labelStyle);
        }
        y += rowH + 5f;

        // 4. Campo Eléctrico E y Velocidad v
        float speed = cachedPlayerRb != null ? cachedPlayerRb.linearVelocity.magnitude : 0f;
        GUI.color = new Color(0.82f, 0.96f, 1f, 0.92f);
        GUI.Label(new Rect(x, y, w, rowH), $"Campo |E| = {latestNetElectricField.magnitude:F1} N/C   |   Vel = {speed:F1} m/s", labelStyle);

        GUI.color = Color.white;
        return py + panelH;
    }

    private void DrawTeleportMenuPanel(float startY)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        runtimeSavedPoints.TryGetValue(sceneName, out List<TeleportEntry> savedList);
        int savedCount = savedList != null ? savedList.Count : 0;
        int totalButtons = sceneTeleportPoints.Count + savedCount;

        float panelW = 245f;
        float btnH = 21f;
        float panelH = 68f + (totalButtons * (btnH + 3f)) + 30f;
        float px = Screen.width - panelW - 10f;
        float py = startY;

        GUI.color = new Color(0.03f, 0.06f, 0.12f, 0.88f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

        GUI.color = new Color(0.25f, 0.95f, 0.65f, 0.8f);
        GUI.DrawTexture(new Rect(px, py, panelW, 2f), Texture2D.whiteTexture);

        float x = px + 8f;
        float w = panelW - 16f;
        float y = py + 6f;

        GUI.color = new Color(0.35f, 1f, 0.75f, 0.98f);
        GUI.Label(new Rect(x, y, w, 16f), $"TELEPORT DEMO — {sceneName}", titleStyle);
        y += 20f;

        // Botones de utilidad rápida: Guardar punto actual + Curar HP
        GUI.color = Color.white;
        GUI.backgroundColor = new Color(0.15f, 0.45f, 0.35f, 0.9f);
        float halfW = (w - 4f) * 0.5f;
        if (GUI.Button(new Rect(x, y, halfW, 20f), "+ Guardar Pos", btnIdleStyle))
        {
            SaveCurrentPlayerPosition();
        }
        if (GUI.Button(new Rect(x + halfW + 4f, y, halfW, 20f), "+ Curar Vida", btnIdleStyle))
        {
            HealPlayerFull();
        }
        y += 24f;

        // Puntos guardados en vivo por el usuario
        if (savedList != null)
        {
            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.35f, 0.9f);
            for (int i = 0; i < savedList.Count; i++)
            {
                if (GUI.Button(new Rect(x, y, w, btnH), "📌 " + savedList[i].label, tpButtonStyle))
                {
                    TeleportPlayerTo(savedList[i].position);
                }
                y += btnH + 3f;
            }
        }

        // Puntos detectados en el mapa
        GUI.backgroundColor = new Color(0.1f, 0.22f, 0.35f, 0.85f);
        for (int i = 0; i < sceneTeleportPoints.Count; i++)
        {
            if (GUI.Button(new Rect(x, y, w, btnH), $"▸ {i + 1}. {sceneTeleportPoints[i].label}", tpButtonStyle))
            {
                TeleportPlayerTo(sceneTeleportPoints[i].position);
            }
            y += btnH + 3f;
        }

        y += 4f;
        // Cambio rápido de nivel por si se necesita saltar en la presentación
        float quarterW = (w - 9f) / 4f;
        GUI.backgroundColor = new Color(0.18f, 0.18f, 0.3f, 0.9f);
        if (GUI.Button(new Rect(x, y, quarterW, 20f), "Lvl 1", btnIdleStyle))
            SceneManager.LoadScene("Level 1");
        if (GUI.Button(new Rect(x + (quarterW + 3f), y, quarterW, 20f), "Lvl 2", btnIdleStyle))
            SceneManager.LoadScene("Level 2");
        if (GUI.Button(new Rect(x + (quarterW + 3f) * 2f, y, quarterW, 20f), "Boss", btnIdleStyle))
            SceneManager.LoadScene("Level 4");
        if (GUI.Button(new Rect(x + (quarterW + 3f) * 3f, y, quarterW, 20f), "Sandbox", btnIdleStyle))
            SceneManager.LoadScene("SceneTesting");

        GUI.backgroundColor = Color.white;
        GUI.color = Color.white;
    }

    private static float GetAnyActiveRodCurrent()
    {
        var rods = CurrentRod.ActiveRods;
        for (int i = 0; i < rods.Count; i++)
        {
            if (rods[i] != null && rods[i].currentActive)
                return rods[i].GetCurrentIntensityAmps();
        }
        return 0f;
    }
}
