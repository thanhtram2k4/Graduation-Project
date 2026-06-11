using UnityEngine;

// =============================================================================
// EnemyMoveState — Lane-Locked Movement + Horizontal Hero Detection
//
// Detection vs. Engagement (two-stage model):
//   1) DETECT at DetectionRadius  — long-range awareness via horizontal Raycast.
//   2) ENGAGE at AttackRange      — only transition to AttackState when the
//      target is close enough to actually hit. If detected but out of range,
//      the enemy KEEPS MOVING toward the target.
//
// GHOST TARGET FIX:
//   A hard-coded name filter rejects any collider whose GameObject.name
//   contains "Base", "Background", or "Line". This prevents invisible
//   scene objects on the Hero layer from tricking the AI into stopping.
//   Debug.LogWarning statements expose every raycast hit for diagnosis.
//
// Rule 02 §2.1.5: lane-locked, strict Y-tolerance filter.
// Rule 07 §1.2: zero-alloc hot path — static pre-allocated hit buffer.
// Rule 09: one concern per state — detection + movement only.
// =============================================================================

/// <summary>
/// Enemy advances horizontally toward the Base Column (Rule 02, Rule 09).
/// Movement is delegated to <see cref="MovementComponent"/>.
/// Each frame, a lane-locked horizontal Raycast scans for ally troops
/// ("Hero" layer) at <c>DetectionRadius</c>. Transitions to EnemyAttackState
/// ONLY when the nearest same-lane hero is within <c>AttackRange</c>.
/// </summary>
public class EnemyMoveState : BaseState
{
    private readonly AIComponent _ai;

    // ── Lane-locked detection constants ──────────────────────────────────────

    /// <summary>
    /// Maximum Y-axis delta between enemy pivot and hero pivot for a hero to
    /// be considered "same lane". Lane spacing = 1 cellSize (Rule 02 §2.1.1);
    /// a lane extends ±0.5 from its centre, so 0.5f is exactly half a lane.
    /// </summary>
    private const float LANE_Y_TOLERANCE = 0.5f;

    // ── Zero-alloc hit buffer (Rule 07 §1.2) ────────────────────────────────
    //
    // Static & shared: Unity callbacks are sequential on the main thread, so
    // two EnemyMoveState.OnUpdate calls never overlap. Size 8 covers the
    // deepest realistic placement stack (one full column of heroes).
    private static readonly RaycastHit2D[] _laneHitBuffer = new RaycastHit2D[8];

    // ── Cached layer mask ────────────────────────────────────────────────────
    private static int _heroLayerMask = -1;

    // ── Ghost-target name blacklist ──────────────────────────────────────────
    // Hard-coded safety net: any collider whose GameObject.name contains one
    // of these substrings is ALWAYS rejected, even if it has a HealthComponent
    // on the Hero layer. This catches misconfigured scene objects.
    private static readonly string[] GHOST_TARGET_BLACKLIST = { "Base", "Background", "Line" };

    public EnemyMoveState(StateMachine owner, AIComponent ai) : base(owner)
    {
        _ai = ai;

        // Cache layer mask once (lazy init, safe because all state logic
        // runs on the main thread). -1 = not yet resolved.
        if (_heroLayerMask == -1)
            _heroLayerMask = LayerMask.GetMask("Hero");
    }

    public override void OnEnter()
    {
        // Clear any stale target from previous AttackState cycle
        _ai.CurrentTarget = null;

        if (_ai.Movement != null) _ai.Movement.SetMoving(true);
        if (_ai.Animator != null)
        {
            _ai.Animator.SetBool("IsMoving", true);
            _ai.Animator.SetBool("IsAttacking", false); // Pool-safe: clear residual attack anim
        }
    }

