using UnityEngine;
using UnityEngine.InputSystem;

public class ChargeInteractor : MonoBehaviour
{
    [Header("Interaction")]
    public float interactRange = 12f;
    public float interactForce = 15f;

    private ChargeResource chargeResource;
    private ChargeAbsorber absorber;
    private Camera cam;
    private Rigidbody2D rb;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        absorber = GetComponent<ChargeAbsorber>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (absorber.IsAbsorbModeActive()) return;
        if (chargeResource.IsNeutral()) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );

        if (Mouse.current.leftButton.isPressed)
            TryInteract(mouseWorld, isPrimary: true);

        if (Mouse.current.rightButton.isPressed)
            TryInteract(mouseWorld, isPrimary: false);
    }

    void TryInteract(Vector2 targetPos, bool isPrimary)
    {
        // Check mouse position for target
        Collider2D hit = Physics2D.OverlapCircle(targetPos, 2.5f);
        if (hit == null || hit.gameObject == gameObject) return;
        if (hit.GetComponent<ChargeResource>() != null) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > interactRange) return;

        ChargedObject fixedObj = hit.GetComponent<ChargedObject>();
        DynamicChargedObject dynObj = hit.GetComponent<DynamicChargedObject>();

        float targetCharge = 0f;
        if (fixedObj != null) targetCharge = fixedObj.charge;
        else if (dynObj != null) targetCharge = dynObj.charge;
        else return;

        Rigidbody2D targetRb = hit.GetComponent<Rigidbody2D>();
        Vector2 dirToTarget = (hit.transform.position - transform.position).normalized;
        float chargeProduct = chargeResource.GetCharge() * targetCharge;

        // isPrimary = left click = affect PLAYER movement
        // not isPrimary = right click = affect TARGET movement
        if (isPrimary)
        {
            // Left click: move yourself
            // Opposite charges: pull yourself toward target
            // Same charges: push yourself away from target
            if (chargeProduct < 0)
                rb.AddForce(dirToTarget * interactForce, ForceMode2D.Force);
            else
                rb.AddForce(-dirToTarget * interactForce, ForceMode2D.Force);
        }
        else
        {
            // Right click: move the target (only dynamic)
            if (targetRb == null || dynObj == null) return;

            // Opposite charges: pull target toward you
            // Same charges: push target away from you
            if (chargeProduct < 0)
                targetRb.AddForce(-dirToTarget * interactForce, ForceMode2D.Force);
            else
                targetRb.AddForce(dirToTarget * interactForce, ForceMode2D.Force);
        }
    }
}