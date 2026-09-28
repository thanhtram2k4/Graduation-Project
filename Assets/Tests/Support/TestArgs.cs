using System;
using System.IO;
using UnityEngine;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Reads custom command-line arguments passed through the Unity CLI
    /// (Unity ignores flags it does not recognise, so tests can read them).
    /// </summary>
    public static class TestArgs
    {
        /// <summary>Returns the value after <paramref name="flag"/> (e.g. "-resmokeIterations"), or null.</summary>
        public static string Get(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        /// <summary>True if <paramref name="flag"/> is present on the command line.</summary>
        public static bool Has(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Parses the int after <paramref name="flag"/>, or returns <paramref name="fallback"/>.</summary>
        public static int GetInt(string flag, int fallback) =>
            int.TryParse(Get(flag), out int value) ? value : fallback;

        /// <summary>Unity project root (the folder containing Assets/).</summary>
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>Resolves <paramref name="path"/> against the project root if it is relative.</summary>
        public static string ResolveProjectPath(string path) =>
            Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ProjectRoot, path));
    }
}
