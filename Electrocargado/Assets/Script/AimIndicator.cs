using UnityEngine;
using UnityEngine.InputSystem;

public class AimIndicator : MonoBehaviour
{
    public float dotSize = 0.5f;
    private Camera cam;
    private SpriteRenderer sr;
    private ChargeAbsorber absorber;
    private ChargeResource chargeResource;

    void Start()
    {
        Cursor.visible = false;
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.one * dotSize;

        // Find player components directly
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            absorber = player.GetComponent<ChargeAbsorber>();
            chargeResource = player.GetComponent<ChargeResource>();
        }
    }

    void Update()
    {
        if (cam == null) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );
        transform.position = mouseWorld;
        transform.localScale = Vector3.one * dotSize;

        if (sr == null) return;

        Color targetColor;
        if (absorber != null && absorber.IsAbsorbModeActive())
            targetColor = new Color(0f, 1f, 0.5f, 0.8f);
        else if (chargeResource != null && !chargeResource.IsNeutral())
            targetColor = new Color(1f, 1f, 0f, 0.8f);
        else
            targetColor = new Color(1f, 1f, 1f, 0.4f);

        sr.color = Color.Lerp(sr.color, targetColor, Time.deltaTime * 10f);
    }

    void OnDisable()
    {
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        Cursor.visible = true;
    }
}