using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestiona la salud de los enemigos (como EnemyEmisor y EnemyCorruptor),
/// el feedback visual de daño y la destrucción del GameObject al llegar a 0 HP.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 3;
    public int currentHealth;

    [Header("Visual Feedback")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.12f;
    [SerializeField] private bool spawnParticlesOnDeath = true;

    [Header("Events")]
    public UnityEvent<int> onDamaged;
    public UnityEvent onDeath;

    private SpriteRenderer spriteRenderer;
    private Coroutine flashCoroutine;
    private bool isDead = false;

    void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (currentHealth <= 0)
            currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (isDead || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"{gameObject.name} took {amount} damage. HP: {currentHealth}/{maxHealth}");

        onDamaged?.Invoke(amount);

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (spriteRenderer != null && gameObject.activeInHierarchy)
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(HitFlashRoutine());
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (spriteRenderer == null) yield break;

        Color originalColor = spriteRenderer.color;
        Vector3 originalScale = transform.localScale;

        spriteRenderer.color = hitFlashColor;
        transform.localScale = originalScale * 1.15f;

        yield return new WaitForSeconds(hitFlashDuration);

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        transform.localScale = originalScale;
        flashCoroutine = null;
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} died");
        onDeath?.Invoke();

        if (spawnParticlesOnDeath)
        {
            SpawnBurstParticles(transform.position, spriteRenderer != null ? spriteRenderer.color : Color.cyan, 18, 4.5f);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Genera un estallido ligero de partículas electromagnéticas al recibir impacto o morir.
    /// </summary>
    public static void SpawnBurstParticles(Vector3 worldPos, Color burstColor, int count = 12, float speed = 3.5f)
    {
        GameObject vfxObj = new GameObject("ElectroImpactVFX");
        vfxObj.transform.position = worldPos;

        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = burstColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        main.maxParticles = count + 4;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                Material mat = new Material(shader);
                renderer.material = mat;
            }
            renderer.sortingOrder = 25;
        }

        ps.Play();
        Destroy(vfxObj, 0.6f);
    }
}
