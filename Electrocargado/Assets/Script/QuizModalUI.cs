using UnityEngine;

/// <summary>
/// Capa 3 (Verificar): Observador desacoplado de eventos del modal de evaluación conceptual (QuizModalUI).
/// Escucha los eventos OnFirewallTriggered, OnQuizQuestionAnswered y OnFirewallEvaluationCompleted
/// de GameEvents.cs para registrar telemetría pedagógica y mostrar notificaciones de retroalimentación.
/// </summary>
[DisallowMultipleComponent]
public class QuizModalUI : MonoBehaviour
{
    [Header("Estado de Telemetría Pedagógica (Capa 3: Verificar)")]
    [Tooltip("Indica si el modal de evaluación del Firewall Gandalf se encuentra activo.")]
    [SerializeField]
    private bool isModalOpen;

    [Tooltip("Última retroalimentación matemática generada tras responder en el Firewall.")]
    [SerializeField, TextArea(2, 4)]
    private string lastPedagogicalFeedback;

    private int totalQuestionsAnsweredSession;
    private int totalCorrectAnswersSession;

    public bool IsModalOpen => isModalOpen;
    public string LastPedagogicalFeedback => lastPedagogicalFeedback;
    public int TotalQuestionsAnsweredSession => totalQuestionsAnsweredSession;
    public int TotalCorrectAnswersSession => totalCorrectAnswersSession;

    private void OnEnable()
    {
        GameEvents.OnFirewallTriggered += HandleFirewallTriggered;
        GameEvents.OnQuizQuestionAnswered += HandleQuizQuestionAnswered;
        GameEvents.OnFirewallEvaluationCompleted += HandleEvaluationCompleted;
    }

    private void OnDisable()
    {
        GameEvents.OnFirewallTriggered -= HandleFirewallTriggered;
        GameEvents.OnQuizQuestionAnswered -= HandleQuizQuestionAnswered;
        GameEvents.OnFirewallEvaluationCompleted -= HandleEvaluationCompleted;
    }

    private void HandleFirewallTriggered(bool active)
    {
        isModalOpen = active;
    }

    private void HandleQuizQuestionAnswered(int questionIndex, bool isCorrect, string feedbackMessage)
    {
        totalQuestionsAnsweredSession++;
        if (isCorrect)
        {
            totalCorrectAnswersSession++;
        }
        lastPedagogicalFeedback = feedbackMessage;
    }

    private void HandleEvaluationCompleted(bool passed)
    {
        isModalOpen = false;
    }
}
