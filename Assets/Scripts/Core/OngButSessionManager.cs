using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// OngButSessionManager — Gameplay-Layer Controller for the Ông Bụt Q&A System
//
// Owns all runtime session state: current phase, score, drawn questions,
// selected skill. Orchestrates the UI state machine via GameEventBus events.
//
// LAYER: Game.Gameplay — never references UI components directly (Rule 07).
// PAUSE: Sets Time.timeScale = 0 on session start, restores to 1 on end.
//        All UI animations must use unscaled time during the session.
// SINGLE-USE: The Pagoda button can only be used once per level. State resets
//             on scene reload (not persisted to save data).
// =============================================================================

/// <summary>
/// Manages the Ông Bụt trivia session lifecycle: question drawing, answer
/// evaluation, score tracking, skill selection validation, and skill granting.
/// Communicates with the UI layer exclusively via <see cref="GameEventBus"/>.
/// </summary>
public class OngButSessionManager : MonoBehaviour
{
    // ── Singleton ───────────────────────────────────────────────────────────

    public static OngButSessionManager Instance { get; private set; }

    // ── Configuration ───────────────────────────────────────────────────────

    [Header("Configuration")]
    [Tooltip("Master config SO for the Ông Bụt system.")]
    [SerializeField] private OngButSessionConfig sessionConfig;

    // ── Runtime State ───────────────────────────────────────────────────────

    private bool _hasBeenUsedThisLevel;
    private OngButPhase _currentPhase = OngButPhase.Inactive;
    private List<HistoricalQuestionData> _drawnQuestions;
    private int _currentQuestionIndex;
    private int _correctAnswerCount;
    private OngButSkillData _selectedSkill;
    private OngButSkillData _grantedSkill;
    private bool _grantedSkillUsed;

    // ── Public Accessors ────────────────────────────────────────────────────

    /// <summary>Whether the Pagoda has been used this level.</summary>
    public bool HasBeenUsedThisLevel => _hasBeenUsedThisLevel;

    /// <summary>Current phase of the Ông Bụt session.</summary>
    public OngButPhase CurrentPhase => _currentPhase;

    /// <summary>Number of correct answers in the current session.</summary>
    public int CorrectAnswerCount => _correctAnswerCount;

    /// <summary>The granted skill (null if none granted).</summary>
    public OngButSkillData GrantedSkill => _grantedSkill;

    /// <summary>Whether the granted skill has been used.</summary>
    public bool GrantedSkillUsed => _grantedSkillUsed;

    /// <summary>Whether a granted skill is available and not yet used.</summary>
    public bool HasUsableSkill => _grantedSkill != null && !_grantedSkillUsed;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // UI → Gameplay subscriptions
        GameEventBus.OnPagodaActivated += HandlePagodaActivated;
        GameEventBus.OnOngButIntroAcknowledged += HandleIntroAcknowledged;
        GameEventBus.OnOngButAnswerSubmitted += HandleAnswerSubmitted;
        GameEventBus.OnOngButNextQuestionRequested += HandleNextQuestionRequested;
        GameEventBus.OnOngButSkillSelected += HandleSkillSelected;
        GameEventBus.OnOngButSkillConfirmed += HandleSkillConfirmed;
        GameEventBus.OnOngButSessionDone += HandleSessionDone;
        GameEventBus.OnOngButReturnRequested += HandleReturnRequested;
        GameEventBus.OnOngButSkillHUDActivated += HandleSkillHUDActivated;

