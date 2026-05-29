using UnityEngine;

/// <summary>
/// Chỉ lưu trữ mảng heroPrefabs để các script khác (HeroDragHandler, HeroSlotUI) truy xuất.
/// </summary>
public class HeroSelector : MonoBehaviour
{
    public GameObject[] heroPrefabs;
}
