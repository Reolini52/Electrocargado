using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float airControlMultiplier = 0.4f;

    [Header("Jump")]
    public float jumpForce = 20f;
    public int maxJumps = 2;
    public int jumpsRemaining;

    private Rigidbody2D rb;
    private bool isGrounded;
    private Animator animator;
    private bool wasGrounded;
    private float normalGravity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        jumpsRemaining = maxJumps;
        normalGravity = rb.gravityScale;
    }

    void Update()
    {
        // Freeze player completely during Gandalf
        if (GandalfNPC.isGandalfActive)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            if (animator != null)
            {
                animator.SetBool("IsRunning", false);
                animator.SetBool("IsJumping", false);
            }
            return;
        }

        // Restore gravity if it was frozen
        rb.gravityScale = normalGravity;

        CheckGround();
        HandleMovement();
        HandleJump();
        UpdateAnimations();
    }

    void HandleMovement()
    {
        float input = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            input = -1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            input = 1f;

        float control = isGrounded ? 1f : airControlMultiplier;

        float targetSpeed = input * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? 6f : 10f;

        rb.AddForce(Vector2.right * speedDiff * accelRate * control,
            ForceMode2D.Force);
        rb.linearVelocity = new Vector2(
            Mathf.Clamp(rb.linearVelocity.x, -moveSpeed, moveSpeed),
            rb.linearVelocity.y
        );

        if (rb.linearVelocity.x > 0.1f)
            transform.localScale = new Vector3(
                Mathf.Abs(transform.localScale.x),
                transform.localScale.y, 1f);
        else if (rb.linearVelocity.x < -0.1f)
            transform.localScale = new Vector3(
                -Mathf.Abs(transform.localScale.x),
                transform.localScale.y, 1f);
    }

    void HandleJump()
    {
        if (isGrounded && !wasGrounded)
            jumpsRemaining = maxJumps;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && jumpsRemaining > 0)
        {
            if (!isGrounded && jumpsRemaining == maxJumps)
                return;

            if (!isGrounded && jumpsRemaining < maxJumps)
            {
                ChargeResource cr = GetComponent<ChargeResource>();
                if (cr == null || cr.IsNeutral())
                    return;
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpsRemaining--;
        }

        wasGrounded = isGrounded;
    }

    void CheckGround()
    {
        Transform groundCheck = transform.Find("groundcheker");
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            0.2f,
            ~LayerMask.GetMask("Player")
        ) != null;
    }

    void UpdateAnimations()
    {
        if (animator == null) return;

        float speed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetBool("IsRunning", isGrounded && speed > 0.1f);
        animator.SetBool("IsJumping", !isGrounded);
    }

    public bool IsGrounded() => isGrounded;
    public Rigidbody2D GetRb() => rb;
}