using UnityEngine;
using UnityEngine.EventSystems;

// =============================================================================
// HeroDragHandler.cs (Refactored — Phase 4)
// Handles drag-and-drop hero placement from HUD slot to grid.
//
// REMOVED: All GameManager.Instance and HeroSelector references (Rule 07).
// NOW: Reads hero prefab from the refactored HeroSlotUI which populates it
//      from the LineupFinalizedEvent. Drag is only allowed during Preparing
//      or Defending states (tracked via LevelStateChangedEvent).
//
// UI layer component — minimal gameplay coupling via LevelState tracking.
// =============================================================================

/// <summary>
/// Drag-and-drop handler for hero placement. Reads the hero prefab from
/// <see cref="HeroSlotUI.HeroPrefab"/> (populated via the draft lineup).
/// Only allows dragging during Preparing or Defending states (Rule 01).
/// </summary>
public class HeroDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private HeroSlotUI _slotUI;
    private GameObject _ghostObject;
    private SpriteRenderer _ghostRenderer;
    private GameObject _heroPrefab;
    private Camera _mainCamera;

    /// <summary>Current level state — tracked via event subscription.</summary>
    private LevelState _currentState;

    private void Awake()
    {
        _slotUI = GetComponent<HeroSlotUI>();
        _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
    }

    /// <summary>
    /// Tracks level state to gate drag-and-drop (Rule 01: placement only
    /// during Preparing or Defending states).
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        _currentState = evt.NewState;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Guard: only allow drag during Preparing or Defending
        if (_currentState != LevelState.Preparing && _currentState != LevelState.Defending)
            return;

        // Guard: slot must be initialized with lineup data
        if (_slotUI == null || !_slotUI.IsInitialized)
            return;

        _heroPrefab = _slotUI.HeroPrefab;
        if (_heroPrefab == null) return;

        // Create ghost sprite (60% opacity per Rule 02)
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f;

        _ghostObject = new GameObject("HeroGhost");
        _ghostObject.transform.localScale = _heroPrefab.transform.localScale;
        _ghostRenderer = _ghostObject.AddComponent<SpriteRenderer>();

        SpriteRenderer prefabRenderer = _heroPrefab.GetComponent<SpriteRenderer>();
        if (prefabRenderer != null)
        {
            _ghostRenderer.sprite = prefabRenderer.sprite;
            _ghostRenderer.sortingOrder = 100;
        }

        Color ghostColor = _ghostRenderer.color;
        ghostColor.a = 0.6f;
        _ghostRenderer.color = ghostColor;

        _ghostObject.transform.position = worldPos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_ghostObject == null) return;

        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f;
        _ghostObject.transform.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_ghostObject != null)
        {
            Destroy(_ghostObject);
            _ghostObject = null;
        }

        if (_heroPrefab == null) return;

        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(eventData.position);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f, LayerMask.GetMask("Terrain"));

        if (hit.collider != null)
        {
            TerrainCell cell = hit.collider.GetComponent<TerrainCell>();
            if (cell != null)
            {
                cell.PlaceHero(_heroPrefab);
            }
        }

        _heroPrefab = null;
    }
}
