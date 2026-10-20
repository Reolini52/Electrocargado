using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Barra conductora de Corriente Eléctrica (I = dQ/dt) que genera un Campo Magnético B
/// alrededor del conductor (Ley de Biot-Savart / Ampère) y aplica Fuerza Magnética sobre cargas.
/// </summary>
public class CurrentRod : MonoBehaviour
{
    public static readonly List<CurrentRod> ActiveRods = new List<CurrentRod>(8);

    [Header("Settings")]
    public float magneticRadius = 8f;
    public float magneticStrength = 25f;
    public bool currentActive = false;

    [Header("Rod")]
    public float rodLength = 4f;

    [Header("Electrons (Corriente I = n·q·v_d / L)")]
    public int electronCount = 8;
    public float electronSpeed = 3f;
    public float electronSize = 0.12f;

    [Tooltip("Carga elemental efectiva por portador para el cálculo de corriente I (Amperios).")]
    [SerializeField, Range(0.05f, 1f)]
    private float carrierCharge = 0.25f;

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    private List<GameObject> electrons = new List<GameObject>();
    private List<SpriteRenderer> electronRenderers = new List<SpriteRenderer>();
    private List<float> electronOffsets = new List<float>();
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

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();

        float zRot = Mathf.Abs(transform.eulerAngles.z % 180f);
        isHorizontal = (zRot > 45f && zRot < 135f);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }

        CreateElectrons();
        UpdateVisual();
    }

    /// <summary>
    /// Calcula la intensidad de corriente eléctrica I = (N · q_e · v_d) / L en Amperios (A).
    /// </summary>
    public float GetCurrentIntensityAmps()
    {
        if (!currentActive || rodLength <= 0.01f) return 0f;
        return (electronCount * carrierCharge * Mathf.Abs(electronSpeed)) / rodLength;
    }

    void CreateElectrons()
    {
        foreach (var e in electrons)
            if (e != null) Destroy(e);
        electrons.Clear();
        electronRenderers.Clear();
        electronOffsets.Clear();

        float halfLength = rodLength / 2f;

        for (int i = 0; i < electronCount; i++)
        {
            GameObject e = new GameObject($"Electron_{i}");
            e.transform.parent = transform;
            e.transform.localRotation = Quaternion.identity;

            SpriteRenderer esr = e.AddComponent<SpriteRenderer>();
            esr.material = new Material(Shader.Find("Sprites/Default"));
            esr.color = new Color(1f, 0.9f, 0.2f, 1f);
            esr.sortingOrder = 5;
            if (sr != null) esr.sprite = sr.sprite;
            e.transform.localScale = Vector3.one * electronSize;

            float offset = Mathf.Lerp(
                -halfLength + 0.05f,
                halfLength - 0.05f,
                (float)i / electronCount);
            electronOffsets.Add(offset);

            if (isHorizontal)
                e.transform.localPosition = new Vector3(offset, 0f, 0f);
            else
                e.transform.localPosition = new Vector3(0f, offset, 0f);

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
        float halfLength = rodLength / 2f - 0.05f;

        for (int i = 0; i < electrons.Count; i++)
        {
            if (electrons[i] == null) continue;

            electronOffsets[i] += electronSpeed * Time.deltaTime;

            if (electronSpeed > 0 && electronOffsets[i] > halfLength)
                electronOffsets[i] = -halfLength;
            else if (electronSpeed < 0 && electronOffsets[i] < -halfLength)
                electronOffsets[i] = halfLength;

            if (isHorizontal)
                electrons[i].transform.localPosition = new Vector3(
                    electronOffsets[i], 0f, 0f);
            else
                electrons[i].transform.localPosition = new Vector3(
                    0f, electronOffsets[i], 0f);

            electrons[i].transform.rotation = Quaternion.identity;

            float normalizedPos = Mathf.Abs(electronOffsets[i]) / Mathf.Max(0.01f, halfLength);
            float sizeMult = Mathf.Lerp(1.3f, 0.5f, normalizedPos);
            electrons[i].transform.localScale =
                Vector3.one * electronSize * sizeMult;

            float alpha = Mathf.Lerp(1f, 0.15f, normalizedPos);
            electronRenderers[i].color = new Color(1f, 0.9f, 0.2f, alpha);
        }
    }

    public void ActivateCurrent(bool active, bool positiveTerminal = true)
    {
        currentActive = active;
        float speed = Mathf.Abs(electronSpeed);
        electronSpeed = positiveTerminal ? -speed : speed;

        foreach (var e in electrons)
            if (e != null) e.SetActive(active);
    }

    void FixedUpdate()
    {
        if (!currentActive || player == null || playerRb == null) return;
        if (player.IsNeutral()) return;

        Vector2 rodPos = transform.position;
        Vector2 playerPos = player.transform.position;
        float dist = Vector2.Distance(rodPos, playerPos);
        if (dist > magneticRadius || dist < 0.1f) return;

        Vector2 dirToPlayer = (playerPos - rodPos).normalized;
        Vector2 perpendicular = new Vector2(-dirToPlayer.y, dirToPlayer.x);

        float normalizedDist = dist / magneticRadius;
        float currentI = GetCurrentIntensityAmps();

        // Campo magnético B proporcional a I e inversamente proporcional a la distancia
        float fieldB = (magneticStrength * 0.1f) * currentI / Mathf.Max(dist, 0.5f);
        float forceMag = magneticStrength * (1f - normalizedDist) * Mathf.Abs(player.GetCharge());

        if (player.GetCharge() < 0) perpendicular *= -1;
        if (electronSpeed < 0) perpendicular *= -1;

        Vector2 magForceVec = perpendicular * forceMag;
        playerRb.AddForce(magForceVec, ForceMode2D.Force);

        GameEvents.RaiseMagneticForceApplied(new CurrentMagneticData(
            rodPos,
            playerPos,
            currentI,
            fieldB,
            dist,
            magForceVec
        ));
    }

    void UpdateVisual()
    {
        if (sr != null)
            sr.color = currentActive ?
                new Color(0.1f, 0.2f, 0.5f) :
                new Color(0.4f, 0.4f, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = currentActive ? Color.yellow : Color.grey;
        Gizmos.DrawWireSphere(transform.position, magneticRadius);
    }
}
