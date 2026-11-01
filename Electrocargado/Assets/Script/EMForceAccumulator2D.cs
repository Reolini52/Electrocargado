using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fase 3 — Estabilidad Física: "presupuesto" de fuerza electromagnética por Rigidbody2D.
///
/// PROBLEMA QUE RESUELVE:
/// Cada fuente (ChargedObject, CoulombForce2D, POLAROUS, MagneticForce...) ya limita SU fuerza individual,
/// pero sobre THEIA se SUMAN varias fuentes en el mismo paso físico. Con 3-4 cargas cerca, la resultante
/// puede triplicar el máximo individual y lanzar a THEIA fuera del nivel.
///
/// SOLUCIÓN:
/// Las fuentes llaman a <see cref="ApplyForce"/> en lugar de rb.AddForce. Si el cuerpo tiene este componente,
/// la fuerza se acumula y, al final del FixedUpdate (DefaultExecutionOrder alto), se aplica UNA sola vez
/// limitada con Vector2.ClampMagnitude(maxNetForce).
/// Si el cuerpo NO tiene este componente, ApplyForce hace un rb.AddForce normal (comportamiento original intacto).
///
/// USO: añadir este componente al prefab/objeto de THEIA (y opcionalmente a esferas dinámicas).
/// No afecta al movimiento del PlayerController (ese sigue usando rb.AddForce directamente).
/// </summary>
[DefaultExecutionOrder(1000)] // Se ejecuta DESPUÉS del FixedUpdate de todas las fuentes de fuerza.
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class EMForceAccumulator2D : MonoBehaviour
{
    #region Registro estático (lookup O(1), sin GetComponent en FixedUpdate)

    private static readonly Dictionary<Rigidbody2D, EMForceAccumulator2D> Registry =
        new Dictionary<Rigidbody2D, EMForceAccumulator2D>(8);

    /// <summary>
    /// Punto de entrada único para fuerzas electromagnéticas.
    /// Acumula (y luego limita) si el cuerpo tiene acumulador; si no, aplica directamente.
    /// </summary>
    public static void ApplyForce(Rigidbody2D rb, Vector2 force)
    {
        if (rb == null) return;

        if (Registry.TryGetValue(rb, out EMForceAccumulator2D acc) && acc != null && acc.isActiveAndEnabled)
            acc.pendingForce += force;
        else
            rb.AddForce(force, ForceMode2D.Force);
    }

    #endregion

    [Header("Límites de la Resultante Electromagnética (Game Feel)")]
    [Tooltip("Magnitud máxima de la SUMA de todas las fuerzas EM recibidas en un paso físico.")]
    [SerializeField, Range(5f, 150f)]
    private float maxNetForce = 40f;

    [Tooltip("Si se activa, tras aplicar la fuerza EM se limita también la velocidad lineal del cuerpo.")]
    [SerializeField]
    private bool clampVelocityAfterForces = false;

    [Tooltip("Velocidad lineal máxima (solo si 'clampVelocityAfterForces' está activo).")]
    [SerializeField, Range(5f, 60f)]
    private float maxVelocity = 20f;

    [Header("Debug (solo lectura)")]
    [SerializeField] private Vector2 lastRawNetForce;
    [SerializeField] private Vector2 lastAppliedNetForce;

    private Rigidbody2D rb;
    private Vector2 pendingForce;

    public Vector2 LastRawNetForce => lastRawNetForce;
    public Vector2 LastAppliedNetForce => lastAppliedNetForce;
    public float MaxNetForce { get => maxNetForce; set => maxNetForce = Mathf.Max(0f, value); }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        Registry[rb] = this;
        pendingForce = Vector2.zero;
    }

    private void OnDisable()
    {
        if (rb != null && Registry.TryGetValue(rb, out EMForceAccumulator2D acc) && acc == this)
            Registry.Remove(rb);
    }

    private void FixedUpdate()
    {
        lastRawNetForce = pendingForce;

        if (pendingForce.sqrMagnitude > 0f)
        {
            // Limitación de la resultante: prioriza Game Feel sobre precisión matemática.
            lastAppliedNetForce = Vector2.ClampMagnitude(pendingForce, maxNetForce);
            rb.AddForce(lastAppliedNetForce, ForceMode2D.Force);
        }
        else
        {
            lastAppliedNetForce = Vector2.zero;
        }

        if (clampVelocityAfterForces)
            ElectroPhysicsUtility.ClampLinearVelocity(rb, maxVelocity);

        pendingForce = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(lastAppliedNetForce * 0.1f));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(lastRawNetForce * 0.1f));
    }
}
