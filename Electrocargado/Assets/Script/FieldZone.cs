using UnityEngine;
using System.Collections.Generic;

public class FieldZone : MonoBehaviour
{
    [Header("Field Settings")]
    public float charge = 1f;
    public float forceStrength = 60f;
    public bool directional = false;
    public Vector2 direction = Vector2.right;

    [Header("Visual")]
    public int arrowRows = 3;
    public int arrowCols = 3;
    public float arrowLength = 0.6f;

    private ChargeResource player;
    private Rigidbody2D playerRb;
    private bool playerInside = false;

    private List<LineRenderer> arrows = new List<LineRenderer>();
    private List<LineRenderer> rings = new List<LineRenderer>();

    void Start()
    {
        // Remove sprite renderer if exists
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }

        if (directional)
            CreateDirectionalArrows();
        else
            CreateRadialRings();
    }

    void CreateDirectionalArrows()
    {
        float width = transform.localScale.x;
        float height = transform.localScale.y;

        for (int x = 0; x < arrowCols; x++)
        {
            for (int y = 0; y < arrowRows; y++)
            {
                Vector3 pos = transform.position + new Vector3(
                    (x - (arrowCols - 1) / 2f) * (width / arrowCols),
                    (y - (arrowRows - 1) / 2f) * (height / arrowRows),
                    0f
                );

                GameObject arrowObj = new GameObject("Arrow");
                // World space — not child
                arrowObj.transform.position = pos;

                LineRenderer lr = arrowObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.startWidth = 0.08f;
                lr.endWidth = 0.02f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.sortingOrder = 3;

                Color c = charge > 0 ?
                    new Color(0.3f, 0.6f, 1f, 0.9f) :
                    new Color(1f, 0.3f, 0.3f, 0.9f);
                lr.startColor = c;
                lr.endColor = new Color(c.r, c.g, c.b, 0.3f);

                // Arrow from pos to pos + direction
                Vector2 dir = direction.normalized;
                lr.SetPosition(0, pos - new Vector3(
                    dir.x, dir.y, 0f) * arrowLength * 0.3f);
                lr.SetPosition(1, pos + new Vector3(
                    dir.x, dir.y, 0f) * arrowLength * 0.7f);

                arrows.Add(lr);
            }
        }
    }

    void CreateRadialRings()
    {
        int ringCount = 2;
        for (int i = 0; i < ringCount; i++)
        {
            GameObject ringObj = new GameObject($"Ring_{i}");
            ringObj.transform.parent = transform;
            ringObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 2;

            int segments = 32;
            lr.positionCount = segments;

            float scaleX = transform.localScale.x * 0.4f + i * 0.5f;
            float scaleY = transform.localScale.y * 0.4f + i * 0.5f;

            for (int j = 0; j < segments; j++)
            {
                float angle = (float)j / segments * Mathf.PI * 2f;
                lr.SetPosition(j, transform.position + new Vector3(
                    Mathf.Cos(angle) * scaleX,
                    Mathf.Sin(angle) * scaleY, 0f));
            }

            Color c = charge > 0 ?
                new Color(0.3f, 0.6f, 1f, 0.5f) :
                new Color(1f, 0.3f, 0.3f, 0.5f);
            lr.startColor = c;
            lr.endColor = c;
            rings.Add(lr);
        }
    }

    void Update()
    {
        // Animate arrows pulsing
        float pulse = Mathf.PingPong(Time.time * 2f, 1f);

        if (directional)
        {
            foreach (var lr in arrows)
            {
                if (lr == null) continue;
                Color c = charge > 0 ?
                    new Color(0.3f, 0.6f, 1f, 0.5f + pulse * 0.5f) :
                    new Color(1f, 0.3f, 0.3f, 0.5f + pulse * 0.5f);
                lr.startColor = c;
                lr.endColor = new Color(c.r, c.g, c.b, 0.1f);
            }
        }
        else
        {
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null) continue;
                float p = Mathf.PingPong(Time.time * 0.8f + i * 0.5f, 1f);
                Color c = charge > 0 ?
                    new Color(0.3f, 0.6f, 1f, p * 0.5f) :
                    new Color(1f, 0.3f, 0.3f, p * 0.5f);
                rings[i].startColor = c;
                rings[i].endColor = c;

                // Update positions
                int segments = rings[i].positionCount;
                float sx = transform.localScale.x * 0.4f + i * 0.5f;
                float sy = transform.localScale.y * 0.4f + i * 0.5f;
                for (int j = 0; j < segments; j++)
                {
                    float angle = (float)j / segments * Mathf.PI * 2f;
                    rings[i].SetPosition(j, transform.position + new Vector3(
                        Mathf.Cos(angle) * sx,
                        Mathf.Sin(angle) * sy, 0f));
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (!playerInside || player == null) return;
        if (player.IsNeutral()) return;

        float chargeProduct = charge * player.GetCharge();

        if (directional)
        {
            // Positive player follows field direction
            // Negative player goes opposite
            Vector2 force = direction.normalized * forceStrength;
            if (chargeProduct < 0) force *= -1;
            playerRb.AddForce(force);
        }
        else
        {
            // Same as ChargedObject logic
            Vector2 dirFromCenter = (player.transform.position
                - transform.position).normalized;

            if (chargeProduct > 0)
                // Same charge = repel from center
                playerRb.AddForce(dirFromCenter * forceStrength);
            else
                // Opposite = attract to center
                playerRb.AddForce(-dirFromCenter * forceStrength);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    void OnDestroy()
    {
        foreach (var lr in arrows)
            if (lr != null) Destroy(lr.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = charge > 0 ? Color.blue : Color.red;
        if (directional)
            Gizmos.DrawRay(transform.position,
                new Vector3(direction.x, direction.y, 0f) * 3f);
        else
            Gizmos.DrawWireSphere(transform.position,
                Mathf.Max(transform.localScale.x,
                    transform.localScale.y) * 0.5f);
    }
}