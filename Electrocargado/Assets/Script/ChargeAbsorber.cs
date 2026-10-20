using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona la mecánica de Absorción y Expulsión de carga (Bending) de THEIA.
/// Soporta tanto la tecla E (especificación del Anexo 1) como la tecla Q (configuración original),
/// y emite eventos desacoplados a GameEvents.cs sin usar GetComponent en el bucle general.
/// </summary>
public class ChargeAbsorber : MonoBehaviour
{
    [Header("Parámetros de Absorción y Expulsión (Bending)")]
    [Tooltip("Rango máximo de transferencia de carga.")]
    [Range(2f, 20f)]
    public float absorbRange = 8f;

    [Tooltip("Tasa de absorción de carga por segundo.")]
    [Range(0.1f, 3f)]
    public float absorbRate = 0.6f;

    [Tooltip("Tasa de expulsión de carga por segundo.")]
    [Range(0.1f, 3f)]
    public float expelRate = 0.6f;

    private ChargeResource chargeResource;
    private Camera cam;
    private bool absorbModeActive = false;
    private int ignoreLayers;

    void Awake()
    {
        chargeResource = GetComponent<ChargeResource>();
        ignoreLayers = ~LayerMask.GetMask("Tilemap", "Player", "Ground");
    }

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (GandalfNPC.isGandalfActive || Keyboard.current == null || Mouse.current == null) return;

        // Soportar tanto Q (original) como E (especificación del Master Prompt)
        if (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
            absorbModeActive = !absorbModeActive;

        if (!absorbModeActive) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        if (Mouse.current.leftButton.isPressed)
            TryAbsorb(mouseWorld);

        if (Mouse.current.rightButton.isPressed)
            TryExpel(mouseWorld);
    }

    void TryAbsorb(Vector2 mouseWorld)
    {
        Collider2D hit = Physics2D.OverlapCircle(mouseWorld, 2.5f, ignoreLayers);
        if (hit == null || hit.gameObject == gameObject) return;
        if (hit.CompareTag("Player")) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > absorbRange) return;

        hit.TryGetComponent(out ChargedObject fixedObj);
        hit.TryGetComponent(out DynamicChargedObject dynObj);

        float resistance = 1f - Mathf.Abs(chargeResource.GetCharge());
        float amount = absorbRate * resistance * Time.deltaTime;

        if (fixedObj != null && fixedObj.isConductor)
        {
            float chargeSign = Mathf.Sign(fixedObj.charge);
            float delta = chargeSign * amount;
            chargeResource.AddCharge(delta);
            GameEvents.RaiseChargeTransferred(delta, fixedObj.transform.position);
        }
        else if (dynObj != null && dynObj.isConductor && Mathf.Abs(dynObj.charge) > 0.01f)
        {
            float chargeSign = Mathf.Sign(dynObj.charge);
            float delta = chargeSign * amount;
            chargeResource.AddCharge(delta);
            dynObj.charge = Mathf.MoveTowards(dynObj.charge, 0f, amount * 0.4f);
            dynObj.UpdateVisual();
            GameEvents.RaiseChargeTransferred(delta, dynObj.transform.position);
        }
    }

    void TryExpel(Vector2 targetPos)
    {
        if (chargeResource.IsNeutral()) return;

        Collider2D hit = Physics2D.OverlapCircle(targetPos, 2.5f, ignoreLayers);
        if (hit == null || hit.gameObject == gameObject) return;
        if (hit.CompareTag("Player")) return;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > absorbRange) return;

        float currentCharge = chargeResource.GetCharge();
        float amount = expelRate * Time.deltaTime;

        hit.TryGetComponent(out ChargedObject fixedObj);
        hit.TryGetComponent(out DynamicChargedObject dynObj);

        if (fixedObj != null && !fixedObj.isFixedSource)
        {
            float delta = currentCharge > 0 ? amount : -amount;
            fixedObj.charge += delta;
            fixedObj.UpdateVisual();
            chargeResource.AddCharge(-delta);
            GameEvents.RaiseChargeTransferred(-delta, fixedObj.transform.position);
        }
        else if (dynObj != null)
        {
            float delta = currentCharge > 0 ? amount : -amount;
            dynObj.charge += delta;
            dynObj.UpdateVisual();
            chargeResource.AddCharge(-delta);
            GameEvents.RaiseChargeTransferred(-delta, dynObj.transform.position);
        }
    }

    public bool IsAbsorbModeActive() => absorbModeActive;
}