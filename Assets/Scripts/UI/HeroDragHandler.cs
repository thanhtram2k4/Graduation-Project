using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gắn vào mỗi UI Button thẻ bài tướng. Xử lý kéo-thả tướng từ UI xuống TerrainCell.
/// </summary>
public class HeroDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private HeroSlotUI slotUI;
    private GameObject ghostObject;
    private SpriteRenderer ghostRenderer;
    private GameObject heroPrefab;
    private Camera mainCamera;

    private void Awake()
    {
        slotUI = GetComponent<HeroSlotUI>();
        mainCamera = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;

        HeroSelector heroSelector = GameManager.Instance.GetComponent<HeroSelector>();
        if (heroSelector == null) return;

        int index = slotUI.slotIndex;
        if (index < 0 || index >= heroSelector.heroPrefabs.Length) return;

        heroPrefab = heroSelector.heroPrefabs[index];
        if (heroPrefab == null) return;

        // Tạo ảnh ảo (Ghost) chỉ có SpriteRenderer, không có Collider
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f;

        ghostObject = new GameObject("HeroGhost");
        ghostObject.transform.localScale = heroPrefab.transform.localScale;
        ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();

        SpriteRenderer prefabRenderer = heroPrefab.GetComponent<SpriteRenderer>();
        if (prefabRenderer != null)
        {
            ghostRenderer.sprite = prefabRenderer.sprite;
            ghostRenderer.sortingOrder = 100;
        }

        // Alpha = 0.6f
        Color ghostColor = ghostRenderer.color;
        ghostColor.a = 0.6f;
        ghostRenderer.color = ghostColor;

        ghostObject.transform.position = worldPos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostObject == null) return;

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f;
        ghostObject.transform.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }

        if (heroPrefab == null) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(eventData.position);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f, LayerMask.GetMask("Terrain"));

        if (hit.collider != null)
        {
            TerrainCell cell = hit.collider.GetComponent<TerrainCell>();
            if (cell != null)
            {
                cell.PlaceHero(heroPrefab);
            }
        }

        heroPrefab = null;
    }
}