        // External game events
        GameEventBus.OnDefeat += HandleDefeat;
    }

    private void OnDisable()
    {
        GameEventBus.OnPagodaActivated -= HandlePagodaActivated;
        GameEventBus.OnOngButIntroAcknowledged -= HandleIntroAcknowledged;
        GameEventBus.OnOngButAnswerSubmitted -= HandleAnswerSubmitted;
        GameEventBus.OnOngButNextQuestionRequested -= HandleNextQuestionRequested;
        GameEventBus.OnOngButSkillSelected -= HandleSkillSelected;
        GameEventBus.OnOngButSkillConfirmed -= HandleSkillConfirmed;
        GameEventBus.OnOngButSessionDone -= HandleSessionDone;
        GameEventBus.OnOngButReturnRequested -= HandleReturnRequested;
        GameEventBus.OnOngButSkillHUDActivated -= HandleSkillHUDActivated;

        GameEventBus.OnDefeat -= HandleDefeat;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PHASE TRANSITIONS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to a new phase and publishes the change event.
    /// </summary>
    private void TransitionToPhase(OngButPhase newPhase)
    {
        _currentPhase = newPhase;
        GameEventBus.Publish(new OngButPhaseChangedEvent { NewPhase = newPhase });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles the Pagoda button click. Validates conditions and starts session.
    /// </summary>
    private void HandlePagodaActivated(PagodaActivatedEvent evt)
    {
        // Guard: already used this level
        if (_hasBeenUsedThisLevel)
        {
            Debug.Log("[OngButSessionManager] Pagoda already used this level.");
            return;
        }

        // Guard: only during Defending state
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            Debug.Log("[OngButSessionManager] Cannot activate during game over.");
            return;
        }

        // Guard: no config
        if (sessionConfig == null || sessionConfig.QuestionBank == null)
        {
            Debug.LogError("[OngButSessionManager] SessionConfig or QuestionBank not assigned.", this);
            return;
        }

        // Mark as used
        _hasBeenUsedThisLevel = true;

        // Reset session state
        _currentQuestionIndex = 0;
        _correctAnswerCount = 0;
        _selectedSkill = null;

        // Draw questions
        _drawnQuestions = sessionConfig.QuestionBank.DrawRandomQuestions();

        if (_drawnQuestions.Count == 0)
        {
            Debug.LogError("[OngButSessionManager] No questions drawn from bank.", this);
            _hasBeenUsedThisLevel = false;
            return;
        }

        // Pause game
        Time.timeScale = 0f;
        GameEventBus.Publish(new GamePausedEvent());

        // Transition to Intro
        TransitionToPhase(OngButPhase.Intro);

        // Send intro data to UI
        GameEventBus.Publish(new OngButIntroDataEvent
        {
            DialogueText = sessionConfig.IntroDialogueText,
            RulesText = sessionConfig.RulesExplanationText,
            PortraitSprite = sessionConfig.OngButPortraitSprite
        });

        Debug.Log("[OngButSessionManager] Session started. Game paused.");
    }

    /// <summary>
    /// Handles "Understood" click from Intro panel. Starts Q&A phase.
    /// </summary>
    private void HandleIntroAcknowledged(OngButIntroAcknowledgedEvent evt)
    {
        if (_currentPhase != OngButPhase.Intro) return;

        TransitionToPhase(OngButPhase.Questioning);
        PublishCurrentQuestion();
    }

    /// <summary>
    /// Handles answer submission. Evaluates correctness and publishes result.
    /// </summary>
    private void HandleAnswerSubmitted(OngButAnswerSubmittedEvent evt)
    {
        if (_currentPhase != OngButPhase.Questioning) return;
        if (_currentQuestionIndex >= _drawnQuestions.Count) return;

        HistoricalQuestionData question = _drawnQuestions[_currentQuestionIndex];
        bool isCorrect = evt.SelectedIndex == question.CorrectAnswerIndex;

        if (isCorrect)
        {
            _correctAnswerCount++;
        }

        string specificFeedback = question.AnswerFeedbacks[evt.SelectedIndex];

        GameEventBus.Publish(new OngButAnswerResultEvent
        {
            IsCorrect = isCorrect,
            Feedback = specificFeedback,
            CorrectIndex = question.CorrectAnswerIndex,
            SelectedIndex = evt.SelectedIndex,
            CurrentScore = _correctAnswerCount
        });
    }

    /// <summary>
    /// Handles "Next" click after answer feedback. Advances or completes quiz.
    /// </summary>
    private void HandleNextQuestionRequested(OngButNextQuestionRequestedEvent evt)
    {
        if (_currentPhase != OngButPhase.Questioning) return;

        _currentQuestionIndex++;

        if (_currentQuestionIndex < _drawnQuestions.Count)
        {
            PublishCurrentQuestion();
        }
        else
        {
            // Quiz complete — transition to Result
            TransitionToPhase(OngButPhase.Result);

            GameEventBus.Publish(new OngButQuizCompletedEvent
            {
                CorrectAnswers = _correctAnswerCount,
                TotalQuestions = _drawnQuestions.Count
            });

            // Send result data to UI
            GameEventBus.Publish(new OngButResultDataEvent
            {
                CorrectAnswers = _correctAnswerCount,
                TotalQuestions = _drawnQuestions.Count,
                AvailableSkills = sessionConfig.AvailableSkills.ToArray(),
                ZeroScoreMessage = _correctAnswerCount == 0 ? sessionConfig.ZeroScoreMessage : null
            });
        }
    }

    /// <summary>
    /// Handles skill selection from the gallery. Validates affordability.
    /// </summary>
    private void HandleSkillSelected(OngButSkillSelectedEvent evt)
    {
        if (_currentPhase != OngButPhase.Result) return;
        if (evt.Skill == null) return;

        // Validate affordability
        if (evt.Skill.CorrectAnswerCost > _correctAnswerCount)
        {
            Debug.Log($"[OngButSessionManager] Skill '{evt.Skill.SkillName}' costs {evt.Skill.CorrectAnswerCost} but player only has {_correctAnswerCount}.");
            return;
        }

        _selectedSkill = evt.Skill;

        GameEventBus.Publish(new OngButSkillSelectionConfirmedEvent
        {
            Skill = _selectedSkill
        });
    }

    /// <summary>
    /// Handles "Confirm" click from the skill gallery. Locks in the skill.
    /// </summary>
    private void HandleSkillConfirmed(OngButSkillConfirmedEvent evt)
    {
        if (_currentPhase != OngButPhase.Result) return;
        if (_selectedSkill == null) return;

        // Double-check affordability
        if (_selectedSkill.CorrectAnswerCost > _correctAnswerCount)
        {
            Debug.LogWarning("[OngButSessionManager] Skill no longer affordable on confirm.");
            return;
        }

        _grantedSkill = _selectedSkill;

        // Transition to Success
        TransitionToPhase(OngButPhase.Success);

        // Send success data to UI
        string message = string.Format(sessionConfig.SuccessMessageTemplate, _grantedSkill.SkillName);
        GameEventBus.Publish(new OngButSuccessDataEvent
        {
            Message = message,
            SkillIcon = _grantedSkill.SkillIcon,
            PortraitSprite = sessionConfig.OngButPortraitSprite
        });
    }

    /// <summary>
    /// Handles "Done" click from the Success popup. Grants skill and resumes game.
    /// </summary>
    private void HandleSessionDone(OngButSessionDoneEvent evt)
    {
        if (_currentPhase != OngButPhase.Success) return;

        // Grant the skill
        GameEventBus.Publish(new OngButSkillGrantedEvent
        {
            SkillID = _grantedSkill.SkillID,
            SkillData = _grantedSkill
        });

        // Resume game
        ResumeGame();

        TransitionToPhase(OngButPhase.SkillReady);

        Debug.Log($"[OngButSessionManager] Skill '{_grantedSkill.SkillName}' granted. Game resumed.");
    }

    /// <summary>
    /// Handles "Return" click when player has 0 correct answers.
    /// </summary>
    private void HandleReturnRequested(OngButReturnRequestedEvent evt)
    {
        if (_currentPhase != OngButPhase.Result) return;

        // Resume without granting any skill
        ResumeGame();
        TransitionToPhase(OngButPhase.Inactive);

        Debug.Log("[OngButSessionManager] Player returned with 0 score. No skill granted.");
    }

    /// <summary>
    /// Handles the Ông Bụt skill HUD button click. Executes the granted skill.
    /// </summary>
    private void HandleSkillHUDActivated(OngButSkillHUDActivatedEvent evt)
    {
        if (!HasUsableSkill) return;

        // Execute the skill effect
        OngButSkillExecutor.Execute(_grantedSkill);
        _grantedSkillUsed = true;

        GameEventBus.Publish(new OngButSkillExecutedEvent
        {
            SkillID = _grantedSkill.SkillID
        });

        Debug.Log($"[OngButSessionManager] Ông Bụt skill '{_grantedSkill.SkillName}' executed.");
    }

    /// <summary>
    /// Handles defeat event. If Ông Bụt session is active, close it and resume time.
    /// </summary>
    private void HandleDefeat(DefeatEvent evt)
    {
        if (_currentPhase == OngButPhase.Inactive || _currentPhase == OngButPhase.SkillReady)
            return;

        // Force-close the session — defeat takes priority
        TransitionToPhase(OngButPhase.Inactive);

        // Time.timeScale will be set to 0 by GameManager.GameOver(),
        // so we don't need to resume here. The defeat UI handles the rest.
        Debug.Log("[OngButSessionManager] Defeat during active session. Closing Ông Bụt UI.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes the current question data for the UI to display.
    /// </summary>
    private void PublishCurrentQuestion()
    {
        if (_currentQuestionIndex >= _drawnQuestions.Count) return;

        GameEventBus.Publish(new OngButQuestionReadyEvent
        {
            Question = _drawnQuestions[_currentQuestionIndex],
            QuestionNumber = _currentQuestionIndex + 1,
            TotalQuestions = _drawnQuestions.Count
        });
    }

    /// <summary>
    /// Resumes the game by restoring Time.timeScale and publishing GameResumedEvent.
    /// </summary>
    private void ResumeGame()
    {
        Time.timeScale = 1f;
        GameEventBus.Publish(new GameResumedEvent());
    }
}
