using UnityEngine;

public class ChargeDetector : MonoBehaviour
{
    [Header("Requirements")]
    public float requiredCharge = 1f; // 1 = positive, -1 = negative
    public float chargeThreshold = 0.3f;

    [Header("Door Reference")]
    public GameObject door;

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
        // Check dynamic object
        DynamicChargedObject dynObj = other.GetComponent<DynamicChargedObject>();
        if (dynObj != null)
        {
            CheckCharge(dynObj.charge);
            return;
        }

        // Check player directly
        ChargeResource playerCharge = other.GetComponent<ChargeResource>();
        if (playerCharge != null)
        {
            CheckCharge(playerCharge.GetCharge());
            return;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        DynamicChargedObject dynObj = other.GetComponent<DynamicChargedObject>();
        ChargeResource playerCharge = other.GetComponent<ChargeResource>();

        if (dynObj != null || playerCharge != null)
        {
            if (!isActivated) return;
            isActivated = false;
            UpdateVisual(inactiveColor);
            if (door != null) door.SetActive(true); // close door
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
            if (door != null) door.SetActive(false); // open door
        }
        else
        {
            UpdateVisual(wrongChargeColor);
        }
    }

    void UpdateVisual(Color c)
    {
        if (sr != null) sr.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = requiredCharge > 0 ? Color.blue : Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
    void OnGUI()
    {
        if (Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
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