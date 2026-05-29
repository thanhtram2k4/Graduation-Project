using UnityEngine;

/// <summary>
/// DEPRECATED — Kept for backward compatibility with existing prefabs.
///
/// Projectile configuration (prefab + firePoint) has moved to
/// <see cref="AttackComponent"/> (set in Inspector on the same prefab).
/// Hero.cs now delegates ranged attacks to AttackComponent.SpawnProjectile().
///
/// This class is safe to remove once prefabs have been migrated.
/// New ranged hero prefabs should use Hero + AttackComponent directly.
/// </summary>
public class Shooter : Hero
{
    // Previously held projectilePrefab and firePoint — now on AttackComponent.
    // This class exists only so prefabs referencing "Shooter" still compile.
}
