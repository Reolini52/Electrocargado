using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Capa 2 (Comprender): Visualizador en tiempo real de la rejilla vectorial de Campo Eléctrico Ambiental
/// y su deformación dinámica (Bending) por el principio de superposición:
/// \vec{E}(x,y) = \sum_{i=1}^{N} k_e * (q_i / r_i^2) * \hat{r}_i
/// Se activa y desactiva mediante el bus de eventos GameEvents.OnDebugOverlayToggled (Tecla V).
/// </summary>
[DisallowMultipleComponent]
public class ElectricFieldVisualizer : MonoBehaviour
{
    #region Parámetros de la Rejilla Vectorial \vec{E}(x,y)

    [Header("Configuración de Rejilla de Campo Eléctrico E(x,y)")]
    [Tooltip("Número de columnas de vectores en la rejilla alrededor de la cámara/THEIA.")]
    [SerializeField, Range(4, 20)]
    private int gridColumns = 11;

    [Tooltip("Número de filas de vectores en la rejilla alrededor de la cámara/THEIA.")]
    [SerializeField, Range(3, 14)]
    private int gridRows = 7;

    [Tooltip("Espaciado en metros (unidades de Unity) entre cada nodo de prueba de la rejilla.")]
    [SerializeField, Range(1.0f, 4.0f)]
    private float gridSpacing = 2.0f;

    [Tooltip("Longitud máxima visual de cada flecha de vector de campo eléctrico.")]
    [SerializeField, Range(0.3f, 1.5f)]
    private float maxArrowLength = 0.75f;

    [Tooltip("Distancia mínima de seguridad r_min al evaluar el campo cerca de una singularidad puntual.")]
    [SerializeField, Range(0.4f, 2.0f)]
    private float minFieldEvalDistance = 0.8f;

    [Header("Paleta Científica de Intensidad de Campo")]
    [Tooltip("Color para intensidad de campo eléctrico baja.")]
    [SerializeField]
    private Color lowFieldColor = new Color(0.2f, 0.75f, 1f, 0.35f);

    [Tooltip("Color para intensidad de campo eléctrico alta (Bending fuerte).")]
    [SerializeField]
    private Color highFieldColor = new Color(1f, 0.35f, 0.65f, 0.9f);

    #endregion

    #region Estado Interno y Pool de LineRenderers (Cero Alojamiento de Basura en Update)

    private bool isFieldOverlayActive = false;
    private readonly List<LineRenderer> arrowPool = new List<LineRenderer>(160);
    private Transform gridContainer;
    private ChargeResource cachedPlayer;

    #endregion

    #region Ciclo de Vida y Eventos

    private void Awake()
    {
        BuildArrowGridPool();
    }

    private void OnEnable()
    {
        GameEvents.OnDebugOverlayToggled += HandleDebugOverlayToggled;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        GameEvents.OnDebugOverlayToggled -= HandleDebugOverlayToggled;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        LocatePlayerInScene();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LocatePlayerInScene();
    }

    private void LocatePlayerInScene()
    {
        cachedPlayer = Object.FindAnyObjectByType<ChargeResource>();
    }

    private void HandleDebugOverlayToggled(bool isActive)
    {
        isFieldOverlayActive = isActive;
        if (gridContainer != null)
        {
            gridContainer.gameObject.SetActive(isFieldOverlayActive);
        }
    }

    private void Update()
    {
        if (!isFieldOverlayActive || GandalfNPC.isGandalfActive)
        {
            return;
        }

        if (cachedPlayer == null)
        {
            LocatePlayerInScene();
        }

        UpdateElectricFieldSuperpositionGrid();
    }

    #endregion

    #region Creación del Pool de Flechas Vectoriales

    private void BuildArrowGridPool()
    {
        GameObject containerObj = new GameObject("ElectricFieldVectorGrid_Pool");
        containerObj.transform.SetParent(transform);
        gridContainer = containerObj.transform;

        int totalArrows = gridColumns * gridRows;
        Shader defaultSpriteShader = Shader.Find("Sprites/Default");

        for (int i = 0; i < totalArrows; i++)
        {
            GameObject arrowObj = new GameObject($"E_Vector_{i}");
            arrowObj.transform.SetParent(gridContainer);

            LineRenderer lr = arrowObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 5; // Tallo + punta de flecha doble
            lr.startWidth = 0.045f;
            lr.endWidth = 0.045f;
            lr.material = new Material(defaultSpriteShader);
            lr.sortingOrder = 10;

            arrowPool.Add(lr);
        }

        gridContainer.gameObject.SetActive(false);
    }

    #endregion

    #region Cálculo de Superposición \vec{E}(x,y) = \sum k_e (q_i / r_i^2) \hat{r}_i

