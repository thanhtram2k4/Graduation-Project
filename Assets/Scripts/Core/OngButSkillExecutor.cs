using UnityEngine;

// =============================================================================
// OngButSkillExecutor — Applies Ông Bụt Reward Skill Effects
//
// Static utility that executes the actual gameplay effect when the player
// activates their granted Ông Bụt skill from the HUD.
//
// LAYER: Game.Gameplay
// SEPARATION: Isolated from OngButSessionManager for single-responsibility.
// POOLING: VFX instantiated via ObjectPoolManager (Rule 07).
// =============================================================================

/// <summary>
/// Executes the gameplay effect of an <see cref="OngButSkillData"/> skill.
/// Called by <see cref="OngButSessionManager"/> when the player activates
/// their granted skill. Each <see cref="OngButSkillEffectType"/> has a
/// dedicated execution branch.
/// </summary>
public static class OngButSkillExecutor
{
    /// <summary>
    /// Executes the specified Ông Bụt skill's effect on the game world.
    /// </summary>
    /// <param name="skill">The skill data to execute.</param>
    public static void Execute(OngButSkillData skill)
    {
        if (skill == null)
        {
            Debug.LogError("[OngButSkillExecutor] Cannot execute null skill.");
            return;
        }

        switch (skill.EffectType)
        {
            case OngButSkillEffectType.HeroRevive:
                ExecuteHeroRevive(skill);
                break;

            case OngButSkillEffectType.ArrowRain:
                ExecuteArrowRain(skill);
                break;

            case OngButSkillEffectType.GoldBlessing:
                ExecuteGoldBlessing(skill);
                break;

            case OngButSkillEffectType.HealAllTroops:
                ExecuteHealAllTroops(skill);
                break;

            case OngButSkillEffectType.FreezeAllEnemies:
                ExecuteFreezeAllEnemies(skill);
                break;

            default:
                Debug.LogWarning($"[OngButSkillExecutor] Unhandled effect type: {skill.EffectType}");
                break;
        }

        // Spawn VFX if configured
        SpawnVFX(skill);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EFFECT IMPLEMENTATIONS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hồi Sinh Anh Hùng — Find the most recently destroyed hero and revive it.
    /// Currently logs intent; full implementation requires tracking destroyed heroes.
    /// </summary>
    private static void ExecuteHeroRevive(OngButSkillData skill)
    {
        // Find all troops tagged as "Hero" that are currently inactive/destroyed
        // For Phase 1: scan for TroopDestroyedEvent history or a hero graveyard list
        // maintained by the gameplay layer.
        //
        // Placeholder implementation: heal all existing heroes to full HP as fallback.
        // Full hero-revive requires a hero graveyard tracking system (future enhancement).
        var heroes = GameObject.FindGameObjectsWithTag("Hero");
        int healed = 0;
        for (int i = 0; i < heroes.Length; i++)
        {
            if (heroes[i] == null || !heroes[i].activeInHierarchy) continue;
            var health = heroes[i].GetComponent<HealthComponent>();
            if (health != null)
            {
                health.Heal(skill.EffectValue);
                healed++;
            }
        }

        Debug.Log($"[OngButSkillExecutor] HeroRevive executed. Healed {healed} heroes by {skill.EffectValue}.");
    }

    /// <summary>
    /// Mưa Tên Thần — Deal damage to all enemies within the effect radius.
    /// For AutoExecute: hits all enemies on the map.
    /// For PointAoE: would need targeting input (handled by OngButSessionManager).
    /// </summary>
    private static void ExecuteArrowRain(OngButSkillData skill)
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        int hits = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || !enemies[i].activeInHierarchy) continue;

            var health = enemies[i].GetComponent<HealthComponent>();
            if (health != null)
            {
                health.TakeDamage(skill.EffectValue);
                hits++;
            }
        }

        Debug.Log($"[OngButSkillExecutor] ArrowRain executed. Hit {hits} enemies for {skill.EffectValue} damage each.");
    }

    /// <summary>
    /// Phúc Lộc Ông Bụt — Grant bonus Gold to the player instantly.
    /// </summary>
    private static void ExecuteGoldBlessing(OngButSkillData skill)
    {
        int goldAmount = Mathf.RoundToInt(skill.EffectValue);

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddGold(goldAmount);
        }

        Debug.Log($"[OngButSkillExecutor] GoldBlessing executed. Granted {goldAmount} Gold.");
    }

    /// <summary>
    /// Hồi Máu Toàn Quân — Heal all deployed ally troops.
    /// </summary>
    private static void ExecuteHealAllTroops(OngButSkillData skill)
    {
        var troops = GameObject.FindGameObjectsWithTag("Hero");
        int healed = 0;

        for (int i = 0; i < troops.Length; i++)
        {
            if (troops[i] == null || !troops[i].activeInHierarchy) continue;

            var health = troops[i].GetComponent<HealthComponent>();
            if (health != null)
            {
                health.Heal(skill.EffectValue);
                healed++;
            }
        }

        Debug.Log($"[OngButSkillExecutor] HealAllTroops executed. Healed {healed} troops by {skill.EffectValue} each.");
    }

    /// <summary>
    /// Đóng Băng Chiến Trường — Force all enemies into stunned state.
    /// Uses AIComponent.ForceState with EnemyStunnedState via StateFactory.
    /// </summary>
    private static void ExecuteFreezeAllEnemies(OngButSkillData skill)
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        int frozen = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || !enemies[i].activeInHierarchy) continue;

            // Use AIComponent.ForceState to enter stunned state (Rule 09)
            var ai = enemies[i].GetComponent<AIComponent>();
            if (ai != null)
            {
                var stunnedState = StateFactory.CreateEnemyStunnedState(ai.FSM, ai, skill.EffectValue);
                ai.ForceState(stunnedState);
                frozen++;
            }
        }

        Debug.Log($"[OngButSkillExecutor] FreezeAllEnemies executed. Froze {frozen} enemies for {skill.EffectValue}s.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VFX HELPER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns the skill's VFX prefab via ObjectPoolManager if configured.
    /// </summary>
    private static void SpawnVFX(OngButSkillData skill)
    {
        if (skill.VfxPrefab == null) return;

        if (ObjectPoolManager.Instance != null)
        {
            GameObject vfx = ObjectPoolManager.Instance.Get(skill.VfxPrefab);
            vfx.transform.position = Vector3.zero;
        }
        else
        {
            GameObject.Instantiate(skill.VfxPrefab, Vector3.zero, Quaternion.identity);
            Debug.LogWarning("[OngButSkillExecutor] ObjectPoolManager not found. VFX instantiated directly.");
        }
    }
}
