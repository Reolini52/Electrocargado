using UnityEngine;

/// <summary>
/// Fase 3 — Estabilidad Física.
/// Utilidades matemáticas puras (estáticas, sin estado, cero GC) para calcular fuerzas
/// electromagnéticas "Game Feel first": se prioriza un comportamiento predecible y jugable
/// sobre la precisión física exacta.
///
/// Todas las fórmulas aplican dos protecciones:
///  1. Clamp del divisor r² (r ∈ [rMin, rMax]) → nunca hay división por cero ni fuerzas → ∞.
///  2. Vector2.ClampMagnitude sobre el vector final → nunca se supera |F_max|.
/// </summary>
public static class ElectroPhysicsUtility
{
    /// <summary>Distancia por debajo de la cual la dirección r̂ se considera indefinida.</summary>
    public const float DirectionEpsilon = 0.0001f;

    /// <summary>
    /// Devuelve el divisor r² seguro: r se acota con Mathf.Clamp a [rMin, rMax].
    /// rMin evita la singularidad 1/r² → ∞ cuando dos cargas se solapan.
    /// rMax (opcional) evita que r² crezca sin necesidad (útil si se usa fuera del radio de corte).
    /// </summary>
    public static float SafeSqrDistance(float rawDistance, float rMin, float rMax = float.MaxValue)
    {
        float r = Mathf.Clamp(rawDistance, Mathf.Max(rMin, DirectionEpsilon), Mathf.Max(rMax, rMin));
        return r * r;
    }

    /// <summary>
    /// Ley de Coulomb vectorial con clamping completo:
    /// F = k · (q_source · q_target) / clamp(r)² · r̂   (r̂ apunta de la fuente al objetivo)
    /// - Mismo signo  → repulsión (+r̂).
    /// - Signo opuesto → atracción (−r̂).
    /// </summary>
    /// <param name="sourcePos">Posición de la carga que ejerce la fuerza.</param>
    /// <param name="targetPos">Posición de la carga que recibe la fuerza.</param>
    /// <param name="ke">Constante electrostática efectiva (escala de juego).</param>
    /// <param name="qSource">Carga de la fuente.</param>
    /// <param name="qTarget">Carga del objetivo.</param>
    /// <param name="rMin">Distancia mínima para el divisor r².</param>
    /// <param name="maxForce">Magnitud máxima permitida del vector resultante.</param>
    /// <param name="clampedDistance">Distancia efectivamente usada (para telemetría).</param>
    public static Vector2 CoulombForce(
        Vector2 sourcePos, Vector2 targetPos,
        float ke, float qSource, float qTarget,
        float rMin, float maxForce,
        out float clampedDistance)
    {
        Vector2 displacement = targetPos - sourcePos;
        float rawDistance = displacement.magnitude;

        // Dirección r̂ segura: si las cargas están exactamente superpuestas usamos un vector arbitrario.
        Vector2 rHat = rawDistance > DirectionEpsilon ? displacement / rawDistance : Vector2.up;

        float sqrR = SafeSqrDistance(rawDistance, rMin);
        clampedDistance = Mathf.Sqrt(sqrR);

        float signedScalar = ke * (qSource * qTarget) / sqrR;
        return Vector2.ClampMagnitude(rHat * signedScalar, maxForce);
    }

    /// <summary>
    /// Aproximación 2D jugable de la Fuerza de Lorentz F = q·(v × B):
    /// la fuerza es perpendicular a la velocidad, proporcional a |v| y acotada a maxForce.
    /// </summary>
    /// <param name="velocity">Velocidad de la carga en movimiento.</param>
    /// <param name="chargeProduct">Producto de cargas (o q·B efectivo) con signo.</param>
    /// <param name="strength">Multiplicador de diseño.</param>
    /// <param name="maxSpeedConsidered">Techo de |v| usado en el cálculo (evita picos por dashes/knockbacks).</param>
    /// <param name="maxForce">Magnitud máxima permitida del vector resultante.</param>
    public static Vector2 LorentzForce(
        Vector2 velocity, float chargeProduct, float strength,
        float maxSpeedConsidered, float maxForce)
    {
        float speed = velocity.magnitude;
        if (speed <= DirectionEpsilon) return Vector2.zero;

        Vector2 vHat = velocity / speed;
        Vector2 perpendicular = new Vector2(-vHat.y, vHat.x); // v × ẑ en 2D

        float clampedSpeed = Mathf.Min(speed, maxSpeedConsidered);
        float scalar = chargeProduct * clampedSpeed * strength;
        return Vector2.ClampMagnitude(perpendicular * scalar, maxForce);
    }

    /// <summary>
    /// Limita la velocidad lineal de un Rigidbody2D (red de seguridad final contra "salir volando").
    /// </summary>
    public static void ClampLinearVelocity(Rigidbody2D rb, float maxSpeed)
    {
        if (rb == null) return;
        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = v.normalized * maxSpeed;
    }

    /// <summary>
    /// Convierte de forma segura una posición de pantalla 2D (p. ej. Mouse.current.position.ReadValue())
    /// a coordenadas de mundo 2D, evitando el error de Unity:
    /// "Screen position out of view frustum (screen pos ..., 0.000000) (Camera rect ...)".
    /// Funciona tanto con cámaras Orthographic como Perspective y acota X/Y al pixelRect de la cámara.
    /// </summary>
    public static bool TryScreenToWorldPoint2D(Camera cam, Vector2 rawScreenPos, out Vector2 worldPoint)
    {
        worldPoint = Vector2.zero;
        if (cam == null) return false;

        Rect pixelRect = cam.pixelRect;
        if (pixelRect.width <= 1f || pixelRect.height <= 1f) return false;
        if (float.IsNaN(rawScreenPos.x) || float.IsNaN(rawScreenPos.y) ||
            float.IsInfinity(rawScreenPos.x) || float.IsInfinity(rawScreenPos.y))
        {
            return false;
        }

        // Acotar X/Y dentro del rectángulo de renderizado de la cámara (con margen de 0.5px)
        float clampedX = Mathf.Clamp(rawScreenPos.x, pixelRect.xMin + 0.5f, pixelRect.xMax - 0.5f);
        float clampedY = Mathf.Clamp(rawScreenPos.y, pixelRect.yMin + 0.5f, pixelRect.yMax - 0.5f);

        // Si la cámara es Perspective, z = 0 está detrás del nearClipPlane y lanza "Screen position out of view frustum".
        // Usamos la distancia desde la cámara hasta el plano z = 0 del mundo (acotada entre nearClipPlane y farClipPlane).
        float zDist = cam.orthographic
            ? Mathf.Max(cam.nearClipPlane + 0.01f, -cam.transform.position.z)
            : Mathf.Clamp(Mathf.Abs(cam.transform.position.z), cam.nearClipPlane + 0.01f, cam.farClipPlane - 0.01f);

        Vector3 w = cam.ScreenToWorldPoint(new Vector3(clampedX, clampedY, zDist));
        worldPoint = new Vector2(w.x, w.y);
        return true;
    }
}
