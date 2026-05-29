// =============================================================================
// DamageCalculator — Static Service: 6-Step Damage Pipeline (Rule 03 §3.3)
//
// Pure-logic, zero-alloc, no MonoBehaviour, no Unity lifecycle dependency.
// Fully unit-testable in Edit Mode tests (Rule 07 §Testability).
//
// Pipeline (executed in this exact sequence every time an attack lands):
//   1. Read Base Damage from attacker
//   2. Apply Damage Type Modifier (Physical/Magical/True)
//   3. Apply active Buff/Debuff multipliers
//   4. Clamp to minimum 1
//   5. Shield absorbs first, overflow to HP
//   6. Check destruction (HP <= 0)
//
// Steps 1-4 happen here (compute effective damage).
// Steps 5-6 happen in HealthComponent.ApplyFinalDamage().
// =============================================================================

/// <summary>
/// Input struct for the damage pipeline. Stack-allocated, zero GC (Rule 07).
/// </summary>
public struct DamageRequest
{
    /// <summary>Raw damage from the attacker's ScriptableObject.</summary>
    public float BaseDamage;

    /// <summary>Determines which defence stat is consulted.</summary>
    public DamageType Type;

    /// <summary>Target's flat Armor value (for Physical).</summary>
    public float TargetArmor;

    /// <summary>Target's percentage Magic Resistance [0,1] (for Magical).</summary>
    public float TargetMagicResistance;

    /// <summary>
    /// Combined damage multiplier from active buffs/debuffs on attacker and target.
    /// 1.0 = no modifier. >1 = amplified. Less than 1 = reduced.
    /// Set by the caller after querying buff systems. Default to 1.0 if no buffs.
    /// </summary>
    public float BuffMultiplier;
}

/// <summary>
/// Output struct from the damage pipeline. Stack-allocated, zero GC (Rule 07).
/// </summary>
public struct DamageResult
{
    /// <summary>Final damage to apply to shield/HP (after all modifiers, min 1).</summary>
    public float FinalDamage;

    /// <summary>The damage type (passed through for VFX/SFX variation).</summary>
    public DamageType Type;
}

/// <summary>
/// Static service that computes effective damage following the 6-step pipeline
/// defined in Rule 03 §3.3. No heap allocation. No MonoBehaviour dependency.
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// Minimum damage value. Final damage is never less than this (Rule 03).
    /// </summary>
    public const float MIN_DAMAGE = 1f;

    /// <summary>
    /// Executes steps 1–4 of the damage pipeline and returns the result.
    /// The caller (HealthComponent) executes steps 5–6 (shield/HP application).
    ///
    /// <b>Step 1:</b> Read Base Damage (from <paramref name="request"/>.BaseDamage).
    /// <b>Step 2:</b> Apply Damage Type Modifier:
    ///   - Physical: BaseDamage − Armor
    ///   - Magical:  BaseDamage × (1 − MagicResistance)
    ///   - True:     BaseDamage (bypasses all defences)
    /// <b>Step 3:</b> Multiply by BuffMultiplier (attacker amp + target reduction).
    /// <b>Step 4:</b> Clamp to minimum 1.
    /// </summary>
    /// <param name="request">All inputs for the calculation.</param>
    /// <returns>The computed effective damage and metadata.</returns>
    public static DamageResult Calculate(DamageRequest request)
    {
        // Step 1: Read Base Damage
        float damage = request.BaseDamage;

        // Step 2: Apply Damage Type Modifier
        switch (request.Type)
        {
            case DamageType.Physical:
                damage -= request.TargetArmor;
                break;

            case DamageType.Magical:
                float resist = request.TargetMagicResistance;
                if (resist < 0f) resist = 0f;
                if (resist > 1f) resist = 1f;
                damage *= (1f - resist);
                break;

            case DamageType.True:
                // Bypasses all defences — no modification
                break;
        }

        // Step 3: Apply Buff/Debuff multipliers
        if (request.BuffMultiplier != 1f && request.BuffMultiplier > 0f)
        {
            damage *= request.BuffMultiplier;
        }

        // Step 4: Clamp to minimum 1
        if (damage < MIN_DAMAGE)
            damage = MIN_DAMAGE;

        return new DamageResult
        {
            FinalDamage = damage,
            Type = request.Type
        };
    }

    /// <summary>
    /// Convenience overload for simple cases without buff modifiers.
    /// </summary>
    public static DamageResult Calculate(float baseDamage, DamageType type,
                                          float targetArmor, float targetMagicResistance)
    {
        return Calculate(new DamageRequest
        {
            BaseDamage = baseDamage,
            Type = type,
            TargetArmor = targetArmor,
            TargetMagicResistance = targetMagicResistance,
            BuffMultiplier = 1f
        });
    }
}
