using System;
using UnityEngine;

/// <summary>
/// Identificadores de estado de POLAROUS. El orden NO importa (se usa como índice de array).
/// </summary>
public enum PolarousStateId
{
    Dormant = 0,          // Esperando a que THEIA entre en la arena.
    PositivePhase = 1,    // Fase 1: Carga Positiva (+Q) — ATRAE a THEIA.
    NegativePhase = 2,    // Fase 2: Carga Negativa (−Q) — REPELE a THEIA.
    AlternatingPhase = 3, // Fase 3: Alterna +Q / −Q cada N segundos.
    Stunned = 4,          // Sobrecarga / cortocircuito (ventana de daño).
    Defeated = 5          // Derrotado.
}

/// <summary>
/// Cómo se calcula la fuerza del campo de POLAROUS sobre THEIA.
/// </summary>
public enum PolarityFieldMode
{
    /// <summary>Coulomb real: el resultado depende del signo de la carga de THEIA.</summary>
    PhysicalCoulomb,
    /// <summary>Diseño: siempre atrae (independiente del signo de THEIA). Lectura clara para el jugador.</summary>
    AlwaysAttract,
    /// <summary>Diseño: siempre repele (independiente del signo de THEIA).</summary>
    AlwaysRepel
}

/// <summary>
/// Fase 3 — ESQUELETO del Jefe Final POLAROUS basado en una Máquina de Estados finita (FSM) por polaridad.
///
/// ARQUITECTURA
/// • Cada estado es una clase anidada (acceso directo a los campos privados del jefe, sin exponer API pública).
/// • Los estados se pre-instancian en Awake en un array indexado por <see cref="PolarousStateId"/> → cero GC en runtime.
/// • Ciclo de vida de un estado: Enter() → Tick(dt) [Update] / FixedTick(dt) [FixedUpdate] → Exit().
/// • Las transiciones de fase se disparan por umbrales de vida en <see cref="TakeDamage"/>.
/// • La UI/VFX/audio se suscriben a eventos C# (OnStateChanged, OnPolarityChanged, OnHealthChanged...) sin acoplarse.
///
/// QUÉ RELLENAR (marcado con "TODO ATAQUE"):
///   PerformPositivePhaseAttack(), PerformNegativePhaseAttack(), PerformAlternatingPhaseAttack(),
///   y opcionalmente los hooks OnPhaseEntered / OnStunStarted / OnDefeatedHook.
///
/// COEXISTENCIA
/// Este componente es INDEPENDIENTE del <see cref="PolarousBossController"/> existente (que sigue funcionando
/// en la escena del jefe). No pongas ambos en el mismo GameObject. Firmas de daño compatibles:
/// TakeMeleeDamage(int) y TakeReflectedProjectileDamage(int), para migrar MeleeHitbox/EmisorProjectile fácilmente.
/// </summary>
[DisallowMultipleComponent]
public class PolarousBoss : MonoBehaviour
{
    #region Inspector — Salud y Umbrales de Fase

    [Header("Salud")]
    [SerializeField, Min(1)] private int maxHealth = 9;

    [Tooltip("Al bajar la vida a este porcentaje (o menos) se pasa a Fase 2 (Negativa).")]
    [SerializeField, Range(0.05f, 0.95f)] private float phase2HealthRatio = 0.66f;

    [Tooltip("Al bajar la vida a este porcentaje (o menos) se pasa a Fase 3 (Alternante).")]
    [SerializeField, Range(0.05f, 0.95f)] private float phase3HealthRatio = 0.33f;

    [Tooltip("Invulnerabilidad breve tras recibir daño (evita multi-hits en un mismo frame).")]
    [SerializeField, Range(0f, 2f)] private float damageCooldown = 0.35f;

    #endregion

    #region Inspector — Campo de Polaridad (Coulomb con clamping)

