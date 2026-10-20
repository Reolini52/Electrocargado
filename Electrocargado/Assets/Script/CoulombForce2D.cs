using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Motor físico vectorial de la Ley de Coulomb en 2D:
/// \vec{F}_e = k_e * (q_1 * q_2 / r^2) * \hat{r}
/// Gestiona el registro global libre de basura (GC-free) de fuentes electrostáticas,
/// aplica clamping de seguridad (r_min y F_max) en FixedUpdate() y emite telemetría en vivo a GameEvents.
/// </summary>
[DisallowMultipleComponent]
public class CoulombForce2D : MonoBehaviour
{
    #region Registro Estático de Cargas (Cero GetComponent / Find en Update o FixedUpdate)

    private static readonly List<CoulombForce2D> ActiveNodes = new List<CoulombForce2D>(32);

    /// <summary>
    /// Lista de solo lectura con todos los nodos electrostáticos activos en la escena.
    /// Ideal para que ElectricFieldVisualizer calcule la superposición \vec{E}(x,y) sin usar FindObjects.
    /// </summary>
    public static IReadOnlyList<CoulombForce2D> RegisteredNodes => ActiveNodes;

    #endregion

    #region Parámetros Físicos de la Ley de Coulomb

    [Header("Constantes y Parámetros Electrostáticos (Ley de Coulomb)")]
    [Tooltip("Constante electrostática efectiva k_e adaptada a la escala de unidades de Unity 2D.")]
    [SerializeField, Range(1f, 500f)]
    private float coulombConstantKe = 85.0f;

    [Tooltip("Carga eléctrica propia del nodo (q_1) cuando no está gobernado por CargaResidual.")]
    [SerializeField, Range(-10f, 10f)]
    private float staticChargeValue = 1.0f;

    [Tooltip("Radio máximo de influencia electrostática (r_max) en unidades de mundo.")]
    [SerializeField, Range(1f, 40f)]
    private float interactionRadius = 12.0f;

    [Header("Clamping de Seguridad Numérica y Estabilidad (Rigidbody2D)")]
    [Tooltip("Distancia mínima r_min para evitar singularidades (división por cero o fuerzas infinitas cuando r -> 0).")]
    [SerializeField, Range(0.25f, 3f)]
    private float minSafeDistanceRMin = 0.75f;

    [Tooltip("Magnitud máxima de fuerza electrostática aplicable por par en un paso de física (|F_max|).")]
    [SerializeField, Range(5f, 250f)]
    private float maxForceMagnitudeClamp = 45.0f;

    [Tooltip("Velocidad lineal máxima permitida en el Rigidbody2D debido a impulsos electrostáticos.")]
    [SerializeField, Range(5f, 50f)]
    private float maxInducedSpeedClamp = 18.0f;

    [Header("Rol del Nodo en la Simulación")]
    [Tooltip("Si es true, este objeto es una fuente fija (ej. generador/imán estático) y no recibe aceleración.")]
    [SerializeField]
    private bool isFixedSource = false;

    [Tooltip("Si es true, este nodo emite eventos de telemetría a la Capa 2 (DebugOverlayUI). Activar en THEIA.")]
    [SerializeField]
    private bool broadcastTelemetryEvents = true;

    #endregion

    #region Referencias Cacheadas (Inicializadas exclusivamente en Awake)

    private Rigidbody2D cachedRb;
    private CargaResidual cachedCargaResidual;
    private ChargeResource cachedLegacyCharge;
    private ChargedObject cachedLegacyFixedObj;
    private DynamicChargedObject cachedLegacyDynObj;

    // Telemetría instantánea de la última interacción dominante para Debug / Gizmos
    private CoulombForceData lastDominantInteraction;
    private Vector2 netForceThisStep;

    #endregion

    #region Propiedades Públicas

