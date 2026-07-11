using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// QuestionBankData — Pool of HistoricalQuestionData assets
//
// Holds a collection of trivia questions and provides a Fisher-Yates partial
// shuffle to draw N non-repeating questions per Ông Bụt session.
// Same shuffle pattern as the hero draft system (Rule 05).
// =============================================================================

/// <summary>
/// ScriptableObject containing a pool of <see cref="HistoricalQuestionData"/>
/// assets. Provides <see cref="DrawRandomQuestions"/> to select non-repeating
/// questions using a Fisher-Yates partial shuffle.
/// </summary>
[CreateAssetMenu(fileName = "QuestionBank_New", menuName = "HaoKhiSuViet/OngBut/QuestionBank")]
public class QuestionBankData : ScriptableObject
{
    [Header("Question Pool")]
    [Tooltip("All available questions in this bank. Must have at least questionsPerSession entries.")]
    [SerializeField] private List<HistoricalQuestionData> questions = new List<HistoricalQuestionData>();

    [Header("Session Settings")]
    [Tooltip("Number of questions drawn per Ông Bụt session.")]
    [SerializeField] private int questionsPerSession = 3;

    /// <summary>Number of questions in the bank.</summary>
    public int TotalQuestions => questions.Count;

    /// <summary>Number of questions drawn per session.</summary>
    public int QuestionsPerSession => questionsPerSession;

    /// <summary>
    /// Draws <paramref name="count"/> non-repeating questions from the bank
    /// using a Fisher-Yates partial shuffle. Does not modify the source list.
    /// Called once per session — not a per-frame operation.
    /// </summary>
    /// <param name="count">Number of questions to draw. Clamped to pool size.</param>
    /// <returns>A new list of randomly selected questions.</returns>
    public List<HistoricalQuestionData> DrawRandomQuestions(int count)
    {
        if (questions.Count == 0)
        {
            Debug.LogError("[QuestionBankData] Question pool is empty.", this);
            return new List<HistoricalQuestionData>();
        }

        int drawCount = Mathf.Min(count, questions.Count);
        if (drawCount < count)
        {
            Debug.LogWarning($"[QuestionBankData] Requested {count} questions but only {questions.Count} available.", this);
        }

        // Create a working copy to shuffle without modifying the asset
        var pool = new List<HistoricalQuestionData>(questions);
        var result = new List<HistoricalQuestionData>(drawCount);

        // Fisher-Yates partial shuffle — O(drawCount)
        for (int i = 0; i < drawCount; i++)
        {
            int swapIndex = Random.Range(i, pool.Count);

            // Swap
            HistoricalQuestionData temp = pool[i];
            pool[i] = pool[swapIndex];
            pool[swapIndex] = temp;

            result.Add(pool[i]);
        }

        return result;
    }

    /// <summary>
    /// Draws the default number of questions (<see cref="QuestionsPerSession"/>).
    /// </summary>
    public List<HistoricalQuestionData> DrawRandomQuestions()
    {
        return DrawRandomQuestions(questionsPerSession);
    }

    private void OnValidate()
    {
        if (questionsPerSession < 1)
        {
            Debug.LogWarning("[QuestionBankData] questionsPerSession must be >= 1.", this);
            questionsPerSession = 1;
        }

        if (questions.Count > 0 && questions.Count < questionsPerSession)
        {
            Debug.LogWarning($"[QuestionBankData] Pool size ({questions.Count}) < questionsPerSession ({questionsPerSession}). " +
                             "Some sessions will draw fewer questions.", this);
        }

        // Check for nulls
        for (int i = 0; i < questions.Count; i++)
        {
            if (questions[i] == null)
            {
                Debug.LogWarning($"[QuestionBankData] Null entry at index {i}.", this);
            }
        }
    }
}
