using System.Collections.Generic;
using UnityEngine;

namespace GravityWells.Services
{
    // Turns unhandled exceptions into an analytics event. Bootstrap subscribes Handle to
    // Application.logMessageReceived, so whatever analytics backend is attached also gets the
    // game's errors. A real crash reporter (Crashlytics, a publisher's SDK) covers native
    // crashes as well; this covers managed exceptions with no dependency.
    public sealed class ExceptionReporter
    {
        // An exception thrown every frame must not turn into thousands of events.
        public const int MaxReportsPerSession = 20;
        private const int MaxTextLength = 200;

        private readonly IAnalyticsService _analytics;
        private readonly HashSet<string> _seen = new HashSet<string>();
        private int _reported;
        private bool _reporting;

        public int ReportedCount => _reported;

        public ExceptionReporter(IAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        public void Handle(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || _reporting || _reported >= MaxReportsPerSession)
            {
                return;
            }

            string where = FirstLine(stackTrace);
            if (!_seen.Add(condition + "|" + where))
            {
                return; // the same failure again; once is enough to know about it
            }

            _reported++;
            _reporting = true; // a backend that logs while reporting must not loop back in
            try
            {
                _analytics.LogEvent(
                    AnalyticsEvents.AppException,
                    new AnalyticsParam("message", Trim(condition)),
                    new AnalyticsParam("where", Trim(where)));
            }
            finally
            {
                _reporting = false;
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            int end = text.IndexOf('\n');
            return (end < 0 ? text : text.Substring(0, end)).Trim();
        }

        private static string Trim(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return text.Length <= MaxTextLength ? text : text.Substring(0, MaxTextLength);
        }
    }
}
