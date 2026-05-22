using UnityEngine;
using System.Collections.Generic;

public class CurrentRod : MonoBehaviour
{
    [Header("Settings")]
    public float magneticRadius = 8f;
    public float magneticStrength = 25f;
    public bool currentActive = false;

    [Header("Rod Shape")]
    public float rodLength = 4f;
    public float rodWidth = 0.3f;

    [Header("Electrons")]
    public int electronCount = 6;
    public float electronSpeed = 2f;
    public Color electronColor = new Color(0.3f, 0.6f, 1f);
    public float electronSize = 0.15f;

    [Header("Field Visual")]
    public Color activeColor = new Color(1f, 0.8f, 0f, 0.3f);
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    // Electron objects
    private List<GameObject> electrons = new List<GameObject>();
    private List<float> electronOffsets = new List<float>();

    // Field ring visuals
    private List<GameObject> fieldRings = new List<GameObject>();

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.size = new Vector2(rodWidth, rodLength);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }

        CreateElectrons();
        CreateFieldRings();
        UpdateVisual();
    }

    void CreateElectrons()
    {
        for (int i = 0; i < electronCount; i++)
        {
            GameObject e = new GameObject($"Electron_{i}");
            e.transform.parent = transform;

            SpriteRenderer esr = e.AddComponent<SpriteRenderer>();
            esr.sprite = GetComponent<SpriteRenderer>().sprite;
            esr.color = electronColor;
            esr.sortingOrder = 2;
            e.transform.localScale = Vector3.one * electronSize;

            float offset = (float)i / electronCount * rodLength
                - rodLength / 2f;
            electronOffsets.Add(offset);
            e.transform.localPosition = new Vector3(0f, offset, 0f);

            electrons.Add(e);
            e.SetActive(false);
        }
    }

    void CreateFieldRings()
    {
        // Visual rings showing magnetic field circles around rod
        int ringCount = 3;
        for (int i = 0; i < ringCount; i++)
        {
            GameObject ring = new GameObject($"FieldRing_{i}");
            ring.transform.parent = transform;
            ring.transform.localPosition = Vector3.zero;

            // Draw ring using LineRenderer
            LineRenderer lr = ring.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 1;

            float radius = 1f + i * 0.8f;
            int segments = 32;
            lr.positionCount = segments;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f;
                lr.SetPosition(j, new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                ));
            }

            Color ringColor = new Color(1f, 0.8f, 0f, 0f);
            lr.startColor = ringColor;
            lr.endColor = ringColor;

            fieldRings.Add(ring);
        }
    }

    void Update()
    {
        if (currentActive)
        {
            UpdateElectrons();
            UpdateFieldRings();
        }

        UpdateVisual();
    }

    void UpdateElectrons()
    {
        for (int i = 0; i < electrons.Count; i++)
        {
            // Move electrons along rod
            electronOffsets[i] -= electronSpeed * Time.deltaTime;

            // Wrap around
            if (electronOffsets[i] < -rodLength / 2f)
                electronOffsets[i] += rodLength;

            electrons[i].transform.localPosition = new Vector3(
                0f, electronOffsets[i], 0f);
        }
    }

    void UpdateFieldRings()
    {
        // Animate rings expanding outward = field propagating
        for (int i = 0; i < fieldRings.Count; i++)
        {
            LineRenderer lr = fieldRings[i].GetComponent<LineRenderer>();
            if (lr == null) continue;

            float pulse = Mathf.PingPong(
                Time.time * 0.8f + i * 0.3f, 1f);

            Color ringColor = new Color(1f, 0.8f, 0f,
                pulse * 0.5f);
            lr.startColor = ringColor;
            lr.endColor = ringColor;
        }
    }

    public void ActivateCurrent(bool active, bool positiveTerminal = true)
    {
        currentActive = active;

        // Reverse electron direction based on which terminal is +
        electronSpeed = positiveTerminal ?
            Mathf.Abs(electronSpeed) :
            -Mathf.Abs(electronSpeed);

        foreach (var e in electrons)
            e.SetActive(active);
    }

    void FixedUpdate()
    {
        if (!currentActive || player == null) return;
        if (player.IsNeutral()) return;

        float dist = Vector2.Distance(
            transform.position, player.transform.position);
        if (dist > magneticRadius || dist < 0.1f) return;

        Vector2 dirToPlayer = (player.transform.position
            - transform.position).normalized;

        // Perpendicular force = circular magnetic field
        Vector2 perpendicular = new Vector2(-dirToPlayer.y, dirToPlayer.x);

        float normalizedDist = dist / magneticRadius;
        float forceMag = magneticStrength * (1f - normalizedDist)
            * Mathf.Abs(player.GetCharge());

        if (player.GetCharge() < 0)
            perpendicular *= -1;

        playerRb.AddForce(perpendicular * forceMag);
    }

    void UpdateVisual()
    {
        if (sr != null)
            sr.color = currentActive ?
                new Color(0.6f, 0.8f, 1f) :
                new Color(0.4f, 0.4f, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = currentActive ? Color.yellow : Color.grey;
        Gizmos.DrawWireSphere(transform.position, magneticRadius);
    }
}