using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Integration tests for the draft pool: LineupManager must build the pool
    /// from heroes unlocked in EraProgressionManager, pad it when too few are
    /// unlocked, and still let the blind pick finish a full lineup.
    /// </summary>
    [TestFixture]
    [Category("Critical")]
    public class LineupDraftPoolTests
    {
        private const int LineupSize = 3;
        private const float RevealLockSeconds = 0.7f;   // LineupManager unlocks the next pick after 0.6 s

        private OngButTestRig _rig;
        private LineupManager _lineup;
        private readonly List<DraftPoolBuiltEvent> _pools = new List<DraftPoolBuiltEvent>();
        private readonly List<HeroCardData> _revealed = new List<HeroCardData>();
        private int _finalizedCount;

        [SetUp]
        public void SetUp()
        {
            GameEventBus.OnDraftPoolBuilt += OnPoolBuilt;
            GameEventBus.OnBlindCardRevealed += OnRevealed;
            GameEventBus.OnLineupFinalized += OnFinalized;
        }

        [TearDown]
        public void TearDown()
        {
            GameEventBus.OnDraftPoolBuilt -= OnPoolBuilt;
            GameEventBus.OnBlindCardRevealed -= OnRevealed;
            GameEventBus.OnLineupFinalized -= OnFinalized;

            if (_lineup != null) Object.DestroyImmediate(_lineup.gameObject);
            _rig?.Dispose();
            _rig = null;
            _pools.Clear();
            _revealed.Clear();
            _finalizedCount = 0;
        }

        [Test]
        public void DraftPool_ContainsOnlyUnlockedHeroes()
        {
            CreateRigAndLineup();
            HeroCardData[] unlocked = { Hero(EraType.History, 0), Hero(EraType.Legend, 1), Hero(EraType.Culture, 0), Hero(EraType.Mythology, 1) };
            foreach (HeroCardData hero in unlocked) _rig.Progression.UnlockHero(hero);

            EnterState(LevelState.Drafting);

            Assert.That(_pools, Has.Count.EqualTo(1), "one DraftPoolBuiltEvent per Drafting entry");
            Assert.That(_pools[0].Heroes, Is.EquivalentTo(unlocked), "locked heroes leaked into the pool");
            Assert.That(_pools[0].UnlockedCount, Is.EqualTo(unlocked.Length));
            Assert.That(_pools[0].PaddedCount, Is.Zero);

            EnterState(LevelState.Shuffling);
            Assert.That(_lineup.DeckSize, Is.EqualTo(unlocked.Length), "deck must be built from the pool");
        }

        [Test]
        public void UnlockedButUnavailableHero_IsExcluded()
        {
            CreateRigAndLineup();
            HeroCardData retired = Hero(EraType.History, 0);
            for (int i = 0; i < _rig.Heroes.Count; i++) _rig.Progression.UnlockHero(_rig.Heroes[i]);
            retired.isAvailable = false;

            EnterState(LevelState.Drafting);

            Assert.That(_pools[0].Heroes, Has.No.Member(retired));
            Assert.That(_pools[0].Heroes, Has.Count.EqualTo(_rig.Heroes.Count - 1));
        }

        [Test]
        public void TooFewUnlocked_PoolIsPaddedToTheLineupSize_WithAWarning()
        {
            CreateRigAndLineup();
            HeroCardData onlyUnlocked = Hero(EraType.FairyTale, 0);
            _rig.Progression.UnlockHero(onlyUnlocked);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                $"Only 1 unlocked heroes for a lineup of {LineupSize}; padded the draft with {LineupSize - 1} locked heroes"));

            EnterState(LevelState.Drafting);

            DraftPoolBuiltEvent pool = _pools[0];
            Assert.That(pool.Heroes, Has.Count.EqualTo(LineupSize));
            Assert.That(pool.Heroes, Does.Contain(onlyUnlocked));
            Assert.That(pool.Heroes, Is.Unique);
            Assert.That(pool.PaddedCount, Is.EqualTo(LineupSize - 1));
            Assert.That(_rig.Progression.UnlockedHeroCount, Is.EqualTo(1), "padding must not unlock heroes permanently");
        }

        [UnityTest]
        public IEnumerator PaddedPool_BlindPickCompletes_AndFinalizesTheLineup()
        {
            CreateRigAndLineup();
            _rig.Progression.UnlockHero(Hero(EraType.History, 0));

            EnterState(LevelState.Drafting);
            EnterState(LevelState.Shuffling);
            GameEventBus.Publish(new ShuffleCompleteEvent());

            for (int pick = 0; pick < LineupSize; pick++)
            {
                GameEventBus.Publish(new BlindCardClickedEvent { CardUIIndex = pick });
                yield return new WaitForSeconds(RevealLockSeconds);
            }

            Assert.That(_revealed, Has.Count.EqualTo(LineupSize), "every blind pick must reveal a hero");
            Assert.That(_revealed, Is.Unique);
            foreach (HeroCardData hero in _revealed)
                Assert.That(_pools[0].Heroes, Does.Contain(hero), $"'{hero.heroID}' was drawn from outside the pool");

            _lineup.ConfirmAndFinalizeLineup();
            Assert.That(_finalizedCount, Is.EqualTo(1), "the draft must not soft-lock when padding was needed");
        }

        [Test]
        public void WithoutEraProgressionManager_EveryAvailableHeroIsDraftable()
        {
            CreateRigAndLineup();
            Object.DestroyImmediate(_rig.Progression.gameObject);
            Assert.That(EraProgressionManager.Instance == null, Is.True);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No EraProgressionManager"));
            EnterState(LevelState.Drafting);

            Assert.That(_pools[0].Heroes, Is.EquivalentTo(_rig.Heroes));
            Assert.That(_pools[0].PaddedCount, Is.Zero);
        }

        // ─────────────────────────────────────────────────────────────────────

        private void CreateRigAndLineup()
        {
            _rig = OngButTestRig.Create(EraType.History);
            _rig.Level.maxLineupSize = LineupSize;

            var go = new GameObject("Test_LineupManager");
            go.SetActive(false);
            _lineup = go.AddComponent<LineupManager>();
            TestReflection.SetField(_lineup, "levelConfig", _rig.Level);
            go.SetActive(true);

            var catalog = new HeroCardData[_rig.Heroes.Count];
            for (int i = 0; i < catalog.Length; i++) catalog[i] = _rig.Heroes[i];
            _lineup.OverrideCatalog(catalog);
        }

        private HeroCardData Hero(EraType era, int index)
        {
            foreach (HeroCardData hero in _rig.Heroes)
                if (hero.heroID == $"Test_{era}_{index}") return hero;
            throw new System.ArgumentException($"No test hero {era}_{index}");
        }

        private static void EnterState(LevelState state)
        {
            RegressionLog.Info("Action", $"Level state → {state}");
            GameEventBus.Publish(new LevelStateChangedEvent { NewState = state });
        }

        private void OnPoolBuilt(DraftPoolBuiltEvent evt)
        {
            // Snapshot: the event's list is owned (and reused) by LineupManager.
            _pools.Add(new DraftPoolBuiltEvent
            {
                Heroes = new List<HeroCardData>(evt.Heroes),
                UnlockedCount = evt.UnlockedCount,
                PaddedCount = evt.PaddedCount
            });
            RegressionLog.Info("DraftPool", $"built count={evt.Heroes.Count} unlocked={evt.UnlockedCount} padded={evt.PaddedCount}");
        }

        private void OnRevealed(BlindCardRevealedEvent evt) => _revealed.Add(evt.RevealedHero);
        private void OnFinalized(LineupFinalizedEvent evt) => _finalizedCount++;
    }
}
