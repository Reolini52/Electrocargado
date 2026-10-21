using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona de Gradiente de Potencial Eléctrico (V = ke * Q / r, E = -grad(V), Delta_K = -q * Delta_V).
/// Nivel 3: Muestra líneas equipotenciales visibles en el mapa desde la región de MAYOR potencial (+V, Azul)
/// hasta la región de MENOR potencial (-V, Rojo) y modifica la velocidad de desplazamiento de THEIA
/// según el signo de su carga q y el sentido en que recorre el gradiente de potencial.
/// </summary>
public class ElectricPotentialZone : MonoBehaviour
{
    [Header("Parámetros de Potencial Eléctrico (V)")]
    [Tooltip("Dirección en la que DISMINUYE el potencial (-grad V, de Mayor Potencial +V hacia Menor Potencial -V).")]
    public Vector2 highToLowPotentialDir = Vector2.right;

    [Tooltip("Diferencia de potencial Delta_V (Voltios equivalentes) a lo largo de la zona.")]
    [Range(5f, 60f)]
    public float potentialDifferenceVolts = 24f;

    [Tooltip("Intensidad del efecto sobre la velocidad del jugador al recorrer el gradiente.")]
    [Range(5f, 40f)]
    public float speedGradientForce = 18f;

    [Tooltip("Número de líneas equipotenciales visibles en la zona.")]
    [Range(4, 12)]
    public int equipotentialLineCount = 7;

    private readonly List<LineRenderer> equipotentialLines = new List<LineRenderer>();
    private GUIStyle labelStyle;

    private void Start()
    {
        // Ocultar el SpriteRenderer base si proviene de un prefab Field para mostrar el gradiente equipotencial limpio
        if (TryGetComponent(out SpriteRenderer sr))
        {
            sr.color = new Color(0.15f, 0.25f, 0.45f, 0.14f);
        }

        BuildEquipotentialVisuals();
    }

    public void BuildEquipotentialVisuals()
    {
        for (int i = 0; i < equipotentialLines.Count; i++)
        {
            if (equipotentialLines[i] != null)
                Destroy(equipotentialLines[i].gameObject);
        }
        equipotentialLines.Clear();

        Vector2 size = transform.localScale;
        Vector2 dir = highToLowPotentialDir.normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        float lengthAlongGrad = Mathf.Abs(dir.x) > 0.5f ? size.x : size.y;
        float spanPerp = Mathf.Abs(dir.x) > 0.5f ? size.y : size.x;

        Shader sprShader = Shader.Find("Sprites/Default");

        for (int i = 0; i < equipotentialLineCount; i++)
        {
            float t = equipotentialLineCount > 1 ? (float)i / (equipotentialLineCount - 1) : 0.5f;
            float offsetAlong = (t - 0.5f) * lengthAlongGrad * 0.88f;

            Vector3 lineCenter = transform.position + (Vector3)(dir * offsetAlong);

            GameObject lineObj = new GameObject($"EquipotentialLine_{i}");
            lineObj.transform.SetParent(transform, true);
            lineObj.transform.position = lineCenter;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.07f;
            lr.endWidth = 0.07f;
            lr.material = new Material(sprShader);
            lr.sortingOrder = 2;

            // Color interpolado desde Mayor Potencial (+V Azul) hasta Menor Potencial (-V Rojo)
            Color equipotentialColor = Color.Lerp(
                new Color(0.2f, 0.72f, 1f, 0.65f),
                new Color(1f, 0.28f, 0.32f, 0.65f),
                t
            );

            lr.startColor = equipotentialColor;
            lr.endColor = equipotentialColor;

            Vector3 p0 = lineCenter - (Vector3)(perp * (spanPerp * 0.46f));
            Vector3 p1 = lineCenter + (Vector3)(perp * (spanPerp * 0.46f));
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);

            equipotentialLines.Add(lr);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Vector2 gradDir = highToLowPotentialDir.normalized;

        // 1. Efecto sobre la velocidad de THEIA: F = q * E = -q * grad(V)
        if (other.TryGetComponent(out ChargeResource playerCharge) && other.TryGetComponent(out Rigidbody2D playerRb))
        {
            if (playerCharge.IsNeutral()) return;

            float q = playerCharge.GetCharge();
            // Una carga +q es acelerada de Mayor Potencial (+V) hacia Menor Potencial (-V).
            // Una carga -q es acelerada de Menor Potencial (-V) hacia Mayor Potencial (+V).
            Vector2 forceVec = gradDir * (q * speedGradientForce);
            playerRb.AddForce(forceVec, ForceMode2D.Force);
            return;
        }

        // 2. Efecto sobre Objetos Dinámicos (llaves): también sienten la pendiente de potencial
        if (other.TryGetComponent(out DynamicChargedObject dynObj) && other.TryGetComponent(out Rigidbody2D dynRb))
        {
            if (Mathf.Abs(dynObj.charge) < 0.05f) return;
            Vector2 forceVec = gradDir * (dynObj.charge * speedGradientForce * 0.5f);
            dynRb.AddForce(forceVec, ForceMode2D.Force);
        }
    }

    private void OnGUI()
    {
        if (GameEvents.IsCleanHudHidden || Camera.main == null) return;

        Vector2 dir = highToLowPotentialDir.normalized;
        float halfSpan = (Mathf.Abs(dir.x) > 0.5f ? transform.localScale.x : transform.localScale.y) * 0.42f;

        Vector3 highVWorld = transform.position - (Vector3)(dir * halfSpan) + Vector3.up * (transform.localScale.y * 0.42f);
        Vector3 lowVWorld = transform.position + (Vector3)(dir * halfSpan) + Vector3.up * (transform.localScale.y * 0.42f);

        Vector3 highScreen = Camera.main.WorldToScreenPoint(highVWorld);
        Vector3 lowScreen = Camera.main.WorldToScreenPoint(lowVWorld);

        if (highScreen.z <= 0 || lowScreen.z <= 0) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        highScreen.y = Screen.height - highScreen.y;
        lowScreen.y = Screen.height - lowScreen.y;

        if (highScreen.x >= -60 && highScreen.x <= Screen.width + 60)
        {
            GUI.color = new Color(0.35f, 0.82f, 1f, 0.92f);
            GUI.Label(new Rect(highScreen.x - 65f, highScreen.y - 12f, 130f, 22f), $"+{potentialDifferenceVolts:F0}V (Alto Potencial)", labelStyle);
        }

        if (lowScreen.x >= -60 && lowScreen.x <= Screen.width + 60)
        {
            GUI.color = new Color(1f, 0.42f, 0.42f, 0.92f);
            GUI.Label(new Rect(lowScreen.x - 65f, lowScreen.y - 12f, 130f, 22f), $"-{potentialDifferenceVolts:F0}V (Bajo Potencial)", labelStyle);
        }
    }
}
