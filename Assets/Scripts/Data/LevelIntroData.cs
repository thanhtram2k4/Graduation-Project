using UnityEngine;

// =============================================================================
// LevelIntroData.cs
// ScriptableObject for the pre-match narrative intro screen content.
//
// Keeps narrative/display data separate from LevelConfig to maintain
// UI-Gameplay separation (Rule 07). Referenced by LevelIntroUI via
// [SerializeField] — a data asset reference, not a gameplay MonoBehaviour.
// =============================================================================

/// <summary>
/// Data asset containing narrative content for the level intro screen.
/// One asset per level, stored under Assets/Data/LevelIntros/.
/// </summary>
[CreateAssetMenu(fileName = "NewLevelIntro", menuName = "HKSV/Data/Level Intro")]
public class LevelIntroData : ScriptableObject
{
    [Header("Display")]

    [Tooltip("Level name shown prominently on the intro screen (e.g. 'Trận Bạch Đằng Giang').")]
    public string levelDisplayName;

    [Tooltip("Narrative text describing the historical context of the battle.\n" +
             "Supports full Vietnamese Unicode. Written in the style of a\n" +
             "historical chronicle (chính sử) per Rule 11.")]
    [TextArea(4, 8)]
    public string narrativeText;

    [Tooltip("Optional background image for the intro screen.\n" +
             "Leave None to use the default background.")]
    public Sprite backgroundSprite;

    [Tooltip("Name of the enemy faction for this level (e.g. 'Quân Nguyên Mông').\n" +
             "Must be drawn from actual historical adversaries per Rule 11.")]
    public string factionName;
}
