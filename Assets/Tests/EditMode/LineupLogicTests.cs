using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HaoKhiSuViet.Tests
{
    [TestFixture]
    [Category("Unit")]
    public class LineupLogicTests
    {
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // ── HeroDeck ────────────────────────────────────────────────────────

        [Test]
        public void Deck_ShuffleThenDraw_ReturnsEveryCardExactlyOnce()
        {
            List<HeroCardData> source = Heroes(8);
            var deck = new HeroDeck(4);   // smaller than the source: Fill must grow it

            deck.Fill(source);
            deck.Shuffle();

            var drawn = new List<HeroCardData>();
            while (!deck.IsExhausted) drawn.Add(deck.Draw());

            Assert.That(deck.Size, Is.EqualTo(8));
            Assert.That(drawn, Is.EquivalentTo(source));
        }

        [Test]
        public void Deck_Refill_RewindsDrawingAndReplacesContents()
        {
            var deck = new HeroDeck(10);
            deck.Fill(Heroes(5));
            deck.Draw();
            deck.Draw();

            List<HeroCardData> second = Heroes(2);
            deck.Fill(second);

            Assert.That(deck.DrawIndex, Is.Zero);
            Assert.That(deck.Size, Is.EqualTo(2));
            Assert.That(new[] { deck.Draw(), deck.Draw() }, Is.EqualTo(second));
            Assert.That(deck.IsExhausted, Is.True);
        }

        // ── BlindPickSession ────────────────────────────────────────────────

        [Test]
        public void Pick_IsRejectedUntilTheShuffleFinishes()
        {
            var session = new BlindPickSession(3);
            HeroDeck deck = FilledDeck(3);

            Assert.That(session.TryPick(deck, out HeroCardData hero), Is.EqualTo(BlindPickRejection.NotAwaitingPicks));
            Assert.That(hero, Is.Null);
            Assert.That(deck.DrawIndex, Is.Zero, "a rejected pick must not consume a card");
        }

        [Test]
        public void RevealLock_BlocksTheNextPickUntilReleased()
        {
            var session = new BlindPickSession(3);
            HeroDeck deck = FilledDeck(3);
            session.BeginPicking();

            Assert.That(session.TryPick(deck, out _), Is.EqualTo(BlindPickRejection.None));
            Assert.That(session.TryPick(deck, out _), Is.EqualTo(BlindPickRejection.RevealInProgress));

            session.ReleaseRevealLock();
            Assert.That(session.TryPick(deck, out _), Is.EqualTo(BlindPickRejection.None));
            Assert.That(session.DrawnCount, Is.EqualTo(2));
        }

        [Test]
        public void FullLineup_StopsPicking_AndRejectsFurtherClicks()
        {
            var session = new BlindPickSession(2);
            HeroDeck deck = FilledDeck(5);
            session.BeginPicking();

            PickAndRelease(session, deck, 2);

            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.AwaitingPicks, Is.False);
            Assert.That(session.TryPick(deck, out _), Is.EqualTo(BlindPickRejection.NotAwaitingPicks));
        }

        [Test]
        public void ExhaustedDeck_IsReported()
        {
            var session = new BlindPickSession(3);
            HeroDeck deck = FilledDeck(1);
            session.BeginPicking();

            PickAndRelease(session, deck, 1);

            Assert.That(session.TryPick(deck, out _), Is.EqualTo(BlindPickRejection.DeckExhausted));
        }

        [Test]
        public void Confirm_SucceedsOnce_AndResetAllowsANewDraft()
        {
            var session = new BlindPickSession(1);
            session.BeginPicking();
            PickAndRelease(session, FilledDeck(1), 1);

            Assert.That(session.TryConfirm(), Is.True);
            Assert.That(session.TryConfirm(), Is.False);

            session.Reset();
            Assert.That(session.IsConfirmed, Is.False);
            Assert.That(session.DrawnCount, Is.Zero);
            Assert.That(session.GetEntry(0), Is.Null);
        }

        [Test]
        public void Restore_AcceptsALargerSavedLineup_AndBlocksConfirm()
        {
            var session = new BlindPickSession(2);
            List<HeroCardData> saved = Heroes(4);

            session.Restore(saved.ToArray());

            Assert.That(session.DrawnCount, Is.EqualTo(4));
            Assert.That(session.GetEntry(3), Is.SameAs(saved[3]));
            Assert.That(session.GetEntry(4), Is.Null);
            Assert.That(session.GetEntry(-1), Is.Null);
            Assert.That(session.TryConfirm(), Is.False, "a restored lineup is already confirmed");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void PickAndRelease(BlindPickSession session, HeroDeck deck, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.That(session.TryPick(deck, out _), Is.EqualTo(BlindPickRejection.None), $"pick {i}");
                session.ReleaseRevealLock();
            }
        }

        private HeroDeck FilledDeck(int count)
        {
            var deck = new HeroDeck(count);
            deck.Fill(Heroes(count));
            return deck;
        }

        private List<HeroCardData> Heroes(int count)
        {
            var heroes = new List<HeroCardData>(count);
            for (int i = 0; i < count; i++)
            {
                var hero = ScriptableObject.CreateInstance<HeroCardData>();
                hero.heroID = $"Lineup_{_assets.Count}";
                _assets.Add(hero);
                heroes.Add(hero);
            }
            return heroes;
        }
    }
}
