using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Barra conductora de Corriente Eléctrica (I = dQ/dt) que genera un Campo Magnético B
/// alrededor del conductor según la Ley de Biot-Savart / Ampère y la Regla de la Mano Derecha:
///  - Corriente hacia ARRIBA (+Y): Campo magnético tangencial en sentido ANTIHORARIO (CCW) -> (-r.y, r.x).normalized
///  - Corriente hacia ABAJO (-Y):  Campo magnético tangencial en sentido HORARIO (CW)     -> (r.y, -r.x).normalized
///  - Magnitud: B = (mu0 * I) / (2 * PI * Mathf.Max(r.magnitude, minDistanceClamp))
/// </summary>
public class CurrentRod : MonoBehaviour
{
    public static readonly List<CurrentRod> ActiveRods = new List<CurrentRod>(8);

    [Header("Settings (Biot-Savart & Right-Hand Rule)")]
    public float magneticRadius = 8f;
    public float magneticStrength = 25f;
    public bool currentActive = false;

    [Tooltip("Permeabilidad magnética efectiva mu0 (escalada para unidades de gameplay 2D).")]
    [SerializeField, Range(1f, 50f)]
    private float mu0 = 12.56637f; // 4 * PI escalado

    [Tooltip("Distancia mínima (clamp) en el denominador para evitar singularidades cuando r -> 0.")]
    [SerializeField, Range(0.25f, 3f)]
    private float minDistanceClamp = 0.65f;

    [Tooltip("Fuerza máxima permitida (ClampMagnitude) para evitar eyecciones caóticas fuera del mapa.")]
    [SerializeField, Range(10f, 80f)]
    private float maxMagneticForce = 35f;

    [Header("Rod Geometry")]
    public float rodLength = 4f;

    [Header("Current Carriers (I = n·q·v_d / L)")]
    public int electronCount = 8;
    [Tooltip("Velocidad del flujo en la barra: > 0 fluye hacia ARRIBA (+Y, CCW), < 0 fluye hacia ABAJO (-Y, CW).")]
    public float electronSpeed = 3f;
    public float electronSize = 0.12f;

    [Tooltip("Carga elemental efectiva por portador para el cálculo de corriente I (Amperios).")]
    [SerializeField, Range(0.05f, 1f)]
    private float carrierCharge = 0.25f;

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;
    private Material sharedElectronMaterial;

    private readonly List<GameObject> electrons = new List<GameObject>();
    private readonly List<SpriteRenderer> electronRenderers = new List<SpriteRenderer>();
    private readonly List<float> electronOffsets = new List<float>();
    private bool isHorizontal = false;

    void OnEnable()
    {
        if (!ActiveRods.Contains(this))
            ActiveRods.Add(this);
    }

    void OnDisable()
    {
        ActiveRods.Remove(this);
    }

    void OnDestroy()
    {
        ActiveRods.Remove(this);
        if (sharedElectronMaterial != null)
        {
            Destroy(sharedElectronMaterial);
            sharedElectronMaterial = null;
        }
    }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();

        float zRot = Mathf.Abs(transform.eulerAngles.z % 180f);
        isHorizontal = (zRot > 45f && zRot < 135f);

