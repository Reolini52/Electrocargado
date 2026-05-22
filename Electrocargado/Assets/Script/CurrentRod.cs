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

    [Header("Electrons")]
    public int electronCount = 6;
    public float electronSpeed = 2f;
    public Color electronColor = new Color(0.3f, 0.6f, 1f);
    public float electronSize = 0.15f;

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;

    private List<GameObject> electrons = new List<GameObject>();
    private List<float> electronOffsets = new List<float>();
    private List<LineRenderer> fieldRings = new List<LineRenderer>();

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();

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
            esr.color = electronColor;
            esr.sortingOrder = 2;

            // Use a simple white square sprite
            esr.sprite = sr.sprite;
            e.transform.localScale = Vector3.one * electronSize;

            float offset = (float)i / electronCount
                * rodLength - rodLength / 2f;
            electronOffsets.Add(offset);
            e.transform.localPosition = new Vector3(0f, offset, 0f);

            electrons.Add(e);
            e.SetActive(false);
        }
    }

    void CreateFieldRings()
    {
        int ringCount = 3;
        for (int i = 0; i < ringCount; i++)
        {
            GameObject ringObj = new GameObject($"Ring_{i}");
            ringObj.transform.parent = transform;
            ringObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.06f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 1;

            float radius = 1.5f + i * 1f;
            int segments = 32;
            lr.positionCount = segments;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f;
                lr.SetPosition(j, transform.position + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f, 0f
                ));
            }

            Color c = new Color(1f, 0.8f, 0f, 0f);
            lr.startColor = c;
            lr.endColor = c;

            fieldRings.Add(lr);
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
            electronOffsets[i] -= electronSpeed * Time.deltaTime;

            if (electronOffsets[i] < -rodLength / 2f)
                electronOffsets[i] += rodLength;

            electrons[i].transform.localPosition = new Vector3(
                0f, electronOffsets[i], 0f);
        }
    }

    void UpdateFieldRings()
    {
        for (int i = 0; i < fieldRings.Count; i++)
        {
            float pulse = Mathf.PingPong(
                Time.time * 0.8f + i * 0.4f, 1f);

            Color c = new Color(1f, 0.8f, 0f, pulse * 0.6f);
            fieldRings[i].startColor = c;
            fieldRings[i].endColor = c;

            // Update ring positions to follow rod
            int segments = fieldRings[i].positionCount;
            float radius = 1.5f + i * 1f;
            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f;
                fieldRings[i].SetPosition(j,
                    transform.position + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f, 0f
                    ));
            }
        }
    }

    public void ActivateCurrent(bool active, bool positiveTerminal = true)
    {
        currentActive = active;

        electronSpeed = positiveTerminal ?
            Mathf.Abs(electronSpeed) :
            -Mathf.Abs(electronSpeed);

        foreach (var e in electrons)
            e.SetActive(active);

        // Hide rings when inactive
        foreach (var ring in fieldRings)
        {
            Color c = new Color(1f, 0.8f, 0f, 0f);
            ring.startColor = c;
            ring.endColor = c;
        }
    }

    void FixedUpdate()
    {
        if (!currentActive || player == null) return;
        if (player.IsNeutral()) return;

        float dist = Vector2.Distance(
            transform.position,
            player.transform.position);

        if (dist > magneticRadius || dist < 0.1f) return;

        Vector2 dirToPlayer = (player.transform.position
            - transform.position).normalized;

        Vector2 perpendicular = new Vector2(
            -dirToPlayer.y, dirToPlayer.x);

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
                new Color(0.5f, 0.8f, 1f) :
                new Color(0.4f, 0.4f, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = currentActive ? Color.yellow : Color.grey;
        Gizmos.DrawWireSphere(transform.position, magneticRadius);
    }
}