using UnityEngine;

public class MagneticForce : MonoBehaviour
{
    [Header("Magnetic Settings")]
    public float magneticStrength = 3f;
    public float effectRadius = 6f;

    private ChargeResource chargeResource;
    private Rigidbody2D rb;

    void Start()
    {
        chargeResource = GetComponent<ChargeResource>();
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (chargeResource.IsNeutral()) return;
        if (rb.linearVelocity.magnitude < 0.5f) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, effectRadius);

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            DynamicChargedObject dynObj =
                hit.GetComponent<DynamicChargedObject>();
            if (dynObj == null || Mathf.Abs(dynObj.charge) < 0.1f) continue;

            Rigidbody2D dynRb = hit.GetComponent<Rigidbody2D>();
            if (dynRb == null) continue;

            Vector2 playerVel = rb.linearVelocity;
            Vector2 perpendicular = new Vector2(
                -playerVel.normalized.y,
                playerVel.normalized.x
            );

            float force = chargeResource.GetCharge()
                * dynObj.charge
                * playerVel.magnitude
                * magneticStrength;

            dynRb.AddForce(perpendicular * force);
        }
    }
}