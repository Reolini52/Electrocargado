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
    private Rigidbody2D grappleTargetDynamicRb;
    private bool attracting = false;

    // Layers to ignore
    private int ignoreLayers;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        absorber = GetComponent<ChargeAbsorber>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();

        ignoreLayers = ~LayerMask.GetMask("Tilemap", "Player", "Ground");

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
        if (GandalfNPC.isGandalfActive || Keyboard.current == null || Mouse.current == null) return;

        if (absorber != null && absorber.IsAbsorbModeActive())
        {
            beam.enabled = false;
            isGrappling = false;
            grappleTarget = null;
            grappleTargetDynamicRb = null;
            return;
        }

        if (chargeResource == null || chargeResource.IsNeutral())
        {
            beam.enabled = false;
            isGrappling = false;
            grappleTarget = null;
            grappleTargetDynamicRb = null;
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );

        if (Mouse.current.leftButton.isPressed)
            TryGrapple(mouseWorld);
        else
        {
            beam.enabled = false;
            isGrappling = false;
            grappleTarget = null;
            grappleTargetDynamicRb = null;
        }
    }

    void FixedUpdate()
    {
        if (!isGrappling) return;

        if (grappleTarget != null)
            grapplePoint = grappleTarget.transform.position;

        Vector2 dirToPoint = (grapplePoint - (Vector2)transform.position).normalized;

        if (attracting)
        {
            rb.AddForce(dirToPoint * holdForce, ForceMode2D.Force);
            // Tercera Ley de Newton sobre objetos dinámicos cargados
            if (grappleTargetDynamicRb != null)
                grappleTargetDynamicRb.AddForce(-dirToPoint * (holdForce * 0.75f), ForceMode2D.Force);
        }
        else
        {
            rb.AddForce(-dirToPoint * holdForce, ForceMode2D.Force);
            if (grappleTargetDynamicRb != null)
                grappleTargetDynamicRb.AddForce(dirToPoint * (holdForce * 0.75f), ForceMode2D.Force);
        }

        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, grapplePoint);
    }

    void TryGrapple(Vector2 mousePos)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(mousePos, 2.5f, ignoreLayers);

        Collider2D bestHit = null;
        float bestDist = float.MaxValue;
        float targetCharge = 0f;
        DynamicChargedObject bestDynamic = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D candidate = hits[i];
            if (candidate == null || candidate.gameObject == gameObject) continue;
            if (candidate.GetComponent<ChargeResource>() != null) continue;

            EmisorProjectile proj = candidate.GetComponent<EmisorProjectile>();
            if (proj != null && chargeResource.GetCharge() * proj.charge > 0.05f)
            {
                proj.ForceDeflectByBeam(transform.position);
            }

            ChargedObject f = candidate.GetComponent<ChargedObject>();
            DynamicChargedObject d = candidate.GetComponent<DynamicChargedObject>();
            EnemyCorruptor c = candidate.GetComponent<EnemyCorruptor>();
            EnemyEmisor e = candidate.GetComponent<EnemyEmisor>();
            PolarousBossController boss = candidate.GetComponent<PolarousBossController>();

            bool isChargedTarget = false;
            float candidateCharge = 0f;

            if (f != null) { candidateCharge = f.charge; isChargedTarget = true; }
            else if (d != null) { candidateCharge = d.charge; isChargedTarget = true; }
            else if (c != null) { candidateCharge = c.charge; isChargedTarget = true; }
            else if (e != null) { candidateCharge = e.charge; isChargedTarget = true; }
            else if (boss != null) { candidateCharge = boss.bossCharge; isChargedTarget = true; }

            if (!isChargedTarget) continue;

            float distToMouse = Vector2.Distance(mousePos, candidate.transform.position);
            if (distToMouse < bestDist)
            {
                bestDist = distToMouse;
                bestHit = candidate;
                targetCharge = candidateCharge;
                bestDynamic = d;
            }
        }

        if (bestHit != null)
        {
            float chargeProduct = chargeResource.GetCharge() * targetCharge;
            attracting = chargeProduct < 0;

            grapplePoint = bestHit.transform.position;
            grappleTarget = bestHit.gameObject;
            grappleTargetDynamicRb = bestDynamic != null ? bestDynamic.GetComponent<Rigidbody2D>() : null;

            if (!isGrappling)
            {
                Vector2 dir = (mousePos - (Vector2)transform.position).normalized;
                Vector2 impulseDir = attracting ? dir : -dir;
                rb.AddForce(impulseDir * force, ForceMode2D.Impulse);
            }

            isGrappling = true;

            beam.enabled = true;
            beam.SetPosition(0, transform.position);
            beam.SetPosition(1, grapplePoint);

            Color beamColor = attracting ?
                new Color(0.3f, 1f, 0.3f, 0.9f) :
                new Color(1f, 0.3f, 0.3f, 0.9f);
            beam.startColor = beamColor;
            beam.endColor = new Color(
                beamColor.r, beamColor.g, beamColor.b, 0f);
            return;
        }

        // No charged target found
        isGrappling = false;
        grappleTarget = null;
        grappleTargetDynamicRb = null;
        beam.enabled = true;
        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, mousePos);
        beam.startColor = new Color(1f, 1f, 1f, 0.15f);
        beam.endColor = new Color(1f, 1f, 1f, 0f);
    }
}
