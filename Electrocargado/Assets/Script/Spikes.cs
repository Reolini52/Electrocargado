using UnityEngine;

public class Spikes : MonoBehaviour
{
    public int damage = 3;

    void OnTriggerEnter2D(Collider2D other)
    {
        // Kill enemies instantly
        EnemyHealth eh = other.GetComponent<EnemyHealth>();
        if (eh != null)
        {
            eh.TakeDamage(damage);
            return;
        }

        // Damage player
        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph != null)
            ph.TakeDamage(damage); // same damage as enemies = instakill
    }
}