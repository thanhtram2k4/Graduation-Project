using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Regression tests for Time.timeScale pause/resume and OngButPhaseChangedEvent
    /// transitions under rapid, repeated, or out-of-order input. Events are
    /// published directly on the bus (bypassing UI guards such as disabled
    /// buttons) because the gameplay layer is the authority on session state.
    /// </summary>
    [TestFixture]
    [Category("Critical")]
    [Category("Regression")]
    public class OngButRegressionTests
    {
        private static readonly OngButPhase[] FullSessionPhases =
        {
            OngButPhase.Intro, OngButPhase.Questioning, OngButPhase.Result, OngButPhase.Success, OngButPhase.SkillReady
        };

        private OngButTestRig _rig;

        [SetUp]
        public void SetUp() => _rig = OngButTestRig.Create(EraType.History);

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        // ── Pause / resume ──────────────────────────────────────────────────

        [Test]
        public void FullSession_PausesOnce_ResumesOnce_AndWalksEveryPhaseInOrder()
        {
            _rig.StartSession();
            Assert.That(Time.timeScale, Is.EqualTo(0f), "session must pause the game");

            _rig.AcknowledgeIntro();
            for (int i = 0; i < 3; i++) { _rig.Answer(true); _rig.Next(); }
            _rig.SelectSkill(_rig.GoldSkill);
            _rig.ConfirmSkill();
            Assert.That(Time.timeScale, Is.EqualTo(0f), "still paused until Done");

            _rig.FinishSession();
            _rig.LogState("session finished");

            Assert.That(_rig.Phases, Is.EqualTo(FullSessionPhases));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_rig.PauseCount, Is.EqualTo(1));
            Assert.That(_rig.ResumeCount, Is.EqualTo(1));
        }

        [Test]
        public void ZeroScore_ReturnWithoutSkill_ResumesTheGame()
        {
            _rig.RunQuiz(false, false, false);
            Assert.That(_rig.ResultData, Has.Count.EqualTo(1));
            Assert.That(_rig.ResultData[0].ZeroScoreMessage, Is.Not.Null, "zero score must show the consolation message");

            _rig.ReturnWithoutSkill();
            _rig.LogState("returned");

            Assert.That(_rig.Session.CurrentPhase, Is.EqualTo(OngButPhase.Inactive));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_rig.PauseCount, Is.EqualTo(_rig.ResumeCount), "every pause needs a matching resume");
        }

        [UnityTest]
        public IEnumerator PausedSession_FreezesScaledTime_AcrossFrames()
        {
            _rig.StartSession();
            float scaledTimeAtPause = Time.time;

            for (int frame = 0; frame < 5; frame++) yield return null;

            Assert.That(Time.time, Is.EqualTo(scaledTimeAtPause), "scaled time advanced while Ông Bụt was open");
            Assert.That(Time.deltaTime, Is.EqualTo(0f));

            _rig.AcknowledgeIntro();
            for (int i = 0; i < 3; i++) { _rig.Answer(false); _rig.Next(); }
            _rig.ReturnWithoutSkill();
            yield return null;
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.time, Is.GreaterThan(scaledTimeAtPause), "scaled time must run again after resume");
        }

        // ── Rapid / duplicated input ────────────────────────────────────────

        [Test]
        public void RapidPagodaClicks_StartOneSession_AndPauseOnce()
        {
            for (int i = 0; i < 10; i++) _rig.StartSession();
            _rig.LogState("after 10 pagoda clicks");

            Assert.That(_rig.Phases, Is.EqualTo(new[] { OngButPhase.Intro }));
            Assert.That(_rig.PauseCount, Is.EqualTo(1));
        }

        [Test]
        public void RapidNextSpam_CompletesTheQuizExactlyOnce()
        {
            _rig.StartSession();
            _rig.AcknowledgeIntro();
            for (int i = 0; i < 20; i++) _rig.Next();
            _rig.LogState("after 20 next");

            Assert.That(_rig.QuizCompletedCount, Is.EqualTo(1));
            Assert.That(_rig.Session.CurrentPhase, Is.EqualTo(OngButPhase.Result));
            Assert.That(_rig.QuestionsShown, Has.Count.EqualTo(3));
        }

        [Test]
        public void DuplicateAnswerSubmission_ForOneQuestion_CountsOnce()
        {
            _rig.StartSession();
            _rig.AcknowledgeIntro();
            _rig.Answer(true);
            _rig.Answer(true);
            _rig.Answer(true);
            _rig.LogState("after triple submit on question 1");

            Assert.That(_rig.Session.CorrectAnswerCount, Is.EqualTo(1),
                "one question answered three times must score once");
            Assert.That(_rig.HeroUnlocks, Has.Count.LessThanOrEqualTo(1),
                "one question must unlock at most one hero");
        }

        [Test]
        public void OutOfPhaseEvents_AreIgnored()
        {
            // Nothing is open yet: every step event must be a no-op.
            _rig.Answer(true);
            _rig.Next();
            _rig.SelectSkill(_rig.GoldSkill);
            _rig.ConfirmSkill();
            _rig.FinishSession();
            _rig.ReturnWithoutSkill();

            Assert.That(_rig.Phases, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // Intro is open: answers and Done must still be ignored.
            _rig.StartSession();
            _rig.Answer(true);
            _rig.FinishSession();
            _rig.LogState("intro with stray events");

            Assert.That(_rig.Session.CurrentPhase, Is.EqualTo(OngButPhase.Intro));
            Assert.That(_rig.Session.CorrectAnswerCount, Is.Zero);
            Assert.That(_rig.HeroUnlocks, Is.Empty);
        }

        [Test]
        public void UnaffordableSkill_IsRejected_AndConfirmDoesNothing()
        {
            _rig.RunQuiz(true, false, false);   // score 1; ExpensiveSkill costs 3

            _rig.SelectSkill(_rig.ExpensiveSkill);
            _rig.ConfirmSkill();
            _rig.LogState("after unaffordable pick");

            Assert.That(_rig.SkillSelectionConfirmedCount, Is.Zero);
            Assert.That(_rig.Session.CurrentPhase, Is.EqualTo(OngButPhase.Result));
            Assert.That(_rig.SkillGrants, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "still inside the paused session");
        }

        [Test]
        public void DefeatDuringQuestioning_ClosesTheSession_AndIgnoresLaterInput()
        {
            _rig.StartSession();
            _rig.AcknowledgeIntro();
            _rig.Answer(true);
            _rig.Defeat();
            _rig.LogState("after defeat");

            Assert.That(_rig.Session.CurrentPhase, Is.EqualTo(OngButPhase.Inactive));

            int unlocksAtDefeat = _rig.HeroUnlocks.Count;
            _rig.Next();
            _rig.Answer(true);

            Assert.That(_rig.Session.CurrentPhase, Is.EqualTo(OngButPhase.Inactive));
            Assert.That(_rig.HeroUnlocks, Has.Count.EqualTo(unlocksAtDefeat), "no unlocks after defeat");
        }

        [Test]
        public void CompletedSession_CannotBeReopenedInTheSameLevel()
        {
            _rig.RunQuiz(true, true, true);
            _rig.SelectSkill(_rig.GoldSkill);
            _rig.ConfirmSkill();
            _rig.FinishSession();

            int phasesBefore = _rig.Phases.Count;
            _rig.StartSession();
            _rig.LogState("pagoda after completion");

            Assert.That(_rig.Phases, Has.Count.EqualTo(phasesBefore), "Pagoda is single-use per level");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