    [Header("Campo de Polaridad sobre THEIA")]
    [Tooltip("Magnitud de la carga del núcleo |Q|. El signo lo decide la fase actual.")]
    [SerializeField, Range(0.1f, 5f)] private float coreChargeMagnitude = 1.4f;

    [Tooltip("Constante k_e efectiva del campo del jefe.")]
    [SerializeField, Range(5f, 200f)] private float fieldKe = 50f;

    [Tooltip("Distancia mínima del divisor r² (evita fuerzas infinitas al tocar el núcleo).")]
    [SerializeField, Range(0.3f, 3f)] private float fieldMinDistance = 1.1f;

    [Tooltip("Radio máximo de influencia del campo.")]
    [SerializeField, Range(2f, 30f)] private float fieldRadius = 12f;

    [Tooltip("Fuerza máxima del campo sobre THEIA (Vector2.ClampMagnitude).")]
    [SerializeField, Range(1f, 80f)] private float fieldMaxForce = 22f;

    [Tooltip("Carga mínima asumida para THEIA en modos de diseño (AlwaysAttract/AlwaysRepel) aunque esté neutra.")]
    [SerializeField, Range(0f, 2f)] private float designMinPlayerCharge = 0.5f;

    [Header("Modo de Campo por Fase")]
    [SerializeField] private PolarityFieldMode positivePhaseFieldMode = PolarityFieldMode.AlwaysAttract;
    [SerializeField] private PolarityFieldMode negativePhaseFieldMode = PolarityFieldMode.AlwaysRepel;
    [SerializeField] private PolarityFieldMode alternatingPhaseFieldMode = PolarityFieldMode.PhysicalCoulomb;

    #endregion

    #region Inspector — Ritmo de Combate

    [Header("Cadencia de Ataques (segundos)")]
    [SerializeField, Range(0.3f, 10f)] private float positivePhaseAttackInterval = 2.5f;
    [SerializeField, Range(0.3f, 10f)] private float negativePhaseAttackInterval = 2.0f;
    [SerializeField, Range(0.3f, 10f)] private float alternatingPhaseAttackInterval = 1.6f;

    [Header("Fase 3 — Alternancia")]
    [Tooltip("Segundos entre cada inversión de polaridad en la Fase Alternante.")]
    [SerializeField, Range(0.5f, 15f)] private float alternatingSwitchInterval = 4f;

    [Tooltip("Tiempo de aviso (telegraph) antes de invertir la polaridad. Úsalo para VFX/SFX de anticipación.")]
    [SerializeField, Range(0f, 3f)] private float alternatingTelegraphTime = 0.75f;

    [Header("Aturdimiento (Stun)")]
    [SerializeField, Range(0.5f, 10f)] private float defaultStunDuration = 3.5f;

    [Tooltip("Multiplicador de daño recibido mientras está aturdido.")]
    [SerializeField, Range(1f, 5f)] private float stunnedDamageMultiplier = 2f;

    [Header("Arranque")]
    [Tooltip("Si es true, el combate empieza en Start. Si es false, llama a StartFight() (p. ej. desde un trigger de arena).")]
    [SerializeField] private bool autoStartFight = true;

    [Header("Debug (solo lectura)")]
    [SerializeField] private PolarousStateId debugCurrentState;
    [SerializeField] private int debugCurrentHealth;
    [SerializeField] private float debugPolarity;

    #endregion

    #region Eventos públicos (UI, VFX, Audio, Cámara)

    /// <summary>(estadoAnterior, estadoNuevo)</summary>
    public event Action<PolarousStateId, PolarousStateId> OnStateChanged;
    /// <summary>Polaridad nueva: +1 o −1.</summary>
    public event Action<float> OnPolarityChanged;
    /// <summary>Aviso previo a una inversión de polaridad (segundos que faltan).</summary>
    public event Action<float> OnPolaritySwitchTelegraph;
    /// <summary>(vidaActual, vidaMáxima)</summary>
    public event Action<int, int> OnHealthChanged;
    public event Action OnDefeated;

