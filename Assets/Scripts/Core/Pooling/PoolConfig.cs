using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that configures initial pool capacities for
/// <see cref="ObjectPoolManager"/>. One asset per scene or per level.
/// Capacities are hints — pools auto-expand if exhausted (with a warning).
/// </summary>
[CreateAssetMenu(fileName = "NewPoolConfig", menuName = "HKSV/Data/Pool Config")]
public class PoolConfig : ScriptableObject
{
    [Tooltip("List of prefab-to-initial-size mappings. " +
             "ObjectPoolManager pre-instantiates this many copies at startup.")]
    public List<PoolEntry> entries = new List<PoolEntry>();
}

/// <summary>
/// One entry in a <see cref="PoolConfig"/>: which prefab to pool and
/// how many instances to pre-allocate.
/// </summary>
[Serializable]
public struct PoolEntry
{
    [Tooltip("Human-readable label (e.g. 'EnemyPool_NguyenInfantry'). " +
             "For debugging only — not used as a lookup key.")]
    public string poolName;

    [Tooltip("The prefab to pool. Must not be null.")]
    public GameObject prefab;

    [Tooltip("Number of instances to pre-instantiate at startup. " +
             "Estimate based on peak concurrent usage.")]
    [Min(0)]
    public int initialSize;
}
