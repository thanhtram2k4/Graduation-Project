using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HaoKhiSuViet.Tests
{
    [TestFixture]
    [Category("Unit")]
    public class EraProgressionStateTests
    {
        private static readonly EraType[] AllEras = (EraType[])Enum.GetValues(typeof(EraType));

        private readonly List<Object> _assets = new List<Object>();
        private EraProgressionState _state;

        [SetUp]
        public void SetUp() => _state = new EraProgressionState();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void UnlockEra_IsIdempotent_AndIndependentPerEra([ValueSource(nameof(AllEras))] EraType era)
        {
            Assert.That(_state.UnlockEra(era), Is.True);
            Assert.That(_state.UnlockEra(era), Is.False, "second unlock must report no change");

            foreach (EraType other in AllEras)
                Assert.That(_state.IsEraUnlocked(other), Is.EqualTo(other == era), $"era {other}");
        }

        [Test]
        public void UnlockHero_RejectsNullAndMissingIds_AndIsIdempotent()
        {
            HeroCardData hero = Hero("H1", EraType.History);
            HeroCardData noId = Hero("", EraType.History);

            Assert.That(_state.UnlockHero(null), Is.False);
            Assert.That(_state.UnlockHero(noId), Is.False);
            Assert.That(_state.UnlockHero(hero), Is.True);
            Assert.That(_state.UnlockHero(hero), Is.False);
            Assert.That(_state.UnlockedHeroCount, Is.EqualTo(1));
        }

        [Test]
        public void PickLockedHero_OnlyReturnsAvailableLockedHeroesOfTheEra([ValueSource(nameof(AllEras))] EraType era)
        {
            HeroCardData target = Hero("Target", era);
            HeroCardData alreadyUnlocked = Hero("Unlocked", era);
            HeroCardData unavailable = Hero("Unavailable", era, available: false);
            var catalog = new List<HeroCardData> { target, alreadyUnlocked, unavailable };
            foreach (EraType other in AllEras)
                if (other != era) catalog.Add(Hero($"Other_{other}", other));

            _state.UnlockHero(alreadyUnlocked);

            for (float roll = 0f; roll < 1f; roll += 0.1f)
                Assert.That(_state.PickLockedHero(era, catalog.ToArray(), roll), Is.SameAs(target), $"roll {roll}");
        }

        [Test]
        public void PickLockedHero_CoversEveryCandidate_AndClampsRollOfOne()
        {
            HeroCardData[] catalog = { Hero("A", EraType.Legend), Hero("B", EraType.Legend), Hero("C", EraType.Legend) };

            Assert.That(_state.PickLockedHero(EraType.Legend, catalog, 0f), Is.SameAs(catalog[0]));
            Assert.That(_state.PickLockedHero(EraType.Legend, catalog, 0.5f), Is.SameAs(catalog[1]));
            Assert.That(_state.PickLockedHero(EraType.Legend, catalog, 0.99f), Is.SameAs(catalog[2]));
            Assert.That(_state.PickLockedHero(EraType.Legend, catalog, 1f), Is.SameAs(catalog[2]), "1.0 must not index out of range");
        }

        [Test]
        public void PickLockedHero_ReturnsNull_WhenEraIsExhaustedOrCatalogMissing()
        {
            HeroCardData only = Hero("Only", EraType.Culture);
            _state.UnlockHero(only);

            Assert.That(_state.PickLockedHero(EraType.Culture, new[] { only }, 0.3f), Is.Null);
            Assert.That(_state.PickLockedHero(EraType.Culture, Array.Empty<HeroCardData>(), 0.3f), Is.Null);
            Assert.That(_state.PickLockedHero(EraType.Culture, null, 0.3f), Is.Null);
        }

        [Test]
        public void SaveData_RoundTripsErasAndHeroes()
        {
            HeroCardData a = Hero("A", EraType.History);
            HeroCardData b = Hero("B", EraType.FairyTale);
            _state.UnlockEra(EraType.History);
            _state.UnlockEra(EraType.FairyTale);
            _state.UnlockHero(a);
            _state.UnlockHero(b);

            var data = new EraProgressSaveData();
            _state.WriteTo(data);
            var json = JsonUtility.ToJson(data);

            var restored = new EraProgressionState();
            restored.LoadFrom(JsonUtility.FromJson<EraProgressSaveData>(json));

            Assert.That(data.saveFormatVersion, Is.EqualTo(EraProgressionState.CurrentSaveFormatVersion));
            Assert.That(restored.IsEraUnlocked(EraType.History) && restored.IsEraUnlocked(EraType.FairyTale), Is.True);
            Assert.That(restored.IsEraUnlocked(EraType.Legend), Is.False);
            Assert.That(restored.IsHeroUnlocked(a) && restored.IsHeroUnlocked(b), Is.True);
            Assert.That(restored.UnlockedHeroCount, Is.EqualTo(2));
        }

        [Test]
        public void LoadFrom_SkipsEmptyIds_AndReplacesPreviousState()
        {
            _state.UnlockHero(Hero("Stale", EraType.History));

            _state.LoadFrom(new EraProgressSaveData { unlockedHeroIDs = new List<string> { "X", "", null } });

            Assert.That(_state.UnlockedHeroCount, Is.EqualTo(1));
            Assert.That(_state.IsHeroUnlocked(Hero("X", EraType.History)), Is.True);
        }

        private HeroCardData Hero(string id, EraType era, bool available = true)
        {
            var hero = ScriptableObject.CreateInstance<HeroCardData>();
            hero.heroID = id;
            hero.eraType = era;
            hero.isAvailable = available;
            _assets.Add(hero);
            return hero;
        }
    }
}
