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
        Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorld, 2.8f, ignoreLayers);

        ChargedObject bestFixed = null;
        DynamicChargedObject bestDyn = null;
        float bestMouseDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D candidate = hits[i];
            if (candidate == null || candidate.gameObject == gameObject || candidate.CompareTag("Player")) continue;

            float distFromPlayer = Vector2.Distance(transform.position, candidate.transform.position);
            if (distFromPlayer > absorbRange) continue;

            candidate.TryGetComponent(out ChargedObject fixedObj);
            candidate.TryGetComponent(out DynamicChargedObject dynObj);

            bool validFixed = fixedObj != null && fixedObj.isConductor;
            bool validDyn = dynObj != null && dynObj.isConductor && Mathf.Abs(dynObj.charge) > 0.01f;

            if (!validFixed && !validDyn) continue;

            float distToMouse = Vector2.Distance(mouseWorld, candidate.transform.position);
            if (distToMouse < bestMouseDist)
            {
                bestMouseDist = distToMouse;
                bestFixed = validFixed ? fixedObj : null;
                bestDyn = validDyn ? dynObj : null;
            }
        }

        float resistance = 1f - Mathf.Abs(chargeResource.GetCharge());
        float amount = absorbRate * resistance * Time.deltaTime;

        if (bestFixed != null)
        {
            float chargeSign = Mathf.Sign(bestFixed.charge);
            float delta = chargeSign * amount;
            chargeResource.AddCharge(delta);
            GameEvents.RaiseChargeTransferred(delta, bestFixed.transform.position);
        }
        else if (bestDyn != null)
        {
            float chargeSign = Mathf.Sign(bestDyn.charge);
            float delta = chargeSign * amount;
            chargeResource.AddCharge(delta);
            bestDyn.charge = Mathf.MoveTowards(bestDyn.charge, 0f, amount * 0.4f);
            bestDyn.UpdateVisual();
            GameEvents.RaiseChargeTransferred(delta, bestDyn.transform.position);
        }
    }

    void TryExpel(Vector2 targetPos)
    {
        if (chargeResource.IsNeutral()) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, 2.8f, ignoreLayers);

        CapacitorPlateNode bestPlate = null;
        ChargedObject bestFixed = null;
        DynamicChargedObject bestDyn = null;
        float bestMouseDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D candidate = hits[i];
            if (candidate == null || candidate.gameObject == gameObject || candidate.CompareTag("Player")) continue;

            float distFromPlayer = Vector2.Distance(transform.position, candidate.transform.position);
            if (distFromPlayer > absorbRange) continue;

            candidate.TryGetComponent(out CapacitorPlateNode plateNode);
            candidate.TryGetComponent(out ChargedObject fixedObj);
            candidate.TryGetComponent(out DynamicChargedObject dynObj);

            bool validPlate = plateNode != null && plateNode.isActiveInCurrentPhase && !plateNode.IsCharged;
            bool validFixed = fixedObj != null && !fixedObj.isFixedSource;
            bool validDyn = dynObj != null;

            if (!validPlate && !validFixed && !validDyn) continue;

            float distToMouse = Vector2.Distance(targetPos, candidate.transform.position);
            if (distToMouse < bestMouseDist)
            {
                bestMouseDist = distToMouse;
                bestPlate = validPlate ? plateNode : null;
                bestFixed = (!validPlate && validFixed) ? fixedObj : null;
                bestDyn = (!validPlate && !validFixed && validDyn) ? dynObj : null;
            }
        }

        float currentCharge = chargeResource.GetCharge();
        float amount = expelRate * Time.deltaTime;

        if (bestPlate != null)
        {
            float delta = currentCharge > 0 ? amount : -amount;
            if (bestPlate.TryInjectCharge(delta))
            {
                chargeResource.AddCharge(-delta * 0.65f);
                GameEvents.RaiseChargeTransferred(-delta, bestPlate.transform.position);
            }
        }
        else if (bestFixed != null)
        {
            float delta = currentCharge > 0 ? amount : -amount;
            bestFixed.charge += delta;
            bestFixed.UpdateVisual();
            chargeResource.AddCharge(-delta);
            GameEvents.RaiseChargeTransferred(-delta, bestFixed.transform.position);
        }
        else if (bestDyn != null)
        {
            float delta = currentCharge > 0 ? amount : -amount;
            bestDyn.charge += delta;
            bestDyn.UpdateVisual();
            chargeResource.AddCharge(-delta);
            GameEvents.RaiseChargeTransferred(-delta, bestDyn.transform.position);
        }
    }

    public bool IsAbsorbModeActive() => absorbModeActive;
}