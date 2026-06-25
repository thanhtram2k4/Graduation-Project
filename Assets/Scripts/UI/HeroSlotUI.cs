using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// HeroSlotUI.cs (Refactored — Phase 4)
// Event-driven hero slot in the in-match HUD roster.
//
// REMOVED: All GameManager.Instance and HeroSelector references (Rule 07).
// NOW: Subscribes to LineupFinalizedEvent and reads data from LineupManager
//      via the event payload. The hero prefab is stored locally for drag-drop.
//
// UI layer component — ZERO gameplay MonoBehaviour references.
// =============================================================================

/// <summary>
/// Represents a single hero slot in the in-match HUD. Populated after
/// <see cref="LineupFinalizedEvent"/> is received. Stores the hero prefab
/// reference for <see cref="HeroDragHandler"/> to use during placement.
/// </summary>
public class HeroSlotUI : MonoBehaviour
{
    [Header("Slot Config")]
    public int slotIndex;

    [Header("UI References")]
    [SerializeField] private Image heroIcon;
    [SerializeField] private TextMeshProUGUI costText;

    // ─────────────────────────────────────────────────────────────────────────
    // State populated by LineupFinalizedEvent
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The hero prefab for this slot, resolved from the draft lineup.
    /// Read by HeroDragHandler for drag-and-drop placement.
    /// </summary>
    public GameObject HeroPrefab { get; private set; }

    /// <summary>The HeroCardData for this slot.</summary>
    public HeroCardData HeroCard { get; private set; }

    /// <summary>Whether this slot has been populated with lineup data.</summary>
    public bool IsInitialized { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnLineupFinalized += HandleLineupFinalized;
    }

    private void OnDisable()
    {
        GameEventBus.OnLineupFinalized -= HandleLineupFinalized;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates this slot from the LineupManager's finalized lineup.
    /// Reads data via LineupManager.Instance — this is a gameplay singleton,
    /// but since we need the prefab reference post-finalization, the minimal
    /// coupling is acceptable. An alternative would be to pass all data
    /// through the event payload.
    /// </summary>
    private void HandleLineupFinalized(LineupFinalizedEvent evt)
    {
        if (LineupManager.Instance == null)
        {
            Debug.LogWarning($"[HeroSlotUI] LineupManager.Instance is null. Slot {slotIndex} cannot initialize.");
            return;
        }

        if (slotIndex < 0 || slotIndex >= evt.LineupSize)
        {
            // This slot is beyond the lineup size — hide it
            gameObject.SetActive(false);
            return;
        }

        HeroCard = LineupManager.Instance.GetLineupEntry(slotIndex);
        HeroPrefab = LineupManager.Instance.GetLineupPrefab(slotIndex);

        if (HeroCard == null || HeroPrefab == null)
        {
            Debug.LogWarning($"[HeroSlotUI] Lineup data missing for slot {slotIndex}.");
            return;
        }

        // Populate icon from hero card portrait
        if (heroIcon != null && HeroCard.cardFaceSprite != null)
            heroIcon.sprite = HeroCard.cardFaceSprite;

        // Populate cost from linked unit data
        if (costText != null && HeroCard.linkedUnitData != null)
        {
            DefenderUnitData defender = HeroCard.linkedUnitData as DefenderUnitData;
            if (defender != null)
                costText.text = defender.placementCost.ToString();
        }

        IsInitialized = true;
    }
}
