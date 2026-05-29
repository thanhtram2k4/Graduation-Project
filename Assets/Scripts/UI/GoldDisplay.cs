using UnityEngine;
using TMPro;

/// <summary>
/// Reactive UI display for the player's Gold balance.
/// Subscribes to <see cref="GoldChangedEvent"/> via <see cref="GameEventBus"/>
/// instead of polling GameManager every frame (Rule 07 — Event-Driven UI).
/// Uses TMP SetText(int) overload to avoid ToString() GC allocation.
/// </summary>
public class GoldDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;

    private void OnEnable()
    {
        GameEventBus.OnGoldChanged += HandleGoldChanged;
    }

    private void OnDisable()
    {
        GameEventBus.OnGoldChanged -= HandleGoldChanged;
    }

    private void HandleGoldChanged(GoldChangedEvent evt)
    {
        if (goldText != null)
        {
            // SetText(string, float) avoids ToString() heap allocation (Rule 07)
            goldText.SetText("{0}", evt.CurrentGold);
        }
    }
}
