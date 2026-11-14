using System;

namespace GravityWells.Services
{
    // Sends every event to several backends. Adding a real one (Firebase, a publisher's SDK)
    // is one more entry in the list Bootstrap builds; gameplay still sees one IAnalyticsService.
    public sealed class CompositeAnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsService[] _sinks;

        public CompositeAnalyticsService(params IAnalyticsService[] sinks)
        {
            _sinks = sinks ?? new IAnalyticsService[0];
        }

        public void LogEvent(string eventName, params AnalyticsParam[] parameters)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                try
                {
                    _sinks[i].LogEvent(eventName, parameters);
                }
                catch (Exception)
                {
                    // Reporting must never break the game, and one broken backend must not
                    // silence the others. Deliberately not logged: the exception reporter
                    // listens to the log and would feed it straight back in here.
                }
            }
        }
    }
}
