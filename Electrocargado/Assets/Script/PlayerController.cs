using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de plataformas 2D de THEIA (Refactorizado con arquitectura desacoplada).
/// Incluye Coyote Time, Jump Buffering, separación estricta Update() (Input/Timers) vs FixedUpdate() (Física),
/// articulación con el peso/carga RC de THEIA y cero llamadas a GetComponent en Update().
/// </summary>
public class PlayerController : MonoBehaviour
{
    #region Parámetros de Movimiento (Inspector)

    [Header("Movimiento Horizontal (Física 2D)")]
    [Tooltip("Velocidad máxima de desplazamiento horizontal de THEIA.")]
    [SerializeField, Range(2f, 20f)]
    public float moveSpeed = 8f;

    [Tooltip("Multiplicador de control en el aire (0 = sin control aéreo, 1 = control total).")]
    [SerializeField, Range(0.1f, 1f)]
    public float airControlMultiplier = 0.4f;

    [Tooltip("Tasa de aceleración al presionar dirección.")]
    [SerializeField, Range(1f, 25f)]
    private float accelerationRate = 6f;

    [Tooltip("Tasa de desaceleración al soltar dirección.")]
    [SerializeField, Range(1f, 25f)]
    private float decelerationRate = 10f;

    #endregion

    #region Parámetros de Salto y Game Feel (Coyote Time & Jump Buffering)

    [Header("Salto y Propulsión Electrostática")]
    [Tooltip("Impulso vertical base de salto.")]
    [SerializeField, Range(5f, 35f)]
    public float jumpForce = 20f;

    [Tooltip("Cantidad máxima de saltos (el segundo salto requiere carga eléctrica activa).")]
    [SerializeField, Range(1, 3)]
    public int maxJumps = 2;

    [Tooltip("Saltos restantes actuales.")]
    public int jumpsRemaining;

    [Header("Game Feel: Coyote Time & Jump Buffering")]
    [Tooltip("Tiempo de gracia (segundos) para saltar tras abandonar el borde de una plataforma.")]
    [SerializeField, Range(0.02f, 0.35f)]
    private float coyoteTimeDuration = 0.14f;

    [Tooltip("Tiempo de memoria (segundos) si el jugador presiona Salto justo antes de tocar el suelo.")]
    [SerializeField, Range(0.02f, 0.35f)]
    private float jumpBufferDuration = 0.15f;

    [Header("Detección de Suelo")]
    [Tooltip("Radio del círculo de detección de suelo en el hijo 'groundcheker'.")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float groundCheckRadius = 0.2f;

    #endregion

    #region Referencias Cacheadas (Cero GetComponent en Update/FixedUpdate)

    private Rigidbody2D rb;
    private Animator animator;
    private ChargeResource chargeResource;
    private CargaResidual cargaResidual;
    private Transform groundCheck;
    private int groundLayerMask;

    #endregion

    #region Estado Interno de Input y Timers

    private float horizontalInput;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool jumpExecutionRequested;
    private bool isGrounded;
    private bool wasGrounded;
    private float normalGravity;

    #endregion

    #region Ciclo de Vida de Unity

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        chargeResource = GetComponent<ChargeResource>();
        cargaResidual = GetComponent<CargaResidual>();
        groundCheck = transform.Find("groundcheker");
        groundLayerMask = ~LayerMask.GetMask("Player");
    }

    protected virtual void Start()
    {
        jumpsRemaining = maxJumps;
        if (rb != null)
        {
            normalGravity = rb.gravityScale;
        }
    }

    protected virtual void Update()
    {
        // Congelar completamente a THEIA durante la evaluación del Firewall Gandalf
        if (GandalfNPC.isGandalfActive)
        {
            horizontalInput = 0f;
            jumpBufferCounter = 0f;
            jumpExecutionRequested = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }

            if (animator != null)
            {
                animator.SetBool("IsRunning", false);
                animator.SetBool("IsJumping", false);
            }
            return;
        }

        if (rb != null)
        {
            rb.gravityScale = normalGravity;
        }

        // 1. Lectura de Input y Timers en Update()
        ReadInputAndTimers();

        // 2. Evaluación de Coyote Time y Jump Buffering
        EvaluateJumpTrigger();

        // 3. Orientación de Sprite y Animaciones
        UpdateSpriteFacingAndAnimations();
    }

    protected virtual void FixedUpdate()
    {
        if (GandalfNPC.isGandalfActive || rb == null) return;

        // 1. Detección física de suelo
        CheckGroundPhysics();

        // 2. Movimiento horizontal con fuerzas físicas
        ApplyHorizontalPhysics();

        // 3. Ejecución física del salto si fue validado por el Buffer/Coyote
        if (jumpExecutionRequested)
        {
            ExecuteJumpPhysics();
            jumpExecutionRequested = false;
        }
    }

    #endregion

    #region Lógica de Input, Coyote Time y Jump Buffering (Update)

    private void ReadInputAndTimers()
    {
        horizontalInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput = 1f;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpBufferCounter = jumpBufferDuration;
            }
            else
            {
                jumpBufferCounter -= Time.deltaTime;
            }
        }

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTimeDuration;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    private void EvaluateJumpTrigger()
    {
        if (jumpBufferCounter <= 0f) return;

        // Caso A: Salto desde suelo o dentro de la ventana de Coyote Time
        if (coyoteTimeCounter > 0f && jumpsRemaining > 0)
        {
            jumpExecutionRequested = true;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            return;
        }

        // Caso B: Doble salto en el aire impulsado por carga eléctrica (requiere THEIA cargada)
        if (!isGrounded && jumpsRemaining > 0 && jumpsRemaining < maxJumps)
        {
            bool hasCharge = (cargaResidual != null && !cargaResidual.IsNeutral) ||
                             (chargeResource != null && !chargeResource.IsNeutral());

            if (hasCharge)
            {
                jumpExecutionRequested = true;
                jumpBufferCounter = 0f;
            }
        }
    }

    private void UpdateSpriteFacingAndAnimations()
    {
        if (rb == null) return;

        if (rb.linearVelocity.x > 0.1f)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                1f);
        }
        else if (rb.linearVelocity.x < -0.1f)
        {
            transform.localScale = new Vector3(
                -Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                1f);
        }

        if (animator != null)
        {
            float speed = Mathf.Abs(rb.linearVelocity.x);
            animator.SetBool("IsRunning", isGrounded && speed > 0.1f);
            animator.SetBool("IsJumping", !isGrounded);
        }
    }

    #endregion

    #region Motor de Movimiento Físico (FixedUpdate)

    private void CheckGroundPhysics()
    {
        if (groundCheck == null)
        {
            groundCheck = transform.Find("groundcheker");
            if (groundCheck == null)
            {
                isGrounded = false;
                return;
            }
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayerMask
        ) != null;

        if (isGrounded && !wasGrounded)
        {
            jumpsRemaining = maxJumps;
        }

        wasGrounded = isGrounded;
    }

    private void ApplyHorizontalPhysics()
    {
        float control = isGrounded ? 1f : airControlMultiplier;
        float targetSpeed = horizontalInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? accelerationRate : decelerationRate;

        rb.AddForce(Vector2.right * (speedDiff * accelRate * control), ForceMode2D.Force);

        rb.linearVelocity = new Vector2(
            Mathf.Clamp(rb.linearVelocity.x, -moveSpeed, moveSpeed),
            rb.linearVelocity.y
        );
    }

    private void ExecuteJumpPhysics()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);
    }

    #endregion

    #region API Pública

    public bool IsGrounded() => isGrounded;
    public Rigidbody2D GetRb() => rb;

    #endregion
}