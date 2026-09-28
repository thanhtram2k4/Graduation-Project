using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Resmoke suite: repeats the critical Ông Bụt / era progression cycle many
    /// times inside one run and checks that nothing accumulates between cycles
    /// (bus subscribers, Unity objects, managed heap, paused time scale), and that
    /// the persistent EraProgressionManager stays subscribed exactly once across
    /// GameEventBus.Reset() + scene loads.
    ///
    /// Tunables (pass on the Unity command line):
    ///   -resmokeIterations N        soak cycles              (default 25)
    ///   -resmokeReloadCycles N      bus reset + scene loads  (default 5)
    ///   -resmokeMaxHeapGrowthKB N   allowed heap growth      (default 4096)
    /// </summary>
    [TestFixture]
    [Category("Resmoke")]
    [PrebuildSetup(typeof(ProbeSceneSetup))]
    public class ResmokeTests
    {
        internal const string ProbeScenePath = "Assets/Tests/PlayMode/Scenes/BusResetProbe.unity";

        private const string AnswerResultEvent = nameof(GameEventBus.OnOngButAnswerResult);
        private const string AnswerResultHandler = "HandleAnswerResult";

        private static readonly EraType[] AllEras = (EraType[])Enum.GetValues(typeof(EraType));

        private static readonly Type[] TrackedObjectTypes =
        {
            typeof(HeroCardData), typeof(HistoricalQuestionData), typeof(QuestionBankData),
            typeof(OngButSessionConfig), typeof(OngButSkillData), typeof(LevelConfig),
            typeof(OngButSessionManager), typeof(EraProgressionManager), typeof(EconomyManager), typeof(CampaignManager)
        };

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test]
        public void Soak_RepeatedSessions_LeaveNothingBehind()
        {
            int iterations = TestArgs.GetInt("-resmokeIterations", 25);
            int maxHeapGrowthKb = TestArgs.GetInt("-resmokeMaxHeapGrowthKB", 4096);

            SortedDictionary<string, List<string>> baselineSubscribers = EventBusTracer.Snapshot();
            Dictionary<Type, int> baselineObjects = CountLiveObjects();
            long heapAfterWarmup = 0;

            RegressionLog.Info("Resmoke", $"soak start iterations={iterations} maxHeapGrowthKB={maxHeapGrowthKb}");

            for (int i = 0; i < iterations; i++)
            {
                EraType era = AllEras[i % AllEras.Length];
                bool[] answers = { (i & 1) != 0, (i & 2) != 0, (i & 4) != 0 };
                string context = $"iteration {i} era={era} answers={string.Join("/", answers)}";

                RunOneCycle(era, answers, context);

                List<string> subscriberDiff = EventBusTracer.Diff(baselineSubscribers, EventBusTracer.Snapshot());
                Assert.That(subscriberDiff, Is.Empty, $"{context}: bus subscribers drifted: {string.Join("; ", subscriberDiff)}");
                Assert.That(Time.timeScale, Is.EqualTo(1f), $"{context}: time scale left paused");
                Assert.That(EraProgressionManager.Instance == null, Is.True, $"{context}: EraProgressionManager singleton survived teardown");
                AssertObjectCountsMatch(baselineObjects, context);

                long heap = GC.GetTotalMemory(true);
                if (i == 0) heapAfterWarmup = heap;
                RegressionLog.Info("Resmoke", $"{context} OK heap={heap / 1024}KB subscribers={EventBusTracer.CountHandlers(EventBusTracer.Snapshot())}");
            }

            long growthKb = (GC.GetTotalMemory(true) - heapAfterWarmup) / 1024;
            RegressionLog.Info("Resmoke", $"soak done heapGrowthSinceWarmup={growthKb}KB");
            Assert.That(growthKb, Is.LessThan(maxHeapGrowthKb), "managed heap kept growing across cycles (possible leak)");
        }

        [UnityTest]
        public IEnumerator BusResetAndSceneLoad_KeepEraProgressionSubscribedExactlyOnce()
        {
#if UNITY_EDITOR
            int cycles = TestArgs.GetInt("-resmokeReloadCycles", 5);
            OngButTestRig rig = OngButTestRig.Create(EraType.Mythology);
            try
            {
                Assert.That(EventBusTracer.CountHandlers(AnswerResultEvent, AnswerResultHandler), Is.EqualTo(1), "initial subscription");

                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    // What GameManager.RestartGame / GameOutcomeUI do before reloading the scene.
                    GameEventBus.Reset();
                    Assert.That(EventBusTracer.CountHandlers(AnswerResultEvent), Is.Zero, $"cycle {cycle}: Reset() left subscribers");
                    rig.ReattachAfterBusReset();

                    // Scene-scoped managers are recreated by the reload; emulate with a disable/enable.
                    Reenable(rig.Session);
                    Reenable(rig.Economy);

                    yield return LoadAndUnloadProbeScene();

                    int handlers = EventBusTracer.CountHandlers(AnswerResultEvent, AnswerResultHandler);
                    RegressionLog.Info("Resmoke", $"reload cycle {cycle}: EraProgressionManager.HandleAnswerResult subscribers={handlers}");
                    Assert.That(handlers, Is.EqualTo(1), $"cycle {cycle}: EraProgressionManager must resubscribe exactly once after a scene load");
                }

                rig.RunQuiz(true, false, false);
                rig.LogState("after reload cycles");
                Assert.That(rig.HeroUnlocks, Has.Count.EqualTo(1), "one correct answer after reloads must unlock exactly one hero");
            }
            finally
            {
                rig.Dispose();
            }
