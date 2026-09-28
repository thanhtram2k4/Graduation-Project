// =============================================================================
// EraType.cs
// Progression-era classification shared by HeroCardData and
// HistoricalQuestionData. Used to group heroes and Ông Bụt questions into the
// same era so progression can unlock / filter content per era.
//
// Value type — comparing, switching on, or passing EraType never allocates.
// Do not call ToString() or Enum.GetName() on it in per-frame code (Rule 07);
// map to display text through a pre-built lookup instead.
// =============================================================================

/// <summary>
/// The era a hero or historical question belongs to.
/// Serialized by index in assets, so new values must be appended at the end —
/// never reorder or insert, or existing assets will change era.
/// </summary>
public enum EraType
{
    /// <summary>Lịch sử — recorded dynastic history (e.g. nhà Trần, nhà Lý).</summary>
    History = 0,

    /// <summary>Thần thoại — creation myths and deities (e.g. Lạc Long Quân, Âu Cơ).</summary>
    Mythology = 1,

    /// <summary>Truyền thuyết — semi-historical legends (e.g. Thánh Gióng, An Dương Vương).</summary>
    Legend = 2,

    /// <summary>Cổ tích — folk fairy tales (e.g. Tấm Cám, Cây Tre Trăm Đốt).</summary>
    FairyTale = 3,

    /// <summary>Văn hóa — customs, festivals, and cultural heritage.</summary>
    Culture = 4
}
