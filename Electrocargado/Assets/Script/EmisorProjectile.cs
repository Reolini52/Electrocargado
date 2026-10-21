using UnityEngine;

/// <summary>
/// Proyectil con carga eléctrica disparado por Emisores o por el Boss POLAROUS.
/// Defensa física (Ley de Coulomb): Si THEIA tiene la MISMA polaridad que el proyectil (q_proj * Q_THEIA > 0),
/// la fuerza de repulsión electrostática desvía/repele el proyectil de vuelta hacia el emisor/Boss.
/// </summary>
public class EmisorProjectile : MonoBehaviour
{
    public int damage = 1;
    public float charge = 1f;
    public float coulombDeflectionStrength = 32f;
    public float deflectionRadius = 4.6f;

    private GameObject owner;
    private Rigidbody2D rb;
    private ChargeResource playerCharge;
    private bool wasDeflected = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerCharge = playerObj.GetComponent<ChargeResource>();
        }
    }

    public void SetOwner(GameObject ownerObj)
    {
        owner = ownerObj;
        Collider2D ownerCol = ownerObj.GetComponent<Collider2D>();
        Collider2D myCol = GetComponent<Collider2D>();
        if (ownerCol != null && myCol != null)
            Physics2D.IgnoreCollision(myCol, ownerCol);
    }

    /// <summary>
    /// Permite reflejar el proyectil instantáneamente cuando THEIA le apunta con el rayo de igual carga.
    /// </summary>
    public void ForceDeflectByBeam(Vector2 beamOrigin)
    {
        if (wasDeflected || rb == null) return;
        wasDeflected = true;

        // Re-habilitar colisión con el Boss para que pueda impactarlo de vuelta
        if (owner != null)
        {
            Collider2D ownerCol = owner.GetComponent<Collider2D>();
            Collider2D myCol = GetComponent<Collider2D>();
            if (ownerCol != null && myCol != null)
                Physics2D.IgnoreCollision(myCol, ownerCol, false);

            Vector2 toOwner = ((Vector2)owner.transform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = toOwner * 11f;
        }
        else
        {
            Vector2 away = ((Vector2)transform.position - beamOrigin).normalized;
            rb.linearVelocity = away * 11f;
        }
    }

    void FixedUpdate()
    {
        if (rb == null || playerCharge == null || playerCharge.IsNeutral()) return;

        Vector2 toProjectile = (Vector2)transform.position - (Vector2)playerCharge.transform.position;
        float dist = toProjectile.magnitude;
        if (dist > deflectionRadius || dist < 0.1f) return;

        float qProd = charge * playerCharge.GetCharge();
        float clampedR = Mathf.Max(dist, 0.65f);
        Vector2 rHat = toProjectile / dist;

        if (qProd > 0.05f)
        {
            // Misma polaridad -> Repulsión intensa (Ley de Coulomb: cargas iguales se repelen)
            float repelForce = (coulombDeflectionStrength * 2.0f * Mathf.Abs(qProd)) / (clampedR * clampedR);
            rb.AddForce(rHat * repelForce, ForceMode2D.Force);

            if (dist < 2.1f && !wasDeflected)
            {
                ForceDeflectByBeam(playerCharge.transform.position);
            }
        }
        else if (qProd < -0.05f)
        {
            // Polaridad opuesta -> Atracción hacia THEIA
            float attractForce = (coulombDeflectionStrength * 0.55f * Mathf.Abs(qProd)) / (clampedR * clampedR);
            rb.AddForce(-rHat * attractForce, ForceMode2D.Force);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.gameObject == owner && !wasDeflected) return;
        if (other.GetComponent<EmisorProjectile>() != null) return;
        if (other.isTrigger && other.GetComponent<PolarousBossController>() == null && other.GetComponentInParent<PlayerHealth>() == null) return;

        // Si el proyectil fue repelido de vuelta hacia el Boss Polarous, lo daña
        PolarousBossController boss = other.GetComponent<PolarousBossController>();
        if (boss != null && wasDeflected)
        {
            boss.TakeReflectedProjectileDamage(1);
            Destroy(gameObject);
            return;
        }

        PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            // Si THEIA tiene la misma polaridad que el proyectil, la repulsión de Coulomb la protege y rebota el proyectil
            if (playerCharge != null && (charge * playerCharge.GetCharge()) > 0.2f)
            {
                ForceDeflectByBeam(playerCharge.transform.position);
                return;
            }

            ph.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        EnemyHealth eh = other.GetComponentInParent<EnemyHealth>();
        if (eh != null) return;

        Destroy(gameObject);
    }
}
