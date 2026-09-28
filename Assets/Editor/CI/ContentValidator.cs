using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HaoKhiSuViet.EditorCI
{
    /// <summary>
    /// Validates the era progression / trivia content that runtime code trusts
    /// but cannot check cheaply. Failures here would otherwise surface only as a
    /// silent in-game no-op (e.g. a Pagoda click that does nothing because the
    /// level's era has no questions).
    ///
    /// CLI:  Unity -batchmode -nographics -projectPath . -quit
    ///             -executeMethod HaoKhiSuViet.EditorCI.ContentValidator.RunFromCommandLine
    ///             [-contentValidationLog Logs/ContentValidation.log]
    /// Exit code 0 = no errors (warnings allowed), 1 = errors found.
    /// Menu: HKSV ▸ CI ▸ Validate Era Content.
    /// </summary>
    public static class ContentValidator
    {
        private const string DefaultLogPath = "Logs/ContentValidation.log";
        private const string HeroCardFolder = "Assets/Resources/Data/HeroCards";

        /// <summary>Entry point for -executeMethod. Exits the editor with 0 (ok) or 1 (errors).</summary>
        public static void RunFromCommandLine()
        {
            int errors;
            try
            {
                errors = Validate(ResolveLogPath());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                errors = 1;
            }
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        /// <summary>Runs validation from the editor menu and reports in the console.</summary>
        [MenuItem("HKSV/CI/Validate Era Content")]
        public static void RunFromMenu()
        {
            int errors = Validate(ResolveLogPath());
            Debug.Log(errors == 0
                ? "[ContentValidator] Era content OK."
                : $"[ContentValidator] {errors} error(s). See {DefaultLogPath}.");
        }

        /// <summary>Validates all content and writes a report to <paramref name="logPath"/>.</summary>
        /// <returns>Number of errors found.</returns>
        public static int Validate(string logPath)
        {
            var report = new Report();
            report.Info($"===== CONTENT VALIDATION START unity={Application.unityVersion}");

            List<QuestionBankData> banks = CollectBanks(report);
            List<HeroCardData> heroes = LoadAll<HeroCardData>(new[] { HeroCardFolder });

            ValidateQuestions(report);
            ValidateHeroes(heroes, report);
            ValidateLevels(banks, heroes, report);

            report.Info($"===== CONTENT VALIDATION FINISHED errors={report.Errors} warnings={report.Warnings}");
            report.WriteTo(logPath);
            return report.Errors;
        }

        // Question banks the game actually uses: those referenced by an OngButSessionConfig.
        private static List<QuestionBankData> CollectBanks(Report report)
        {
            var banks = new List<QuestionBankData>();
            foreach (OngButSessionConfig config in LoadAll<OngButSessionConfig>())
            {
                if (config.QuestionBank == null)
                {
                    report.Error($"OngButSessionConfig '{AssetPath(config)}' has no question bank.");
                    continue;
                }
                if (!banks.Contains(config.QuestionBank)) banks.Add(config.QuestionBank);
            }

            if (banks.Count == 0)
                report.Error("No OngButSessionConfig references a QuestionBankData; Ông Bụt cannot start.");

            return banks;
        }

        private static void ValidateQuestions(Report report)
        {
            foreach (HistoricalQuestionData question in LoadAll<HistoricalQuestionData>())
            {
                string where = AssetPath(question);
                if (string.IsNullOrWhiteSpace(question.QuestionID)) report.Error($"{where}: questionID is empty.");
                if (string.IsNullOrWhiteSpace(question.QuestionText)) report.Error($"{where}: questionText is empty.");
                if (question.Answers == null || question.Answers.Length != 4) report.Error($"{where}: must have exactly 4 answers.");
                if (question.CorrectAnswerIndex < 0 || question.CorrectAnswerIndex > 3) report.Error($"{where}: correctAnswerIndex out of range.");
            }
        }

        private static void ValidateHeroes(List<HeroCardData> heroes, Report report)
        {
            var seenIds = new Dictionary<string, string>();
            foreach (HeroCardData hero in heroes)
            {
                string where = AssetPath(hero);
                if (string.IsNullOrWhiteSpace(hero.heroID))
                {
                    report.Error($"{where}: heroID is empty; it cannot be saved as unlocked.");
                    continue;
                }
                if (seenIds.TryGetValue(hero.heroID, out string other))
                    report.Error($"{where}: heroID '{hero.heroID}' duplicates {other}; unlocks would collide.");
                else
                    seenIds[hero.heroID] = where;
            }
        }

        private static void ValidateLevels(List<QuestionBankData> banks, List<HeroCardData> heroes, Report report)
        {
            foreach (LevelConfig level in LoadAll<LevelConfig>())
            {
                string where = $"{AssetPath(level)} (era {level.levelEra})";

                foreach (QuestionBankData bank in banks)
                {
                    int available = CountQuestions(bank, level.levelEra);
                    if (available == 0)
                        report.Error($"{where}: bank '{bank.name}' has no {level.levelEra} questions; the Pagoda will do nothing on this level.");
                    else if (available < bank.QuestionsPerSession)
                        report.Warn($"{where}: bank '{bank.name}' has {available} {level.levelEra} questions < {bank.QuestionsPerSession} per session.");
                    else
                        report.Info($"{where}: {available} {level.levelEra} questions in '{bank.name}'.");
                }

                int unlockable = 0;
                foreach (HeroCardData hero in heroes)
                    if (hero.isAvailable && hero.eraType == level.levelEra) unlockable++;

                if (unlockable == 0)
                    report.Warn($"{where}: no available {level.levelEra} heroes; correct answers will unlock nothing.");
            }
        }

        private static int CountQuestions(QuestionBankData bank, EraType era)
        {
            // Read the serialized list directly so the check does not mutate the bank.
            var serialized = new SerializedObject(bank).FindProperty("questions");
            int count = 0;
            for (int i = 0; i < serialized.arraySize; i++)
            {
                var question = serialized.GetArrayElementAtIndex(i).objectReferenceValue as HistoricalQuestionData;
                if (question != null && question.Era == era) count++;
            }
            return count;
        }

        private static List<T> LoadAll<T>(string[] folders = null) where T : UnityEngine.Object
        {
            var results = new List<T>();
            string filter = "t:" + typeof(T).Name;
            string[] guids = folders == null ? AssetDatabase.FindAssets(filter) : AssetDatabase.FindAssets(filter, folders);
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) results.Add(asset);
            }
            return results;
        }

        private static string AssetPath(UnityEngine.Object asset) => AssetDatabase.GetAssetPath(asset);

        private static string ResolveLogPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            string path = DefaultLogPath;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-contentValidationLog") path = args[i + 1];

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path);
        }

        // Same line format as the test RegressionLog so both can be read together.
        private sealed class Report
        {
            private readonly StringBuilder _lines = new StringBuilder();

            public int Errors { get; private set; }
            public int Warnings { get; private set; }

            public void Info(string message) => Add("INFO ", message);
            public void Warn(string message) { Warnings++; Add("WARN ", message); }
            public void Error(string message) { Errors++; Add("ERROR", message); }

            public void WriteTo(string path)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, _lines.ToString(), new UTF8Encoding(false));
                Debug.Log($"[ContentValidator] errors={Errors} warnings={Warnings} report={path}");
            }

            private void Add(string level, string message)
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {level} | {"Content",-12} | {message}";
                _lines.AppendLine(line);
                if (level == "ERROR") Debug.LogError("[ContentValidator] " + message);
                else if (level == "WARN ") Debug.LogWarning("[ContentValidator] " + message);
            }
        }
    }
}
