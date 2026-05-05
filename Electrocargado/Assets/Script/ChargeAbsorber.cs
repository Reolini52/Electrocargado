using UnityEngine;
using UnityEngine.InputSystem;

public class ChargeAbsorber : MonoBehaviour
{
    [Header("Absorb Settings")]
    public float absorbRange = 8f;
    public float absorbRate = 0.8f;
    public float expelRate = 0.8f;

    private ChargeResource chargeResource;
    private Camera cam;
    private bool absorbModeActive = false;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        cam = Camera.main;
    }

    void Update()
    {
        // Q toggles absorb mode
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            absorbModeActive = !absorbModeActive;
            Debug.Log($"Absorb mode: {absorbModeActive}");
        }

        if (!absorbModeActive) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );

        if (Mouse.current.leftButton.isPressed)
            TryAbsorb(mouseWorld);

        if (Mouse.current.rightButton.isPressed)
            TryExpel(mouseWorld);
    }

    void TryAbsorb(Vector2 targetPos)
    {
        Collider2D hit = Physics2D.OverlapCircle(targetPos, 1.2f);
        if (hit == null || hit.gameObject == gameObject) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > absorbRange) return;

        ChargedObject fixedObj = hit.GetComponent<ChargedObject>();
        DynamicChargedObject dynObj = hit.GetComponent<DynamicChargedObject>();

        if (fixedObj != null)
        {
            float amount = absorbRate * Time.deltaTime;
            chargeResource.AddCharge(fixedObj.charge > 0 ? amount : -amount);
        }
        else if (dynObj != null)
        {
            float amount = absorbRate * Time.deltaTime;
            chargeResource.AddCharge(dynObj.charge > 0 ? amount : -amount);
            dynObj.charge = Mathf.MoveTowards(dynObj.charge, 0f, amount);
        }
    }

    void TryExpel(Vector2 targetPos)
    {
        if (chargeResource.IsNeutral()) return;

        Collider2D hit = Physics2D.OverlapCircle(targetPos, 1.2f);
        if (hit == null || hit.gameObject == gameObject) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > absorbRange) return;

        ChargedObject fixedObj = hit.GetComponent<ChargedObject>();
        if (fixedObj != null)
        {
            float amount = expelRate * Time.deltaTime;
            float currentCharge = chargeResource.GetCharge();
            fixedObj.charge += currentCharge > 0 ? amount : -amount;
            chargeResource.AddCharge(currentCharge > 0 ? -amount : amount);
        }
    }

    public bool IsAbsorbModeActive() => absorbModeActive;
}