    /// <summary>
    /// Obtiene la carga instantánea q del nodo leyendo de CargaResidual (si es THEIA) o de su valor configurado.
    /// </summary>
    public float CurrentCharge
    {
        get
        {
            if (cachedCargaResidual != null)
                return cachedCargaResidual.CurrentCharge;
            if (cachedLegacyCharge != null)
                return cachedLegacyCharge.GetCharge();
            if (cachedLegacyFixedObj != null)
                return cachedLegacyFixedObj.charge;
            if (cachedLegacyDynObj != null)
                return cachedLegacyDynObj.charge;

            return staticChargeValue;
        }
        set
        {
            staticChargeValue = value;
            if (cachedCargaResidual != null)
                cachedCargaResidual.SetCharge(value);
        }
    }

    public float CoulombConstantKe => coulombConstantKe;
    public float InteractionRadius => interactionRadius;
    public float MinSafeDistanceRMin => minSafeDistanceRMin;
    public bool IsFixedSource => isFixedSource;
    public Vector2 NetForceThisStep => netForceThisStep;
    public CoulombForceData LastDominantInteraction => lastDominantInteraction;

    #endregion

    #region Ciclo de Vida y Registro sin Garbage Collection

    private void Awake()
    {
        cachedRb = GetComponent<Rigidbody2D>();
        cachedCargaResidual = GetComponent<CargaResidual>();
        cachedLegacyCharge = GetComponent<ChargeResource>();
        cachedLegacyFixedObj = GetComponent<ChargedObject>();
        cachedLegacyDynObj = GetComponent<DynamicChargedObject>();

        // Si no tiene Rigidbody2D o es Kinematic/Static, actúa como fuente fija
        if (cachedRb == null || cachedRb.bodyType != RigidbodyType2D.Dynamic)
        {
            isFixedSource = true;
        }
    }