    #endregion

    #region Estado runtime y propiedades de solo lectura

    private BossState[] states;
    private BossState currentState;
    private PolarousStateId currentStateId = PolarousStateId.Dormant;

    private int currentHealth;
    private float polarity = 1f;      // +1 (positivo) / −1 (negativo)
    private float damageCooldownTimer;

    // Referencias cacheadas (cero Find/GetComponent en Update)
    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private ChargeResource playerCharge;

    public PolarousStateId CurrentStateId => currentStateId;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float Polarity => polarity;
    public bool IsStunned => currentStateId == PolarousStateId.Stunned;
    public bool IsDefeated => currentStateId == PolarousStateId.Defeated;
    public float HealthRatio => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    #endregion

    #region Ciclo de vida Unity

    private void Awake()
    {
        currentHealth = maxHealth;

        // Pre-instanciar todos los estados una sola vez (sin GC durante el combate).
        states = new BossState[Enum.GetValues(typeof(PolarousStateId)).Length];
        states[(int)PolarousStateId.Dormant] = new DormantState(this);
        states[(int)PolarousStateId.PositivePhase] = new PositivePhaseState(this);
        states[(int)PolarousStateId.NegativePhase] = new NegativePhaseState(this);
        states[(int)PolarousStateId.AlternatingPhase] = new AlternatingPhaseState(this);
        states[(int)PolarousStateId.Stunned] = new StunnedState(this);
        states[(int)PolarousStateId.Defeated] = new DefeatedState(this);

        currentState = states[(int)PolarousStateId.Dormant];
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerRb = playerObj.GetComponent<Rigidbody2D>();
            playerCharge = playerObj.GetComponent<ChargeResource>();
        }

        currentState.Enter();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (autoStartFight) StartFight();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (damageCooldownTimer > 0f) damageCooldownTimer -= dt;

        currentState.TimeInState += dt;
        currentState.Tick(dt);

