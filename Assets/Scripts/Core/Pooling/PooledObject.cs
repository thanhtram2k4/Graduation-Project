using UnityEngine;

/// <summary>
/// Lightweight tracking component auto-added by <see cref="ObjectPoolManager"/>
/// to every pooled instance. Stores the prefab's InstanceID so the pool
/// knows which queue to return the object to on Release.
/// Zero overhead at runtime — no Update, no allocation.
/// </summary>
public class PooledObject : MonoBehaviour
{
    /// <summary>
    /// InstanceID of the source prefab. Set once by ObjectPoolManager
    /// when the instance is first created. Used as the dictionary key
    /// to locate the correct return queue on Release.
    /// </summary>
    public int PrefabInstanceID { get; set; }
}
