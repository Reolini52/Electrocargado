using System;
using UnityEngine;

/// <summary>
/// Bus de eventos de Electrocargado.
/// Conecta la física en tiempo real con la interfaz y el firewall sin acoplar componentes.
/// </summary>
public static class GameEvents
{
    public static bool IsCleanHudHidden { get; private set; } = false;

    // Eventos de carga y circuito RC
    public static event Action<float, float, float> OnChargeChanged;
    public static event Action<float, float, float, float> OnRCDischargeStep;
    public static event Action<bool> OnConductorContactChanged;
    public static event Action<float, Vector2> OnChargeTransferred;

    // Eventos de fuerzas (Coulomb, Campo E y Corriente / Fuerza Magnética)
    public static event Action<CoulombForceData> OnForceApplied;
    public static event Action<Vector2, Vector2> OnElectricFieldUpdated;
    public static event Action<CurrentMagneticData> OnMagneticForceApplied;

    // Eventos de visualización (Teclas V, B/T, H)
    public static event Action<bool> OnDebugOverlayToggled;
    public static event Action<bool> OnMathTableToggled;
    public static event Action<bool> OnHudCleanModeToggled;

    // Eventos del Firewall Gandalf
    public static event Action<bool> OnFirewallTriggered;
    public static event Action<int, bool, string> OnQuizQuestionAnswered;
    public static event Action<bool> OnFirewallEvaluationCompleted;

    public static void RaiseChargeChanged(float currentCharge, float maxCharge)
    {
        float ratio = maxCharge > 0.0001f ? Mathf.Clamp(currentCharge / maxCharge, -1f, 1f) : 0f;
        OnChargeChanged?.Invoke(currentCharge, maxCharge, ratio);
    }

    public static void RaiseRCDischargeStep(float currentCharge, float initialCharge, float elapsedTime, float tauRC)
    {
        OnRCDischargeStep?.Invoke(currentCharge, initialCharge, elapsedTime, tauRC);
    }

    public static void RaiseConductorContactChanged(bool isContacting)
    {
        OnConductorContactChanged?.Invoke(isContacting);
    }

    public static void RaiseForceApplied(CoulombForceData data)
    {
        OnForceApplied?.Invoke(data);
    }

    public static void RaiseMagneticForceApplied(CurrentMagneticData data)
    {
        OnMagneticForceApplied?.Invoke(data);
    }

    public static void RaiseChargeTransferred(float deltaCharge, Vector2 sourcePosition)
    {
        OnChargeTransferred?.Invoke(deltaCharge, sourcePosition);
    }

    public static void RaiseDebugOverlayToggled(bool isActive)
    {
        OnDebugOverlayToggled?.Invoke(isActive);
    }

    public static void RaiseMathTableToggled(bool isActive)
    {
        OnMathTableToggled?.Invoke(isActive);
    }

    public static void RaiseHudCleanModeToggled(bool isHidden)
    {
        IsCleanHudHidden = isHidden;
        OnHudCleanModeToggled?.Invoke(isHidden);
    }

    public static void RaiseElectricFieldUpdated(Vector2 position, Vector2 netField)
    {
        OnElectricFieldUpdated?.Invoke(position, netField);
    }

    public static void RaiseFirewallTriggered(bool isActive)
    {
        OnFirewallTriggered?.Invoke(isActive);
    }

    public static void RaiseQuizQuestionAnswered(int questionIndex, bool isCorrect, string feedback)
    {
        OnQuizQuestionAnswered?.Invoke(questionIndex, isCorrect, feedback);
    }

    public static void RaiseFirewallEvaluationCompleted(bool passed)
    {
        OnFirewallEvaluationCompleted?.Invoke(passed);
    }
}

public readonly struct CoulombForceData
{
    public readonly Vector2 SourcePosition;
    public readonly Vector2 TargetPosition;
    public readonly float ChargeSource;      // q1
    public readonly float ChargeTarget;      // Q (THEIA)
    public readonly float Distance;          // r
    public readonly float CoulombConstant;   // k
    public readonly Vector2 ForceVector;     // Fe
    public readonly float ForceMagnitude;    // |Fe|
    public readonly bool IsAttractive;

    public CoulombForceData(
        Vector2 sourcePos,
        Vector2 targetPos,
        float q1,
        float q2,
        float distance,
        float ke,
        Vector2 forceVector,
        bool isAttractive)
    {
        SourcePosition = sourcePos;
        TargetPosition = targetPos;
        ChargeSource = q1;
        ChargeTarget = q2;
        Distance = distance;
        CoulombConstant = ke;
        ForceVector = forceVector;
        ForceMagnitude = forceVector.magnitude;
        IsAttractive = isAttractive;
    }
}

/// <summary>
/// Datos instantáneos de Corriente Eléctrica (I = dQ/dt = n·q·v_d) y Fuerza Magnética (Fm)
/// generados por CurrentRod o MagneticForce cuando interactúan con THEIA.
/// </summary>
public readonly struct CurrentMagneticData
{
    public readonly Vector2 RodPosition;
    public readonly Vector2 TargetPosition;
    public readonly float CurrentIntensityI;   // Corriente I (Amperios escalados)
    public readonly float MagneticFieldB;      // Campo magnético B (Teslas escalados)
    public readonly float DistanceR;           // Distancia al conductor r
    public readonly Vector2 MagneticForceVec;  // Vector de fuerza magnética Fm
    public readonly float MagneticForceMag;    // |Fm|

    public CurrentMagneticData(
        Vector2 rodPos,
        Vector2 targetPos,
        float currentI,
        float fieldB,
        float distR,
        Vector2 forceVec)
    {
        RodPosition = rodPos;
        TargetPosition = targetPos;
        CurrentIntensityI = currentI;
        MagneticFieldB = fieldB;
        DistanceR = distR;
        MagneticForceVec = forceVec;
        MagneticForceMag = forceVec.magnitude;
    }
}
