using UnityEngine;

// =============================================================================
// HistoricalQuestionData — ScriptableObject for a single trivia question
//
// Part of the Ông Bụt (Fairy God-Grandfather) Q&A system.
// Each asset represents one historical question with 4 answer options.
// All text content is in Vietnamese (Rule 11 — Cultural Integration).
//
// Data-driven: no question content is hard-coded in runtime scripts (Rule 07).
// =============================================================================

/// <summary>
/// Defines a single historical trivia question for the Ông Bụt Q&A system.
/// Contains the question text, four answer options, the correct answer index,
/// and a historical explanation shown after the player answers.
/// </summary>
[CreateAssetMenu(fileName = "Question_New", menuName = "HaoKhiSuViet/OngBut/HistoricalQuestion")]
public class HistoricalQuestionData : ScriptableObject
{
    // ── Identity ────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Unique identifier (e.g. 'Q_BachDang_01').")]
    [SerializeField] private string questionID;

    // ── Question Content ────────────────────────────────────────────────────

    [Header("Question Content")]
    [Tooltip("The trivia question text in Vietnamese.")]
    [TextArea(3, 6)]
    [SerializeField] private string questionText;

    [Tooltip("Exactly 4 answer options. Order matters — correctAnswerIndex references this array.")]
    [SerializeField] private string[] answers = new string[4];

    [Tooltip("Index (0–3) of the correct answer in the answers array.")]
    [Range(0, 3)]
    [SerializeField] private int correctAnswerIndex;

    // ── Feedback ────────────────────────────────────────────────────────────

    [Header("Feedback")]
    [Tooltip("Individual feedback for each answer option (Vietnamese). Index must match the answers array.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] answerFeedbacks = new string[4];

    // ── Difficulty ──────────────────────────────────────────────────────────

    [Header("Difficulty")]
    [Tooltip("Optional difficulty tier for future scaling (1 = easy, 3 = hard).")]
    [Range(1, 3)]
    [SerializeField] private int difficultyTier = 1;

    // ── Public Accessors ────────────────────────────────────────────────────

    /// <summary>Unique question identifier.</summary>
    public string QuestionID => questionID;

    /// <summary>The trivia question text in Vietnamese.</summary>
    public string QuestionText => questionText;

    /// <summary>Four answer options.</summary>
    public string[] Answers => answers;

    /// <summary>Index (0–3) of the correct answer.</summary>
    public int CorrectAnswerIndex => correctAnswerIndex;

    /// <summary>Per-answer feedback strings (length always 4).</summary>
    public string[] AnswerFeedbacks => answerFeedbacks;

    /// <summary>Difficulty tier (1–3).</summary>
    public int DifficultyTier => difficultyTier;

    // ── Validation ──────────────────────────────────────────────────────────

    private void OnValidate()
    {
        if (answers == null || answers.Length != 4)
        {
            Debug.LogWarning($"[HistoricalQuestionData] '{name}': answers array must have exactly 4 entries.", this);
            if (answers == null)
                answers = new string[4];
            else if (answers.Length != 4)
                System.Array.Resize(ref answers, 4);
        }

        if (answerFeedbacks == null || answerFeedbacks.Length != 4)
        {
            Debug.LogWarning($"[HistoricalQuestionData] '{name}': answerFeedbacks array must have exactly 4 entries.", this);
            if (answerFeedbacks == null)
                answerFeedbacks = new string[4];
            else if (answerFeedbacks.Length != 4)
                System.Array.Resize(ref answerFeedbacks, 4);
        }

        if (correctAnswerIndex < 0 || correctAnswerIndex > 3)
        {
            Debug.LogWarning($"[HistoricalQuestionData] '{name}': correctAnswerIndex must be 0–3.", this);
            correctAnswerIndex = Mathf.Clamp(correctAnswerIndex, 0, 3);
        }

        if (string.IsNullOrEmpty(questionText))
        {
            Debug.LogWarning($"[HistoricalQuestionData] '{name}': questionText is empty.", this);
        }

        if (string.IsNullOrEmpty(questionID))
        {
            Debug.LogWarning($"[HistoricalQuestionData] '{name}': questionID is empty.", this);
        }
    }
}
