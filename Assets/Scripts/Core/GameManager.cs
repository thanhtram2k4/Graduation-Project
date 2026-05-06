using UnityEngine;

/// <summary>
/// Temporary game manager for Phase 2. Will be decomposed into
/// LevelStateManager, EconomyManager, and PauseManager in Phase 3.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Configuration")]
    [Tooltip("Assign the LevelConfig ScriptableObject for the current level. " +
             "Starting Gold and Base HP are read from this asset — never hardcoded.")]
    public LevelConfig currentLevelConfig;

    [Header("Runtime State (read-only at runtime)")]
    public int currentGold;
    public bool isGameOver;
    public bool isGameWon;

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
        // Read starting gold from the data-driven LevelConfig ScriptableObject
        // instead of a hardcoded value (Rule 01, Rule 07).
        if (currentLevelConfig != null)
        {
            currentGold = currentLevelConfig.startingGold;
        }
        else
        {
            Debug.LogError("[GameManager] currentLevelConfig is not assigned! " +
                           "Starting Gold cannot be determined.", this);
            currentGold = 0;
        }

        isGameOver = false;
        isGameWon = false;
    }

    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            // TODO Phase 3: Publish GoldChangedEvent on GameEventBus
            return true;
        }
        return false;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        // TODO Phase 3: Publish GoldChangedEvent on GameEventBus
    }

    public void GameOver()
    {
        isGameOver = true;
        // NOTE: Time.timeScale manipulation removed.
        // Will be handled by PauseManager in Phase 3 (Rule 10).
        Debug.Log("Game Over!");
    }

    public void GameWin()
    {
        isGameWon = true;
        // NOTE: Time.timeScale manipulation removed.
        // Will be handled by PauseManager in Phase 3 (Rule 10).
        Debug.Log("You Win!");
    }

    public void RestartGame()
    {
        // NOTE: Time.timeScale reset removed.
        // Will be handled by PauseManager in Phase 3 (Rule 10).
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