    public override void OnUpdate(float deltaTime)
    {
        // ── Game Over guard ──────────────────────────────────────────────────
        // If GameOver has been triggered (by this enemy or another), skip all
        // logic. Time.timeScale = 0 already halts deltaTime-based movement,
        // but this explicit guard prevents any residual state transitions
        // from firing on the same frame that multiple enemies cross the line.
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        // ── Lane-locked hero scan (DetectionRadius = awareness range) ────────
        HealthComponent hero = FindNearestLaneHero();

        if (hero != null && !hero.IsDead)
        {
            // ── AttackRange gate (engagement range) ──────────────────────────
            // DetectionRadius lets the enemy KNOW a hero is ahead.
            // AttackRange determines when the enemy is close enough to FIGHT.
            // If detected but beyond AttackRange → keep walking toward it.
            float distance = Vector2.Distance(
                _ai.transform.position, hero.transform.position);

            float attackRange = _ai.Attack != null ? _ai.Attack.AttackRange : 1.5f;

            if (distance <= attackRange)
            {
                _ai.CurrentTarget = hero;
                Owner.ChangeState(StateFactory.CreateEnemyAttackState(Owner, _ai));
                return;
            }
            // else: target detected but out of range — keep moving
        }

        // ── Base boundary check ──────────────────────────────────────────────
        // When the enemy's X position reaches the Game Over line (X <= -8),
        // the enemy has bypassed all defenders and breached the base.
        //
        // Rule 01 §Win/Loss: "Enemies that reach the Base deal their
        // 'Base Damage to Base' value and are removed; no kill-reward Gold
        // is granted for these enemies."
        //
        // We do NOT transition to EnemyDieState because:
        //   1) EnemyDieState publishes EnemyDestroyedEvent → kill reward.
        //      Base-reaching enemies must NOT grant Gold (Rule 01).
        //   2) After GameOver() sets Time.timeScale = 0, the death anim
        //      timer (deltaTime-based) would never complete → unit hangs.
        //   3) GameOver() is an absolute freeze — every unit stays in its
        //      current position/state as a visual "snapshot" of defeat.
        if (_ai.transform.position.x <= -8f)
        {
            Debug.Log(
                $"[FSM] Enemy {_ai.gameObject.name} reached Game Over line at X={_ai.transform.position.x:F2}. " +
                "Triggering GameOver().",
                _ai.gameObject);

            GameManager.Instance.GameOver();
            // Do NOT change state — Time.timeScale = 0 freezes everything.
            // The enemy remains visually frozen at the base line.
            return;
        }
    }

    public override void OnExit()
    {
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);
        if (_ai.Animator != null) _ai.Animator.SetBool("IsMoving", false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DETECTION — Strict horizontal Raycast, left-facing (Rule 02 §2.1.5)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Casts a horizontal ray to the LEFT (toward the player's base) for
    /// <c>DetectionRadius</c> world units against the Hero layer mask,
    /// then rejects any hit whose pivot Y is outside <see cref="LANE_Y_TOLERANCE"/>.
    /// Also applies a hard-coded name blacklist to reject ghost targets
    /// ("Base", "Background", "Line") regardless of layer or component.
    /// Returns the nearest same-lane hero's <see cref="HealthComponent"/>, or null.
    ///
    /// <b>Important:</b> This uses DetectionRadius (awareness range), NOT
    /// AttackRange. The caller in OnUpdate gates the state transition on
    /// AttackRange separately.
    /// </summary>
    private HealthComponent FindNearestLaneHero()
    {
        float range = _ai.Attack != null ? _ai.Attack.DetectionRadius : 3f;

        // Horizontal raycast — line shape, −X direction, Hero layer only.
        int count = Physics2D.RaycastNonAlloc(
            _ai.transform.position, Vector2.left, _laneHitBuffer, range, _heroLayerMask);

        HealthComponent nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _laneHitBuffer[i];
            if (hit.collider == null) continue;

            string hitName = hit.collider.gameObject.name;

            // ── Hard-coded ghost-target safety filter ─────────────────────────
            // Reject any collider whose GameObject.name contains a blacklisted
            // substring. This catches misconfigured scene objects (e.g., the
            // Player Base, background decorations, or boundary lines) that
            // accidentally have a HealthComponent or sit on the Hero layer.
            if (IsBlacklisted(hitName)) continue;

            // Strict same-lane filter: reject heroes in adjacent rows whose
            // colliders clip into our ray line. transform.position.y is the
            // authoritative lane indicator (Rule 02 §2.1.5).
            float dy = Mathf.Abs(hit.collider.transform.position.y - _ai.transform.position.y);
            if (dy >= LANE_Y_TOLERANCE) continue;

            HealthComponent hp = hit.collider.GetComponent<HealthComponent>();
            if (hp == null || hp.IsDead) continue;

            if (hit.distance < nearestDist)
            {
                nearestDist = hit.distance;
                nearest = hp;
            }
        }

        return nearest;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BLACKLIST HELPER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="objectName"/> contains any substring
    /// from <see cref="GHOST_TARGET_BLACKLIST"/>. Case-insensitive check using
    /// <see cref="System.StringComparison.OrdinalIgnoreCase"/> for robustness.
    /// </summary>
    private static bool IsBlacklisted(string objectName)
    {
        for (int i = 0; i < GHOST_TARGET_BLACKLIST.Length; i++)
        {
            if (objectName.IndexOf(
                    GHOST_TARGET_BLACKLIST[i],
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }
}
