using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HaoKhiSuViet.Tests
{
    [TestFixture]
    [Category("Unit")]
    public class QuestionBankEraTests
    {
        private static readonly EraType[] AllEras = (EraType[])Enum.GetValues(typeof(EraType));

        private readonly List<Object> _assets = new List<Object>();
        private List<HistoricalQuestionData> _source;
        private QuestionBankData _bank;

        [SetUp]
        public void SetUp()
        {
            // 4 questions for every era except Culture, which has 1.
            _source = new List<HistoricalQuestionData>();
            foreach (EraType era in AllEras)
            {
                int count = era == EraType.Culture ? 1 : 4;
                for (int i = 0; i < count; i++) _source.Add(Question(era, i));
            }
            _source.Add(null);   // a stray null entry must be skipped, not crash

            _bank = ScriptableObject.CreateInstance<QuestionBankData>();
            _assets.Add(_bank);
            TestReflection.SetField(_bank, "questions", _source);
            TestReflection.SetField(_bank, "questionsPerSession", 3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void GetQuestionsByEra_ReturnsOnlyThatEra_WithoutRepeats(
            [Values(EraType.History, EraType.Mythology, EraType.Legend, EraType.FairyTale)] EraType era)
        {
            for (int run = 0; run < 20; run++)
            {
                List<HistoricalQuestionData> drawn = _bank.GetQuestionsByEra(era, 3);

                Assert.That(drawn, Has.Count.EqualTo(3));
                Assert.That(drawn, Is.Unique);
                Assert.That(drawn, Has.All.Matches<HistoricalQuestionData>(q => q.Era == era));
            }
        }

        [Test]
        public void GetQuestionsByEra_ClampsToThePoolSize_WithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Requested 3 Culture questions but only 1 available"));

            List<HistoricalQuestionData> drawn = _bank.GetQuestionsByEra(EraType.Culture, 3);

            Assert.That(drawn, Has.Count.EqualTo(1));
            Assert.That(drawn[0].Era, Is.EqualTo(EraType.Culture));
        }

        [Test]
        public void GetQuestionsByEra_EraWithNoQuestions_ReturnsZeroAndLogsError()
        {
            TestReflection.SetField(_bank, "questions", _source.FindAll(q => q != null && q.Era != EraType.Legend));
            var results = new List<HistoricalQuestionData> { Question(EraType.History, 99) };

            LogAssert.Expect(LogType.Error, new Regex("No questions for era Legend"));

            Assert.That(_bank.GetQuestionsByEra(EraType.Legend, 3, results), Is.Zero);
            Assert.That(results, Is.Empty, "results list must be cleared even when nothing is drawn");
        }

        [Test]
        public void GetQuestionsByEra_ReusesCallerList_AndLeavesSourceUntouched()
        {
            var sourceOrder = new List<HistoricalQuestionData>(_source);
            var results = new List<HistoricalQuestionData>();

            _bank.GetQuestionsByEra(EraType.History, 3, results);
            _bank.GetQuestionsByEra(EraType.Mythology, 2, results);

            Assert.That(results, Has.Count.EqualTo(2), "second call must replace, not append");
            Assert.That(results, Has.All.Matches<HistoricalQuestionData>(q => q.Era == EraType.Mythology));
            Assert.That(_source, Is.EqualTo(sourceOrder), "asset question list must not be reordered");
        }

        private HistoricalQuestionData Question(EraType era, int index)
        {
            var question = ScriptableObject.CreateInstance<HistoricalQuestionData>();
            question.name = $"Q_{era}_{index}";
            TestReflection.SetField(question, "era", era);
            _assets.Add(question);
            return question;
        }
    }
}
