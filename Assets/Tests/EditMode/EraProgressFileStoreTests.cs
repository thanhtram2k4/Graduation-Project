using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HaoKhiSuViet.Tests
{
    [TestFixture]
    [Category("Unit")]
    public class EraProgressFileStoreTests
    {
        private const int FlushTimeoutMs = 5000;

        private string _folder;
        private EraProgressFileStore _store;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "HKSV_FileStoreTests_" + Guid.NewGuid().ToString("N"));
            _store = new EraProgressFileStore(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            _store.Flush(FlushTimeoutMs);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void TryLoad_WithNoFiles_ReturnsFalse()
        {
            Assert.That(_store.TryLoad(new EraProgressSaveData(), out bool usedBackup), Is.False);
            Assert.That(usedBackup, Is.False);
        }

        [Test]
        public void SaveAsync_ThenTryLoad_RoundTrips()
        {
            Save(mask: 0b101, "A", "B");

            var loaded = new EraProgressSaveData();
            Assert.That(_store.TryLoad(loaded, out bool usedBackup), Is.True);
            Assert.That(usedBackup, Is.False);
            Assert.That(loaded.unlockedEraMask, Is.EqualTo(0b101));
            Assert.That(loaded.unlockedHeroIDs, Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void SecondSave_KeepsThePreviousFileAsBackup_AndLeavesNoTempFile()
        {
            Save(mask: 1, "First");
            Save(mask: 3, "First", "Second");

            Assert.That(File.Exists(_store.BackupPath), Is.True);
            Assert.That(File.ReadAllText(_store.BackupPath), Does.Contain("First").And.Not.Contain("Second"));
            Assert.That(Directory.GetFiles(_folder, "*.tmp"), Is.Empty);
        }

        [Test]
        public void CorruptMainFile_FallsBackToBackup()
        {
            Save(mask: 1, "Good");
            Save(mask: 3, "Good", "Newer");
            File.WriteAllText(_store.SavePath, "{ not json");

            LogAssert.Expect(LogType.Warning, new Regex("Failed to parse"));

            var loaded = new EraProgressSaveData();
            Assert.That(_store.TryLoad(loaded, out bool usedBackup), Is.True);
            Assert.That(usedBackup, Is.True);
            Assert.That(loaded.unlockedHeroIDs, Is.EqualTo(new[] { "Good" }));
        }

        [Test]
        public void QueuedWrites_LandInOrder()
        {
            for (int i = 0; i < 20; i++)
                _store.SaveAsync(JsonUtility.ToJson(new EraProgressSaveData { unlockedEraMask = i }));

            Assert.That(_store.Flush(FlushTimeoutMs), Is.True);

            var loaded = new EraProgressSaveData();
            _store.TryLoad(loaded, out _);
            Assert.That(loaded.unlockedEraMask, Is.EqualTo(19), "the last queued write must win");
        }

        private void Save(int mask, params string[] heroIds)
        {
            var data = new EraProgressSaveData
            {
                saveFormatVersion = EraProgressionState.CurrentSaveFormatVersion,
                unlockedEraMask = mask,
                unlockedHeroIDs = new List<string>(heroIds)
            };
            _store.SaveAsync(JsonUtility.ToJson(data));
            Assert.That(_store.Flush(FlushTimeoutMs), Is.True, "write did not finish");
        }
    }
}