    private void UpdateElectricFieldSuperpositionGrid()
    {
        Vector2 centerOrigin = Vector2.zero;
        if (cachedPlayer != null)
        {
            centerOrigin = cachedPlayer.transform.position;
        }
        else if (Camera.main != null)
        {
            centerOrigin = Camera.main.transform.position;
        }

        // Anclar la rejilla a múltiplos discretos de gridSpacing en espacio mundial para evitar mareo visual al mover la cámara
        float snappedCenterX = Mathf.Round(centerOrigin.x / gridSpacing) * gridSpacing;
        float snappedCenterY = Mathf.Round(centerOrigin.y / gridSpacing) * gridSpacing;

        int halfCols = gridColumns / 2;
        int halfRows = gridRows / 2;
        int arrowIndex = 0;

        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridColumns; c++)
            {
                if (arrowIndex >= arrowPool.Count) break;

                LineRenderer lr = arrowPool[arrowIndex++];
                Vector2 samplePos = new Vector2(
                    snappedCenterX + (c - halfCols) * gridSpacing,
                    snappedCenterY + (r - halfRows) * gridSpacing
                );

                Vector2 eField = ComputeNetFieldAtPoint(samplePos);
                float eMag = eField.magnitude;

                if (eMag < 0.05f)
                {
                    lr.enabled = false;
                    continue;
                }

                lr.enabled = true;
                Vector2 dir = eField / eMag;

                // Escala logarítmica/suavizada para visualizar tanto campos débiles como intensos (Bending)
                float normalizedIntensity = Mathf.Clamp01(Mathf.Log10(1f + eMag * 0.35f));
                float arrowLen = Mathf.Lerp(0.25f, maxArrowLength, normalizedIntensity);

                Vector2 tail = samplePos - dir * (arrowLen * 0.45f);
                Vector2 tip = samplePos + dir * (arrowLen * 0.55f);
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector2 wingLeft = tip - dir * (arrowLen * 0.28f) + perp * (arrowLen * 0.18f);
                Vector2 wingRight = tip - dir * (arrowLen * 0.28f) - perp * (arrowLen * 0.18f);

                lr.SetPosition(0, tail);
                lr.SetPosition(1, tip);
                lr.SetPosition(2, wingLeft);
                lr.SetPosition(3, tip);
                lr.SetPosition(4, wingRight);

                Color vecColor = Color.Lerp(lowFieldColor, highFieldColor, normalizedIntensity);
                lr.startColor = vecColor;
                lr.endColor = vecColor;
            }
        }

        // Notificar el campo eléctrico neto exacto en la posición de THEIA para el panel matemático
        if (cachedPlayer != null)
        {
            Vector2 playerPos = cachedPlayer.transform.position;
            Vector2 netFieldAtPlayer = ComputeNetFieldAtPoint(playerPos, excludePlayerSelfField: true);
            GameEvents.RaiseElectricFieldUpdated(playerPos, netFieldAtPlayer);
        }
    }

    /// <summary>
    /// Suma vectorialmente las contribuciones de todas las fuentes ChargedObject, DynamicChargedObject
    /// y de la propia carga de THEIA (efecto Bending sobre el campo ambiental).
    /// </summary>
    public Vector2 ComputeNetFieldAtPoint(Vector2 samplePos, bool excludePlayerSelfField = false)
    {
        Vector2 netE = Vector2.zero;

        // 1. Contribución de fuentes estáticas (ChargedObject)
        var staticSources = ChargedObject.ActiveChargedObjects;
        for (int i = 0; i < staticSources.Count; i++)
        {
            ChargedObject src = staticSources[i];
            if (src == null || Mathf.Abs(src.charge) <= 0.01f) continue;

            Vector2 rVec = samplePos - (Vector2)src.transform.position;
            float rawDist = rVec.magnitude;
            if (rawDist > src.effectRadius * 1.4f || rawDist < 0.001f) continue;

            float clampedR = Mathf.Max(rawDist, minFieldEvalDistance);
            Vector2 rHat = rVec / rawDist;
            float eScalar = src.forceStrength * src.charge / (clampedR * clampedR);
            netE += rHat * eScalar;
        }

        // 2. Contribución de objetos dinámicos cargados (DynamicChargedObject)
        var dynSources = DynamicChargedObject.ActiveDynamicObjects;
        for (int i = 0; i < dynSources.Count; i++)
        {
            DynamicChargedObject dyn = dynSources[i];
            if (dyn == null || Mathf.Abs(dyn.charge) <= 0.01f) continue;

            Vector2 rVec = samplePos - (Vector2)dyn.transform.position;
            float rawDist = rVec.magnitude;
            if (rawDist > dyn.effectRadius * 1.4f || rawDist < 0.001f) continue;

            float clampedR = Mathf.Max(rawDist, minFieldEvalDistance);
            Vector2 rHat = rVec / rawDist;
            float eScalar = dyn.forceStrength * dyn.charge / (clampedR * clampedR);
            netE += rHat * eScalar;
        }

        // 3. Efecto Bending de la propia carga de THEIA sobre la rejilla visual
        if (!excludePlayerSelfField && cachedPlayer != null && !cachedPlayer.IsNeutral())
        {
            Vector2 rVec = samplePos - (Vector2)cachedPlayer.transform.position;
            float rawDist = rVec.magnitude;
            if (rawDist > 0.001f && rawDist < 10f)
            {
                float clampedR = Mathf.Max(rawDist, minFieldEvalDistance);
                Vector2 rHat = rVec / rawDist;
                float eScalar = 45f * cachedPlayer.GetCharge() / (clampedR * clampedR);
                netE += rHat * eScalar;
            }
        }

        return netE;
    }

    #endregion

    #region Gizmos en el Editor

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.4f);
        Vector3 origin = transform.position;
        Gizmos.DrawWireCube(origin, new Vector3(gridColumns * gridSpacing, gridRows * gridSpacing, 0.1f));
    }

    #endregion
}
