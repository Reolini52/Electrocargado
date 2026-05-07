using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 3;
    public int currentHealth;

    [Header("Level Resets")]
    public int maxResets = 3;
    public int currentResets;

    [Header("Invincibility")]
    public float invincibilityDuration = 1.5f;
    private float invincibilityTimer = 0f;
    private bool isInvincible = false;

    [Header("Visual")]
    public float flashInterval = 0.1f;
    private float flashTimer = 0f;
    private SpriteRenderer sr;

    // Static persists between scene reloads
    private static int savedResets = -1;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;

        // If first time load, initialize resets
        // If reloading, use saved value
        if (savedResets == -1)
            savedResets = maxResets;

        currentResets = savedResets;
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame && currentResets > 0)
            ResetLevel();

        // Test damage
        if (Keyboard.current.tKey.wasPressedThisFrame)
            TakeDamage(1);

        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f)
            {
                sr.enabled = !sr.enabled;
                flashTimer = flashInterval;
            }

            if (invincibilityTimer <= 0f)
            {
                isInvincible = false;
                sr.enabled = true;
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (isInvincible) return;

        currentHealth -= amount;
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
        flashTimer = flashInterval;

        if (currentHealth <= 0)
            ResetLevel();
    }

    void ResetLevel()
    {
        savedResets--;
        currentResets = savedResets;

        if (savedResets <= 0)
        {
            GameOver();
            return;
        }

        currentHealth = maxHealth;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void GameOver()
    {
        Debug.Log("GAME OVER");
        savedResets = maxResets; // reset for next playthrough
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool IsInvincible() => isInvincible;
}