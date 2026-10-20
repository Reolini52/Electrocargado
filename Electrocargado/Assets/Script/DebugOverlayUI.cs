using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Overlay de vectores y telemetría matemática de THEIA.
/// [V]: Vectores (Fuerza Fe, Fuerza Magnética Fm, Velocidad v y Campo E)
/// [B] o [T]: Cuadro compacto de ecuaciones en esquina superior derecha
/// [H]: Ocultar todo el HUD
/// </summary>
[DisallowMultipleComponent]
public class DebugOverlayUI : MonoBehaviour
{
    private static DebugOverlayUI instance;

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

    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle btnActiveStyle;
    private GUIStyle btnIdleStyle;

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
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LocatePlayerRigidbodyOnce();
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

        if (Keyboard.current.bKey.wasPressedThisFrame || Keyboard.current.tKey.wasPressedThisFrame)
        {
            ToggleMathTableLayer();
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

        // 3. Vector Fm (Fuerza Magnética por Corriente)
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
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = new Color(0.4f, 0.85f, 1f, 0.95f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            wordWrap = false
        };
        labelStyle.normal.textColor = new Color(0.9f, 0.95f, 1f, 0.9f);

        btnActiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        btnActiveStyle.normal.textColor = new Color(0.3f, 1f, 0.75f);

        btnIdleStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        btnIdleStyle.normal.textColor = new Color(0.8f, 0.88f, 1f, 0.8f);
    }

    private void OnGUI()
    {
        if (GandalfNPC.isGandalfActive || hideAllHudCleanMode) return;

        InitStyles();
        DrawFloatingTopRightButtons();

        if (showMathTables)
        {
            DrawCompactCornerMathHUD();
        }
    }

    private void DrawFloatingTopRightButtons()
    {
        float btnW = 96f;
        float btnH = 20f;
        float gap = 5f;
        float rightMargin = 10f;
        float topMargin = 8f;

        Rect vecRect = new Rect(Screen.width - (btnW * 2f + gap + rightMargin), topMargin, btnW, btnH);
        GUI.backgroundColor = showVectorGraphics
            ? new Color(0.1f, 0.45f, 0.55f, 0.85f)
            : new Color(0.08f, 0.1f, 0.18f, 0.55f);

        if (GUI.Button(vecRect, "[V] Vectores", showVectorGraphics ? btnActiveStyle : btnIdleStyle))
        {
            ToggleVectorGraphicsLayer();
        }

        Rect mathRect = new Rect(Screen.width - (btnW + rightMargin), topMargin, btnW, btnH);
        GUI.backgroundColor = showMathTables
            ? new Color(0.1f, 0.45f, 0.55f, 0.85f)
            : new Color(0.08f, 0.1f, 0.18f, 0.55f);

        if (GUI.Button(mathRect, "[B] Datos", showMathTables ? btnActiveStyle : btnIdleStyle))
        {
            ToggleMathTableLayer();
        }

        GUI.backgroundColor = Color.white;
    }

    private void DrawCompactCornerMathHUD()
    {
        float panelW = 235f;
        float panelH = 148f;
        float px = Screen.width - panelW - 10f;
        float py = 32f;

        // Fondo oscuro translúcido del mismo estilo que el resto del juego
        GUI.color = new Color(0.03f, 0.05f, 0.12f, 0.58f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.8f, 1f, 0.65f);
        GUI.DrawTexture(new Rect(px, py, panelW, 2f), Texture2D.whiteTexture);

        float x = px + 8f;
        float w = panelW - 16f;
        float y = py + 5f;

        // 1. Fuerza de Coulomb
        GUI.color = new Color(0.4f, 0.85f, 1f, 0.95f);
        GUI.Label(new Rect(x, y, w, 14f), "Fuerza Eléctrica  F = k·|q·Q| / r²", titleStyle);
        y += 14f;

        bool hasCoulomb = (Time.time - lastForceTimestamp) < 0.2f && latestForceData.ForceMagnitude > 0.01f;
        if (hasCoulomb)
        {
            float k = latestForceData.CoulombConstant;
            float q1 = latestForceData.ChargeSource;
            float q2 = latestForceData.ChargeTarget;
            float r = latestForceData.Distance;
            float f = latestForceData.ForceMagnitude;
            string tipo = latestForceData.IsAttractive ? "Atrae" : "Repele";

            GUI.color = new Color(0.75f, 1f, 0.85f, 0.95f);
            GUI.Label(new Rect(x, y, w, 14f), $"F = {k:F0}·|{q1:F1}·{q2:F2}| / {r:F1}² = {f:F1} N ({tipo})", labelStyle);
        }
        else
        {
            GUI.color = new Color(0.75f, 0.8f, 0.88f, 0.7f);
            GUI.Label(new Rect(x, y, w, 14f), "F = 0.0 N  (lejos de cargas)", labelStyle);
        }
        y += 16f;

        // 2. Descarga RC (THEIA)
        GUI.color = new Color(0.4f, 0.85f, 1f, 0.95f);
        GUI.Label(new Rect(x, y, w, 14f), "Carga THEIA  Q(t) = Q₀·e^(-t/RC)", titleStyle);
        y += 14f;

        string suelo = isGroundedOnConductor ? "Suelo" : "Aire";
        GUI.color = isGroundedOnConductor ? new Color(1f, 0.88f, 0.35f, 0.95f) : new Color(0.85f, 0.92f, 1f, 0.9f);
        GUI.Label(new Rect(x, y, w, 14f), $"Q = {currentPlayerChargeQ:F2} C  |  RC = {rcTimeConstantTau:F1}s ({suelo})", labelStyle);
        y += 16f;

        // 3. Corriente Eléctrica y Magnetismo (CurrentRod / MagneticForce)
        GUI.color = new Color(0.4f, 0.85f, 1f, 0.95f);
        GUI.Label(new Rect(x, y, w, 14f), "Corriente y Magnetismo  I = q·v/L", titleStyle);
        y += 14f;

        bool hasMag = (Time.time - lastMagTimestamp) < 0.25f && latestMagData.MagneticForceMag > 0.01f;
        if (hasMag)
        {
            GUI.color = new Color(0.95f, 0.65f, 1f, 0.95f);
            GUI.Label(new Rect(x, y, w, 14f),
                $"I = {latestMagData.CurrentIntensityI:F1} A  |  B = {latestMagData.MagneticFieldB:F1} T  |  Fm = {latestMagData.MagneticForceMag:F1} N",
                labelStyle);
        }
        else
        {
            // Revisar si hay alguna barra CurrentRod activa en el nivel aunque THEIA esté lejos
            float activeRodCurrent = GetAnyActiveRodCurrent();
            GUI.color = new Color(0.75f, 0.8f, 0.88f, 0.7f);
            GUI.Label(new Rect(x, y, w, 14f),
                activeRodCurrent > 0.01f
                    ? $"I = {activeRodCurrent:F1} A (Barra activa)  |  Fm = 0.0 N"
                    : "I = 0.0 A  |  Fm = 0.0 N",
                labelStyle);
        }
        y += 16f;

        // 4. Campo Eléctrico E y Velocidad v
        float speed = cachedPlayerRb != null ? cachedPlayerRb.linearVelocity.magnitude : 0f;
        GUI.color = new Color(0.8f, 0.95f, 1f, 0.85f);
        GUI.Label(new Rect(x, y, w, 14f), $"Campo |E| = {latestNetElectricField.magnitude:F1} N/C   |   Vel = {speed:F1} m/s", labelStyle);
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
