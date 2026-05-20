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
        if (other.GetComponentInParent<MeleeAttack>() != null) return;
        if (other.GetComponentInParent<MeleeHitbox>() != null) return;
        // Damage enemies
        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);

            // Knockback
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Push enemy AWAY from player in the direction player is facing
                Vector2 dirToEnemy = (other.transform.position -
                    transform.parent.position).normalized;
                Vector2 knockback = new Vector2(
                    dirToEnemy.x * force,
                    force * 0.4f
                );
                rb.AddForce(knockback, ForceMode2D.Impulse);
            }
        }
    }
}