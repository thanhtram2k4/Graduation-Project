using UnityEngine;

// =============================================================================
// ResourcePickup — Single Responsibility: Collectible Resource (Dragon Egg)
//
// Attached to resource prefabs spawned by ResourceGeneratorComponent.
// Player clicks to collect Gold via EconomyManager. Releases back to pool
// via ObjectPoolManager — no Destroy() (Rule 07).
//
// Uses OnMouseDown for click detection (requires Collider2D on prefab).
// Includes a lifetime timer to auto-release uncollected resources.
//
// Rule 07: Zero-alloc where possible. No string concat in Update.
// Rule 07: Object pooling mandatory — Release, never Destroy.
// Rule 01: Gold added via EconomyManager.AddGold (event-driven).
// =============================================================================

/// <summary>
/// Collectible resource pickup that grants Gold when clicked by the player.
/// Spawned by <see cref="ResourceGeneratorComponent"/>, returned to pool
/// on collection or after its lifetime expires.
/// </summary>
public class ResourcePickup : MonoBehaviour
{
    // ── Configuration ───────────────────────────────────────────────────────
    private int _goldAmount;

    // ── Lifetime auto-release (prevents memory bloat) ───────────────────────
    private const float MAX_LIFETIME = 10f;
    private float _lifetimeTimer;

    // ── Jump arc motion ─────────────────────────────────────────────────────
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _jumpHeight;
    private float _jumpDuration;
    private float _jumpTimer;
    private bool _isJumping;

    // ── State ───────────────────────────────────────────────────────────────
    private bool _isCollected;
    private bool _isInitialized;

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the pickup with its Gold value and jump arc parameters.
    /// Called by <see cref="ResourceGeneratorComponent"/> on spawn.
    /// </summary>
    /// <param name="goldAmount">Gold granted to the player on collection.</param>
    /// <param name="targetPosition">World position to land at after the jump arc.</param>
    /// <param name="jumpHeight">Peak height of the arc above the start position.</param>
    /// <param name="jumpDuration">Duration of the jump arc in seconds.</param>
    public void Initialize(int goldAmount, Vector3 targetPosition, float jumpHeight, float jumpDuration)
    {
        _goldAmount = goldAmount;
        _targetPos = targetPosition;
        _jumpHeight = jumpHeight;
        _jumpDuration = jumpDuration;

        _startPos = transform.position;
        _jumpTimer = 0f;
        _isJumping = jumpDuration > 0f;
        _isCollected = false;
        _lifetimeTimer = MAX_LIFETIME;
        _isInitialized = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE — Jump arc + lifetime countdown
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isInitialized || _isCollected) return;

        // ── Jump arc motion ─────────────────────────────────────────────────
        if (_isJumping)
        {
            _jumpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_jumpTimer / _jumpDuration);

            // Parabolic arc: lerp XY linearly, add parabolic Y offset
            Vector3 currentPos = Vector3.Lerp(_startPos, _targetPos, t);
            float arcOffset = _jumpHeight * 4f * t * (1f - t); // peak at t=0.5
            currentPos.y += arcOffset;

            transform.position = currentPos;

            if (t >= 1f)
            {
                _isJumping = false;
                transform.position = _targetPos;
            }
        }

        // ── Lifetime countdown — auto-release if not collected ──────────────
        _lifetimeTimer -= Time.deltaTime;
        if (_lifetimeTimer <= 0f)
        {
            ReleaseToPool();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLICK DETECTION — Requires Collider2D on the prefab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Unity when the player clicks on this object's Collider2D.
    /// Grants Gold to the player and releases this pickup back to the pool.
    /// </summary>
    private void OnMouseDown()
    {
        if (_isCollected || !_isInitialized) return;

        _isCollected = true;

        // Grant Gold via EconomyManager (Rule 01)
        EconomyManager.Instance.AddGold(_goldAmount);

        // Publish event for AudioManager / UI feedback (Rule 08)
        GameEventBus.Publish(new ResourceCollectedEvent
        {
            GoldAmount = _goldAmount,
            Position = transform.position
        });

        ReleaseToPool();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POOL LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns this pickup to the ObjectPoolManager. Never calls Destroy (Rule 07).
    /// </summary>
    private void ReleaseToPool()
    {
        _isInitialized = false;
        ObjectPoolManager.Instance.Release(gameObject);
    }

    /// <summary>
    /// Reset state on pool release (OnDisable fires when SetActive(false)).
    /// Ensures clean state for the next pool-get cycle.
    /// </summary>
    private void OnDisable()
    {
        _isCollected = false;
        _isInitialized = false;
        _isJumping = false;
        _jumpTimer = 0f;
        _lifetimeTimer = 0f;
    }
}
