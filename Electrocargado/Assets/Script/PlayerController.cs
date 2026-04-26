using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float jumpForce = 16f;

    [Header("Charge")]
    public float currentCharge = 1f;
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        UpdateChargeVisual();
    }

    void Update()
    {
        HandleMovement();
        HandleJump();
        HandleChargeSwitch();
        CheckGround();
    }

    void HandleMovement()
    {
        float input = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            input = -1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            input = 1f;

        // Use force instead of directly setting velocity
        // This lets external forces (charge fields) actually affect the player
        float targetSpeed = input * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? 6f : 10f;
        float movement = speedDiff * accelRate;

        rb.AddForce(Vector2.right * movement, ForceMode2D.Force);

        // Cap horizontal speed so player cant go mach 10
        rb.linearVelocity = new Vector2(
            Mathf.Clamp(rb.linearVelocity.x, -moveSpeed, moveSpeed),
            rb.linearVelocity.y
        );
    }

    void HandleJump()
    {
        if ((Keyboard.current.spaceKey.wasPressedThisFrame) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    void HandleChargeSwitch()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            currentCharge *= -1f;
            UpdateChargeVisual();
        }
    }

    void CheckGround()
    {
        // Raycast straight down from player center
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            Vector2.down,
            0.6f,
            ~LayerMask.GetMask("Player")
        );
        isGrounded = hit.collider != null;
    }

    void UpdateChargeVisual()
    {
        sr.color = currentCharge > 0 ? positiveColor : negativeColor;
    }

    public float GetCharge() => currentCharge;
}