    private void OnEnable()
    {
        if (!ActiveNodes.Contains(this))
        {
            ActiveNodes.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveNodes.Remove(this);
    }

    private void FixedUpdate()
    {
        // Las fuentes fijas no sufren aceleración; solo ejercen campo sobre cuerpos dinámicos (como THEIA)
        if (isFixedSource || cachedRb == null) return;

        float qTarget = CurrentCharge;
        if (Mathf.Abs(qTarget) <= 0.01f)
        {
            netForceThisStep = Vector2.zero;
            return;
        }

        ApplySuperpositionCoulombForces(qTarget);
    }

    #endregion

    #region Motor Físico Vectorial: Ley de Coulomb y Principio de Superposición

    /// <summary>
    /// Calcula la superposición vectorial de fuerzas electrostáticas ejercidas por todas las cargas fuente:
    /// \vec{F}_e = \sum k_e * (q_1 * q_2 / r^2) * \hat{r}
    /// </summary>
    private void ApplySuperpositionCoulombForces(float qTarget)
    {
        Vector2 myPos = cachedRb.position;
        netForceThisStep = Vector2.zero;

        float maxSingleForceMag = -1f;
        CoulombForceData dominantData = default;
        bool hadInteraction = false;

        int nodeCount = ActiveNodes.Count;
        for (int i = 0; i < nodeCount; i++)
        {
            CoulombForce2D sourceNode = ActiveNodes[i];
            if (sourceNode == null || sourceNode == this) continue;

            float qSource = sourceNode.CurrentCharge;
            if (Mathf.Abs(qSource) <= 0.01f) continue;

            Vector2 sourcePos = sourceNode.transform.position;
            Vector2 displacement = myPos - sourcePos; // Vector desde la fuente q_1 hacia q_2 (THEIA)
            float rawDistance = displacement.magnitude;

            // Fuera del radio de corte de interacción
            if (rawDistance > sourceNode.interactionRadius && rawDistance > interactionRadius)
                continue;

            // Dirección unitaria colineal \hat{r} (saliente de la fuente hacia el objetivo)
            Vector2 rHat = rawDistance > 0.0001f ? (displacement / rawDistance) : Vector2.up;

            // Clamping de seguridad en distancia mínima r_min para evitar singularidad 1/r^2 -> \infty
            float clampedDistance = Mathf.Max(rawDistance, minSafeDistanceRMin);

            // Constante efectiva del par interactuante
            float ke = 0.5f * (coulombConstantKe + sourceNode.coulombConstantKe);

            // Ley de Coulomb Escalar con Signo: F = k_e * (q_1 * q_2) / r^2
            // Si q_1 * q_2 > 0 (mismo signo) -> Repulsión (+rHat, vectores colineales salientes)
            // Si q_1 * q_2 < 0 (signo opuesto) -> Atracción (-rHat, vectores colineales entrantes)
            float signedForceScalar = ke * (qSource * qTarget) / (clampedDistance * clampedDistance);

            // Clamping de seguridad en magnitud máxima de fuerza
            float clampedForceScalar = Mathf.Clamp(signedForceScalar, -maxForceMagnitudeClamp, maxForceMagnitudeClamp);
            Vector2 pairForceVector = rHat * clampedForceScalar;

            netForceThisStep += pairForceVector;

            float absMag = Mathf.Abs(clampedForceScalar);
            if (absMag > maxSingleForceMag)
            {
                maxSingleForceMag = absMag;
                bool isAttractive = (qSource * qTarget) < 0f;
                dominantData = new CoulombForceData(
                    sourcePos,
                    myPos,
                    qSource,
                    qTarget,
                    clampedDistance,
                    ke,
                    pairForceVector,
                    isAttractive
                );
                hadInteraction = true;
            }
        }

        if (hadInteraction)
        {
            // Clamping global del vector neto resultante antes de inyectarlo al Rigidbody2D
            if (netForceThisStep.sqrMagnitude > maxForceMagnitudeClamp * maxForceMagnitudeClamp)
            {
                netForceThisStep = netForceThisStep.normalized * maxForceMagnitudeClamp;
            }

            cachedRb.AddForce(netForceThisStep, ForceMode2D.Force);

            // Clamping de seguridad en velocidad máxima inducida para preservar el Game Feel
            if (cachedRb.linearVelocity.sqrMagnitude > maxInducedSpeedClamp * maxInducedSpeedClamp)
            {
                cachedRb.linearVelocity = cachedRb.linearVelocity.normalized * maxInducedSpeedClamp;
            }

            lastDominantInteraction = dominantData;

            if (broadcastTelemetryEvents)
            {
                GameEvents.RaiseForceApplied(dominantData);
            }
        }
    }

    /// <summary>
    /// Evalúa analíticamente el vector de Campo Eléctrico neto \vec{E}(x,y) = \sum k_e (q_i / r_i^2) \hat{r}_i
    /// en cualquier punto arbitrario del espacio 2D (utilizado por la Capa 2: ElectricFieldVisualizer).
    /// </summary>
    public static Vector2 EvaluateNetElectricFieldAtPoint(Vector2 worldPoint, float minR = 0.5f)
    {
        Vector2 netField = Vector2.zero;
        int count = ActiveNodes.Count;

        for (int i = 0; i < count; i++)
        {
            CoulombForce2D node = ActiveNodes[i];
            if (node == null) continue;

            float q = node.CurrentCharge;
            if (Mathf.Abs(q) <= 0.01f) continue;

            Vector2 rVec = worldPoint - (Vector2)node.transform.position;
            float dist = Mathf.Max(rVec.magnitude, minR);
            if (dist > node.interactionRadius) continue;

            Vector2 rHat = rVec.sqrMagnitude > 0.0001f ? rVec.normalized : Vector2.right;
            float eMag = node.coulombConstantKe * q / (dist * dist);
            netField += rHat * eMag;
        }

        return netField;
    }

    #endregion

    #region Gizmos de Visualización Vectorial (Editor & Debug)

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CurrentCharge > 0f
            ? new Color(0.25f, 0.65f, 1f, 0.35f)
            : CurrentCharge < 0f
                ? new Color(1f, 0.28f, 0.32f, 0.35f)
                : new Color(0.7f, 0.7f, 0.7f, 0.25f);

        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        // Esfera interna de distancia mínima de seguridad r_min
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, minSafeDistanceRMin);

        if (Application.isPlaying && netForceThisStep.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(netForceThisStep * 0.1f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.08f);
        }
    }

    #endregion
}