        debugCurrentState = currentStateId;
        debugCurrentHealth = currentHealth;
        debugPolarity = polarity;
    }

    private void FixedUpdate()
    {
        currentState.FixedTick(Time.fixedDeltaTime);
    }

    #endregion

    #region API pública

    /// <summary>Inicia el combate (Fase 1). Llamar desde un trigger de arena si autoStartFight = false.</summary>
    public void StartFight()
    {
        if (currentStateId == PolarousStateId.Dormant)
            ChangeState(PolarousStateId.PositivePhase);
    }

    /// <summary>Aturde al jefe (p. ej. tras una descarga RC del capacitor). Vuelve a su fase al terminar.</summary>
    public void TriggerStun(float duration = -1f)
    {
        if (IsDefeated || currentStateId == PolarousStateId.Dormant) return;

        StunnedState stun = (StunnedState)states[(int)PolarousStateId.Stunned];
        stun.Duration = duration > 0f ? duration : defaultStunDuration;

        // Al terminar, StunnedState vuelve a la fase que corresponda a la vida en ese momento.
        ChangeState(PolarousStateId.Stunned, force: true); // force: reinicia el stun si ya estaba aturdido
    }

    /// <summary>Daño genérico. Gestiona multiplicador de stun, cooldown, transición de fase y derrota.</summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDefeated || currentStateId == PolarousStateId.Dormant) return;
        if (damageCooldownTimer > 0f) return;
        damageCooldownTimer = damageCooldown;

        int finalDamage = IsStunned ? Mathf.CeilToInt(amount * stunnedDamageMultiplier) : amount;
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            ChangeState(PolarousStateId.Defeated);
            return;
        }

        // Si está aturdido NO cambiamos de fase ahora: se aplicará al salir del stun (ver StunnedState.Exit).
        if (!IsStunned)
        {
            PolarousStateId targetPhase = EvaluatePhaseForHealth();
            if (targetPhase != currentStateId)
                ChangeState(targetPhase);
        }
    }

    /// <summary>Compatibilidad con MeleeHitbox (misma firma que PolarousBossController).</summary>
    public void TakeMeleeDamage(int amount) => TakeDamage(amount);

    /// <summary>Compatibilidad con proyectiles reflejados (misma firma que PolarousBossController).</summary>
    public void TakeReflectedProjectileDamage(int amount) => TakeDamage(amount);

    #endregion

    #region Núcleo de la FSM

    private void ChangeState(PolarousStateId newStateId, bool force = false)
    {
        if (!force && newStateId == currentStateId) return;
        if (IsDefeated) return; // Estado terminal.

        PolarousStateId previous = currentStateId;

        currentState.Exit();
        currentStateId = newStateId;
        currentState = states[(int)newStateId];
        currentState.TimeInState = 0f;
        currentState.Enter();

        OnStateChanged?.Invoke(previous, newStateId);
    }

    /// <summary>Fase que corresponde a la vida actual según los umbrales del Inspector.</summary>
    private PolarousStateId EvaluatePhaseForHealth()
    {
        float ratio = HealthRatio;
        if (ratio <= phase3HealthRatio) return PolarousStateId.AlternatingPhase;
        if (ratio <= phase2HealthRatio) return PolarousStateId.NegativePhase;
        return PolarousStateId.PositivePhase;
    }

    private void SetPolarity(float newPolarity)
    {
        newPolarity = newPolarity >= 0f ? 1f : -1f;
        if (Mathf.Approximately(newPolarity, polarity)) return;

        polarity = newPolarity;
        OnPolarityChanged?.Invoke(polarity);
        // TODO VISUAL: cambiar color del sprite / aura / partículas según la polaridad.
    }

    #endregion

    #region Física del campo (Coulomb con clamping, enrutada por EMForceAccumulator2D)

    /// <summary>
    /// Aplica la fuerza del núcleo de POLAROUS sobre THEIA según el modo de campo de la fase.
    /// Usa ElectroPhysicsUtility (r² acotado + ClampMagnitude) → nunca lanza a THEIA por cálculos explosivos.
    /// </summary>
    private void ApplyPolarityField(PolarityFieldMode mode)
    {
        if (playerRb == null || playerTransform == null) return;

        Vector2 bossPos = transform.position;
        Vector2 playerPos = playerRb.position;
        Vector2 toPlayer = playerPos - bossPos;
        if (toPlayer.sqrMagnitude > fieldRadius * fieldRadius) return;

        float qPlayer = playerCharge != null ? playerCharge.GetCharge() : 0f;
        float qBoss = polarity * coreChargeMagnitude;

        switch (mode)
        {
            case PolarityFieldMode.PhysicalCoulomb:
                // Física real: si THEIA está neutra, no hay fuerza.
                if (Mathf.Abs(qPlayer) <= 0.01f) return;
                break;

            case PolarityFieldMode.AlwaysAttract:
                // Forzamos signos opuestos → atracción garantizada.
                qBoss = coreChargeMagnitude;
                qPlayer = -Mathf.Max(Mathf.Abs(qPlayer), designMinPlayerCharge);
                break;

            case PolarityFieldMode.AlwaysRepel:
                // Forzamos mismo signo → repulsión garantizada.
                qBoss = coreChargeMagnitude;
                qPlayer = Mathf.Max(Mathf.Abs(qPlayer), designMinPlayerCharge);
                break;
        }

        Vector2 force = ElectroPhysicsUtility.CoulombForce(
            bossPos, playerPos, fieldKe, qBoss, qPlayer,
            fieldMinDistance, fieldMaxForce, out float clampedR);

        EMForceAccumulator2D.ApplyForce(playerRb, force);

        // Telemetría para el DebugOverlayUI existente (tecla V).
        GameEvents.RaiseForceApplied(new CoulombForceData(
            bossPos, playerPos, qBoss, qPlayer, clampedR, fieldKe, force, (qBoss * qPlayer) < 0f));
    }

    #endregion

    #region Hooks de ataques — RELLENAR AQUÍ

    /// <summary>Se llama al entrar en cualquier fase de combate (1, 2 o 3). Ideal para banners, música, cámara.</summary>
    protected virtual void OnPhaseEntered(PolarousStateId phase)
    {
        // TODO: banner de fase, cambio de música, screen shake, reconfigurar placas del capacitor, etc.
    }

    /// <summary>FASE 1 (+Q, atrae). Sugerencia: proyectil único dirigido; el campo arrastra a THEIA hacia el núcleo.</summary>
    protected virtual void PerformPositivePhaseAttack()
    {
        // TODO ATAQUE: p. ej. disparar un proyectil cargado positivo hacia THEIA.
    }

    /// <summary>FASE 2 (−Q, repele). Sugerencia: abanico de proyectiles; el campo empuja a THEIA hacia los bordes/pinchos.</summary>
    protected virtual void PerformNegativePhaseAttack()
    {
        // TODO ATAQUE: p. ej. abanico de 2-3 proyectiles negativos o una onda expansiva radial.
    }

    /// <summary>FASE 3 (alternante). Recibe la polaridad actual para variar el patrón.</summary>
    protected virtual void PerformAlternatingPhaseAttack(float currentPolarity)
    {
        // TODO ATAQUE: p. ej. ráfaga mixta (+/−) o activar la barra de corriente (Fuerza de Lorentz).
    }

    /// <summary>Se llama al iniciar el stun. Ideal para VFX de cortocircuito / ventana de daño.</summary>
    protected virtual void OnStunStarted(float duration)
    {
        // TODO: VFX de chispas, bajar al suelo, SFX de sobrecarga.
    }

    /// <summary>Se llama al morir (una sola vez).</summary>
    protected virtual void OnDefeatedHook()
    {
        // TODO: animación de muerte, desbloquear progreso (PlayerPrefs) y cargar "WinScreen".
    }

    #endregion

    #region Estados (clases anidadas)

    /// <summary>Clase base de estado. Las anidadas pueden leer/escribir los campos privados del jefe.</summary>
    private abstract class BossState
    {
        protected readonly PolarousBoss boss;
        public float TimeInState;

        protected BossState(PolarousBoss owner) { boss = owner; }

        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void FixedTick(float fixedDt) { }
        public virtual void Exit() { }
    }

    /// <summary>Base para las 3 fases de combate: gestiona cadencia de ataque y campo de polaridad.</summary>
    private abstract class CombatPhaseState : BossState
    {
        private float attackTimer;

        protected CombatPhaseState(PolarousBoss owner) : base(owner) { }

        protected abstract float AttackInterval { get; }
        protected abstract PolarityFieldMode FieldMode { get; }
        protected abstract PolarousStateId Id { get; }
        protected abstract void Attack();

        public override void Enter()
        {
            attackTimer = AttackInterval; // Pequeña gracia al entrar en la fase antes del primer ataque.
            boss.OnPhaseEntered(Id);
        }

        public override void Tick(float dt)
        {
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                Attack();
                attackTimer = AttackInterval;
            }
        }

        public override void FixedTick(float fixedDt)
        {
            boss.ApplyPolarityField(FieldMode);
        }
    }

    /// <summary>Estado inicial: no ataca ni ejerce campo.</summary>
    private sealed class DormantState : BossState
    {
        public DormantState(PolarousBoss owner) : base(owner) { }
    }

    /// <summary>FASE 1 — Carga Positiva (+Q): ATRAE.</summary>
    private sealed class PositivePhaseState : CombatPhaseState
    {
        public PositivePhaseState(PolarousBoss owner) : base(owner) { }
        protected override float AttackInterval => boss.positivePhaseAttackInterval;
        protected override PolarityFieldMode FieldMode => boss.positivePhaseFieldMode;
        protected override PolarousStateId Id => PolarousStateId.PositivePhase;
        protected override void Attack() => boss.PerformPositivePhaseAttack();

        public override void Enter()
        {
            boss.SetPolarity(+1f);
            base.Enter();
        }
    }

    /// <summary>FASE 2 — Carga Negativa (−Q): REPELE.</summary>
    private sealed class NegativePhaseState : CombatPhaseState
    {
        public NegativePhaseState(PolarousBoss owner) : base(owner) { }
        protected override float AttackInterval => boss.negativePhaseAttackInterval;
        protected override PolarityFieldMode FieldMode => boss.negativePhaseFieldMode;
        protected override PolarousStateId Id => PolarousStateId.NegativePhase;
        protected override void Attack() => boss.PerformNegativePhaseAttack();

        public override void Enter()
        {
            boss.SetPolarity(-1f);
            base.Enter();
        }
    }

    /// <summary>FASE 3 — Alternante: invierte +Q/−Q cada N segundos con aviso previo (telegraph).</summary>
    private sealed class AlternatingPhaseState : CombatPhaseState
    {
        private float switchTimer;
        private bool telegraphSent;

        public AlternatingPhaseState(PolarousBoss owner) : base(owner) { }
        protected override float AttackInterval => boss.alternatingPhaseAttackInterval;
        protected override PolarousStateId Id => PolarousStateId.AlternatingPhase;
        protected override void Attack() => boss.PerformAlternatingPhaseAttack(boss.polarity);

        // En modo físico, la polaridad alternante cambia realmente si atrae o repele según la carga de THEIA.
        // En modos de diseño, alternamos atracción/repulsión siguiendo la polaridad.
        protected override PolarityFieldMode FieldMode =>
            boss.alternatingPhaseFieldMode != PolarityFieldMode.PhysicalCoulomb
                ? (boss.polarity > 0f ? PolarityFieldMode.AlwaysAttract : PolarityFieldMode.AlwaysRepel)
                : PolarityFieldMode.PhysicalCoulomb;

        public override void Enter()
        {
            switchTimer = boss.alternatingSwitchInterval;
            telegraphSent = false;
            base.Enter();
        }

        public override void Tick(float dt)
        {
            base.Tick(dt); // Cadencia de ataques.

            switchTimer -= dt;

            if (!telegraphSent && switchTimer <= boss.alternatingTelegraphTime)
            {
                telegraphSent = true;
                boss.OnPolaritySwitchTelegraph?.Invoke(Mathf.Max(0f, switchTimer));
            }

            if (switchTimer <= 0f)
            {
                boss.SetPolarity(-boss.polarity);
                switchTimer = boss.alternatingSwitchInterval;
                telegraphSent = false;
            }
        }
    }

    /// <summary>Aturdido: no ataca, no ejerce campo, recibe daño multiplicado. Vuelve a la fase adecuada al terminar.</summary>
    private sealed class StunnedState : BossState
    {
        public float Duration = 3.5f;

        public StunnedState(PolarousBoss owner) : base(owner) { }

        public override void Enter()
        {
            boss.OnStunStarted(Duration);
        }

        public override void Tick(float dt)
        {
            if (TimeInState >= Duration)
            {
                // Volver a la fase que corresponda a la vida actual (puede haber bajado durante el stun).
                boss.ChangeState(boss.EvaluatePhaseForHealth());
            }
        }
    }

    /// <summary>Estado terminal.</summary>
    private sealed class DefeatedState : BossState
    {
        public DefeatedState(PolarousBoss owner) : base(owner) { }

        public override void Enter()
        {
            boss.OnDefeatedHook();
            boss.OnDefeated?.Invoke();
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = polarity > 0f ? new Color(0.25f, 0.65f, 1f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, fieldRadius);
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, fieldMinDistance);
    }

    #endregion
}
