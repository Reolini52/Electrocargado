using System;
using UnityEngine;

/// <summary>
/// Terminal de Placa del Capacitor (⊕ Placa Positiva / ⊖ Placa Negativa) en la arena de POLAROUS.
/// A diferencia de un checkpoint secuencial, funciona como un verdadero terminal eléctrico:
/// puede cargarse en cualquier orden mediante:
/// 1. Conducción directa (THEIA pisa el terminal con la carga del mismo signo).
/// 2. Inyección a distancia (Modo [Q/E] + Click Derecho apuntando al terminal).
/// 3. Objeto cargado móvil (Empujar un DynamicChargedObject con Ley de Coulomb hacia el terminal).
/// </summary>
public class CapacitorPlateNode : MonoBehaviour
{
    [Header("Parámetros Físicos del Terminal de Placa")]
    [Tooltip("Índice identificador del terminal.")]
    public int sequenceIndex = 0;

    [Tooltip("Polaridad requerida de la placa: +1 para Placa Positiva (⊕ Azul), -1 para Placa Negativa (⊖ Roja).")]
    public float requiredPolarity = 1f;

    [Tooltip("Si este terminal forma parte del circuito en la fase actual.")]
    public bool isActiveInCurrentPhase = true;

    [Tooltip("Nivel de carga almacenada en esta placa (0 a 1). Al llegar a 1 (100%), el terminal queda cargado.")]
    [Range(0f, 1f)]
    public float chargeLevel = 0f;

    [Tooltip("Punto del bus conductor central (CurrentRod) al que se conecta el cable de este terminal.")]
    public Vector3 circuitBusAnchor = new Vector3(0f, 0.8f, 0f);

    private bool isCharged = false;
    private float wrongPolarityWarningTimer = 0f;
    private SpriteRenderer sr;
    private LineRenderer pulseRing;
    private LineRenderer circuitWire;

    public event Action<CapacitorPlateNode> OnNodeChargedByPlayer;

