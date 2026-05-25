using UnityEngine;
using System.Collections.Generic;

public class DynamicChargedObject : MonoBehaviour
{
    [Header("Charge")]
    public float charge = 1f;
    public float effectRadius = 10f;
    public float forceStrength = 40f;
    public float maxSpeed = 8f;

    [Header("Settings")]
    public bool isConductor = true;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.8f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color neutralColor = new Color(0.8f, 0.8f, 0.8f);

    [Header("Field Rings")]
    public bool showRings = true;
    public int ringCount = 2;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private ChargeResource player;
    private Rigidbody2D playerRb;
    private List<LineRenderer> fieldRings = new List<LineRenderer>();

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
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
        if (!showRings || Mathf.Abs(charge) < 0.05f) return;

        for (int i = 0; i < fieldRings.Count; i++)
        {
            float pulse = Mathf.PingPong(
                Time.time * 1f + i * 0.4f, 1f);
            float alpha = pulse * 0.5f;

            Color ringColor = charge > 0.1f ?
                new Color(0.3f, 0.6f, 1f, alpha) :
                charge < -0.1f ?
                new Color(1f, 0.3f, 0.3f, alpha) :
                new Color(0.5f, 0.5f, 0.5f, 0f);

            fieldRings[i].startColor = ringColor;
            fieldRings[i].endColor = ringColor;

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
        if (player == null) return;
        if (player.IsNeutral()) return;

        float dist = Vector2.Distance(
            transform.position, player.transform.position);
        if (dist > effectRadius || dist < 0.1f) return;

        float normalizedDist = dist / effectRadius;
        float forceMag = forceStrength * (1f - normalizedDist)
            / (dist * dist + 0.5f);
        forceMag = Mathf.Clamp(forceMag, 2f, 25f);

        Vector2 dirToPlayer = (player.transform.position
            - transform.position).normalized;
        float chargeProduct = charge * player.GetCharge();

        if (chargeProduct > 0)
            rb.AddForce(-dirToPlayer * forceMag);
        else
            rb.AddForce(dirToPlayer * forceMag);

        // Magnetic force
        if (playerRb != null && playerRb.linearVelocity.magnitude > 0.5f)
        {
            Vector2 playerVel = playerRb.linearVelocity;
            Vector2 perp = new Vector2(
                -playerVel.normalized.y, playerVel.normalized.x);
            float magForce = player.GetCharge() * charge
                * playerVel.magnitude * 1.5f;
            rb.AddForce(perp * magForce);
        }

        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    public void UpdateVisual()
    {
        if (sr == null) return;
        sr.color = charge > 0.1f ? positiveColor :
                   charge < -0.1f ? negativeColor : neutralColor;
    }
}