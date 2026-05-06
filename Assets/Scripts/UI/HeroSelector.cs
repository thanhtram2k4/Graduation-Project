using UnityEngine;

public class HeroSelector : MonoBehaviour
{
    public GameObject[] heroPrefabs;
    private GameObject selectedHeroPrefab;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (selectedHeroPrefab == null) return;
        if (GameManager.Instance.isGameOver) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Terrain"));

            if (hit.collider != null)
            {
                TerrainCell cell = hit.collider.GetComponent<TerrainCell>();
                if (cell != null && cell.PlaceHero(selectedHeroPrefab))
                {
                    selectedHeroPrefab = null; // Bỏ chọn sau khi đặt
                }
            }
        }

        // Nhấn chuột phải để hủy chọn
        if (Input.GetMouseButtonDown(1))
        {
            selectedHeroPrefab = null;
        }
    }

    // Gọi từ UI Button
    public void SelectHero(int index)
    {
        if (index >= 0 && index < heroPrefabs.Length)
        {
            selectedHeroPrefab = heroPrefabs[index];
        }
    }
}
