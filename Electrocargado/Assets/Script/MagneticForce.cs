using UnityEngine;

/// <summary>
/// Fuerza Magnética (Lorentz: Fm = q · v × B) generada por el movimiento de THEIA
/// sobre objetos cargados dinámicos cercanos.
/// [Fase 3] Añadido: clamping de distancia (r ∈ [rMin, effectRadius]), techo de velocidad considerada,
/// Vector2.ClampMagnitude sobre la fuerza y eliminación de GetComponent dentro de FixedUpdate.
/// </summary>
public class MagneticForce : MonoBehaviour
{
    [Header("Magnetic Settings")]
    public float magneticStrength = 3f;
    public float effectRadius = 6f;

    [Header("Clamping de Seguridad Física (Fase 3)")]
    [Tooltip("Distancia mínima usada en los divisores (evita corrientes / campos infinitos al solaparse).")]
    [SerializeField, Range(0.1f, 2f)]
    private float minSafeDistance = 0.5f;

    [Tooltip("Velocidad de THEIA máxima considerada para Lorentz (dashes y knockbacks no generan picos).")]
    [SerializeField, Range(1f, 40f)]
    private float maxSpeedConsidered = 12f;

    [Tooltip("Magnitud máxima de la fuerza magnética aplicada a cada objeto dinámico por paso físico.")]
    [SerializeField, Range(1f, 80f)]
    private float maxMagneticForce = 20f;

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

        Vector2 playerVel = rb.linearVelocity;
        float speed = playerVel.magnitude;
        if (speed < 0.5f) return;

        var dynList = DynamicChargedObject.ActiveDynamicObjects;
        Vector2 myPos = transform.position;
        float qPlayer = chargeResource.GetCharge();
        float sqrRadius = effectRadius * effectRadius;
        float clampedSpeed = Mathf.Min(speed, maxSpeedConsidered);

        for (int i = 0; i < dynList.Count; i++)
        {
            DynamicChargedObject dynObj = dynList[i];
            if (dynObj == null || Mathf.Abs(dynObj.charge) < 0.1f) continue;

            Vector2 dynPos = dynObj.transform.position;
            float sqrDist = (dynPos - myPos).sqrMagnitude;
            if (sqrDist > sqrRadius) continue; // Comparación con sqrMagnitude: evita una raíz por objeto lejano

            // [Fase 3] Rigidbody2D cacheado en el propio objeto (antes: GetComponent por objeto y por paso físico)
            Rigidbody2D dynRb = dynObj.Body;
            if (dynRb == null) continue;

            float dist = Mathf.Sqrt(sqrDist);

            // Lorentz 2D limitada (perpendicular a v, proporcional a |v|, |F| <= maxMagneticForce)
            Vector2 magForceVec = ElectroPhysicsUtility.LorentzForce(
                playerVel, qPlayer * dynObj.charge, magneticStrength,
                maxSpeedConsidered, maxMagneticForce);

            EMForceAccumulator2D.ApplyForce(dynRb, magForceVec);

            // Corriente equivalente de carga puntual en movimiento: I_eq = |q| * v / r  (r acotado con Mathf.Clamp)
            float safeR = Mathf.Clamp(dist, minSafeDistance, effectRadius);
            float eqCurrentI = Mathf.Abs(qPlayer) * clampedSpeed / safeR;
            float eqFieldB = magneticStrength * eqCurrentI;

            GameEvents.RaiseMagneticForceApplied(new CurrentMagneticData(
                myPos,
                dynPos,
                eqCurrentI,
                eqFieldB,
                dist,
                magForceVec
            ));
        }
    }
}
