using System;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

// Discovered by the Unity Test Framework in both Edit Mode and Play Mode runs,
// whether started from the CLI (-runTests) or the Test Runner window.
[assembly: TestRunCallback(typeof(HaoKhiSuViet.Tests.RegressionLogCallback))]

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Writes the structured regression log for every test run:
    /// run header, per-test START/PASS/FAIL lines, assertion messages and stack
    /// traces, test output, a per-test subscriber-leak check, and a summary.
    /// </summary>
    public sealed class RegressionLogCallback : ITestRunCallback
    {
        private readonly List<string> _failures = new List<string>();
        private SortedDictionary<string, List<string>> _subscribersAtTestStart;
        private int _subscriberLeaks;

        /// <inheritdoc />
        public void RunStarted(ITest testsToRun) => Guard(() =>
        {
            RegressionLog.Open();
            _failures.Clear();
            _subscriberLeaks = 0;

            string mode = Application.isPlaying ? "PlayMode" : "EditMode";
            RegressionLog.Info("Run", $"===== RUN START mode={mode} tests={testsToRun.TestCaseCount} unity={Application.unityVersion}");
            RegressionLog.Info("Run", $"args: {string.Join(" ", Environment.GetCommandLineArgs())}");
            EventBusTracer.LogSnapshot("Baseline subscribers", EventBusTracer.Snapshot());
        });

        /// <inheritdoc />
        public void TestStarted(ITest test) => Guard(() =>
        {
            if (test.IsSuite) return;

            RegressionLog.Info("Test", $"----- START {test.FullName}{IterationSuffix(test)}");
            _subscribersAtTestStart = EventBusTracer.Snapshot();
        });

        /// <inheritdoc />
        public void TestFinished(ITestResult result) => Guard(() =>
        {
            if (result.Test.IsSuite) return;

            TestStatus status = result.ResultState.Status;
            string label = status == TestStatus.Passed ? "PASS" : status.ToString().ToUpperInvariant();
            RegressionLogLevel level = status == TestStatus.Failed ? RegressionLogLevel.Error : RegressionLogLevel.Info;

            RegressionLog.Write(level, "Test", $"----- {label} {result.Test.FullName} ({result.Duration:0.000}s){IterationSuffix(result.Test)}");

            if (status == TestStatus.Failed)
            {
                RegressionLog.Block(RegressionLogLevel.Error, "Assertion", result.Message?.Trim(), result.StackTrace);
                _failures.Add($"{result.Test.FullName}: {FirstLine(result.Message)}");
            }
            else if (status != TestStatus.Passed && !string.IsNullOrEmpty(result.Message))
            {
                RegressionLog.Info("Test", $"reason: {FirstLine(result.Message)}");
            }

            if (!string.IsNullOrEmpty(result.Output))
                RegressionLog.Block(RegressionLogLevel.Debug, "TestOutput", "test output:", result.Output);

            CheckSubscriberLeak(result.Test.FullName);
        });

        /// <inheritdoc />
        public void RunFinished(ITestResult result) => Guard(() =>
        {
            int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
            RegressionLogLevel level = result.FailCount > 0 ? RegressionLogLevel.Error : RegressionLogLevel.Info;

            RegressionLog.Write(level, "Run",
                $"===== RUN FINISHED result={result.ResultState.Status} total={total} passed={result.PassCount} " +
                $"failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount} " +
                $"subscriberLeaks={_subscriberLeaks} duration={result.Duration:0.00}s");

            foreach (string failure in _failures)
                RegressionLog.Error("Run", $"FAILED {failure}");

            RegressionLog.Close();
        });

        private void CheckSubscriberLeak(string testName)
        {
            if (_subscribersAtTestStart == null) return;

            List<string> changes = EventBusTracer.Diff(_subscribersAtTestStart, EventBusTracer.Snapshot());
            if (changes.Count == 0) return;

            _subscriberLeaks++;
            RegressionLog.Warn("Subscribers", $"LEAK? {testName} left the subscriber set changed:");
            foreach (string change in changes)
                RegressionLog.Warn("Subscribers", "  " + change);
        }

        private static readonly bool IsRepeating = TestArgs.Has("-repeat");

        private static string IterationSuffix(ITest test)
        {
            if (!IsRepeating) return string.Empty;
            object iteration = test.Properties.Get("repeatIteration");
            return iteration == null ? string.Empty : $" [iteration {iteration}]";
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string trimmed = text.Trim();
            int newline = trimmed.IndexOf('\n');
            return newline < 0 ? trimmed : trimmed.Substring(0, newline).TrimEnd('\r');
        }

        // The framework rethrows callback exceptions into the run; a logging
        // failure must never fail or abort the tests themselves.
        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RegressionLogCallback] Logging failed: {e.Message}");
            }
        }
    }
}
