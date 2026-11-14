using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class AnalyticsSinkTests
    {
        private sealed class Recorder : IAnalyticsService
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<AnalyticsParam[]> Params = new List<AnalyticsParam[]>();

            public void LogEvent(string eventName, params AnalyticsParam[] parameters)
            {
                Events.Add(eventName);
                Params.Add(parameters);
            }
        }

        private sealed class Thrower : IAnalyticsService
        {
            public void LogEvent(string eventName, params AnalyticsParam[] parameters) =>
                throw new InvalidOperationException("backend down");
        }

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GravityWellsAnalyticsTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        // ---- composite ----

        [Test]
        public void Composite_SendsEveryEventToEverySink()
        {
            var a = new Recorder();
            var b = new Recorder();
            var composite = new CompositeAnalyticsService(a, b);

            composite.LogEvent("level_start", new AnalyticsParam("level", 3));

            Assert.AreEqual(new[] { "level_start" }, a.Events);
            Assert.AreEqual(new[] { "level_start" }, b.Events);
            Assert.AreEqual(3, b.Params[0][0].Value);
        }

        [Test]
        public void Composite_ABrokenSink_DoesNotThrowOrSilenceTheOthers()
        {
            var after = new Recorder();
            var composite = new CompositeAnalyticsService(new Thrower(), after);

            Assert.DoesNotThrow(() => composite.LogEvent("level_start"));
            Assert.AreEqual(1, after.Events.Count);
        }

        // ---- file ----

        [Test]
        public void File_WritesOneTabSeparatedLinePerEvent()
        {
            var time = new DateTime(2026, 11, 10, 17, 14, 54, DateTimeKind.Utc);
            var file = new FileAnalyticsService(_tempDir, () => time, 1024);

            file.LogEvent("level_complete", new AnalyticsParam("level", 4), new AnalyticsParam("mode", "campaign"));
            file.LogEvent("daily_started");

            string[] lines = File.ReadAllLines(Path.Combine(_tempDir, FileAnalyticsService.FileName));
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("2026-11-10T17:14:54Z\tlevel_complete\tlevel=4\tmode=campaign", lines[0]);
            Assert.AreEqual("2026-11-10T17:14:54Z\tdaily_started", lines[1]);
        }

        [Test]
        public void File_ValueWithLineBreaksOrTabs_StaysOnOneLine()
        {
            var file = new FileAnalyticsService(_tempDir, () => DateTime.UtcNow, 1024);

            file.LogEvent("app_exception", new AnalyticsParam("message", "bad\nthing\thappened"));

            string[] lines = File.ReadAllLines(Path.Combine(_tempDir, FileAnalyticsService.FileName));
            Assert.AreEqual(1, lines.Length);
            StringAssert.EndsWith("message=bad thing happened", lines[0]);
        }

        [Test]
        public void File_AtTheSizeLimit_RotatesAndNeverKeepsMoreThanTwoFiles()
        {
            var file = new FileAnalyticsService(_tempDir, () => DateTime.UtcNow, 200);

            for (int i = 0; i < 100; i++)
            {
                file.LogEvent("level_start", new AnalyticsParam("level", i));
            }

            Assert.AreEqual(2, Directory.GetFiles(_tempDir).Length);
            Assert.Less(new FileInfo(Path.Combine(_tempDir, FileAnalyticsService.FileName)).Length, 300);
            Assert.Less(new FileInfo(Path.Combine(_tempDir, FileAnalyticsService.PreviousFileName)).Length, 300);
        }

        [Test]
        public void File_UnwritableDirectory_NeverThrows()
        {
            var file = new FileAnalyticsService(Path.Combine(_tempDir, "missing", "folder"), () => DateTime.UtcNow, 1024);

            Assert.DoesNotThrow(() => file.LogEvent("level_start"));
            Assert.DoesNotThrow(() => file.LogEvent("level_start"));
        }

        // ---- exceptions ----

        [Test]
        public void Exceptions_AnUnhandledException_BecomesOneEventWithMessageAndPlace()
        {
            var recorder = new Recorder();
            var reporter = new ExceptionReporter(recorder);

            reporter.Handle("NullReferenceException: boom", "GravityWells.Game.BoardView.LoadLevel ()\nmore", LogType.Exception);

            Assert.AreEqual(new[] { AnalyticsEvents.AppException }, recorder.Events);
            Assert.AreEqual("NullReferenceException: boom", recorder.Params[0][0].Value);
            Assert.AreEqual("GravityWells.Game.BoardView.LoadLevel ()", recorder.Params[0][1].Value);
        }

        [Test]
        public void Exceptions_OrdinaryLogsWarningsAndErrors_AreIgnored()
        {
            var recorder = new Recorder();
            var reporter = new ExceptionReporter(recorder);

            reporter.Handle("hello", "", LogType.Log);
            reporter.Handle("careful", "", LogType.Warning);
            reporter.Handle("oops", "", LogType.Error);

            Assert.AreEqual(0, recorder.Events.Count);
        }

        [Test]
        public void Exceptions_TheSameFailureEveryFrame_IsReportedOnce()
        {
            var recorder = new Recorder();
            var reporter = new ExceptionReporter(recorder);

            for (int i = 0; i < 500; i++)
            {
                reporter.Handle("NullReferenceException: boom", "A.B ()", LogType.Exception);
            }

            Assert.AreEqual(1, recorder.Events.Count);
        }

        [Test]
        public void Exceptions_ManyDifferentFailures_StopAtTheSessionCap()
        {
            var recorder = new Recorder();
            var reporter = new ExceptionReporter(recorder);

            for (int i = 0; i < 100; i++)
            {
                reporter.Handle("Exception " + i, "A.B ()", LogType.Exception);
            }

            Assert.AreEqual(ExceptionReporter.MaxReportsPerSession, recorder.Events.Count);
        }

        [Test]
        public void Exceptions_ABackendThatThrows_DoesNotBreakTheReporter()
        {
            var reporter = new ExceptionReporter(new CompositeAnalyticsService(new Thrower()));

            Assert.DoesNotThrow(() => reporter.Handle("Exception", "A.B ()", LogType.Exception));
        }
    }
}
