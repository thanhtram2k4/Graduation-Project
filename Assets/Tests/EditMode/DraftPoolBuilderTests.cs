using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HaoKhiSuViet.Tests
{
    [TestFixture]
    [Category("Unit")]
    public class DraftPoolBuilderTests
    {
        private readonly List<Object> _assets = new List<Object>();
        private readonly HashSet<HeroCardData> _unlocked = new HashSet<HeroCardData>();
        private readonly List<HeroCardData> _pool = new List<HeroCardData>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
            _unlocked.Clear();
        }

        [Test]
        public void Pool_ContainsOnlyUnlockedAvailableHeroes_WhenEnoughAreUnlocked()
        {
            HeroCardData[] catalog = Heroes(6);
            Unlock(catalog[0], catalog[2], catalog[4], catalog[5]);

            DraftPoolStats stats = Build(catalog, minimumSize: 3);

            Assert.That(_pool, Is.EquivalentTo(new[] { catalog[0], catalog[2], catalog[4], catalog[5] }));
            Assert.That(stats.UnlockedCount, Is.EqualTo(4));
            Assert.That(stats.PaddedCount, Is.Zero, "no padding when enough heroes are unlocked");
        }

        [Test]
        public void UnavailableHeroes_AreExcluded_EvenWhenUnlocked()
        {
            HeroCardData[] catalog = Heroes(4);
            catalog[1].isAvailable = false;
            Unlock(catalog);

            Build(catalog, minimumSize: 2);

            Assert.That(_pool, Has.No.Member(catalog[1]));
            Assert.That(_pool, Has.Count.EqualTo(3));
        }

        [Test]
        public void TooFewUnlocked_PadsWithLockedAvailableHeroes_UpToTheLineupSize()
        {
            HeroCardData[] catalog = Heroes(6);
            catalog[5].isAvailable = false;
            Unlock(catalog[0]);

            for (int run = 0; run < 20; run++)   // padding is random: check the invariant repeatedly
            {
                DraftPoolStats stats = Build(catalog, minimumSize: 3);

                Assert.That(_pool, Has.Count.EqualTo(3));
                Assert.That(_pool, Is.Unique);
                Assert.That(_pool, Does.Contain(catalog[0]), "unlocked heroes are always in the pool");
                Assert.That(_pool, Has.No.Member(catalog[5]), "padding never uses unavailable heroes");
                Assert.That(stats.UnlockedCount, Is.EqualTo(1));
                Assert.That(stats.PaddedCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void NothingUnlocked_StillProducesAFullPool()
        {
            HeroCardData[] catalog = Heroes(5);

            DraftPoolStats stats = Build(catalog, minimumSize: 5);

            Assert.That(_pool, Is.EquivalentTo(catalog));
            Assert.That(stats.PaddedCount, Is.EqualTo(5));
        }

        [Test]
        public void CatalogSmallerThanLineup_ReturnsEverythingAvailable()
        {
            HeroCardData[] catalog = Heroes(2);

            DraftPoolStats stats = Build(catalog, minimumSize: 5);

            Assert.That(_pool, Has.Count.EqualTo(2));
            Assert.That(stats.PaddedCount, Is.EqualTo(2));
        }

        [Test]
        public void NullPredicate_TreatsEveryAvailableHeroAsUnlocked()
        {
            HeroCardData[] catalog = Heroes(4);
            catalog[3].isAvailable = false;

            DraftPoolStats stats = DraftPoolBuilder.Build(catalog, null, 2, _pool);

            Assert.That(_pool, Is.EquivalentTo(new[] { catalog[0], catalog[1], catalog[2] }));
            Assert.That(stats.PaddedCount, Is.Zero);
        }

        [Test]
        public void NullEntriesAndNullCatalog_AreHandled_AndThePoolIsCleared()
        {
            _pool.Add(Heroes(1)[0]);   // stale content from a previous build

            DraftPoolBuilder.Build(null, IsUnlocked, 3, _pool);
            Assert.That(_pool, Is.Empty);

            HeroCardData[] catalog = { null, Heroes(1)[0], null };
            Unlock(catalog[1]);
            Build(catalog, minimumSize: 1);
            Assert.That(_pool, Is.EqualTo(new[] { catalog[1] }));
        }

        private DraftPoolStats Build(HeroCardData[] catalog, int minimumSize) =>
            DraftPoolBuilder.Build(catalog, IsUnlocked, minimumSize, _pool);

        private bool IsUnlocked(HeroCardData hero) => _unlocked.Contains(hero);

        private void Unlock(params HeroCardData[] heroes)
        {
            foreach (HeroCardData hero in heroes) _unlocked.Add(hero);
        }

        private HeroCardData[] Heroes(int count)
        {
            var heroes = new HeroCardData[count];
            for (int i = 0; i < count; i++)
            {
                heroes[i] = ScriptableObject.CreateInstance<HeroCardData>();
                heroes[i].heroID = $"Draft_{_assets.Count}";
                heroes[i].isAvailable = true;
                _assets.Add(heroes[i]);
            }
            return heroes;
        }
    }
}
