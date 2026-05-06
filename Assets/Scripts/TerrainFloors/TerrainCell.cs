using UnityEngine;

public class TerrainCell : MonoBehaviour
{
    public bool isOccupied;
    public Hero placedHero;

    public bool PlaceHero(GameObject heroPrefab)
    {
        if (isOccupied) return false;

        Hero hero = heroPrefab.GetComponent<Hero>();
        if (hero == null) return false;

        if (!GameManager.Instance.SpendGold(hero.cost)) return false;

        GameObject heroObj = Instantiate(heroPrefab, transform.position, Quaternion.identity);
        placedHero = heroObj.GetComponent<Hero>();
        isOccupied = true;
        return true;
    }

    public void RemoveHero()
    {
        if (placedHero != null)
        {
            Destroy(placedHero.gameObject);
            placedHero = null;
            isOccupied = false;
        }
    }
}
