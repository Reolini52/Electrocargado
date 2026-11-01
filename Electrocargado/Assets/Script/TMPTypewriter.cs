using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Fase 3 — Pulido UI. Efecto "máquina de escribir" (Typewriter) para cualquier TMP_Text.
///
/// • Usa TMP_Text.maxVisibleCharacters → compatible con rich text (&lt;color&gt;, &lt;b&gt;, &lt;sprite&gt;...),
///   no reconstruye strings carácter a carácter (cero GC por frame).
/// • Funciona con Time.timeScale = 0 (el Firewall Gandalf pausa el juego) usando tiempo no escalado.
/// • Pausas automáticas en signos de puntuación para un ritmo de lectura natural.
/// • Opción de fuente personalizada (TMP_FontAsset) asignable desde el Inspector.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class TMPTypewriter : MonoBehaviour
{
    [Header("Velocidad")]
    [Tooltip("Caracteres revelados por segundo.")]
    [SerializeField, Range(5f, 300f)] private float charactersPerSecond = 55f;

    [Tooltip("Multiplicador de pausa tras . , ; : ! ? (1 = sin pausa extra).")]
    [SerializeField, Range(1f, 12f)] private float punctuationPauseMultiplier = 5f;

    [Tooltip("Usar tiempo no escalado (necesario si el juego se pausa con Time.timeScale = 0).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Tipografía (opcional)")]
    [Tooltip("Fuente TMP personalizada. Si se deja vacío se conserva la del componente.")]
    [SerializeField] private TMP_FontAsset fontOverride;

    [Header("Audio (opcional)")]
    [Tooltip("Clip corto (blip) reproducido cada N caracteres visibles.")]
    [SerializeField] private AudioSource blipSource;
    [SerializeField, Range(1, 10)] private int blipEveryNCharacters = 2;

    /// <summary>Se invoca cuando el texto queda completamente visible (por tiempo o por Skip).</summary>
    public event Action OnTypingFinished;

    private TMP_Text textComponent;
    private int totalCharacters;
    private float revealProgress; // caracteres "acumulados" (fraccionario)
    private int lastVisibleCount;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public TMP_Text Text => textComponent != null ? textComponent : (textComponent = GetComponent<TMP_Text>());

    /// <summary>Velocidad modificable en runtime (p. ej. acelerar mientras se mantiene un botón).</summary>
    public float CharactersPerSecond
    {
        get => charactersPerSecond;
        set => charactersPerSecond = Mathf.Max(1f, value);
    }

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        ApplyFontOverride();
    }

    private void OnValidate()
    {
        if (textComponent == null) textComponent = GetComponent<TMP_Text>();
        ApplyFontOverride();
    }

    /// <summary>Asigna una fuente TMP personalizada en runtime.</summary>
    public void SetFont(TMP_FontAsset font)
    {
        fontOverride = font;
        ApplyFontOverride();
    }

    private void ApplyFontOverride()
    {
        if (fontOverride != null && textComponent != null && textComponent.font != fontOverride)
            textComponent.font = fontOverride;
    }

    /// <summary>
    /// Muestra un texto nuevo. Si instant = true aparece completo de golpe.
    /// </summary>
    public void Play(string content, bool instant = false)
    {
        TMP_Text t = Text;
        t.text = content ?? string.Empty;

        // Necesario para conocer el nº real de caracteres visibles (excluye etiquetas de rich text).
        t.ForceMeshUpdate();
        totalCharacters = t.textInfo.characterCount;

        revealProgress = 0f;
        lastVisibleCount = 0;

        if (instant || totalCharacters == 0)
        {
            Complete();
            return;
        }

        t.maxVisibleCharacters = 0;
        isPlaying = true;
    }

    /// <summary>Revela el texto completo inmediatamente.</summary>
    public void Skip()
    {
        if (isPlaying) Complete();
    }

    /// <summary>Oculta todo el texto (útil antes de un fade-in).</summary>
    public void Clear()
    {
        isPlaying = false;
        Text.text = string.Empty;
        Text.maxVisibleCharacters = 0;
        totalCharacters = 0;
    }

    private void Complete()
    {
        isPlaying = false;
        Text.maxVisibleCharacters = 99999; // Valor alto = "todo visible" (evita depender de characterCount).
        OnTypingFinished?.Invoke();
    }

    private void Update()
    {
        if (!isPlaying) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Pausa extra si el último carácter revelado es puntuación.
        float speed = charactersPerSecond;
        if (lastVisibleCount > 0 && lastVisibleCount <= totalCharacters)
        {
            char last = textComponent.textInfo.characterInfo[lastVisibleCount - 1].character;
            if (IsPausePunctuation(last)) speed /= punctuationPauseMultiplier;
        }

        revealProgress += speed * dt;
        int visible = Mathf.Min(totalCharacters, Mathf.FloorToInt(revealProgress));

        if (visible != lastVisibleCount)
        {
            // Blip de audio opcional (cada N caracteres no-espacio).
            if (blipSource != null && visible % blipEveryNCharacters == 0)
            {
                char c = textComponent.textInfo.characterInfo[visible - 1].character;
                if (!char.IsWhiteSpace(c)) blipSource.Play();
            }

            lastVisibleCount = visible;
            textComponent.maxVisibleCharacters = visible;
        }

        if (visible >= totalCharacters) Complete();
    }

    private static bool IsPausePunctuation(char c)
    {
        return c == '.' || c == ',' || c == ';' || c == ':' || c == '!' || c == '?' || c == '\n';
    }
}
