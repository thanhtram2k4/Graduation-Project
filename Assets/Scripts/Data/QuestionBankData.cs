using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// QuestionBankData — Pool of HistoricalQuestionData assets
//
// Holds a collection of trivia questions and provides a Fisher-Yates partial
// shuffle to draw N non-repeating questions of a single era per Ông Bụt
// session. Same shuffle pattern as the hero draft system (Rule 05).
// =============================================================================

/// <summary>
/// ScriptableObject containing a pool of <see cref="HistoricalQuestionData"/>
/// assets. Provides <see cref="GetQuestionsByEra(EraType, int)"/> to select
/// non-repeating questions of one era using a Fisher-Yates partial shuffle.
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

    // Reused shuffle buffer so repeated draws don't allocate. Runtime-only.
    [System.NonSerialized] private List<HistoricalQuestionData> _eraPoolBuffer;

    /// <summary>
    /// Draws up to <paramref name="count"/> non-repeating questions whose
    /// <see cref="HistoricalQuestionData.Era"/> equals <paramref name="currentEra"/>.
    /// Allocates the returned list; prefer the overload taking a results list
    /// when calling repeatedly.
    /// </summary>
    /// <param name="currentEra">Only questions from this era are drawn.</param>
    /// <param name="count">Number of questions to draw. Clamped to the era's pool size.</param>
    /// <returns>A new list of randomly selected questions; empty if the era has none.</returns>
    public List<HistoricalQuestionData> GetQuestionsByEra(EraType currentEra, int count)
    {
        var results = new List<HistoricalQuestionData>(Mathf.Max(count, 0));
        GetQuestionsByEra(currentEra, count, results);
        return results;
    }

    /// <summary>
    /// Clears <paramref name="results"/> and fills it with up to <paramref name="count"/>
    /// non-repeating questions from <paramref name="currentEra"/>, using a
    /// Fisher-Yates partial shuffle. Does not modify the source list or allocate
    /// once the internal buffer has grown to the pool size.
    /// </summary>
    /// <param name="currentEra">Only questions from this era are drawn.</param>
    /// <param name="count">Number of questions to draw. Clamped to the era's pool size.</param>
    /// <param name="results">Caller-owned list that receives the drawn questions.</param>
    /// <returns>Number of questions drawn.</returns>
    public int GetQuestionsByEra(EraType currentEra, int count, List<HistoricalQuestionData> results)
    {
        results.Clear();

        if (_eraPoolBuffer == null)
            _eraPoolBuffer = new List<HistoricalQuestionData>(questions.Count);
        _eraPoolBuffer.Clear();

        for (int i = 0; i < questions.Count; i++)
        {
            HistoricalQuestionData question = questions[i];
            if (question != null && question.Era == currentEra)
                _eraPoolBuffer.Add(question);
        }

        if (_eraPoolBuffer.Count == 0)
        {
            Debug.LogError($"[QuestionBankData] No questions for era {currentEra}.", this);
            return 0;
        }

        int drawCount = Mathf.Min(count, _eraPoolBuffer.Count);
        if (drawCount < count)
        {
            Debug.LogWarning($"[QuestionBankData] Requested {count} {currentEra} questions " +
                             $"but only {_eraPoolBuffer.Count} available.", this);
        }

        // Fisher-Yates partial shuffle — O(drawCount)
        for (int i = 0; i < drawCount; i++)
        {
            int swapIndex = Random.Range(i, _eraPoolBuffer.Count);

            HistoricalQuestionData temp = _eraPoolBuffer[i];
            _eraPoolBuffer[i] = _eraPoolBuffer[swapIndex];
            _eraPoolBuffer[swapIndex] = temp;

            results.Add(_eraPoolBuffer[i]);
        }

        _eraPoolBuffer.Clear();
        return drawCount;
    }

    private void OnValidate()
    {
        if (questionsPerSession < 1)
        {
            Debug.LogWarning("[QuestionBankData] questionsPerSession must be >= 1.", this);
            questionsPerSession = 1;
        }

        // Check for nulls and count questions per era
        var eraCounts = new int[System.Enum.GetValues(typeof(EraType)).Length];
        for (int i = 0; i < questions.Count; i++)
        {
            if (questions[i] == null)
            {
                Debug.LogWarning($"[QuestionBankData] Null entry at index {i}.", this);
                continue;
            }

            int eraIndex = (int)questions[i].Era;
            if (eraIndex >= 0 && eraIndex < eraCounts.Length)
                eraCounts[eraIndex]++;
        }

        // Sessions draw from one era only, so each non-empty era needs a full session's worth.
        for (int era = 0; era < eraCounts.Length; era++)
        {
            if (eraCounts[era] > 0 && eraCounts[era] < questionsPerSession)
            {
                Debug.LogWarning($"[QuestionBankData] Era {(EraType)era} has {eraCounts[era]} questions " +
                                 $"< questionsPerSession ({questionsPerSession}). " +
                                 "Sessions in that era will draw fewer questions.", this);
            }
        }
    }
}
