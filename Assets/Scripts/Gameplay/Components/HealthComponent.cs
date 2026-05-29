using UnityEngine;

// =============================================================================
// HealthComponent — Single Responsibility: HP, Shield, Death Detection
//
// Reads max values from the unit's ScriptableObject via Initialize().
// Damage calculation is simplified here — the full 6-step pipeline will be
// implemented in C5 (DamageCalculator service) and called BEFORE this method.
//
// Rule 07: Component-Based. No attack/movement/AI logic in this class.
// Rule 03: All values from SO — zero hardcoded stats.
// =============================================================================

/// <summary>
/// Manages a unit's hit points and shield. Fires <see cref="OnHealthDepleted"/>
/// when HP reaches zero. Attached to both ally troops and enemy units.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    /// <summary>
    /// Fired when current HP reaches zero. Subscribers: Enemy/Hero facade
    /// (triggers death sequence), FSM (transitions to DieState).
    /// </summary>
    public event System.Action OnHealthDepleted;

    // ── Stats (set by Initialize, read from SO) ─────────────────────────────
    private float _maxHealth;
    private float _armor;
    private float _magicResistance;
    private float _maxShield;

    // ── Runtime ─────────────────────────────────────────────────────────────
    private float _currentHealth;
    private float _currentShield;
    private bool _isDead;

    // ── Public read-only accessors ──────────────────────────────────────────
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public float Armor => _armor;
    public float MagicResistance => _magicResistance;
    public float CurrentShield => _currentShield;
    public bool IsDead => _isDead;

    /// <summary>HP as a fraction [0, 1] for UI bars and star-rating calc.</summary>
    public float HealthFraction => _maxHealth > 0f ? _currentHealth / _maxHealth : 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets all health parameters from the unit's ScriptableObject data and
    /// performs a FULL runtime reset. Must be called by the unit facade
    /// (Enemy/Hero) on every spawn AND every pool-get so a recycled unit
    /// starts the next life from a clean slate.
    ///
    /// Reset contract (all three must happen every call):
    ///   • <c>_currentHealth</c> = maxHealth   — restore full HP
    ///   • <c>_currentShield</c> = maxShield   — restore full shield
    ///   • <c>_isDead</c>        = false       — clear death flag so TakeDamage works again
    ///
    /// Without this reset, a pooled enemy whose previous life ended with
    /// <c>_isDead == true</c> would silently ignore all incoming damage on
    /// its next spawn, or (if HP wasn't restored) appear with 0 HP and
    /// die to the very next TakeDamage call.
    /// </summary>
    public void Initialize(float maxHealth, float armor, float magicResistance, float shieldHP)
    {
        // Spawn-time data validation — surfaces a misconfigured SO immediately
        // instead of letting the unit spawn dead. Asked for explicitly in the
        // spawn-instant-death audit.
        if (maxHealth <= 0f)
        {
            Debug.LogError($"[HealthComponent] '{name}' Initialize received maxHealth = {maxHealth}. " +
                           "The unit will spawn at 0 HP and die on first hit (or immediately). " +
                           "Check the unit's ScriptableObject — Max Health must be > 0.", this);
        }

        _maxHealth = maxHealth;
        _armor = armor;
        _magicResistance = magicResistance;
        _maxShield = shieldHP;

        // Full runtime reset — critical for pooled units.
        _currentHealth = _maxHealth;
        _currentShield = _maxShield;
        _isDead = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DAMAGE — Uses DamageCalculator pipeline (C5, Rule 03 §3.3)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Entry point for incoming damage. Runs the full 6-step pipeline:
    /// Steps 1–4 via <see cref="DamageCalculator.Calculate"/> (type modifiers,
    /// buffs, clamp min 1). Steps 5–6 here (shield absorption, HP, death check).
    /// </summary>
    /// <param name="rawDamage">Base damage before defence modifiers.</param>
    /// <param name="damageType">Determines which defence stat is consulted.</param>
    /// <param name="buffMultiplier">Combined attacker/target buff modifier (default 1.0).</param>
    public void TakeDamage(float rawDamage, DamageType damageType = DamageType.Physical,
                           float buffMultiplier = 1f)
    {
        if (_isDead) return;

        // Steps 1–4: DamageCalculator computes effective damage
        DamageResult result = DamageCalculator.Calculate(new DamageRequest
        {
            BaseDamage = rawDamage,
            Type = damageType,
            TargetArmor = _armor,
            TargetMagicResistance = _magicResistance,
            BuffMultiplier = buffMultiplier
        });

        // Steps 5–6: Apply to shield/HP
        ApplyFinalDamage(result.FinalDamage);
    }

    /// <summary>
    /// Applies pre-computed final damage directly (skips the calculator).
    /// Used when DamageCalculator has already been called externally,
    /// or for special cases like LaneSweeper instant-kill.
    ///
    /// Step 5: Shield absorbs first, overflow goes to HP.
    /// Step 6: HP &lt;= 0 fires <see cref="OnHealthDepleted"/>.
    /// </summary>
    /// <param name="finalDamage">Damage after all modifiers (already clamped).</param>
    public void ApplyFinalDamage(float finalDamage)
    {
        if (_isDead) return;

        // Step 5: Shield absorbs first
        if (_currentShield > 0f)
        {
            float absorbed = Mathf.Min(_currentShield, finalDamage);
            _currentShield -= absorbed;
            finalDamage -= absorbed;
        }

        // Step 6: Apply remaining to HP, check destruction
        _currentHealth -= finalDamage;

        if (_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            _isDead = true;
            OnHealthDepleted?.Invoke();
        }
    }

    /// <summary>
    /// Heals the unit by <paramref name="amount"/>, clamped to MaxHealth.
    /// </summary>
    public void Heal(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
    }
}
