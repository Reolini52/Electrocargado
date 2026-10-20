using UnityEngine;

/// <summary>
/// Clase oficial de movimiento 2D del documento de arquitectura "Electrocargado" (Uniautónoma del Cauca).
/// Extiende e integra toda la física de Coyote Time, Jump Buffering y acoplamiento con CargaResidual
/// definida en PlayerController para compatibilidad directa tanto en prefabs existentes como en nuevos objetos.
/// </summary>
[DisallowMultipleComponent]
public class PlayerMovement2D : PlayerController
{
    [Header("Acoplamiento Electromecánico Adicional (THEIA)")]
    [Tooltip("Bonificación porcentual de salto cuando THEIA posee carga máxima (+Q o -Q).")]
    [SerializeField, Range(0f, 0.5f)]
    private float chargedJumpBoostRatio = 0.12f;

    private float baseJumpForce;
    private CargaResidual residualCapacitor;

    protected override void Awake()
    {
        base.Awake();
        baseJumpForce = jumpForce;
        residualCapacitor = GetComponent<CargaResidual>();
    }

    private void OnEnable()
    {
        GameEvents.OnChargeChanged += HandleChargeChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnChargeChanged -= HandleChargeChanged;
    }

    private void HandleChargeChanged(float currentCharge, float maxCharge, float normalizedRatio)
    {
        float absRatio = Mathf.Abs(normalizedRatio);
        jumpForce = baseJumpForce * (1f + chargedJumpBoostRatio * absRatio);
    }
}
