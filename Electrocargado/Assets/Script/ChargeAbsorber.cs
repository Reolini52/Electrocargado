using UnityEngine;
using UnityEngine.InputSystem;

public class ChargeAbsorber : MonoBehaviour
{
    [Header("Absorb Settings")]
    public float absorbRange = 8f;
    public float absorbRate = 0.6f;
    public float expelRate = 0.6f;

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
        if (Keyboard.current.qKey.wasPressedThisFrame)
            absorbModeActive = !absorbModeActive;

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
        Collider2D hit = Physics2D.OverlapCircle(
            targetPos, 2.5f,
            LayerMask.GetMask("ChargedObjects")
        );
        if (hit == null || hit.gameObject == gameObject) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > absorbRange) return;

        ChargedObject fixedObj = hit.GetComponent<ChargedObject>();
        DynamicChargedObject dynObj = hit.GetComponent<DynamicChargedObject>();

        float resistance = 1f - Mathf.Abs(chargeResource.GetCharge());
        float amount = absorbRate * resistance * Time.deltaTime;

        if (fixedObj != null && fixedObj.isConductor)
        {
            float chargeSign = Mathf.Sign(fixedObj.charge);
            chargeResource.AddCharge(chargeSign * amount);
        }
        else if (dynObj != null && dynObj.isConductor)
        {
            float chargeSign = Mathf.Sign(dynObj.charge);
            chargeResource.AddCharge(chargeSign * amount);
            dynObj.charge = Mathf.MoveTowards(dynObj.charge, 0f, amount * 0.4f);
            dynObj.UpdateVisual();
        }
    }

    void TryExpel(Vector2 targetPos)
    {
        if (chargeResource.IsNeutral()) return;

        Collider2D hit = Physics2D.OverlapCircle(
            targetPos, 2.5f,
            LayerMask.GetMask("ChargedObjects")
        );
        if (hit == null || hit.gameObject == gameObject) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > absorbRange) return;

        float currentCharge = chargeResource.GetCharge();
        float amount = expelRate * Time.deltaTime;

        ChargedObject fixedObj = hit.GetComponent<ChargedObject>();
        DynamicChargedObject dynObj = hit.GetComponent<DynamicChargedObject>();

        if (fixedObj != null && !fixedObj.isFixedSource)
        {
            fixedObj.charge += currentCharge > 0 ? amount : -amount;
            fixedObj.UpdateVisual();
            chargeResource.AddCharge(currentCharge > 0 ? -amount : amount);
        }
        else if (dynObj != null)
        {
            dynObj.charge += currentCharge > 0 ? amount : -amount;
            dynObj.UpdateVisual();
            chargeResource.AddCharge(currentCharge > 0 ? -amount : amount);
        }
    }

    public bool IsAbsorbModeActive() => absorbModeActive;
}