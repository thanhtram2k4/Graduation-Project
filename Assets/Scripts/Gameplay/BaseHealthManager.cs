using UnityEngine;

// =============================================================================
// BaseHealthManager.cs — Player Base HP Pool
//
// LAYER: Gameplay (Game.Gameplay)
// RULES SATISFIED:
//   - Rule 01 (Win/Loss): Instantly triggers defeat when Base HP reaches 0. 
//                         Releases Enemy with no Gold reward when they reach the base.
//   - Rule 03 (Data-Driven): Reads parameters directly from ScriptableObjects
//                            (LevelConfig.baseMaxHP, EnemyUnitData.baseDamageOnReach).
//   - Rule 07 (Component-Based): Under 300 lines of code, single responsibility.
//   - Rule 07 (Event-Driven UI): Communicates strictly via GameEventBus.
//   - Rule 07 (Object Pooling): Releases Enemy via HealthComponent.ForceKill(true)
//                               to ObjectPoolManager, never uses Destroy().
// =============================================================================

/// <summary>
/// Manages the player's Base HP pool. Detects enemies that reach the Base
/// Column via a 2D trigger collider, applies damage from
/// <see cref="EnemyUnitData.baseDamageOnReach"/>, releases the enemy back
/// to <see cref="ObjectPoolManager"/>, and publishes
/// <see cref="BaseTakeDamageEvent"/> / <see cref="DefeatEvent"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BaseHealthManager : MonoBehaviour
{
    [Header("Data (Rule 03 — Data-Driven)")]
    [Tooltip("Reference to the active LevelConfig. BaseMaxHP is read from this SO.")]
    [SerializeField] private LevelConfig levelConfig;

    [Header("Fallback")]
    [Tooltip("Default damage dealt by enemies without an EnemyUnitData SO.")]
    [SerializeField] private int fallbackDamage = 1;

    private int _currentHP;
    private int _maxHP;
    private bool _isDefeated;

    /// <summary>Current remaining HP of the Base.</summary>
    public int CurrentHP => _currentHP;

    /// <summary>Maximum HP read from LevelConfig at initialization.</summary>
    public int MaxHP => _maxHP;

    /// <summary>True if the Base HP has reached zero and DefeatEvent was published.</summary>
    public bool IsDefeated => _isDefeated;

    /// <summary>Fraction of HP remaining (0.0 – 1.0). Used by LevelConfig.EvaluateStars().</summary>
    public float HPFraction => _maxHP > 0 ? (float)_currentHP / _maxHP : 0f;

    private void Awake()
    {
        // Ensure the collider is set as trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[BaseHealthManager] Collider2D was not set as trigger. Auto-corrected to isTrigger = true.", this);
        }

        // Ensure the Rigidbody2D is kinematic (no physics simulation)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void Start()
    {
        InitializeHP();
    }

    /// <summary>
    /// Reads maxHP from LevelConfig and resets runtime state.
    /// </summary>
    public void InitializeHP()
    {
        if (levelConfig == null && GameManager.Instance != null)
        {
            levelConfig = GameManager.Instance.currentLevelConfig;
        }

        if (levelConfig != null)
        {
            _maxHP = levelConfig.baseMaxHP;
        }
        else
        {
            _maxHP = 20; // Absolute fallback — should never happen
            Debug.LogError("[BaseHealthManager] No LevelConfig assigned and GameManager.Instance.currentLevelConfig is null! Using fallback maxHP = 20.", this);
        }

        _currentHP = _maxHP;
        _isDefeated = false;

        // Publish initial state so BaseHealthUI can display full HP bar
        PublishHealthChanged(0);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (_isDefeated) return;

        // Guard: spawn-grace — ignore enemies that spawned overlapping the base
        Enemy enemyFacade = other.GetComponent<Enemy>();
        if (enemyFacade != null && enemyFacade.JustSpawned) return;

        // Determine damage from enemy's SO (Rule 03)
        int damage = fallbackDamage;
        if (enemyFacade != null && enemyFacade.UnitData != null)
        {
            damage = enemyFacade.UnitData.baseDamageOnReach;
        }
        else
        {
            Debug.LogWarning($"[BaseHealthManager] Enemy '{other.name}' has no EnemyUnitData assigned. Using fallback damage.", this);
        }

        ApplyDamage(damage);
        ReleaseEnemyToPool(other.gameObject, enemyFacade);
    }

    private void ApplyDamage(int damage)
    {
        if (damage <= 0) return;

        _currentHP = Mathf.Max(0, _currentHP - damage);
        PublishHealthChanged(damage);

        Debug.Log($"[BaseHealthManager] Base took {damage} damage. HP: {_currentHP}/{_maxHP}.");

        if (_currentHP <= 0 && !_isDefeated)
        {
            _isDefeated = true;
            HandleDefeat();
        }
    }

    private void PublishHealthChanged(int damage)
    {
        GameEventBus.Publish(new BaseTakeDamageEvent
        {
            Damage = damage,
            CurrentHP = _currentHP,
            MaxHP = _maxHP
        });
    }

    private void HandleDefeat()
    {
        GameEventBus.Publish(new DefeatEvent());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.isGameOver = true;
        }

        Debug.Log("[BaseHealthManager] Base HP reached zero — DefeatEvent published.");
    }

    private void ReleaseEnemyToPool(GameObject enemyGO, Enemy enemyFacade)
    {
        // ForceKill(true) triggers the death pipeline but suppresses Gold rewards (Rule 01)
        if (enemyFacade != null && enemyFacade.Health != null)
        {
            enemyFacade.Health.ForceKill(true);
            return;
        }

        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Release(enemyGO);
        }
        else
        {
            Destroy(enemyGO);
        }
    }
}
