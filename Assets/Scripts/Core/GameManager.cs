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

    /// <summary>
    /// Triggers an absolute Game Over (defeat). Freezes the entire game by
    /// setting <c>Time.timeScale = 0f</c>, which halts all physics, FSM
    /// Update() loops, animations driven by deltaTime, and projectiles.
    ///
    /// <para><b>To resume time when restarting:</b> call
    /// <c>Time.timeScale = 1f;</c> before <c>SceneManager.LoadScene()</c>.
    /// See <see cref="RestartGame"/> for reference.</para>
    ///
    /// <para><b>Architecture note (Rule 10):</b> Time.timeScale ownership
    /// should belong to PauseManager / LevelStateManager. This is a
    /// temporary placement until those systems are implemented.</para>
    /// </summary>
    public void GameOver()
    {
        // Guard: prevent duplicate calls (e.g., two enemies cross the
        // base line on the same frame).
        if (isGameOver) return;

        isGameOver = true;

        // ── Instant freeze ──────────────────────────────────────────────
        // Setting timeScale to 0 stops:
        //   • All Time.deltaTime-based movement (MovementComponent, FSM)
        //   • All physics simulation (Rigidbody2D, projectiles)
        //   • All Animator updates that use normal time
        //   • All coroutines using WaitForSeconds
        //
        // UI animations that must continue during Game Over should use
        // Time.unscaledDeltaTime (Rule 10 §Time Scale Contract).
        Time.timeScale = 0f;

        GameEventBus.Publish(new DefeatEvent());
        Debug.Log("[GameManager] Game Over — Time frozen (timeScale = 0).");
    }

    public void GameWin()
    {
        isGameWon = true;
        Debug.Log("You Win!");
    }

    public void RestartGame()
    {
        // ── CRITICAL: Restore time before scene reload ──────────────────
        // Time.timeScale persists across scene loads. If GameOver() froze
        // time (timeScale = 0), we MUST reset it to 1 here, otherwise
        // the reloaded scene starts frozen.
        // Rule 10 §Restart Flow step 3a: "Resume() is called first to
        // restore Time.timeScale = 1 before scene load."
        Time.timeScale = 1f;

        GameEventBus.Reset();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
