using UnityEngine;
using UnityEngine.InputSystem;

public class ChargeResource : MonoBehaviour
{
    [Header("Charge")]
    [Range(-1f, 1f)]
    public float charge = 0f;
    public float dissipationRate = 0.01f;

    [Header("Bubble")]
    public bool bubbleActive = false;
    public float bubbleChargeCost = 0.2f;

    [Header("Discharge Penalty")]
    public float graceTime = 3f;
    public float dischargeDamageInterval = 2f;

    [Header("Visual")]
    public Color positiveColor = new Color(0.3f, 0.6f, 1f);
    public Color negativeColor = new Color(1f, 0.3f, 0.3f);
    public Color neutralColor = new Color(0.8f, 0.8f, 0.8f);

    private SpriteRenderer sr;
    private float graceTimer = 0f;
    private float damageTimer = 0f;
    private bool wasNeutral = false;
    private PlayerHealth playerHealth;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
        graceTimer = graceTime;
        damageTimer = dischargeDamageInterval;
        UpdateVisual();
    }

    void Update()
    {
        HandleBubble();
        DissipateCharge();
        HandleDischargePenalty();
        UpdateVisual();
    }

    void DissipateCharge()
    {
        if (bubbleActive) return;
        charge = Mathf.MoveTowards(charge, 0f, dissipationRate * Time.deltaTime);
    }

    void HandleBubble()
    {
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            if (!bubbleActive && Mathf.Abs(charge) > 0.1f)
                bubbleActive = true;
            else
                bubbleActive = false;
        }

        if (bubbleActive)
        {
            charge = Mathf.MoveTowards(charge, 0f, bubbleChargeCost * Time.deltaTime);
            if (Mathf.Abs(charge) < 0.05f)
                bubbleActive = false;
        }
    }

    void HandleDischargePenalty()
    {
        if (IsNeutral())
        {
            if (!wasNeutral)
            {
                graceTimer = graceTime;
                wasNeutral = true;
                damageTimer = dischargeDamageInterval;
            }

            if (graceTimer > 0f)
            {
                graceTimer -= Time.deltaTime;
                return;
            }

            // Grace expired
            damageTimer -= Time.deltaTime;
            if (damageTimer <= 0f)
            {
                playerHealth?.TakeDamage(1);
                damageTimer = dischargeDamageInterval;
            }
        }
        else
        {
            wasNeutral = false;
            graceTimer = graceTime;
            damageTimer = dischargeDamageInterval;
        }
    }

    void UpdateVisual()
    {
        if (sr == null) return;
        if (IsPositive())
            sr.color = Color.Lerp(neutralColor, positiveColor, Mathf.Abs(charge));
        else if (IsNegative())
            sr.color = Color.Lerp(neutralColor, negativeColor, Mathf.Abs(charge));
        else
            sr.color = neutralColor;
    }

    // Called by ActionFeedback to show grace timer
    public float GetGraceTimer() => graceTimer;
    public bool IsInGrace() => wasNeutral && graceTimer > 0f;

    public void AddCharge(float amount)
    {
        charge = Mathf.Clamp(charge + amount, -1f, 1f);
    }

    public float GetCharge() => charge;
    public bool IsPositive() => charge > 0.1f;
    public bool IsNegative() => charge < -0.1f;
    public bool IsNeutral() => Mathf.Abs(charge) <= 0.1f;
    public bool IsBubbleActive() => bubbleActive;
}