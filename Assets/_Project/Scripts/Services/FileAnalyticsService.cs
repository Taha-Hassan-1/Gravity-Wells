using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GravityWells.Services
{
    // Appends each event as one line to a small file next to the save. It sends nothing
    // anywhere: it exists so a release build with no analytics backend still leaves a record
    // that a tester (or a support request) can pull off the device. The file is capped: when it
    // reaches the limit it becomes the ".1" file and a new one starts, so at most two are kept.
    public sealed class FileAnalyticsService : IAnalyticsService
    {
        public const string FileName = "events.log";
        public const string PreviousFileName = "events.1.log";

        private readonly string _path;
        private readonly string _previousPath;
        private readonly Func<DateTime> _utcNow;
        private readonly long _maxBytes;
        private readonly StringBuilder _line = new StringBuilder(160);
        private bool _broken; // the disk refused once; stop trying rather than fail on every event

        public FileAnalyticsService(string directory, Func<DateTime> utcNow, long maxBytes)
        {
            _path = Path.Combine(directory, FileName);
            _previousPath = Path.Combine(directory, PreviousFileName);
            _utcNow = utcNow;
            _maxBytes = maxBytes;
        }

        public void LogEvent(string eventName, params AnalyticsParam[] parameters)
        {
            if (_broken)
            {
                return;
            }

            try
            {
                RotateIfFull();
                File.AppendAllText(_path, Format(eventName, parameters));
            }
            catch (Exception)
            {
                _broken = true;
            }
        }

        // "2026-11-10T17:14:54Z<TAB>level_complete<TAB>level=4<TAB>moves=9". Tabs and line breaks
        // inside a value are flattened so one event is always exactly one line.
        private string Format(string eventName, AnalyticsParam[] parameters)
        {
            _line.Length = 0;
            _line.Append(_utcNow().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            _line.Append('\t').Append(eventName);
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    _line.Append('\t').Append(parameters[i].Key).Append('=');
                    AppendValue(parameters[i].Value);
                }
            }
            _line.Append('\n');
            return _line.ToString();
        }

        private void AppendValue(object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                _line.Append(c == '\t' || c == '\n' || c == '\r' ? ' ' : c);
            }
        }

        private void RotateIfFull()
        {
            var info = new FileInfo(_path);
            if (!info.Exists || info.Length < _maxBytes)
            {
                return;
            }
            if (File.Exists(_previousPath))
            {
                File.Delete(_previousPath);
            }
            File.Move(_path, _previousPath);
        }
    }
}
