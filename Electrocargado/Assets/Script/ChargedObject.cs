using UnityEngine;
using System.Collections.Generic;

public class ChargedObject : MonoBehaviour
{
    [Header("Charge")]
    public float charge = 1f;
    public float effectRadius = 10f;
    public float forceStrength = 50f;

    [Header("Settings")]
    public bool isConductor = true;
    public bool isFixedSource = true;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);

    [Header("Field Rings")]
    public bool showRings = true;
    public int ringCount = 3;

    private SpriteRenderer sr;
    private ChargeResource player;
    private Rigidbody2D playerRb;
    private List<LineRenderer> fieldRings = new List<LineRenderer>();

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        player = Object.FindAnyObjectByType<ChargeResource>();
        if (player != null)
            playerRb = player.GetComponent<Rigidbody2D>();

        if (showRings) CreateFieldRings();
        UpdateVisual();
    }

    void CreateFieldRings()
    {
        // Get object dimensions
        float scaleX = transform.localScale.x * 0.5f;
        float scaleY = transform.localScale.y * 0.5f;

        for (int i = 0; i < ringCount; i++)
        {
            GameObject ringObj = new GameObject($"FieldRing_{i}");
            ringObj.transform.parent = transform;
            ringObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = 0.04f;
            lr.endWidth = 0.04f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 1;

            int segments = 32;
            lr.positionCount = segments;

            // Offset grows with each ring
            float offsetX = scaleX + 0.3f + i * 0.4f;
            float offsetY = scaleY + 0.3f + i * 0.4f;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f;
                lr.SetPosition(j, transform.position + new Vector3(
                    Mathf.Cos(angle) * offsetX,
                    Mathf.Sin(angle) * offsetY,
                    0f));
            }

            Color c = new Color(0.3f, 0.6f, 1f, 0f);
            lr.startColor = c;
            lr.endColor = c;
            fieldRings.Add(lr);
        }
    }

    void Update()
    {
        if (player == null)
        {
            player = Object.FindAnyObjectByType<ChargeResource>();
            if (player != null)
                playerRb = player.GetComponent<Rigidbody2D>();
        }

        UpdateRings();
    }

    void UpdateRings()
    {
        if (!showRings) return;

        float dist = player != null ?
            Vector2.Distance(transform.position, player.transform.position)
            : float.MaxValue;

        bool inRange = dist < effectRadius && !player.IsNeutral();

        for (int i = 0; i < fieldRings.Count; i++)
        {
            float pulse = Mathf.PingPong(
                Time.time * 0.8f + i * 0.5f, 1f);

            float alpha = inRange ? pulse * 0.6f : pulse * 0.25f;

            Color ringColor = charge > 0 ?
                new Color(0.3f, 0.6f, 1f, alpha) :
                new Color(1f, 0.3f, 0.3f, alpha);

            fieldRings[i].startColor = ringColor;
            fieldRings[i].endColor = ringColor;

            // Update positions
            int segments = fieldRings[i].positionCount;
            float offsetX = transform.localScale.x * 0.5f + 0.3f + i * 0.4f;
            float offsetY = transform.localScale.y * 0.5f + 0.3f + i * 0.4f;
            float rotOffset = Time.time * (charge > 0 ? 0.3f : -0.3f) + i * 0.5f;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f + rotOffset;
                fieldRings[i].SetPosition(j,
                    transform.position + new Vector3(
                        Mathf.Cos(angle) * offsetX,
                        Mathf.Sin(angle) * offsetY,
                        0f));
            }
        }
    }

    void FixedUpdate()
    {
        if (player == null || playerRb == null) return;
        if (player.IsNeutral()) return;

        float dist = Vector2.Distance(
            transform.position, player.transform.position);
        if (dist > effectRadius || dist < 0.1f) return;

        float normalizedDist = dist / effectRadius;
        float forceMag = forceStrength * (1f - normalizedDist)
            / (dist * dist + 0.5f);
        forceMag = Mathf.Clamp(forceMag, 2f, 30f);

        Vector2 dirToPlayer = (player.transform.position
            - transform.position).normalized;
        float chargeProduct = charge * player.GetCharge();

        if (chargeProduct > 0)
            playerRb.AddForce(dirToPlayer * forceMag);
        else
            playerRb.AddForce(-dirToPlayer * forceMag);
    }

    public void UpdateVisual()
    {
        if (sr != null)
            sr.color = charge > 0 ? positiveColor : negativeColor;
    }
}