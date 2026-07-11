using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// OngButSessionConfig — Master configuration for the Ông Bụt Q&A system
//
// Single config asset holding references to the question bank, available
// skills, and all display text. Assigned to OngButSessionManager in Inspector.
// =============================================================================

/// <summary>
/// Master configuration ScriptableObject for the Ông Bụt trivia system.
/// References the question bank, available reward skills, and UI text.
/// </summary>
[CreateAssetMenu(fileName = "OngButConfig", menuName = "HaoKhiSuViet/OngBut/SessionConfig")]
public class OngButSessionConfig : ScriptableObject
{
    [Header("Question Bank")]
    [Tooltip("Reference to the question bank containing all trivia questions.")]
    [SerializeField] private QuestionBankData questionBank;

    [Header("Available Skills")]
    [Tooltip("All special skills the player can purchase in the skill gallery.")]
    [SerializeField] private List<OngButSkillData> availableSkills = new List<OngButSkillData>();

    [Header("UI Text (Vietnamese)")]
    [Tooltip("Ông Bụt's introduction speech.")]
    [TextArea(3, 6)]
    [SerializeField] private string introDialogueText = "Chào con, ta là Ông Bụt! Ta sẽ ban cho con một phép màu nếu con trả lời đúng các câu hỏi lịch sử.";

    [Tooltip("Rules explanation shown in the Intro panel.")]
    [TextArea(3, 6)]
    [SerializeField] private string rulesExplanationText = "Con sẽ trả lời 3 câu hỏi lịch sử. Số câu trả lời đúng sẽ là điểm để con đổi lấy kỹ năng đặc biệt.";

    [Tooltip("Success message template. Use {0} for skill name placeholder.")]
    [TextArea(2, 4)]
    [SerializeField] private string successMessageTemplate = "Chúc mừng!!! Điều ước kỹ năng đặc biệt \"{0}\" đã được Ông Bụt ban tặng!";

    [Tooltip("Message shown when player scores 0 and no skills are affordable.")]
    [TextArea(2, 4)]
    [SerializeField] private string zeroScoreMessage = "Ông Bụt thương cảm... Con chưa trả lời đúng câu nào. Hãy cố gắng lần sau nhé!";

    [Header("Visuals")]
    [Tooltip("Ông Bụt character portrait sprite.")]
    [SerializeField] private Sprite ongButPortraitSprite;

    [Tooltip("Pagoda button icon for the HUD.")]
    [SerializeField] private Sprite pagodaButtonIcon;

    // ── Public Accessors ────────────────────────────────────────────────────

    /// <summary>Question bank containing all trivia questions.</summary>
    public QuestionBankData QuestionBank => questionBank;

    /// <summary>All purchasable reward skills.</summary>
    public List<OngButSkillData> AvailableSkills => availableSkills;

    /// <summary>Introduction dialogue text.</summary>
    public string IntroDialogueText => introDialogueText;

    /// <summary>Rules explanation text.</summary>
    public string RulesExplanationText => rulesExplanationText;

    /// <summary>Success message template (use string.Format with skill name).</summary>
    public string SuccessMessageTemplate => successMessageTemplate;

    /// <summary>Message for zero-score result.</summary>
    public string ZeroScoreMessage => zeroScoreMessage;

    /// <summary>Ông Bụt portrait sprite.</summary>
    public Sprite OngButPortraitSprite => ongButPortraitSprite;

    /// <summary>Pagoda HUD button icon.</summary>
    public Sprite PagodaButtonIcon => pagodaButtonIcon;

    private void OnValidate()
    {
        if (questionBank == null)
            Debug.LogWarning("[OngButSessionConfig] questionBank is not assigned.", this);

        if (availableSkills.Count == 0)
            Debug.LogWarning("[OngButSessionConfig] No skills configured.", this);
    }
}
