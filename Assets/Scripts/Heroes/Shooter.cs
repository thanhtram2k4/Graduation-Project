using UnityEngine;

public class Shooter : Hero
{
    [Header("Shooter Settings")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    protected override void Attack()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        }
    }
}
