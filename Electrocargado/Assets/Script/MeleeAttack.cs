using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de combate cuerpo a cuerpo en combo de 3 golpes de THEIA.
/// Refactorizado en Fase 1 para soportar Teclado/Ratón + Gamepad (Botón X/Cuadrado o RB/R1 + dirección de Stick).
/// </summary>
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

    [Header("Soporte Gamepad y New Input System")]
    [Tooltip("Opcional: Acción 'Attack' de tu InputActionAsset.")]
    [SerializeField]
    private InputActionReference attackActionReference;

    // Hitbox sizes per hit
    private readonly Vector2[] hitboxSizes = {
        new Vector2(0.5f, 0.4f),  // puño 1
        new Vector2(0.8f, 0.3f),  // patada
        new Vector2(0.6f, 0.5f)   // puño final
    };

    private readonly Vector2[] hitboxOffsets = {
        new Vector2(0.6f, 0f),    // puño 1 - al frente
        new Vector2(0.7f, -0.2f), // patada - al frente abajo
        new Vector2(0.65f, 0.1f)  // puño final - al frente arriba
    };

    private readonly Color[] hitColors = {
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
    private bool usingGamepadAim = false;
    private Vector2 lastMouseScreenPos;

    private Camera cam;
    private PlayerController playerController;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        if (attackActionReference != null && attackActionReference.action != null)
            attackActionReference.action.Enable();
    }

    void Start()
    {
        cam = Camera.main;
        if (hitbox != null) hitbox.enabled = false;
        if (hitboxVisual != null) hitboxVisual.enabled = false;

        if (Mouse.current != null)
            lastMouseScreenPos = Mouse.current.position.ReadValue();
    }

    void Update()
    {
        if (GandalfNPC.isGandalfActive) return;

        UpdateDirection();
        UpdateHitboxPosition();

        if (WasAttackPressedThisFrame())
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

    /// <summary>
    /// Detecta ataque desde InputActionReference, Teclado ([Z]) o Gamepad (Botón X/Cuadrado o RB/R1).
    /// </summary>
    private bool WasAttackPressedThisFrame()
    {
        if (attackActionReference != null && attackActionReference.action != null &&
            attackActionReference.action.WasPressedThisFrame())
        {
            return true;
        }

        bool kbAttack = Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;
        bool gpAttack = Gamepad.current != null &&
                        (Gamepad.current.buttonWest.wasPressedThisFrame ||
                         Gamepad.current.rightShoulder.wasPressedThisFrame);

        if (gpAttack)
            usingGamepadAim = true;

        return kbAttack || gpAttack;
    }

    void UpdateDirection()
    {
        // 1. Si se mueve el stick del Gamepad, usar dirección del mando
        if (Gamepad.current != null)
        {
            float rightStickX = Gamepad.current.rightStick.ReadValue().x;
            float leftStickX = Gamepad.current.leftStick.ReadValue().x;
            float dpadX = Gamepad.current.dpad.ReadValue().x;

            if (Mathf.Abs(rightStickX) > 0.2f)
            {
                usingGamepadAim = true;
                isFacingRight = rightStickX > 0f;
                return;
            }
            if (Mathf.Abs(leftStickX) > 0.2f || Mathf.Abs(dpadX) > 0.1f)
            {
                usingGamepadAim = true;
                float dirX = Mathf.Abs(leftStickX) > 0.2f ? leftStickX : dpadX;
                isFacingRight = dirX > 0f;
                return;
            }
        }

        // 2. Si el ratón se movió recientemente, apuntar hacia el cursor
        if (Mouse.current != null)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            if ((currentMousePos - lastMouseScreenPos).sqrMagnitude > 4f)
            {
                usingGamepadAim = false;
                lastMouseScreenPos = currentMousePos;
            }

            if (!usingGamepadAim)
            {
                if (cam == null) cam = Camera.main;
                if (ElectroPhysicsUtility.TryScreenToWorldPoint2D(cam, currentMousePos, out Vector2 mouseWorld))
                {
                    isFacingRight = mouseWorld.x > transform.position.x;
                    return;
                }
            }
        }

        // 3. Fallback con Gamepad: orientar según hacia dónde mira THEIA
        if (playerController != null)
        {
            isFacingRight = playerController.GetFacingDirectionSign() >= 0;
        }
        else
        {
            isFacingRight = transform.localScale.x >= 0f;
        }
    }

    void UpdateHitboxPosition()
    {
        if (hitboxTransform == null) return;

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

        BoxCollider2D box = hitbox as BoxCollider2D;

        if (box != null)
        {
            box.size = hitboxSizes[index];
            box.offset = Vector2.zero;
        }

        int dir = isFacingRight ? 1 : -1;
        hitboxTransform.localPosition = new Vector3(
            hitboxOffsets[index].x * dir,
            hitboxOffsets[index].y,
            0f
        );

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