        AcquirePlayerReferences();
        CreateElectrons();
        UpdateVisual();
    }

    private void AcquirePlayerReferences()
    {
        if (player != null && playerRb != null) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }
    }

    /// <summary>
    /// Indica si el flujo de corriente en la barra va hacia ARRIBA (+Y -> sentido Antihorario CCW)
    /// o hacia ABAJO (-Y -> sentido Horario CW) según la Regla de la Mano Derecha.
    /// </summary>
    public bool IsCurrentFlowingUpward()
    {
        return electronSpeed >= 0f;
    }

    /// <summary>
    /// Calcula la intensidad de corriente eléctrica I = (N · q_e · |v_d|) / L en Amperios (A).
    /// </summary>
    public float GetCurrentIntensityAmps()
    {
        if (!currentActive || rodLength <= 0.01f) return 0f;
        return (electronCount * carrierCharge * Mathf.Abs(electronSpeed)) / rodLength;
    }

    void CreateElectrons()
    {
        for (int i = 0; i < electrons.Count; i++)
        {
            if (electrons[i] != null)
                Destroy(electrons[i]);
        }
        electrons.Clear();
        electronRenderers.Clear();
        electronOffsets.Clear();

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (sharedElectronMaterial == null && spriteShader != null)
        {
            sharedElectronMaterial = new Material(spriteShader);
        }

        float halfLength = rodLength / 2f;
        int count = Mathf.Max(1, electronCount);

        for (int i = 0; i < count; i++)
        {
            GameObject e = new GameObject($"Electron_{i}");
            e.transform.SetParent(transform, false);
            e.transform.localRotation = Quaternion.identity;

            SpriteRenderer esr = e.AddComponent<SpriteRenderer>();
            if (sharedElectronMaterial != null)
                esr.sharedMaterial = sharedElectronMaterial;
            esr.color = new Color(1f, 0.9f, 0.2f, 1f);
            esr.sortingOrder = 5;
            if (sr != null) esr.sprite = sr.sprite;
            e.transform.localScale = Vector3.one * electronSize;

            float offset = Mathf.Lerp(
                -halfLength + 0.05f,
                halfLength - 0.05f,
                (float)i / count);
            electronOffsets.Add(offset);

            e.transform.localPosition = isHorizontal
                ? new Vector3(offset, 0f, 0f)
                : new Vector3(0f, offset, 0f);

            electrons.Add(e);
            electronRenderers.Add(esr);
            e.SetActive(currentActive);
        }
    }

    void Update()
    {
        if (currentActive)
            UpdateElectrons();
        UpdateVisual();
    }

    void UpdateElectrons()
    {
        float halfLength = Mathf.Max(0.05f, rodLength / 2f - 0.05f);

        for (int i = 0; i < electrons.Count; i++)
        {
            GameObject e = electrons[i];
            if (e == null) continue;

            electronOffsets[i] += electronSpeed * Time.deltaTime;

            if (electronSpeed > 0f && electronOffsets[i] > halfLength)
                electronOffsets[i] = -halfLength;
            else if (electronSpeed < 0f && electronOffsets[i] < -halfLength)
                electronOffsets[i] = halfLength;

            e.transform.localPosition = isHorizontal
                ? new Vector3(electronOffsets[i], 0f, 0f)
                : new Vector3(0f, electronOffsets[i], 0f);

            e.transform.rotation = Quaternion.identity;

            float normalizedPos = Mathf.Clamp01(Mathf.Abs(electronOffsets[i]) / halfLength);
            float sizeMult = Mathf.Lerp(1.3f, 0.5f, normalizedPos);
            e.transform.localScale = Vector3.one * (electronSize * sizeMult);

            if (i < electronRenderers.Count && electronRenderers[i] != null)
            {
                float alpha = Mathf.Lerp(1f, 0.15f, normalizedPos);
                electronRenderers[i].color = new Color(1f, 0.9f, 0.2f, alpha);
            }
        }
    }

    public void ActivateCurrent(bool active, bool positiveTerminal = true)
    {
        currentActive = active;
        float speed = Mathf.Max(0.1f, Mathf.Abs(electronSpeed));
        // positiveTerminal = true -> corriente hacia ARRIBA (+Y), false -> hacia ABAJO (-Y)
        electronSpeed = positiveTerminal ? speed : -speed;

        for (int i = 0; i < electrons.Count; i++)
        {
            if (electrons[i] != null)
                electrons[i].SetActive(active);
        }
    }

    void FixedUpdate()
    {
        if (!currentActive) return;

        if (player == null || playerRb == null)
        {
            AcquirePlayerReferences();
            if (player == null || playerRb == null) return;
        }

        if (player.IsNeutral()) return;

        // 1. Vector de desplazamiento relativo: r = player.position - rod.position
        Vector2 rodPos = transform.position;
        Vector2 playerPos = player.transform.position;
        Vector2 r = playerPos - rodPos;
        float dist = r.magnitude;

        if (dist > magneticRadius || dist < 0.05f) return;

        // 2. Dirección tangencial del campo magnético (Regla de la Mano Derecha en 2D):
        //    - Corriente hacia ARRIBA (+Y): Antihorario (Counter-Clockwise) -> (-r.y, r.x).normalized
        //    - Corriente hacia ABAJO (-Y):  Horario (Clockwise)            -> (r.y, -r.x).normalized
        bool currentUp = IsCurrentFlowingUpward();
        Vector2 fieldDir = currentUp
            ? new Vector2(-r.y, r.x).normalized
            : new Vector2(r.y, -r.x).normalized;

        // 3. Magnitud del campo magnético (Ley de Biot-Savart / Ampère con clamping en r):
        //    B = (mu0 * I) / (2 * PI * Mathf.Max(r.magnitude, minDistanceClamp))
        float currentI = GetCurrentIntensityAmps();
        float clampedDist = Mathf.Max(dist, minDistanceClamp);
        float fieldB = (mu0 * currentI) / (2f * Mathf.PI * clampedDist);

        // 4. Fuerza magnética / Lorentz resultante sobre THEIA modulada por su carga Q y atenuación espacial
        float qPlayer = player.GetCharge();
        float radialFalloff = Mathf.Clamp01(1f - (dist / magneticRadius));
        float rawForceMag = fieldB * magneticStrength * Mathf.Abs(qPlayer) * Mathf.Lerp(0.35f, 1f, radialFalloff);

        // El signo de la carga Q de THEIA invierte el sentido de empuje sobre la línea tangencial
        Vector2 rawForce = fieldDir * (Mathf.Sign(qPlayer) * rawForceMag);
        Vector2 magForceVec = Vector2.ClampMagnitude(rawForce, maxMagneticForce);

        EMForceAccumulator2D.ApplyForce(playerRb, magForceVec);

        GameEvents.RaiseMagneticForceApplied(new CurrentMagneticData(
            rodPos,
            playerPos,
            currentI,
            fieldB,
            clampedDist,
            magForceVec
        ));
    }

    void UpdateVisual()
    {
        if (sr != null)
        {
            sr.color = currentActive
                ? new Color(0.1f, 0.2f, 0.5f)
                : new Color(0.4f, 0.4f, 0.5f);
        }
    }

    /// <summary>
    /// Dibuja en el Editor el radio magnético, la flecha del pulgar (dirección de corriente I)
    /// y 8 flechas tangenciales de circulación de B para verificar la Regla de la Mano Derecha.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        bool currentUp = IsCurrentFlowingUpward();

        // 1. Esfera exterior de alcance y anillo interior de referencia
        Gizmos.color = currentActive ? new Color(1f, 0.9f, 0.2f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
        Gizmos.DrawWireSphere(center, magneticRadius);

        float ringRadius = magneticRadius * 0.55f;
        Gizmos.color = currentUp ? new Color(0.2f, 0.95f, 1f, 0.85f) : new Color(1f, 0.45f, 0.2f, 0.85f);

        // 2. Flecha central en la barra indicando el pulgar (Corriente I: +Y Arriba o -Y Abajo)
        Vector3 currentDir3D = currentUp ? Vector3.up : Vector3.down;
        Vector3 arrowStart = center - currentDir3D * (rodLength * 0.35f);
        Vector3 arrowTip = center + currentDir3D * (rodLength * 0.35f);
        Gizmos.DrawLine(arrowStart, arrowTip);
        DrawGizmoArrowHead(arrowTip, currentDir3D, 0.35f);

        // 3. Flechas tangenciales alrededor del cable verificando CCW (+Y) vs CW (-Y)
        const int sampleCount = 8;
        for (int i = 0; i < sampleCount; i++)
        {
            float angleRad = (i * Mathf.PI * 2f) / sampleCount;
            Vector2 r = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * ringRadius;
            Vector2 fieldDir = currentUp
                ? new Vector2(-r.y, r.x).normalized
                : new Vector2(r.y, -r.x).normalized;

            Vector3 samplePos = center + new Vector3(r.x, r.y, 0f);
            Vector3 tangentEnd = samplePos + new Vector3(fieldDir.x, fieldDir.y, 0f) * 0.85f;

            Gizmos.DrawLine(samplePos, tangentEnd);
            DrawGizmoArrowHead(tangentEnd, new Vector3(fieldDir.x, fieldDir.y, 0f), 0.25f);
        }
    }

    private static void DrawGizmoArrowHead(Vector3 tip, Vector3 dirNormalized, float size)
    {
        if (dirNormalized.sqrMagnitude < 0.0001f) return;
        Vector3 right = new Vector3(-dirNormalized.y, dirNormalized.x, 0f);
        Vector3 back = -dirNormalized * size;
        Gizmos.DrawLine(tip, tip + back + right * (size * 0.55f));
        Gizmos.DrawLine(tip, tip + back - right * (size * 0.55f));
    }
}
