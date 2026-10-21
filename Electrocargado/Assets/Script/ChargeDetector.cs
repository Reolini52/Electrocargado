using UnityEngine;

public class ChargeDetector : MonoBehaviour
{
    [Header("Requirements")]
    public float requiredCharge = 1f;
    public float chargeThreshold = 0.3f;
    [Tooltip("Si es true, solo un objeto dinámico cargado (llave) puede activar este receptor.")]
    public bool requireDynamicObjectKey = false;

    [Header("Connections")]
    public GameObject door;
    public CurrentRod connectedRod;
    public bool isPositiveTerminal = true; // + or - terminal of rod

    [Header("Rod Partner")]
    public ChargeDetector partnerDetector; // the other terminal

    [Header("Visual")]
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f);
    public Color activeColor = new Color(0.2f, 1f, 0.4f);
    public Color wrongChargeColor = new Color(1f, 0.3f, 0.3f);

    private SpriteRenderer sr;
    private bool isActivated = false;
    private DynamicChargedObject currentDynamicOnPad;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual(inactiveColor);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        EvaluateCollider(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!isActivated)
        {
            EvaluateCollider(other);
        }
    }

    void EvaluateCollider(Collider2D other)
    {
        if (other.TryGetComponent(out DynamicChargedObject dynObj))
        {
            currentDynamicOnPad = dynObj;
            CheckCharge(dynObj.charge);
        }
        else if (!requireDynamicObjectKey && other.TryGetComponent(out ChargeResource playerCharge))
        {
            CheckCharge(playerCharge.GetCharge());
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out DynamicChargedObject dynObj))
        {
            if (currentDynamicOnPad == dynObj)
                currentDynamicOnPad = null;
        }

        // Si todavía queda una llave dinámica válida sobre el receptor, mantener la puerta abierta
        if (currentDynamicOnPad != null && IsChargeValid(currentDynamicOnPad.charge))
        {
            return;
        }

        if (dynObj != null || other.GetComponent<ChargeResource>() != null)
        {
            isActivated = false;
            UpdateVisual(inactiveColor);

            if (door != null) door.SetActive(true);

            // Deactivate rod if partner also deactivates
            if (connectedRod != null)
                connectedRod.ActivateCurrent(false, false);
        }
    }

    bool IsChargeValid(float incomingCharge)
    {
        return requiredCharge > 0f
            ? incomingCharge > chargeThreshold
            : incomingCharge < -chargeThreshold;
    }

    void CheckCharge(float incomingCharge)
    {
        if (IsChargeValid(incomingCharge))
        {
            isActivated = true;
            UpdateVisual(activeColor);

            if (door != null) door.SetActive(false);

            // Check if partner is also activated
            TryActivateRod();
        }
        else if (Mathf.Abs(incomingCharge) > 0.05f)
        {
            UpdateVisual(wrongChargeColor);
        }
    }

    void TryActivateRod()
    {
        if (connectedRod == null) return;

        bool partnerActive = partnerDetector != null &&
            partnerDetector.IsActivated();

        if (partnerActive)
        {
            connectedRod.ActivateCurrent(true, isPositiveTerminal);
        }
    }

    public bool IsActivated() => isActivated;

    void UpdateVisual(Color c)
    {
        if (sr != null) sr.color = c;
    }

    void OnGUI()
    {
        if (GameEvents.IsCleanHudHidden || Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);

        if (screenPos.z < 0) return;
        if (screenPos.x < 0 || screenPos.x > Screen.width) return;
        if (screenPos.y < 0 || screenPos.y > Screen.height) return;

        screenPos.y = Screen.height - screenPos.y;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = requireDynamicObjectKey ? 14 : 20;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        GUI.color = isActivated
            ? new Color(0.25f, 1f, 0.5f)
            : (requiredCharge > 0 ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.3f, 0.3f));

        string symbol = requiredCharge > 0 ? "⊕" : "⊖";
        string text = requireDynamicObjectKey
            ? (isActivated ? $"{symbol} LLAVE ✓" : $"Receptor {symbol}")
            : symbol;

        GUI.Label(
            new Rect(screenPos.x - 45, screenPos.y - 32, 90, 30),
            text,
            style
        );
    }
}
