using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HaoKhiSuViet.Tests
{
    /// <summary>Options for <see cref="OngButTestRig.Create"/>.</summary>
    public sealed class OngButRigOptions
    {
        /// <summary>Available heroes created per era.</summary>
        public int HeroesPerEra = 2;

        /// <summary>Questions created per era that has questions.</summary>
        public int QuestionsPerEra = 4;

        /// <summary>QuestionBankData.questionsPerSession.</summary>
        public int QuestionsPerSession = 3;

        /// <summary>Eras that get questions in the bank. Null = every era.</summary>
        public EraType[] ErasWithQuestions;

        /// <summary>EraProgressionManager.startingEras. Null = none.</summary>
        public EraType[] StartingEras;
    }

    /// <summary>
    /// Builds an isolated Ông Bụt + era progression environment in Play Mode:
    /// in-memory assets (heroes and questions for every era, a question bank,
    /// reward skills, a LevelConfig), real manager components, a throwaway
    /// save folder, and an <see cref="EventBusTracer"/>. Records every event the
    /// tests assert on and logs state transitions to <see cref="RegressionLog"/>.
    ///
    /// Managers are added to inactive GameObjects so private config can be
    /// injected before Awake runs. Dispose() tears everything down immediately
    /// (DestroyImmediate) so subscriber checks see the final state.
    /// </summary>
    public sealed class OngButTestRig : IDisposable
    {
        /// <summary>Gold on the test LevelConfig.</summary>
        public const int StartingGold = 100;

        /// <summary>Gold granted by <see cref="GoldSkill"/>.</summary>
        public const int GoldSkillValue = 50;

        private const int FlushTimeoutMs = 5000;
        private const string NoResourcesPath = "__HKSV_Tests_NoResources__";

        private readonly List<Object> _assets = new List<Object>();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<HeroCardData> _heroes = new List<HeroCardData>();
        private readonly string _saveFolder;
        private bool _disposed;

        // ── Environment ─────────────────────────────────────────────────────

        public EraType LevelEra { get; }
        public LevelConfig Level { get; private set; }
        public QuestionBankData Bank { get; private set; }
        public OngButSessionConfig Config { get; private set; }
        public OngButSkillData GoldSkill { get; private set; }
        public OngButSkillData ExpensiveSkill { get; private set; }
        public IReadOnlyList<HeroCardData> Heroes => _heroes;

        public CampaignManager Campaign { get; private set; }
        public EconomyManager Economy { get; private set; }
        public EraProgressionManager Progression { get; private set; }
        public OngButSessionManager Session { get; private set; }
        public EventBusTracer Tracer { get; private set; }

        // ── Recorded events ─────────────────────────────────────────────────

        public readonly List<OngButPhase> Phases = new List<OngButPhase>();
        public readonly List<HistoricalQuestionData> QuestionsShown = new List<HistoricalQuestionData>();
        public readonly List<OngButAnswerResultEvent> AnswerResults = new List<OngButAnswerResultEvent>();
        public readonly List<HeroUnlockedEvent> HeroUnlocks = new List<HeroUnlockedEvent>();
        public readonly List<OngButSkillGrantedEvent> SkillGrants = new List<OngButSkillGrantedEvent>();
        public readonly List<GoldChangedEvent> GoldChanges = new List<GoldChangedEvent>();
        public readonly List<OngButResultDataEvent> ResultData = new List<OngButResultDataEvent>();
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int QuizCompletedCount { get; private set; }
        public int SkillSelectionConfirmedCount { get; private set; }

        /// <summary>The question most recently published by the session manager.</summary>
        public HistoricalQuestionData CurrentQuestion { get; private set; }

        private OngButTestRig(EraType levelEra)
        {
            LevelEra = levelEra;
            _saveFolder = "SaveData_Test_" + Guid.NewGuid().ToString("N");
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONSTRUCTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Builds assets and managers for a level of <paramref name="levelEra"/>.</summary>
        public static OngButTestRig Create(EraType levelEra, OngButRigOptions options = null)
        {
            options = options ?? new OngButRigOptions();
            var rig = new OngButTestRig(levelEra);

            rig.Tracer = new EventBusTracer();   // first subscriber → logs publish before handlers run
            rig.SubscribeRecorders();
            rig.BuildAssets(options);
            rig.BuildManagers(options);

            RegressionLog.Info("Rig", $"READY levelEra={levelEra} heroes={rig._heroes.Count} " +
                                      $"questions={rig.Bank.TotalQuestions} perSession={rig.Bank.QuestionsPerSession} " +
                                      $"saveFolder={rig._saveFolder}");
            EventBusTracer.LogSnapshot("Subscribers after rig setup", EventBusTracer.Snapshot());
            return rig;
        }

        private void BuildAssets(OngButRigOptions options)
        {
            EraType[] allEras = (EraType[])Enum.GetValues(typeof(EraType));

            foreach (EraType era in allEras)
            {
                for (int i = 0; i < options.HeroesPerEra; i++)
                {
                    var hero = Track(ScriptableObject.CreateInstance<HeroCardData>());
                    hero.heroID = $"Test_{era}_{i}";
                    hero.heroName = hero.heroID;
                    hero.name = hero.heroID;
                    hero.eraType = era;
                    hero.isAvailable = true;
                    _heroes.Add(hero);
                }
            }

            var questions = new List<HistoricalQuestionData>();
            foreach (EraType era in options.ErasWithQuestions ?? allEras)
            {
                for (int i = 0; i < options.QuestionsPerEra; i++)
                    questions.Add(CreateQuestion(era, i));
            }

            Bank = Track(ScriptableObject.CreateInstance<QuestionBankData>());
            TestReflection.SetField(Bank, "questions", questions);
            TestReflection.SetField(Bank, "questionsPerSession", options.QuestionsPerSession);

            GoldSkill = CreateSkill("Test_GoldBlessing", cost: 1, value: GoldSkillValue);
            ExpensiveSkill = CreateSkill("Test_Expensive", cost: 3, value: 999);

            Config = Track(ScriptableObject.CreateInstance<OngButSessionConfig>());
            TestReflection.SetField(Config, "questionBank", Bank);
            TestReflection.SetField(Config, "availableSkills", new List<OngButSkillData> { GoldSkill, ExpensiveSkill });

            Level = Track(ScriptableObject.CreateInstance<LevelConfig>());
            Level.name = $"Test_Level_{LevelEra}";
            Level.levelDisplayName = Level.name;
            Level.levelEra = LevelEra;
            Level.startingGold = StartingGold;
        }

        private HistoricalQuestionData CreateQuestion(EraType era, int index)
        {
            var question = Track(ScriptableObject.CreateInstance<HistoricalQuestionData>());
            question.name = $"Test_Q_{era}_{index}";
            TestReflection.SetField(question, "questionID", question.name);
            TestReflection.SetField(question, "era", era);
            TestReflection.SetField(question, "questionText", $"Câu hỏi {era} #{index}");
            TestReflection.SetField(question, "answers", new[] { "A", "B", "C", "D" });
            TestReflection.SetField(question, "answerFeedbacks", new[] { "fA", "fB", "fC", "fD" });
            TestReflection.SetField(question, "correctAnswerIndex", index % 4);
            return question;
        }

        private OngButSkillData CreateSkill(string id, int cost, float value)
        {
            var skill = Track(ScriptableObject.CreateInstance<OngButSkillData>());
            skill.name = id;
            TestReflection.SetField(skill, "skillID", id);
            TestReflection.SetField(skill, "skillName", id);
            TestReflection.SetField(skill, "correctAnswerCost", cost);
            TestReflection.SetField(skill, "effectType", OngButSkillEffectType.GoldBlessing);
            TestReflection.SetField(skill, "effectValue", value);
            return skill;
        }

        private void BuildManagers(OngButRigOptions options)
        {
            Campaign = AddInactive<CampaignManager>("Test_CampaignManager");
            Campaign.campaignLevels = new[] { Level };
            Campaign.currentLevelIndex = 0;
            Activate(Campaign);

            Economy = AddInactive<EconomyManager>("Test_EconomyManager");
            Activate(Economy);
            Economy.InitializeForLevel(Level);

            Progression = AddInactive<EraProgressionManager>("Test_EraProgressionManager");
            TestReflection.SetField(Progression, "saveFolderName", _saveFolder);
            TestReflection.SetField(Progression, "heroCardResourcesPath", NoResourcesPath);
            TestReflection.SetField(Progression, "startingEras", options.StartingEras ?? Array.Empty<EraType>());
            TestReflection.SetField(Progression, "startingHeroes", Array.Empty<HeroCardData>());
            Activate(Progression);
            Progression.OverrideHeroCatalog(_heroes.ToArray());

            Session = AddInactive<OngButSessionManager>("Test_OngButSessionManager");
            TestReflection.SetField(Session, "sessionConfig", Config);
            Activate(Session);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PLAYER ACTIONS (published exactly as the UI layer would)
        // ─────────────────────────────────────────────────────────────────────

        public void StartSession() => Step("Pagoda clicked", () => GameEventBus.Publish(new PagodaActivatedEvent()));
        public void AcknowledgeIntro() => Step("Intro acknowledged", () => GameEventBus.Publish(new OngButIntroAcknowledgedEvent()));
        public void Next() => Step("Next question", () => GameEventBus.Publish(new OngButNextQuestionRequestedEvent()));
        public void SelectSkill(OngButSkillData skill) => Step($"Skill selected '{skill.SkillID}'", () => GameEventBus.Publish(new OngButSkillSelectedEvent { Skill = skill }));
        public void ConfirmSkill() => Step("Skill confirmed", () => GameEventBus.Publish(new OngButSkillConfirmedEvent()));
        public void FinishSession() => Step("Session done", () => GameEventBus.Publish(new OngButSessionDoneEvent()));
        public void ReturnWithoutSkill() => Step("Return (no skill)", () => GameEventBus.Publish(new OngButReturnRequestedEvent()));
        public void UseGrantedSkill() => Step("HUD skill used", () => GameEventBus.Publish(new OngButSkillHUDActivatedEvent()));
        public void Defeat() => Step("Defeat", () => GameEventBus.Publish(new DefeatEvent()));

        /// <summary>Submits the correct (or a wrong) answer for <see cref="CurrentQuestion"/>.</summary>
        public void Answer(bool correct)
        {
            int correctIndex = CurrentQuestion != null ? CurrentQuestion.CorrectAnswerIndex : 0;
            int selected = correct ? correctIndex : (correctIndex + 1) % 4;
            Step($"Answer {(correct ? "correct" : "wrong")} (index {selected})",
                 () => GameEventBus.Publish(new OngButAnswerSubmittedEvent { SelectedIndex = selected }));
        }

        /// <summary>Pagoda → Intro → one Answer + Next per entry → Result.</summary>
        public void RunQuiz(params bool[] answers)
        {
            StartSession();
            AcknowledgeIntro();
            foreach (bool correct in answers)
            {
                Answer(correct);
                Next();
            }
        }

        private void Step(string action, Action publish)
        {
            RegressionLog.Info("Action", action);
            publish();
        }

        // ─────────────────────────────────────────────────────────────────────
        // INSPECTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Hero IDs written to the save file, after flushing pending writes.</summary>
        public List<string> ReadSavedHeroIDs()
        {
            if (!Progression.FlushPendingSaves(FlushTimeoutMs))
                throw new TimeoutException("Era progress save did not finish writing in time.");

            string path = Progression.SaveFilePath;
            if (!File.Exists(path)) return new List<string>();
            return JsonUtility.FromJson<EraProgressSaveData>(File.ReadAllText(path)).unlockedHeroIDs;
        }

        /// <summary>Logs a one-line snapshot of both managers plus time scale and gold.</summary>
        public void LogState(string label)
        {
            var unlocked = new List<HeroCardData>();
            Progression.GetUnlockedHeroes(unlocked);

            var heroIds = new StringBuilder();
            foreach (HeroCardData hero in unlocked) heroIds.Append(heroIds.Length > 0 ? "," : "").Append(hero.heroID);

            var eras = new StringBuilder();
            foreach (EraType era in (EraType[])Enum.GetValues(typeof(EraType)))
                if (Progression.IsEraUnlocked(era)) eras.Append(eras.Length > 0 ? "," : "").Append(era);

            RegressionLog.Info("State",
                $"{label}: session.phase={Session.CurrentPhase} score={Session.CorrectAnswerCount} " +
                $"usedThisLevel={Session.HasBeenUsedThisLevel} granted={(Session.GrantedSkill != null ? Session.GrantedSkill.SkillID : "none")} " +
                $"| progression.heroes=[{heroIds}] eras=[{eras}] | timeScale={Time.timeScale:0.##} gold={Economy.CurrentGold}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EVENT RECORDERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-attaches the tracer and recorders after <see cref="GameEventBus.Reset"/>,
        /// which removes test subscribers along with production ones.
        /// </summary>
        public void ReattachAfterBusReset()
        {
            Tracer.Attach();
            UnsubscribeRecorders();
            SubscribeRecorders();
        }

        private void SubscribeRecorders()
        {
            GameEventBus.OnOngButPhaseChanged += OnPhaseChanged;
            GameEventBus.OnOngButQuestionReady += OnQuestionReady;
            GameEventBus.OnOngButAnswerResult += OnAnswerResult;
            GameEventBus.OnHeroUnlocked += OnHeroUnlocked;
            GameEventBus.OnOngButSkillGranted += OnSkillGranted;
            GameEventBus.OnGoldChanged += OnGoldChanged;
            GameEventBus.OnOngButResultData += OnResultData;
            GameEventBus.OnGamePaused += OnPaused;
            GameEventBus.OnGameResumed += OnResumed;
            GameEventBus.OnOngButQuizCompleted += OnQuizCompleted;
            GameEventBus.OnOngButSkillSelectionConfirmed += OnSelectionConfirmed;
        }

        private void UnsubscribeRecorders()
        {
            GameEventBus.OnOngButPhaseChanged -= OnPhaseChanged;
            GameEventBus.OnOngButQuestionReady -= OnQuestionReady;
            GameEventBus.OnOngButAnswerResult -= OnAnswerResult;
            GameEventBus.OnHeroUnlocked -= OnHeroUnlocked;
            GameEventBus.OnOngButSkillGranted -= OnSkillGranted;
            GameEventBus.OnGoldChanged -= OnGoldChanged;
            GameEventBus.OnOngButResultData -= OnResultData;
            GameEventBus.OnGamePaused -= OnPaused;
            GameEventBus.OnGameResumed -= OnResumed;
            GameEventBus.OnOngButQuizCompleted -= OnQuizCompleted;
            GameEventBus.OnOngButSkillSelectionConfirmed -= OnSelectionConfirmed;
        }

        private void OnPhaseChanged(OngButPhaseChangedEvent evt)
        {
            Phases.Add(evt.NewPhase);
            RegressionLog.Info("OngButState", $"phase → {evt.NewPhase} (timeScale={Time.timeScale:0.##})");
        }

        private void OnQuestionReady(OngButQuestionReadyEvent evt)
        {
            CurrentQuestion = evt.Question;
            QuestionsShown.Add(evt.Question);
            RegressionLog.Info("OngButState", $"question {evt.QuestionNumber}/{evt.TotalQuestions} '{evt.Question.QuestionID}' era={evt.Question.Era}");
        }

        private void OnAnswerResult(OngButAnswerResultEvent evt)
        {
            AnswerResults.Add(evt);
            RegressionLog.Info("OngButState", $"answer correct={evt.IsCorrect} era={evt.Era} score={evt.CurrentScore}");
        }

        private void OnHeroUnlocked(HeroUnlockedEvent evt)
        {
            HeroUnlocks.Add(evt);
            RegressionLog.Info("EraProgress", $"hero unlocked '{evt.Hero.heroID}' era={evt.Era} total={Progression.UnlockedHeroCount}");
        }

        private void OnSkillGranted(OngButSkillGrantedEvent evt) => SkillGrants.Add(evt);
        private void OnGoldChanged(GoldChangedEvent evt) => GoldChanges.Add(evt);
        private void OnResultData(OngButResultDataEvent evt) => ResultData.Add(evt);
        private void OnPaused(GamePausedEvent evt) => PauseCount++;
        private void OnResumed(GameResumedEvent evt) => ResumeCount++;
        private void OnQuizCompleted(OngButQuizCompletedEvent evt) => QuizCompletedCount++;
        private void OnSelectionConfirmed(OngButSkillSelectionConfirmedEvent evt) => SkillSelectionConfirmedCount++;

        // ─────────────────────────────────────────────────────────────────────
        // TEARDOWN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Destroys managers and assets, deletes the save folder, restores time scale.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            RegressionLog.Info("Rig", "TEARDOWN");
            UnsubscribeRecorders();

            if (Progression != null) Progression.FlushPendingSaves(FlushTimeoutMs);

            // Reverse creation order; DestroyImmediate so OnDisable unsubscribes now.
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);

            foreach (Object asset in _assets)
                if (asset != null) Object.DestroyImmediate(asset);

            string saveFolder = Path.Combine(Application.persistentDataPath, _saveFolder);
            if (Directory.Exists(saveFolder)) Directory.Delete(saveFolder, true);

            Tracer?.Dispose();

            // OngButSessionManager pauses via timeScale; never leak a paused clock into the next test.
            Time.timeScale = 1f;
        }

        private T Track<T>(T asset) where T : Object
        {
            _assets.Add(asset);
            return asset;
        }

        private T AddInactive<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        private static void Activate(Component component) => component.gameObject.SetActive(true);
    }
}
