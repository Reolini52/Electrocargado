using UnityEngine;

public class ChargeDetector : MonoBehaviour
{
    [Header("Requirements")]
    public float requiredCharge = 1f;
    public float chargeThreshold = 0.3f;

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

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual(inactiveColor);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Detector hit: {other.name} hasScript: {other.GetComponent<DynamicChargedObject>() != null}");
        DynamicChargedObject dynObj = other.GetComponent<DynamicChargedObject>();
        ChargeResource playerCharge = other.GetComponent<ChargeResource>();

        if (dynObj != null)
            CheckCharge(dynObj.charge);
        else if (playerCharge != null)
            CheckCharge(playerCharge.GetCharge());
    }

    void OnTriggerExit2D(Collider2D other)
    {
        DynamicChargedObject dynObj = other.GetComponent<DynamicChargedObject>();
        ChargeResource playerCharge = other.GetComponent<ChargeResource>();

        if (dynObj != null || playerCharge != null)
        {
            isActivated = false;
            UpdateVisual(inactiveColor);

            if (door != null) door.SetActive(true);

            // Deactivate rod if partner also deactivates
            if (connectedRod != null)
                connectedRod.ActivateCurrent(false, false);
        }
    }

    void CheckCharge(float incomingCharge)
    {
        bool correct = requiredCharge > 0 ?
            incomingCharge > chargeThreshold :
            incomingCharge < -chargeThreshold;

        if (correct)
        {
            isActivated = true;
            UpdateVisual(activeColor);

            if (door != null) door.SetActive(false);

            // Check if partner is also activated
            TryActivateRod();
        }
        else
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
            // Both terminals active = current flows
            // Direction depends on which is + and which is -
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
        if (Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);

        // Only draw if object is visible on screen
        if (screenPos.z < 0) return; // behind camera
        if (screenPos.x < 0 || screenPos.x > Screen.width) return;
        if (screenPos.y < 0 || screenPos.y > Screen.height) return;

        screenPos.y = Screen.height - screenPos.y;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        GUI.color = requiredCharge > 0 ?
            new Color(0.3f, 0.6f, 1f) :
            new Color(1f, 0.3f, 0.3f);

        GUI.Label(
            new Rect(screenPos.x - 20, screenPos.y - 30, 40, 30),
            requiredCharge > 0 ? "⊕" : "⊖",
            style
        );
    }
}