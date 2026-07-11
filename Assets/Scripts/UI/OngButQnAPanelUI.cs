using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButQnAPanelUI — Q&A Phase Panel for the Ông Bụt Trivia System
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// Displays questions, handles answer selection, shows feedback with
// historical explanation, and provides "Next" navigation.
//
// All text uses TMP for Vietnamese Unicode support (Rule 11).
// =============================================================================

/// <summary>
/// UI controller for the Ông Bụt Q&A panel. Subscribes to
/// <see cref="OngButQuestionReadyEvent"/> and <see cref="OngButAnswerResultEvent"/>
/// to populate questions and show feedback. Publishes
/// <see cref="OngButAnswerSubmittedEvent"/> and <see cref="OngButNextQuestionRequestedEvent"/>.
/// </summary>
public class OngButQnAPanelUI : MonoBehaviour
{
    // ── Header / Counter ────────────────────────────────────────────────────

    [Header("Header")]
    [Tooltip("Question counter label (e.g. 'Câu 1/3').")]
    [SerializeField] private TextMeshProUGUI questionCounterText;

    [Tooltip("Running score tracker (e.g. 'Đúng: 0').")]
    [SerializeField] private TextMeshProUGUI scoreTrackerText;

    // ── Question Area ───────────────────────────────────────────────────────

    [Header("Question Area")]
    [Tooltip("The question text display.")]
    [SerializeField] private TextMeshProUGUI questionText;


    // ── Answer Buttons ──────────────────────────────────────────────────────

    [Header("Answer Buttons")]
    [Tooltip("Exactly 4 answer buttons in order (A, B, C, D).")]
    [SerializeField] private Button[] answerButtons = new Button[4];

    [Tooltip("Text components on each answer button.")]
    [SerializeField] private TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[4];

    // ── Feedback Area ───────────────────────────────────────────────────────

    [Header("Feedback Area")]
    [Tooltip("Container for feedback elements (hidden until answered).")]
    [SerializeField] private GameObject feedbackArea;

    [Tooltip("Result label ('Chính xác!' or 'Sai rồi!').")]
    [SerializeField] private TextMeshProUGUI resultLabel;

    [Tooltip("Historical explanation text.")]
    [SerializeField] private TextMeshProUGUI explanationText;

    [Tooltip("'Tiếp theo' (Next) / 'Xem kết quả' button.")]
    [SerializeField] private Button nextButton;

    [Tooltip("Text on the Next button (changes on last question).")]
    [SerializeField] private TextMeshProUGUI nextButtonText;

    // ── Visual Feedback Colors ──────────────────────────────────────────────

    [Header("Colors")]
    [Tooltip("Background color for the correct answer button.")]
    [SerializeField] private Color correctColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    [Tooltip("Background color for the wrong selected answer.")]
    [SerializeField] private Color wrongColor = new Color(1f, 0.41f, 0.71f, 1f);

    [Tooltip("Default button color.")]
    [SerializeField] private Color defaultColor = Color.white;

    // ── Runtime State ───────────────────────────────────────────────────────

    private int _totalQuestions;
    private int _currentQuestionNumber;
    private bool _hasAnswered;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnOngButQuestionReady += HandleQuestionReady;
        GameEventBus.OnOngButAnswerResult += HandleAnswerResult;
    }

    private void OnDisable()
    {
        GameEventBus.OnOngButQuestionReady -= HandleQuestionReady;
        GameEventBus.OnOngButAnswerResult -= HandleAnswerResult;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the panel with a new question.
    /// </summary>
    private void HandleQuestionReady(OngButQuestionReadyEvent evt)
    {
        _currentQuestionNumber = evt.QuestionNumber;
        _totalQuestions = evt.TotalQuestions;
        _hasAnswered = false;

        // Update counter
        if (questionCounterText != null)
            questionCounterText.text = string.Concat("Câu ", evt.QuestionNumber.ToString(), "/", evt.TotalQuestions.ToString());

        // Populate question
        if (questionText != null)
            questionText.text = evt.Question.QuestionText;

        // Populate answer buttons
        string[] answers = evt.Question.Answers;
        for (int i = 0; i < 4; i++)
        {
            if (i < answerButtons.Length && answerButtons[i] != null)
            {
                answerButtons[i].interactable = true;
                SetButtonColor(answerButtons[i], defaultColor);
            }

            if (i < answerTexts.Length && answerTexts[i] != null && i < answers.Length)
            {
                answerTexts[i].text = answers[i];
            }
        }

        // Hide feedback area
        if (feedbackArea != null) feedbackArea.SetActive(false);
    }

    /// <summary>
    /// Shows feedback after the player answers.
    /// </summary>
    private void HandleAnswerResult(OngButAnswerResultEvent evt)
    {
        _hasAnswered = true;

        // Disable all answer buttons
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] != null)
                answerButtons[i].interactable = false;
        }

        // Highlight correct answer in green
        if (evt.CorrectIndex < answerButtons.Length && answerButtons[evt.CorrectIndex] != null)
        {
            SetButtonColor(answerButtons[evt.CorrectIndex], correctColor);
        }

        // If wrong, highlight the selected wrong answer in hot pink
        if (!evt.IsCorrect && evt.SelectedIndex < answerButtons.Length && answerButtons[evt.SelectedIndex] != null)
        {
            SetButtonColor(answerButtons[evt.SelectedIndex], wrongColor);
        }

        // Show feedback area
        if (feedbackArea != null) feedbackArea.SetActive(true);

        // Result label
        if (resultLabel != null)
        {
            resultLabel.text = evt.IsCorrect ? "Chính xác!" : "Sai rồi!";
            resultLabel.color = evt.IsCorrect ? correctColor : wrongColor;
        }

        // Per-answer feedback
        if (explanationText != null)
            explanationText.text = evt.Feedback;

        // Update score tracker
        if (scoreTrackerText != null)
            scoreTrackerText.text = string.Concat("Đúng: ", evt.CurrentScore.ToString());

        // Next button text (last question shows different text)
        if (nextButtonText != null)
        {
            nextButtonText.text = (_currentQuestionNumber >= _totalQuestions)
                ? "Xem kết quả"
                : "Tiếp theo";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BUTTON HANDLERS (wired via Inspector UnityEvent)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when answer button 0 (A) is clicked.
    /// </summary>
    public void OnAnswer0Clicked() { SubmitAnswer(0); }

    /// <summary>
    /// Called when answer button 1 (B) is clicked.
    /// </summary>
    public void OnAnswer1Clicked() { SubmitAnswer(1); }

    /// <summary>
    /// Called when answer button 2 (C) is clicked.
    /// </summary>
    public void OnAnswer2Clicked() { SubmitAnswer(2); }

    /// <summary>
    /// Called when answer button 3 (D) is clicked.
    /// </summary>
    public void OnAnswer3Clicked() { SubmitAnswer(3); }

    /// <summary>
    /// Called when the "Next" / "Xem kết quả" button is clicked.
    /// </summary>
    public void OnNextClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButNextQuestionRequestedEvent());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes the answer selection event if the player hasn't answered yet.
    /// </summary>
    private void SubmitAnswer(int index)
    {
        if (_hasAnswered) return;

        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButAnswerSubmittedEvent { SelectedIndex = index });
    }

    /// <summary>
    /// Sets the background color of a button via its Image component.
    /// </summary>
    private void SetButtonColor(Button button, Color color)
    {
        Image img = button.GetComponent<Image>();
        if (img != null)
            img.color = color;
    }
}
