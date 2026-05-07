using UnityEngine;

public class MeleeHitbox : MonoBehaviour
{
    private int damage = 1;
    private float force = 8f;
    private bool facingRight = true;

    public void SetDamage(int dmg, float f, bool right)
    {
        damage = dmg;
        force = f;
        facingRight = right;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) return;

        // Damage enemies
        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);

            // Knockback
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 knockback = new Vector2(
                    facingRight ? force : -force,
                    force * 0.5f
                );
                rb.AddForce(knockback, ForceMode2D.Impulse);
            }
        }
    }
}