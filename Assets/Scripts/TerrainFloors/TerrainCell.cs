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
    /// Removes the hero from this cell. Returns the unit to the pool
    /// instead of Destroy (Rule 07).
    /// </summary>
    public void RemoveHero()
    {
        if (placedHero != null)
        {
            // Pool.Release() instead of Destroy (Rule 07)
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Release(placedHero.gameObject);
            else
                Destroy(placedHero.gameObject);

            placedHero = null;
            isOccupied = false;
        }
    }
}
