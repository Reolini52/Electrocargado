using UnityEngine;
using UnityEngine.InputSystem;

public class ChargeInteractor : MonoBehaviour
{
    [Header("Grapple")]
    public float maxRange = 12f;
    public float force = 20f;
    public float holdForce = 10f;

    private ChargeResource chargeResource;
    private ChargeAbsorber absorber;
    private Camera cam;
    private Rigidbody2D rb;
    private LineRenderer beam;

    private bool isGrappling = false;
    private Vector2 grapplePoint;
    private GameObject grappleTarget;
    private bool attracting = false;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        absorber = GetComponent<ChargeAbsorber>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();

        beam = GetComponent<LineRenderer>();
        if (beam == null)
            beam = gameObject.AddComponent<LineRenderer>();

        beam.positionCount = 2;
        beam.startWidth = 0.08f;
        beam.endWidth = 0.02f;
        beam.material = new Material(Shader.Find("Sprites/Default"));
        beam.sortingOrder = 5;
        beam.useWorldSpace = true;
        beam.enabled = false;
    }

    void Update()
    {
        if (absorber.IsAbsorbModeActive())
        {
            beam.enabled = false;
            isGrappling = false;
            return;
        }

        if (chargeResource.IsNeutral())
        {
            beam.enabled = false;
            isGrappling = false;
            return;
        }

        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );

        if (Mouse.current.leftButton.isPressed)
        {
            TryGrapple(mouseWorld);
        }
        else
        {
            beam.enabled = false;
            isGrappling = false;
            grappleTarget = null;
        }
    }

    void FixedUpdate()
    {
        if (!isGrappling) return;

        if (grappleTarget != null)
            grapplePoint = grappleTarget.transform.position;

        Vector2 dirToPoint = (grapplePoint - (Vector2)transform.position).normalized;

        if (attracting)
            rb.AddForce(dirToPoint * holdForce, ForceMode2D.Force);
        else
            rb.AddForce(-dirToPoint * holdForce, ForceMode2D.Force);

        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, grapplePoint);
    }

    void TryGrapple(Vector2 mousePos)
    {
        // Check for charged object near mouse cursor
        Collider2D hit = Physics2D.OverlapCircle(mousePos, 2f);

        if (hit != null && hit.gameObject != gameObject)
        {
            float targetCharge = 0f;
            bool found = false;

            ChargedObject f = hit.GetComponent<ChargedObject>();
            DynamicChargedObject d = hit.GetComponent<DynamicChargedObject>();
            EnemyCorruptor c = hit.GetComponent<EnemyCorruptor>();
            EnemyEmisor e = hit.GetComponent<EnemyEmisor>();

            if (f != null) { targetCharge = f.charge; found = true; }
            else if (d != null) { targetCharge = d.charge; found = true; }
            else if (c != null) { targetCharge = c.charge; found = true; }
            else if (e != null) { targetCharge = e.charge; found = true; }

            if (found)
            {
                float chargeProduct = chargeResource.GetCharge() * targetCharge;
                attracting = chargeProduct < 0;

                grapplePoint = hit.transform.position;
                grappleTarget = hit.gameObject;

                if (!isGrappling)
                {
                    Vector2 dir = (mousePos - (Vector2)transform.position).normalized;
                    Vector2 impulseDir = attracting ? dir : -dir;
                    rb.AddForce(impulseDir * force, ForceMode2D.Impulse);
                }

                isGrappling = true;

                // Update beam
                beam.enabled = true;
                beam.SetPosition(0, transform.position);
                beam.SetPosition(1, grapplePoint);

                Color beamColor = attracting ?
                    new Color(0.3f, 1f, 0.3f, 0.9f) :
                    new Color(1f, 0.3f, 0.3f, 0.9f);
                beam.startColor = beamColor;
                beam.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);
                return;
            }
        }

        // No charged target — show faint beam to mouse
        isGrappling = false;
        grappleTarget = null;
        beam.enabled = true;
        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, mousePos);
        beam.startColor = new Color(1f, 1f, 1f, 0.15f);
        beam.endColor = new Color(1f, 1f, 1f, 0f);
    }
}