using UnityEngine;
using UnityEngine.InputSystem;

public class AimIndicator : MonoBehaviour
{
    public float dotSize = 0.2f;
    private Camera cam;
    private SpriteRenderer sr;
    private ChargeAbsorber absorber;
    private ChargeResource chargeResource;

    void Start()
    {
        Cursor.visible = false;
        cam = Camera.main;
        absorber = GetComponentInParent<ChargeAbsorber>();
        chargeResource = GetComponentInParent<ChargeResource>();
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.one * dotSize;
    }

    void Update()
    {
        // Follow mouse in world space
        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );
        transform.position = mouseWorld;

        // Color based on mode
        if (absorber.IsAbsorbModeActive())
            sr.color = new Color(0f, 1f, 0.5f, 0.8f); // green = absorb mode
        else if (!chargeResource.IsNeutral())
            sr.color = new Color(1f, 1f, 0f, 0.8f); // yellow = bending mode
        else
            sr.color = new Color(1f, 1f, 1f, 0.4f); // white = neutral
    }
}