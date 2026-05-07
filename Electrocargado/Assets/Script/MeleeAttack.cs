using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeAttack : MonoBehaviour
{
    [Header("Combo Settings")]
    public float hit1Duration = 0.12f;
    public float hit2Duration = 0.18f;
    public float hit3Duration = 0.12f;
    public float comboWindow = 0.5f;
    public float comboCooldown = 0.6f;

    [Header("Damage")]
    public int hit1Damage = 1;
    public int hit2Damage = 1;
    public int hit3Damage = 2;

    [Header("Force")]
    public float hitForce = 8f;

    [Header("References")]
    public Collider2D hitbox;
    public Transform hitboxTransform;
    public SpriteRenderer hitboxVisual;

    // Hitbox sizes per hit
    // Puño 1: pequeño y adelante
    // Patada: largo y bajo
    // Puño 3: grande adelante
    private Vector2[] hitboxSizes = {
        new Vector2(0.5f, 0.4f),  // puño 1
        new Vector2(0.8f, 0.3f),  // patada
        new Vector2(0.6f, 0.5f)   // puño final
    };

    private Vector2[] hitboxOffsets = {
        new Vector2(0.6f, 0f),    // puño 1 - al frente
        new Vector2(0.7f, -0.2f), // patada - al frente abajo
        new Vector2(0.65f, 0.1f)  // puño final - al frente arriba
    };

    private Color[] hitColors = {
        new Color(1f, 0.8f, 0f, 0.6f),   // amarillo puño
        new Color(0f, 0.8f, 1f, 0.6f),   // azul patada
        new Color(1f, 0.3f, 0f, 0.7f)    // naranja puño final
    };

    private int comboStep = 0;
    private float hitTimer = 0f;
    private float comboTimer = 0f;
    private float cooldownTimer = 0f;
    private bool inputBuffered = false;
    private bool isFacingRight = true;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (hitbox != null) hitbox.enabled = false;
        if (hitboxVisual != null) hitboxVisual.enabled = false;
    }

    void Update()
    {
        UpdateDirection();
        UpdateHitboxPosition();

        if (Keyboard.current.zKey.wasPressedThisFrame)
            inputBuffered = true;

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        if (hitTimer > 0f)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0f)
            {
                DeactivateHit();
                comboTimer = comboWindow;
            }
            return;
        }

        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;

            if (inputBuffered)
            {
                inputBuffered = false;
                AdvanceCombo();
                return;
            }

            if (comboTimer <= 0f)
            {
                comboStep = 0;
                cooldownTimer = comboCooldown;
            }
            return;
        }

        if (inputBuffered && comboStep == 0)
        {
            inputBuffered = false;
            AdvanceCombo();
        }
    }

    void UpdateDirection()
    {
        // Face toward mouse X position
        Vector2 mouseWorld = cam.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );
        isFacingRight = mouseWorld.x > transform.position.x;
    }

    void UpdateHitboxPosition()
    {
        if (hitboxTransform == null) return;

        // Only flip on X axis
        int dir = isFacingRight ? 1 : -1;
        Vector2 currentOffset = hitboxTransform.localPosition;
        hitboxTransform.localPosition = new Vector3(
            Mathf.Abs(currentOffset.x) * dir,
            currentOffset.y,
            0f
        );
    }

    void AdvanceCombo()
    {
        comboStep++;
        int index = comboStep - 1;

        switch (comboStep)
        {
            case 1:
                ActivateHit(hit1Duration, hit1Damage, index);
                break;
            case 2:
                ActivateHit(hit2Duration, hit2Damage, index);
                break;
            case 3:
                ActivateHit(hit3Duration, hit3Damage, index);
                comboStep = 0;
                comboTimer = 0f;
                cooldownTimer = comboCooldown;
                break;
        }
    }

    void ActivateHit(float duration, int damage, int index)
    {
        if (hitbox == null) return;

        // Resize hitbox
        CircleCollider2D circle = hitbox as CircleCollider2D;
        BoxCollider2D box = hitbox as BoxCollider2D;

        if (box != null)
        {
            box.size = hitboxSizes[index];
            box.offset = Vector2.zero;
        }

        // Reposition
        int dir = isFacingRight ? 1 : -1;
        hitboxTransform.localPosition = new Vector3(
            hitboxOffsets[index].x * dir,
            hitboxOffsets[index].y,
            0f
        );

        // Visual
        if (hitboxVisual != null)
        {
            hitboxVisual.enabled = true;
            hitboxVisual.color = hitColors[index];
            if (box != null)
                hitboxVisual.transform.localScale = new Vector3(
                    hitboxSizes[index].x,
                    hitboxSizes[index].y,
                    1f
                );
        }

        hitbox.enabled = true;
        hitbox.GetComponent<MeleeHitbox>()?.SetDamage(damage, hitForce, isFacingRight);
        hitTimer = duration;
    }

    void DeactivateHit()
    {
        if (hitbox != null) hitbox.enabled = false;
        if (hitboxVisual != null) hitboxVisual.enabled = false;
    }
}