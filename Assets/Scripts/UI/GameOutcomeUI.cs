using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// =============================================================================
// GameOutcomeUI.cs — Victory/Defeat Panels
//
// LAYER: UI (Game.UI)
// RULES SATISFIED:
//   - Rule 07 (UI-Gameplay Separation): NO direct references to gameplay components.
//   - Rule 10 (Scene Cleanup/Pause Flow): Sets timeScale to 0f on defeat/victory.
//                                         Restores timeScale to 1f and resets
//                                         GameEventBus BEFORE scene transitions.
// =============================================================================

/// <summary>
/// Manages Victory and Defeat UI panels. Subscribes to
/// <see cref="DefeatEvent"/> and <see cref="VictoryEvent"/> via
/// <see cref="GameEventBus"/>. Freezes time on game end and provides
/// button handlers for scene transitions with proper cleanup (Rule 10).
/// </summary>
public class GameOutcomeUI : MonoBehaviour
{
    [Header("Defeat Panel")]
    [SerializeField] private GameObject defeatPanel;
    [Tooltip("Play Again button on the Defeat screen.")]
    [SerializeField] private Button defeatPlayAgainButton;
    [SerializeField] private TextMeshProUGUI defeatTitleText;

    [Header("Victory Panel")]
    [SerializeField] private GameObject victoryPanel;
    [Tooltip("Next Level button on the Victory screen.")]
    [SerializeField] private Button nextLevelButton;
    [Tooltip("Play Again button on the Victory screen.")]
    [SerializeField] private Button victoryPlayAgainButton;
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private TextMeshProUGUI starsText;

    private void OnEnable()
    {
        GameEventBus.OnDefeat += HandleDefeat;
        GameEventBus.OnVictory += HandleVictory;
    }

    private void OnDisable()
    {
        GameEventBus.OnDefeat -= HandleDefeat;
        GameEventBus.OnVictory -= HandleVictory;
    }

    private void Start()
    {
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    private void HandleDefeat(DefeatEvent evt)
    {
        Time.timeScale = 0f;

        if (defeatPanel != null)
        {
            defeatPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[GameOutcomeUI] defeatPanel is not assigned! The player cannot see the Game Over screen.", this);
        }

        Debug.Log("[GameOutcomeUI] Defeat — Panel shown, time frozen.");
    }

    private void HandleVictory(VictoryEvent evt)
    {
        Time.timeScale = 0f;

        if (starsText != null)
        {
            starsText.SetText("★ {0}", evt.StarsEarned);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[GameOutcomeUI] victoryPanel is not assigned! The player cannot see the Victory screen.", this);
        }

        Debug.Log($"[GameOutcomeUI] Victory — {evt.StarsEarned} stars, panel shown, time frozen.");
    }

    /// <summary>
    /// Restores time, resets event bus, and reloads current scene.
    /// </summary>
    public void OnPlayAgainClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());

        // CRITICAL ORDER (Rule 10):
        Time.timeScale = 1f;
        GameEventBus.Reset();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Saves the current Gold and Lineup to CampaignManager, then reloads the
    /// scene. The reloaded scene reads the next LevelConfig from CampaignManager
    /// and skips Intro/Draft/Shuffle (campaign continuation).
    /// </summary>
    public void OnNextLevelClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());

        // Save campaign state before scene reload
        if (CampaignManager.Instance != null)
        {
            // Collect current Gold from EconomyManager
            int gold = EconomyManager.Instance != null ? EconomyManager.Instance.CurrentGold : 0;

            // Collect current lineup from LineupManager
            HeroCardData[] lineupArray = null;
            if (LineupManager.Instance != null)
            {
                int size = LineupManager.Instance.MaxLineupSize;
                int count = 0;

                // Count valid entries first (zero-alloc counting)
                for (int i = 0; i < size; i++)
                {
                    if (LineupManager.Instance.GetLineupEntry(i) != null)
                        count++;
                }

                lineupArray = new HeroCardData[count];
                int writeIdx = 0;
                for (int i = 0; i < size; i++)
                {
                    HeroCardData entry = LineupManager.Instance.GetLineupEntry(i);
                    if (entry != null)
                    {
                        lineupArray[writeIdx] = entry;
                        writeIdx++;
                    }
                }
            }

            CampaignManager.Instance.SaveStateAndAdvance(gold, lineupArray);
        }

        // CRITICAL ORDER (Rule 10):
        Time.timeScale = 1f;
        GameEventBus.Reset();

        // Reload current scene — CampaignManager (DDOL) will provide the next LevelConfig
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
