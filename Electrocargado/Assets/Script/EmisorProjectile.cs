using UnityEngine;

public class EmisorProjectile : MonoBehaviour
{
    public int damage = 1;
    public float charge = 1f;

    private GameObject owner;

    public void SetOwner(GameObject ownerObj)
    {
        owner = ownerObj;
        Collider2D ownerCol = ownerObj.GetComponent<Collider2D>();
        Collider2D myCol = GetComponent<Collider2D>();
        if (ownerCol != null && myCol != null)
            Physics2D.IgnoreCollision(myCol, ownerCol);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"HIT: {other.name} tag:{other.tag}");

        if (owner != null && other.gameObject == owner) return;
        if (other.GetComponent<EmisorProjectile>() != null) return;

        // Find PlayerHealth anywhere in the hit object or its parent
        PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            Debug.Log("DAMAGING PLAYER");
            ph.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Destroy on anything else except enemies
        EnemyHealth eh = other.GetComponentInParent<EnemyHealth>();
        if (eh != null) return; // pass through enemies

        Destroy(gameObject);
    }
}