    public bool IsCharged => isCharged;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        CreatePulseRing();
        CreateCircuitWire();
    }

    private void CreatePulseRing()
    {
        GameObject ringObj = new GameObject("TerminalRing");
        ringObj.transform.SetParent(transform);
        ringObj.transform.localPosition = Vector3.zero;

        pulseRing = ringObj.AddComponent<LineRenderer>();
        pulseRing.useWorldSpace = true;
        pulseRing.loop = true;
        pulseRing.startWidth = 0.06f;
        pulseRing.endWidth = 0.06f;
        pulseRing.material = new Material(Shader.Find("Sprites/Default"));
        pulseRing.sortingOrder = 5;
        pulseRing.positionCount = 24;
    }

    private void CreateCircuitWire()
    {
        GameObject wireObj = new GameObject("CircuitWire");
        wireObj.transform.SetParent(transform);
        wireObj.transform.localPosition = Vector3.zero;

        circuitWire = wireObj.AddComponent<LineRenderer>();
        circuitWire.useWorldSpace = true;
        circuitWire.positionCount = 3;
        circuitWire.startWidth = 0.05f;
        circuitWire.endWidth = 0.05f;
        circuitWire.material = new Material(Shader.Find("Sprites/Default"));
        circuitWire.sortingOrder = 1;
    }

    public void ConfigureNode(int index, float polarity, bool active, Vector3 busAnchorPos = default)
    {
        sequenceIndex = index;
        requiredPolarity = polarity >= 0f ? 1f : -1f;
        isActiveInCurrentPhase = active;
        isCharged = false;
        chargeLevel = 0f;
        wrongPolarityWarningTimer = 0f;

        if (busAnchorPos != default)
            circuitBusAnchor = busAnchorPos;

        gameObject.SetActive(active);
        UpdateVisuals();
    }

    public void SetAsNextTarget(bool isNext)
    {
        UpdateVisuals();
    }

    public void ResetNodeCharge()
    {
        isCharged = false;
        chargeLevel = 0f;
        UpdateVisuals();
    }

    /// <summary>
    /// Permite inyectar carga a distancia con [Q/E] + Click Derecho (ChargeAbsorber).
    /// </summary>
    public bool TryInjectCharge(float deltaCharge)
    {
        if (!isActiveInCurrentPhase || isCharged) return false;

        bool matchesPolarity = (requiredPolarity > 0f && deltaCharge > 0f) ||
                               (requiredPolarity < 0f && deltaCharge < 0f);

        if (matchesPolarity)
        {
            AddChargeProgress(Mathf.Abs(deltaCharge) * 2.4f);
            return true;
        }
        else
        {
            wrongPolarityWarningTimer = 1.2f;
            return false;
        }
    }

    private void AddChargeProgress(float amount)
    {
        if (isCharged) return;

        chargeLevel = Mathf.Clamp01(chargeLevel + amount);
        UpdateVisuals();

        if (chargeLevel >= 0.99f)
        {
            chargeLevel = 1f;
            isCharged = true;
            UpdateVisuals();
            OnNodeChargedByPlayer?.Invoke(this);
        }
    }

    private void Update()
    {
        if (!isActiveInCurrentPhase) return;

        if (wrongPolarityWarningTimer > 0f)
            wrongPolarityWarningTimer -= Time.deltaTime;

        if (pulseRing != null)
        {
            float baseRadius = isCharged ? 0.72f : (0.78f + Mathf.Sin(Time.time * 5f) * 0.10f);
            int count = pulseRing.positionCount;

            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f;
                pulseRing.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * baseRadius, Mathf.Sin(angle) * baseRadius, 0f));
            }

            Color ringCol = GetPolarityColor(0.9f);
            if (isCharged)
                ringCol = new Color(0.2f, 1f, 0.45f, 0.95f);
            else if (wrongPolarityWarningTimer > 0f)
                ringCol = new Color(1f, 0.85f, 0.1f, 0.95f);

            pulseRing.startColor = ringCol;
            pulseRing.endColor = ringCol;
        }

        if (circuitWire != null)
        {
            Vector3 p0 = transform.position;
            Vector3 p2 = circuitBusAnchor;
            Vector3 p1 = new Vector3(p0.x, p2.y, 0f); // Cable ortogonal estilo circuito impreso (PCB)

            circuitWire.SetPosition(0, p0);
            circuitWire.SetPosition(1, p1);
            circuitWire.SetPosition(2, p2);

            Color wireCol = isCharged
                ? new Color(0.25f, 1f, 0.55f, 0.75f)
                : GetPolarityColor(0.28f);
            circuitWire.startColor = wireCol;
            circuitWire.endColor = wireCol;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActiveInCurrentPhase || isCharged) return;

        // 1. Transferencia desde Objeto Cargado Dinámico (empujado con Ley de Coulomb)
        DynamicChargedObject dynObj = other.GetComponent<DynamicChargedObject>();
        if (dynObj != null && Mathf.Abs(dynObj.charge) > 0.1f)
        {
            bool dynMatches = (requiredPolarity > 0f && dynObj.charge > 0.1f) ||
                              (requiredPolarity < 0f && dynObj.charge < -0.1f);
            if (dynMatches)
            {
                AddChargeProgress(Time.deltaTime * 2.2f);
                return;
            }
        }

        // 2. Conducción directa cuando THEIA pisa el terminal con la carga requerida
        ChargeResource playerCharge = other.GetComponent<ChargeResource>();
        if (playerCharge == null) return;

        float qPlayer = playerCharge.GetCharge();
        if (Mathf.Abs(qPlayer) <= 0.12f) return;

        bool matchesPolarity = (requiredPolarity > 0f && qPlayer > 0.12f) ||
                               (requiredPolarity < 0f && qPlayer < -0.12f);

        if (matchesPolarity)
        {
            AddChargeProgress(Time.deltaTime * 2.5f);
        }
        else
        {
            wrongPolarityWarningTimer = 0.8f;
        }
    }

    private Color GetPolarityColor(float alpha)
    {
        return requiredPolarity > 0f
            ? new Color(0.25f, 0.68f, 1f, alpha)
            : new Color(1f, 0.32f, 0.32f, alpha);
    }

    private void UpdateVisuals()
    {
        if (sr == null) return;

        if (isCharged)
        {
            sr.color = new Color(0.2f, 1f, 0.45f, 1f);
        }
        else
        {
            sr.color = GetPolarityColor(0.85f);
        }
    }

    private void OnGUI()
    {
        if (!isActiveInCurrentPhase || GameEvents.IsCleanHudHidden || Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z <= 0) return;
        screenPos.y = Screen.height - screenPos.y;

        GUIStyle symbolStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        string symbol = requiredPolarity > 0f ? "⊕" : "⊖";
        string signText = requiredPolarity > 0f ? "+Q" : "-Q";

        if (isCharged)
        {
            GUI.color = new Color(0.25f, 1f, 0.5f);
            GUI.Label(new Rect(screenPos.x - 45f, screenPos.y - 36f, 90f, 22f), $"{symbol} CARGADA ✓", subStyle);
        }
        else if (wrongPolarityWarningTimer > 0f)
        {
            GUI.color = new Color(1f, 0.85f, 0.2f);
            GUI.Label(new Rect(screenPos.x - 55f, screenPos.y - 36f, 110f, 22f), $"¡Pide {symbol} ({signText})!", subStyle);
        }
        else
        {
            GUI.color = requiredPolarity > 0f ? new Color(0.35f, 0.78f, 1f) : new Color(1f, 0.4f, 0.4f);
            int pct = Mathf.RoundToInt(chargeLevel * 100f);
            string label = pct > 0 ? $"{symbol} {signText} ({pct}%)" : $"Placa {symbol} ({signText})";
            GUI.Label(new Rect(screenPos.x - 50f, screenPos.y - 36f, 100f, 22f), label, subStyle);
        }
    }
}
