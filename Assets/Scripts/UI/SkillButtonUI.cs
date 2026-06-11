using UnityEngine;
using UnityEngine.UI;

// =============================================================================
// SkillButtonUI — UI-Layer Skill Button with Cooldown Visual
//
// Attached to a UI Button GameObject. Publishes RequestEggShowerEvent when
// clicked and displays a cooldown overlay until the skill is ready again.
//
// CRITICAL SEPARATION (Rule 07 — UI-Gameplay):
//   - This script lives in the UI layer and NEVER references gameplay
//     MonoBehaviours (EggShowerManager, EconomyManager, etc.) directly.
//   - Communication is strictly event-driven:
//     • OUT: Publishes RequestEggShowerEvent on click.
//     • IN:  Subscribes to EggShowerActivatedEvent to start cooldown visual.
//   - The UI does NOT own the cooldown truth. The gameplay layer (EggShowerManager)
//     validates the request and only publishes EggShowerActivatedEvent if
//     the activation succeeds. The UI mirrors the confirmed cooldown.
//
// Rule 07: All subscriptions unregistered in OnDisable (no stale listeners).
// Rule 07: No string concat in Update. No per-frame allocation.
// =============================================================================

/// <summary>
/// UI button component for the "Egg Shower" board-level active skill.
/// Publishes <see cref="RequestEggShowerEvent"/> on click and displays a
/// cooldown overlay using <see cref="Image.fillAmount"/> radial fill.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class SkillButtonUI : MonoBehaviour
{
    // ── Inspector References ────────────────────────────────────────────────

    [Header("Cooldown Overlay")]

    [Tooltip("Image component used for the radial cooldown fill overlay. " +
             "Must be set to Image Type = Filled, Fill Method = Radial 360. " +
             "If null, the component on this GameObject is used.")]
    [SerializeField] private Image cooldownOverlay;

    [Header("Visual Feedback")]

    [Tooltip("Color applied to the button image when the skill is on cooldown.")]
    [SerializeField] private Color cooldownColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Tooltip("Color applied to the button image when the skill is ready.")]
    [SerializeField] private Color readyColor = Color.white;

    // ── Cached References ───────────────────────────────────────────────────
    private Button _button;
    private Image _buttonImage;

    // ── Runtime State ───────────────────────────────────────────────────────
    private float _cooldownTimer;
    private float _cooldownDuration;
    private bool _isOnCooldown;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _button = GetComponent<Button>();
        _buttonImage = GetComponent<Image>();

        // Default to ready state
        SetReadyVisual();
    }

    private void OnEnable()
    {
        // Subscribe to gameplay confirmation event (Rule 07 — event-driven UI)
        GameEventBus.OnEggShowerActivated += HandleEggShowerActivated;
    }

    private void OnDisable()
    {
        // Unregister to prevent stale listeners after scene transition (Rule 07)
        GameEventBus.OnEggShowerActivated -= HandleEggShowerActivated;
    }

    private void Update()
    {
        if (!_isOnCooldown) return;

        _cooldownTimer -= Time.deltaTime;

        if (_cooldownTimer <= 0f)
        {
            // Cooldown finished — restore ready state
            _cooldownTimer = 0f;
            _isOnCooldown = false;
            SetReadyVisual();
        }
        else
        {
            // Update radial fill overlay (fills clockwise as cooldown progresses)
            UpdateCooldownVisual();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLICK HANDLER — Publishes event, does NOT call gameplay directly
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the Button's OnClick UnityEvent (wired in the Inspector).
    /// Publishes a request event if the skill is not on cooldown.
    /// The gameplay layer validates and responds with
    /// <see cref="EggShowerActivatedEvent"/>.
    /// Must be public so it appears in the Inspector's UnityEvent dropdown.
    /// </summary>
    public void OnSkillButtonClicked()
    {
        Debug.Log("[SkillButtonUI] Button clicked!");

        if (_isOnCooldown) return;

        Debug.Log("[SkillButtonUI] Publishing RequestEggShowerEvent...");

        // Publish request — gameplay layer handles validation and spawning
        GameEventBus.Publish(new RequestEggShowerEvent());

        // Publish generic button click for UI SFX (Rule 08)
        GameEventBus.Publish(new ButtonClickEvent());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLER — Gameplay confirms activation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles the gameplay layer confirming that the Egg Shower was
    /// successfully activated. Starts the cooldown visual on the UI side.
    /// </summary>
    private void HandleEggShowerActivated(EggShowerActivatedEvent evt)
    {
        _cooldownDuration = evt.CooldownDuration;
        _cooldownTimer = evt.CooldownDuration;
        _isOnCooldown = true;
        SetCooldownVisual();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VISUAL STATE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the button to its ready (interactable) visual state.
    /// </summary>
    private void SetReadyVisual()
    {
        _buttonImage.color = readyColor;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.gameObject.SetActive(false);
        }

        _button.interactable = true;
    }

    /// <summary>
    /// Sets the button to its cooldown (non-interactable) visual state.
    /// </summary>
    private void SetCooldownVisual()
    {
        _buttonImage.color = cooldownColor;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(true);
            cooldownOverlay.fillAmount = 1f;
        }

        _button.interactable = false;
    }

    /// <summary>
    /// Updates the radial fill overlay during cooldown. Fill decreases from
    /// 1 (fully covered) to 0 (ready) as the cooldown progresses.
    /// </summary>
    private void UpdateCooldownVisual()
    {
        if (cooldownOverlay != null && _cooldownDuration > 0f)
        {
            cooldownOverlay.fillAmount = _cooldownTimer / _cooldownDuration;
        }
    }
}
