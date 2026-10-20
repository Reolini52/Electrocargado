using UnityEngine;

/// <summary>
/// Fuerza Magnética (Lorentz: Fm = q · v × B) generada por el movimiento de THEIA
/// sobre objetos cargados dinámicos cercanos.
/// </summary>
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
        if (chargeResource == null || rb == null) return;
        if (chargeResource.IsNeutral()) return;

        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.5f) return;

        var dynList = DynamicChargedObject.ActiveDynamicObjects;
        Vector2 myPos = transform.position;
        Vector2 playerVel = rb.linearVelocity;
        Vector2 perpendicular = new Vector2(-playerVel.normalized.y, playerVel.normalized.x);

        for (int i = 0; i < dynList.Count; i++)
        {
            DynamicChargedObject dynObj = dynList[i];
            if (dynObj == null || Mathf.Abs(dynObj.charge) < 0.1f) continue;

            float dist = Vector2.Distance(myPos, dynObj.transform.position);
            if (dist > effectRadius) continue;

            Rigidbody2D dynRb = dynObj.GetComponent<Rigidbody2D>();
            if (dynRb == null) continue;

            float qPlayer = chargeResource.GetCharge();
            float forceScalar = qPlayer * dynObj.charge * speed * magneticStrength;
            Vector2 magForceVec = perpendicular * forceScalar;

            dynRb.AddForce(magForceVec, ForceMode2D.Force);

            // Corriente equivalente de carga puntual en movimiento: I_eq = |q| * v / r
            float eqCurrentI = Mathf.Abs(qPlayer) * speed / Mathf.Max(dist, 0.5f);
            float eqFieldB = magneticStrength * eqCurrentI;

            GameEvents.RaiseMagneticForceApplied(new CurrentMagneticData(
                myPos,
                dynObj.transform.position,
                eqCurrentI,
                eqFieldB,
                dist,
                magForceVec
            ));
        }
    }
}
