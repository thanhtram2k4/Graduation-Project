using UnityEngine;

/// <summary>
/// Central bootstrapper and session-state holder. Owns the LevelConfig
/// reference and coordinates initialization of subsystems (EconomyManager,
/// future LevelStateManager, PauseManager).
///
/// Gold logic has been extracted to <see cref="EconomyManager"/> (C8).
/// Remaining GameOver/GameWin will be replaced by LevelStateManager (C7).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Configuration")]
    [Tooltip("Assign the LevelConfig ScriptableObject for the current level. " +
             "Starting Gold and Base HP are read from this asset — never hardcoded.")]
    public LevelConfig currentLevelConfig;

    [Header("Runtime State (read-only at runtime)")]
    public bool isGameOver;
    public bool isGameWon;

    /// <summary>
    /// Convenience property: reads Gold from EconomyManager.
    /// Exists for backward compatibility during refactoring — new code should
    /// use EconomyManager.Instance.CurrentGold directly.
    /// </summary>
    public int currentGold => EconomyManager.Instance != null
        ? EconomyManager.Instance.CurrentGold
        : 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Initialize EconomyManager with level config (data-driven, Rule 01/03)
        if (EconomyManager.Instance != null && currentLevelConfig != null)
        {
            EconomyManager.Instance.InitializeForLevel(currentLevelConfig);
        }
        else if (currentLevelConfig == null)
        {
            Debug.LogError("[GameManager] currentLevelConfig is not assigned!", this);
        }

        isGameOver = false;
        isGameWon = false;
    }

    /// <summary>
    /// Delegates to EconomyManager. Exists for backward compatibility
    /// during refactoring — new code should call EconomyManager directly.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (EconomyManager.Instance == null) return false;
        return EconomyManager.Instance.SpendGold(amount);
    }

    /// <summary>
    /// Delegates to EconomyManager. Exists for backward compatibility
    /// during refactoring — new code should call EconomyManager directly.
    /// </summary>
    public void AddGold(int amount)
    {
        if (EconomyManager.Instance == null) return;
        EconomyManager.Instance.AddGold(amount);
    }

    public void GameOver()
    {
        isGameOver = true;
        GameEventBus.Publish(new DefeatEvent());
        Debug.Log("Game Over!");
    }

    public void GameWin()
    {
        isGameWon = true;
        Debug.Log("You Win!");
    }

    public void RestartGame()
    {
        GameEventBus.Reset();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
