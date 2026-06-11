using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// BaseHealthUI.cs — Event-Driven Base HP Display
//
// LAYER: UI (Game.UI)
// RULES SATISFIED:
//   - Rule 07 (Event-Driven UI): Contains NO Update() polling. Subscribes
//                                to BaseTakeDamageEvent reactive pipeline.
//   - Rule 07 (UI-Gameplay Separation): NO direct references to gameplay components.
//   - Rule 07 (GC Prevention): Uses TextMeshPro's SetText overload to bypass GC.
// =============================================================================

/// <summary>
/// Reactive UI display for the player's Base HP.
/// Subscribes to <see cref="BaseTakeDamageEvent"/> via <see cref="GameEventBus"/>
/// and updates a fill-based HP bar. No <c>Update()</c> polling (Rule 07).
/// </summary>
public class BaseHealthUI : MonoBehaviour
{
    [Header("HP Bar — Choose ONE method")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Slider hpSlider;

    [Header("Optional")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Visual Feedback")]
    [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    private void OnEnable()
    {
        GameEventBus.OnBaseTakeDamage += HandleBaseTakeDamage;
    }

    private void OnDisable()
    {
        GameEventBus.OnBaseTakeDamage -= HandleBaseTakeDamage;
    }

    private void HandleBaseTakeDamage(BaseTakeDamageEvent evt)
    {
        float fraction = evt.MaxHP > 0 ? (float)evt.CurrentHP / evt.MaxHP : 0f;

        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = fraction;
            hpFillImage.color = EvaluateColor(fraction);
        }

        if (hpSlider != null)
        {
            hpSlider.maxValue = 1f;
            hpSlider.value = fraction;

            Image fillRect = hpSlider.fillRect != null ? hpSlider.fillRect.GetComponent<Image>() : null;
            if (fillRect != null)
            {
                fillRect.color = EvaluateColor(fraction);
            }
        }

        if (hpText != null)
        {
            hpText.SetText("{0} / {1}", evt.CurrentHP, evt.MaxHP);
        }
    }

    private Color EvaluateColor(float fraction)
    {
        if (fraction <= lowHealthThreshold) return lowHealthColor;

        float t = (fraction - lowHealthThreshold) / (1f - lowHealthThreshold);
        return Color.Lerp(lowHealthColor, fullHealthColor, t);
    }
}
