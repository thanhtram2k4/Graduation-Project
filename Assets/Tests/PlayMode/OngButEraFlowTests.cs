using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Operations reproduction for the era-based Ông Bụt flow, run once per era:
    /// Pagoda → era-filtered questions → answers → hero unlocks → skill reward → gold.
    /// Everything is driven through GameEventBus exactly as the UI layer drives it.
    /// </summary>
    [TestFixture]
    [Category("Critical")]
    public class OngButEraFlowTests
    {
        private static readonly EraType[] AllEras = (EraType[])Enum.GetValues(typeof(EraType));

        private OngButTestRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [Test]
        public void Session_PresentsOnlyQuestionsFromTheLevelEra([ValueSource(nameof(AllEras))] EraType era)
        {
            _rig = OngButTestRig.Create(era);

            _rig.RunQuiz(true, false, true);
            _rig.LogState("after quiz");

            Assert.That(_rig.QuestionsShown, Has.Count.EqualTo(3), "questions presented");
            foreach (HistoricalQuestionData question in _rig.QuestionsShown)
                Assert.That(question.Era, Is.EqualTo(era), $"question '{question.QuestionID}' is from the wrong era");

            Assert.That(_rig.QuestionsShown, Is.Unique, "a question was presented twice in one session");
            Assert.That(_rig.AnswerResults, Has.All.Matches<OngButAnswerResultEvent>(r => r.Era == era),
                "OngButAnswerResultEvent.Era must carry the answered question's era");
        }

        [Test]
        public void CorrectAnswers_UnlockHeroesFromTheLevelEra_AndPersistThem([ValueSource(nameof(AllEras))] EraType era)
        {
            _rig = OngButTestRig.Create(era);

            _rig.RunQuiz(true, false, true);
            _rig.LogState("after quiz");

            Assert.That(_rig.HeroUnlocks, Has.Count.EqualTo(2), "one unlock per correct answer");

            var unlockedIds = new List<string>();
            foreach (HeroUnlockedEvent unlock in _rig.HeroUnlocks)
            {
                Assert.That(unlock.Hero.eraType, Is.EqualTo(era), $"'{unlock.Hero.heroID}' is from the wrong era");
                Assert.That(unlock.Era, Is.EqualTo(era));
                Assert.That(_rig.Progression.IsHeroUnlocked(unlock.Hero), Is.True);
                unlockedIds.Add(unlock.Hero.heroID);
            }

            Assert.That(unlockedIds, Is.Unique, "the same hero was unlocked twice");
            Assert.That(_rig.ReadSavedHeroIDs(), Is.EquivalentTo(unlockedIds), "save file must hold exactly the unlocked heroes");
        }

        [Test]
        public void WrongAnswers_UnlockNothing([ValueSource(nameof(AllEras))] EraType era)
        {
            _rig = OngButTestRig.Create(era);

            _rig.RunQuiz(false, false, false);
            _rig.LogState("after quiz");

            Assert.That(_rig.HeroUnlocks, Is.Empty);
            Assert.That(_rig.Progression.UnlockedHeroCount, Is.Zero);
            Assert.That(_rig.Session.CorrectAnswerCount, Is.Zero);
        }

        [Test]
        public void CorrectAnswers_BeyondTheErasHeroCount_UnlockNothingMore()
        {
            // 2 heroes per era, 3 correct answers: the third finds nothing left to unlock.
            _rig = OngButTestRig.Create(EraType.Legend);

            _rig.RunQuiz(true, true, true);
            _rig.LogState("after quiz");

            Assert.That(_rig.Session.CorrectAnswerCount, Is.EqualTo(3));
            Assert.That(_rig.HeroUnlocks, Has.Count.EqualTo(2));
            Assert.That(_rig.Progression.UnlockedHeroCount, Is.EqualTo(2));
        }

        [Test]
        public void GoldBlessingReward_IsGrantedThroughTheEconomy()
        {
            _rig = OngButTestRig.Create(EraType.Culture);

            _rig.RunQuiz(true, false, false);
            _rig.SelectSkill(_rig.GoldSkill);
            _rig.ConfirmSkill();
            _rig.FinishSession();
            _rig.LogState("skill granted");

            Assert.That(_rig.SkillGrants, Has.Count.EqualTo(1));
            Assert.That(_rig.SkillGrants[0].SkillID, Is.EqualTo(_rig.GoldSkill.SkillID));
            Assert.That(_rig.Session.HasUsableSkill, Is.True);

            int goldBefore = _rig.Economy.CurrentGold;
            _rig.UseGrantedSkill();
            _rig.LogState("skill used");

            Assert.That(_rig.Economy.CurrentGold, Is.EqualTo(goldBefore + OngButTestRig.GoldSkillValue));
            Assert.That(_rig.GoldChanges, Has.Some.Matches<GoldChangedEvent>(
                g => g.CurrentGold == goldBefore + OngButTestRig.GoldSkillValue), "UI must be notified via GoldChangedEvent");
            Assert.That(_rig.Session.HasUsableSkill, Is.False, "the reward skill is single-use");
        }

        [Test]
        public void LevelEraWithoutQuestions_DoesNotStartTheSession()
        {
            _rig = OngButTestRig.Create(EraType.FairyTale, new OngButRigOptions
            {
                ErasWithQuestions = new[] { EraType.History, EraType.Mythology }
            });

            LogAssert.Expect(LogType.Error, new Regex(@"No questions for era FairyTale"));
            LogAssert.Expect(LogType.Error, new Regex(@"No FairyTale questions in the bank"));

            _rig.StartSession();
            _rig.LogState("after pagoda");

            Assert.That(_rig.Phases, Is.Empty, "no phase change expected");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "game must not be paused");
            Assert.That(_rig.PauseCount, Is.Zero);
            Assert.That(_rig.Session.HasBeenUsedThisLevel, Is.False, "a failed start must not consume the Pagoda");
        }
    }
}
