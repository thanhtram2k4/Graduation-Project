# Core Gameplay Loop

Rules governing level states, the economic system, and win/loss conditions for *Hao Khi Su Viet*.

## Level States

A level has exactly three sequential states: **Preparing → Defending → Ending**.

- **Preparing:** Game is paused. No enemies spawn or move. Player may freely place, reposition, or sell troops. "Start Wave" transitions to Defending.
- **Defending:** Wave timer runs; enemies spawn at entry points. Troops auto-attack enemies in range. Player may still place/sell troops. State ends when all enemies in the current wave are defeated or have reached the exit. If more waves remain → back to Preparing; otherwise → Ending.
- **Ending:** No gameplay input accepted. Victory/Defeat is evaluated and shown. Post-level summary (score, resources, stars) plus Retry / Return options.

## Economic System

- **Starting Gold:** Fixed per level via level config ScriptableObject — never hard-coded.
- **Kill Rewards:** Each destroyed enemy grants Gold defined on its ScriptableObject (`Kill Reward` field).
- **Passive Income (optional):** Certain troops may generate periodic Gold while deployed.
- **Placement Cost:** Deducted on placement. Reject the action if the player cannot afford it.
- **Sell Refund:** Returns `Sell Refund Rate × Placement Cost` (e.g. 50–70%). Available any time.
- **Upgrade Cost:** Deducted on upgrade. Set to 0 if no upgrade path exists.
- **Invariant:** Gold balance must never go below zero. Reject any transaction that would cause a negative balance.

## Win / Loss Conditions

- **Win:** All waves cleared AND Base HP > 0 at the end of the final wave.
  - Stars: ≥ 80% Base HP = 3 stars; ≥ 40% = 2 stars; > 0% = 1 star.
- **Loss:** Base HP reaches zero at any point during any wave. Transition immediately to Ending (Defeat).
- **Base HP:** Starts at the max defined in level config. No regeneration unless a mechanic explicitly grants it. Enemies that reach the Base deal their `Base Damage to Base` value and are removed; no kill-reward Gold is granted for these enemies.
