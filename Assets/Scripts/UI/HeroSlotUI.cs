using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gắn vào mỗi UI Button đại diện cho một thẻ bài tướng trên Canvas.
/// Tự động lấy icon và cost từ heroPrefabs theo slotIndex.
/// </summary>
public class HeroSlotUI : MonoBehaviour
{
    [Header("Slot Config")]
    public int slotIndex;

    [Header("UI References")]
    [SerializeField] private Image heroIcon;
    [SerializeField] private TextMeshProUGUI costText;

    private void Start()
    {
        InitSlot();
    }

    private void InitSlot()
    {
        // Kiểm tra GameManager tồn tại
        if (GameManager.Instance == null)
        {
            Debug.LogWarning($"[HeroSlotUI] GameManager.Instance is null. Slot {slotIndex} cannot initialize.");
            return;
        }

        HeroSelector heroSelector = GameManager.Instance.GetComponent<HeroSelector>();
        if (heroSelector == null)
        {
            Debug.LogWarning($"[HeroSlotUI] HeroSelector not found on GameManager. Slot {slotIndex} cannot initialize.");
            return;
        }

        // Kiểm tra slotIndex hợp lệ
        if (slotIndex < 0 || slotIndex >= heroSelector.heroPrefabs.Length)
        {
            Debug.LogWarning($"[HeroSlotUI] slotIndex {slotIndex} out of range (heroPrefabs length: {heroSelector.heroPrefabs.Length}).");
            return;
        }

        GameObject heroPrefab = heroSelector.heroPrefabs[slotIndex];
        if (heroPrefab == null)
        {
            Debug.LogWarning($"[HeroSlotUI] heroPrefabs[{slotIndex}] is null.");
            return;
        }

        // Lấy cost từ Hero component
        Hero hero = heroPrefab.GetComponent<Hero>();
        if (hero != null && costText != null)
        {
            costText.text = hero.cost.ToString();
        }
        else if (hero == null)
        {
            Debug.LogWarning($"[HeroSlotUI] Hero component not found on prefab '{heroPrefab.name}'.");
        }

        // Lấy sprite từ SpriteRenderer và gán vào Image
        SpriteRenderer spriteRenderer = heroPrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && heroIcon != null)
        {
            heroIcon.sprite = spriteRenderer.sprite;
        }
        else if (spriteRenderer == null)
        {
            Debug.LogWarning($"[HeroSlotUI] SpriteRenderer not found on prefab '{heroPrefab.name}'.");
        }
    }
}