#else
            Assert.Ignore("Loads a scene asset by path; editor only.");
            yield break;
#endif
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void RunOneCycle(EraType era, bool[] answers, string context)
        {
            using (OngButTestRig rig = OngButTestRig.Create(era))
            {
                rig.RunQuiz(answers);

                int correct = 0;
                foreach (bool answer in answers) if (answer) correct++;

                if (correct > 0)
                {
                    rig.SelectSkill(rig.GoldSkill);
                    rig.ConfirmSkill();
                    rig.FinishSession();
                    rig.UseGrantedSkill();
                    Assert.That(rig.Economy.CurrentGold, Is.EqualTo(OngButTestRig.StartingGold + OngButTestRig.GoldSkillValue), $"{context}: gold reward");
                }
                else
                {
                    rig.ReturnWithoutSkill();
                }

                Assert.That(rig.HeroUnlocks, Has.Count.EqualTo(Math.Min(correct, 2)), $"{context}: hero unlocks");
                Assert.That(rig.HeroUnlocks, Has.All.Matches<HeroUnlockedEvent>(u => u.Era == era), $"{context}: unlock era");
                Assert.That(Time.timeScale, Is.EqualTo(1f), $"{context}: time scale after session");
                Assert.That(rig.PauseCount, Is.EqualTo(1), $"{context}: pause count");
                Assert.That(rig.ResumeCount, Is.EqualTo(1), $"{context}: resume count");
            }
        }

        private static Dictionary<Type, int> CountLiveObjects()
        {
            var counts = new Dictionary<Type, int>();
            foreach (Type type in TrackedObjectTypes)
                counts[type] = Resources.FindObjectsOfTypeAll(type).Length;
            return counts;
        }

        private static void AssertObjectCountsMatch(Dictionary<Type, int> baseline, string context)
        {
            foreach (KeyValuePair<Type, int> pair in CountLiveObjects())
                Assert.That(pair.Value, Is.EqualTo(baseline[pair.Key]), $"{context}: leaked {pair.Key.Name} instances");
        }

        private static void Reenable(Component component)
        {
            component.gameObject.SetActive(false);
            component.gameObject.SetActive(true);
        }

#if UNITY_EDITOR
        private static IEnumerator LoadAndUnloadProbeScene()
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ProbeScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!load.isDone) yield return null;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(ProbeScenePath));
            while (!unload.isDone) yield return null;
        }
#endif
    }

    /// <summary>
    /// Creates the empty additive scene the reload test loads, before Play Mode
    /// starts. Checked in after the first run; recreated if missing.
    /// </summary>
    public class ProbeSceneSetup : IPrebuildSetup
    {
        /// <inheritdoc />
        public void Setup()
        {
#if UNITY_EDITOR
            if (File.Exists(ResmokeTests.ProbeScenePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ResmokeTests.ProbeScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ResmokeTests.ProbeScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
#endif
        }
    }
}
