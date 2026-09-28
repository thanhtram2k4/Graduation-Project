using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HaoKhiSuViet.Tests
{
    /// <summary>Severity of a <see cref="RegressionLog"/> line.</summary>
    public enum RegressionLogLevel { Debug, Info, Warn, Error }

    /// <summary>
    /// Timestamped, thread-safe file logger for test runs.
    ///
    /// Line format (pipe-separated so it greps and imports cleanly):
    ///   2026-09-28 14:32:00.123 | INFO  | EventBus     | PUBLISH OngButPhaseChangedEvent NewPhase=Intro
    ///
    /// Output: Logs/RegressionTest_Output.log by default; override with
    /// "-regressionLog &lt;path&gt;". The file is truncated at run start unless
    /// "-regressionLogAppend" is passed (the CLI scripts pass it and rotate
    /// the file themselves so EditMode + PlayMode land in one log).
    /// </summary>
    public static class RegressionLog
    {
        /// <summary>Default log location, relative to the project root.</summary>
        public const string DefaultRelativePath = "Logs/RegressionTest_Output.log";

        private static readonly object Gate = new object();
        private static StreamWriter _writer;

        /// <summary>Absolute path of the open log file, or null when closed.</summary>
        public static string FilePath { get; private set; }

        /// <summary>True while a run is being logged.</summary>
        public static bool IsOpen
        {
            get { lock (Gate) return _writer != null; }
        }

        /// <summary>Opens the log for a new run and starts capturing Unity console output.</summary>
        public static void Open()
        {
            string path = TestArgs.ResolveProjectPath(TestArgs.Get("-regressionLog") ?? DefaultRelativePath);
            bool append = TestArgs.Has("-regressionLogAppend");

            lock (Gate)
            {
                CloseWriter();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                _writer = new StreamWriter(path, append, new UTF8Encoding(false)) { AutoFlush = true };
                FilePath = path;
            }

            Application.logMessageReceivedThreaded -= OnUnityLog;
            Application.logMessageReceivedThreaded += OnUnityLog;
        }

        /// <summary>Stops capturing and closes the file.</summary>
        public static void Close()
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            lock (Gate) CloseWriter();
        }

        /// <summary>Writes one line. No-op when the log is not open (e.g. tests run outside a logged run).</summary>
        public static void Write(RegressionLogLevel level, string category, string message)
        {
            lock (Gate)
            {
                if (_writer == null) return;

                _writer.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                _writer.Write(" | ");
                _writer.Write(LevelLabel(level));
                _writer.Write(" | ");
                _writer.Write(category.PadRight(12));
                _writer.Write(" | ");
                _writer.WriteLine(message);
            }
        }

        /// <summary>Writes an Info line.</summary>
        public static void Info(string category, string message) => Write(RegressionLogLevel.Info, category, message);

        /// <summary>Writes a Warn line.</summary>
        public static void Warn(string category, string message) => Write(RegressionLogLevel.Warn, category, message);

        /// <summary>Writes an Error line.</summary>
        public static void Error(string category, string message) => Write(RegressionLogLevel.Error, category, message);

        /// <summary>Writes a message followed by an indented multi-line block (stack traces, XML).</summary>
        public static void Block(RegressionLogLevel level, string category, string message, string block)
        {
            Write(level, category, message);
            if (string.IsNullOrEmpty(block)) return;

            lock (Gate)
            {
                if (_writer == null) return;
                foreach (string line in block.Split('\n'))
                {
                    string trimmed = line.TrimEnd('\r');
                    if (trimmed.Length > 0) _writer.WriteLine("        " + trimmed);
                }
            }
        }

        // Mirrors the Unity console into the log: game Debug.Log lines (manager
        // state traces) at Debug level, warnings, and errors/exceptions with stacks.
        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    Write(RegressionLogLevel.Debug, "UnityLog", condition);
                    break;
                case LogType.Warning:
                    Write(RegressionLogLevel.Warn, "UnityLog", condition);
                    break;
                default:
                    Block(RegressionLogLevel.Error, "UnityLog", $"{type}: {condition}", stackTrace);
                    break;
            }
        }

        private static void CloseWriter()
        {
            _writer?.Dispose();
            _writer = null;
        }

        private static string LevelLabel(RegressionLogLevel level)
        {
            switch (level)
            {
                case RegressionLogLevel.Debug: return "DEBUG";
                case RegressionLogLevel.Info:  return "INFO ";
                case RegressionLogLevel.Warn:  return "WARN ";
                default:                       return "ERROR";
            }
        }
    }
}
