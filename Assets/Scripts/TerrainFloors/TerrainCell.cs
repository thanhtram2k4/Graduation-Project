using UnityEngine;

/// <summary>
/// Represents one cell in the terrain grid. Handles hero placement via
/// <see cref="ObjectPoolManager"/> (Rule 07) and gold deduction via
/// <see cref="EconomyManager"/> (C8).
/// </summary>
public class TerrainCell : MonoBehaviour
{
    public bool isOccupied;
    public Hero placedHero;

    // Cached HealthComponent of the placed hero — used exclusively for
    // subscribing/unsubscribing to OnHealthDepleted. No other Hero.cs
    // logic is accessed through this reference (component-based rule).
    private HealthComponent _placedHealth;

    /// <summary>
    /// Attempts to place a hero on this cell. Uses ObjectPoolManager.Get()
    /// instead of Instantiate (Rule 07). Deducts gold via EconomyManager.
    /// </summary>
    public bool PlaceHero(GameObject heroPrefab)
    {
        if (isOccupied) return false;

        Hero hero = heroPrefab.GetComponent<Hero>();
        if (hero == null) return false;

        // Deduct gold via EconomyManager (C8)
        if (!GameManager.Instance.SpendGold(hero.cost)) return false;

        // Pool.Get() instead of Instantiate (Rule 07)
        GameObject heroObj;
        if (ObjectPoolManager.Instance != null)
        {
            heroObj = ObjectPoolManager.Instance.Get(heroPrefab);
        }
        else
        {
            heroObj = Instantiate(heroPrefab);
        }

        heroObj.transform.position = transform.position;
        heroObj.transform.rotation = Quaternion.identity;

        placedHero = heroObj.GetComponent<Hero>();
        isOccupied = true;

        // Subscribe to the hero's death so the cell auto-clears (Rule 07:
        // event-driven, no polling). Uses HealthComponent directly — no
        // coupling to Hero.cs logic.
        _placedHealth = heroObj.GetComponent<HealthComponent>();
        if (_placedHealth != null)
            _placedHealth.OnHealthDepleted += HandleHeroDeath;

        // Publish placement event for AudioManager / analytics
        GameEventBus.Publish(new TroopPlacedEvent
        {
            UnitID = placedHero.UnitData != null ? placedHero.UnitData.unitID : "",
            Column = 0,
            Row = 0,
            Cost = hero.cost
        });

        return true;
    }

    /// <summary>
    /// Removes the hero from this cell (player-initiated sell/reposition).
    /// Returns the unit to the pool instead of Destroy (Rule 07).
    /// </summary>
    public void RemoveHero()
    {
        if (placedHero != null)
        {
            UnsubscribeFromHero();

            // Pool.Release() instead of Destroy (Rule 07)
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Release(placedHero.gameObject);
            else
                Destroy(placedHero.gameObject);

            placedHero = null;
            isOccupied = false;
        }
    }

    /// <summary>
    /// Called when the placed hero's HP reaches zero. Clears the cell so a
    /// new unit can be placed here. The hero's own death handler
    /// (Hero.HandleDeath) handles pool release — this method only manages
    /// the cell's occupancy state.
    /// </summary>
    private void HandleHeroDeath()
    {
        UnsubscribeFromHero();
        placedHero = null;
        isOccupied = false;
    }

    /// <summary>
    /// Unsubscribes from the current hero's <see cref="HealthComponent.OnHealthDepleted"/>
    /// to prevent memory leaks and stale callbacks (Rule 07). Safe to call
    /// when no hero is placed or when already unsubscribed.
    /// </summary>
    private void UnsubscribeFromHero()
    {
        if (_placedHealth != null)
        {
            _placedHealth.OnHealthDepleted -= HandleHeroDeath;
            _placedHealth = null;
        }
    }
}
