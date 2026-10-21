using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Campo Magnético (Prefab Field) que aplica la Ley de Fuerza de Lorentz sobre el jugador:
/// \vec{F}_B = q * (\vec{v} \times \vec{B})
/// El jugador actúa como la partícula cargada en movimiento que entra al campo magnético,
/// experimentando una deflexión perpendicular a su velocidad (\vec{v}) proporcional a su carga (q) y rapidez (|\vec{v}|).
/// </summary>
public class FieldZone : MonoBehaviour
{
    public static int ActiveFieldContacts = 0;
    public static bool IsPlayerInAnyField => ActiveFieldContacts > 0;

    [Header("Field Settings (Lorentz: F = q · v × B)")]
    public float charge = 1f;
    public float forceStrength = 60f;
    public bool directional = false;
    public Vector2 direction = Vector2.right;

    [Header("Visual")]
    public int arrowRows = 3;
    public int arrowCols = 3;
    public float arrowLength = 0.6f;
    public Color positiveColor = new Color(0.3f, 0.6f, 1f, 0.18f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f, 0.18f);

    private ChargeResource player;
    private Rigidbody2D playerRb;
    private BoxCollider2D fieldCollider;
    private bool playerInside = false;
    private bool countedInStatic = false;

    private List<LineRenderer> arrows = new List<LineRenderer>();
    private List<LineRenderer> rings = new List<LineRenderer>();

    void Start()
    {
        fieldCollider = GetComponent<BoxCollider2D>();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.enabled = true;
            sr.color = charge > 0
                ? new Color(0.25f, 0.55f, 1f, 0.12f)
                : new Color(1f, 0.3f, 0.35f, 0.12f);
        }

        LocatePlayer();

        if (directional)
            CreateDirectionalArrows();
        else
            CreateRadialRings();
    }

    private void LocatePlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<ChargeResource>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
        }
        else
        {
            player = Object.FindAnyObjectByType<ChargeResource>();
            if (player != null)
                playerRb = player.GetComponent<Rigidbody2D>();
        }
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

                Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
                lr.SetPosition(0, pos - new Vector3(dir.x, dir.y, 0f) * arrowLength * 0.3f);
                lr.SetPosition(1, pos + new Vector3(dir.x, dir.y, 0f) * arrowLength * 0.7f);

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
        if (player == null || playerRb == null)
        {
            LocatePlayer();
            if (player == null || playerRb == null) return;
        }

        // Verificación geométrica de respaldo por si algún hijo del jugador alteró el trigger
        bool insideNow = playerInside;
        if (!insideNow && fieldCollider != null)
        {
            insideNow = fieldCollider.OverlapPoint(player.transform.position);
        }

        UpdateStaticFieldCount(insideNow);

        if (!insideNow) return;
        if (player.IsNeutral()) return;

        float qPlayer = player.GetCharge(); // [-1, +1]
        float qSign = Mathf.Sign(charge * qPlayer); // +1 si misma polaridad, -1 si opuesta
        float qMag = Mathf.Clamp(Mathf.Abs(qPlayer), 0.35f, 1f);

        Vector2 v = playerRb.linearVelocity;
        float speed = v.magnitude;

        // Ley de Fuerza de Lorentz en 2D: \vec{F}_B = q * (\vec{v} \times \vec{B})
        // Con \vec{B} = (0, 0, B_z) perpendicular al plano 2D, el producto vectorial (\vec{v} \times \vec{B})
        // es perpendicular a la velocidad \vec{v}: (-v_y, v_x).
        // Si la partícula entra moviéndose a la derecha (v_x > 0) con qSign > 0, la fuerza apunta hacia ARRIBA (+y).
        Vector2 lorentzPerp;
        if (speed > 0.35f)
        {
            // Realzamos la componente vertical cuando hay velocidad horizontal para curvar la trayectoria sobre fosos
            Vector2 rawPerp = new Vector2(-v.y * 0.65f, v.x * 1.35f);
            lorentzPerp = rawPerp.sqrMagnitude > 0.001f ? rawPerp.normalized : Vector2.up;
        }
        else
        {
            lorentzPerp = Vector2.up;
        }

        Vector2 lorentzForceDir = lorentzPerp * qSign;

        // Magnitud del campo magnético efectivo B y fuerza de Lorentz |F_B| = |q| * v * B
        float effectiveSpeed = Mathf.Clamp(speed, 4.0f, 12.0f);
        float baseB = Mathf.Clamp(forceStrength, 15f, 65f);
        float forceMag = qMag * effectiveSpeed * (baseB * 0.22f);
        forceMag = Mathf.Clamp(forceMag, 55f, 125f);

        Vector2 finalForce;

        if (directional)
        {
            // Campo direccional + curvatura de Lorentz:
            // Combina la dirección del campo configurada en el Inspector con la deflexión perpendicular de Lorentz
            Vector2 fieldDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            if (qSign < 0f) fieldDir *= -1f;

            Vector2 combinedDir = (fieldDir * 0.72f + lorentzForceDir * 0.28f).normalized;
            finalForce = combinedDir * forceMag;
        }
        else
        {
            // Campo magnético puro (anillos): Ley de Lorentz \vec{F}_B = q * (\vec{v} \times \vec{B})
            finalForce = lorentzForceDir * forceMag;
        }

        // Si la fuerza de Lorentz empuja hacia arriba (+y), compensamos la gravedad (gravityScale = 3)
        // y amortiguamos la caída previa para que la partícula describa el arco magnético en lugar de hundirse
        if (finalForce.y > 5f)
        {
            float gravityCompensation = playerRb.mass * Mathf.Abs(Physics2D.gravity.y) * playerRb.gravityScale * 0.9f;
            finalForce.y += gravityCompensation;

            if (playerRb.linearVelocity.y < -1.5f)
            {
                playerRb.linearVelocity = new Vector2(
                    playerRb.linearVelocity.x,
                    playerRb.linearVelocity.y * 0.78f
                );
            }
        }

        playerRb.AddForce(finalForce, ForceMode2D.Force);

        // Emitir telemetría en vivo para el HUD matemático [B] y los vectores [V]
        float dist = Vector2.Distance(transform.position, player.transform.position);
        float fieldB = baseB * 0.1f;
        GameEvents.RaiseMagneticForceApplied(new CurrentMagneticData(
            transform.position,
            player.transform.position,
            Mathf.Abs(qPlayer) * effectiveSpeed,
            fieldB,
            Mathf.Max(dist, 0.2f),
            finalForce
        ));
    }

    private void UpdateStaticFieldCount(bool isInside)
    {
        if (isInside && !countedInStatic)
        {
            countedInStatic = true;
            ActiveFieldContacts++;
        }
        else if (!isInside && countedInStatic)
        {
            countedInStatic = false;
            ActiveFieldContacts = Mathf.Max(0, ActiveFieldContacts - 1);
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        return other.GetComponentInParent<PlayerController>() != null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerCollider(other))
            playerInside = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (IsPlayerCollider(other))
            playerInside = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayerCollider(other))
        {
            // Verificar si el centro del jugador sigue dentro antes de marcar salida
            if (fieldCollider != null && player != null && fieldCollider.OverlapPoint(player.transform.position))
                return;

            playerInside = false;
        }
    }

    void OnDisable()
    {
        UpdateStaticFieldCount(false);
    }

    void OnDestroy()
    {
        UpdateStaticFieldCount(false);
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
