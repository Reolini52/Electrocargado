using System;
using UnityEngine;

/// <summary>
/// Level 0 Tutorial ("Boot Sequence") — ScriptableObject & Data Preset Container.
/// Contiene todas las estructuras serializadas exactas para:
///  1. Las 5 burbujas flotantes (DialogueSystem) estilo arcade de alta visibilidad.
///  2. El cartel/póster (Billboard) de Conservación de la Carga ("Q_total = constant").
///  3. El chequeo diagnóstico de 3 preguntas de GandalfNPC al final del tutorial.
///
/// Puedes crear un asset desde el menú: Create > Electrocargado > Level 0 Tutorial Data,
/// o usar los métodos estáticos / ContextMenu en cualquier escena.
/// </summary>
[CreateAssetMenu(fileName = "TutorialBootSequenceData", menuName = "Electrocargado/Level 0 Tutorial Data", order = 0)]
public class TutorialBootSequenceData : ScriptableObject
{
    [Serializable]
    public struct DialogueBubblePreset
    {
        public string bubbleId;
        public string speakerTitle;
        [TextArea(1, 3)]
        public string instructionText;
        public float triggerRadius;
        public Color accentColor;
    }

    [Serializable]
    public struct BillboardPreset
    {
        public string topic;
        public string formula;
        [TextArea(2, 3)]
        public string keyTakeaway;
        public float interactRadius;
    }

    [Header("1. Dialogue Bubbles (Ultra-short, Arcade Style)")]
    public DialogueBubblePreset[] dialogueBubbles = GetDefaultBubbles();

    [Header("2. Tutorial Billboard Data")]
    public BillboardPreset billboardData = GetDefaultBillboard();

    [Header("3. Gandalf Diagnostic Check (3 Questions)")]
    public TrialQuestion[] gandalfQuestions = GandalfNPC.GetTutorialBootSequenceQuestions();

    /// <summary>
    /// Devuelve los 5 presets oficiales de burbujas del Level 0 ("Boot Sequence").
    /// </summary>
    public static DialogueBubblePreset[] GetDefaultBubbles()
    {
        return new DialogueBubblePreset[]
        {
            new DialogueBubblePreset
            {
                bubbleId = "BURBUJA_MOVIMIENTO",
                speakerTitle = "INICIO DE SISTEMA",
                instructionText = "INICIO DE SISTEMA: Usa [WASD] para Moverte. [Espacio] para Saltar.",
                triggerRadius = 3.5f,
                accentColor = new Color(0.3f, 0.85f, 1f, 1f)
            },
            new DialogueBubblePreset
            {
                bubbleId = "BURBUJA_ABSORBER",
                speakerTitle = "PÉRDIDA DE CARGA",
                instructionText = "PÉRDIDA DE CARGA: Tu energía se disipa. Mantén [Q] + Clic a un nodo para absorber.",
                triggerRadius = 3.5f,
                accentColor = new Color(1f, 0.78f, 0.25f, 1f)
            },
            new DialogueBubblePreset
            {
                bubbleId = "BURBUJA_DOBLE_SALTO",
                speakerTitle = "IMPULSO CAPACITOR",
                instructionText = "IMPULSO CAPACITOR: El doble salto requiere carga. Mira el color de tu cabello y la barra.",
                triggerRadius = 3.5f,
                accentColor = new Color(0.4f, 1f, 0.55f, 1f)
            },
            new DialogueBubblePreset
            {
                bubbleId = "BURBUJA_POLARIDAD",
                speakerTitle = "REGLA DE COULOMB",
                instructionText = "REGLA DE COULOMB: Iguales se repelen, opuestos se atraen. Cuidado con campos y proyectiles.",
                triggerRadius = 3.5f,
                accentColor = new Color(1f, 0.35f, 0.45f, 1f)
            },
            new DialogueBubblePreset
            {
                bubbleId = "BURBUJA_PUERTA",
                speakerTitle = "CIRCUITO DE SALIDA",
                instructionText = "CIRCUITO DE SALIDA: Transfiere tu carga a la terminal para desbloquear la compuerta.",
                triggerRadius = 3.5f,
                accentColor = new Color(0.75f, 0.45f, 1f, 1f)
            }
        };
    }

    /// <summary>
    /// Devuelve los datos exactos del Billboard del Level 0 ("Conservation of Charge").
    /// </summary>
    public static BillboardPreset GetDefaultBillboard()
    {
        return new BillboardPreset
        {
            topic = "CONSERVATION OF CHARGE (Charging by Conduction)",
            formula = "Q_total = constant",
            keyTakeaway = "Charge cannot be created or destroyed, only transferred. Keep THEIA energized.",
            interactRadius = 3.5f
        };
    }
}
