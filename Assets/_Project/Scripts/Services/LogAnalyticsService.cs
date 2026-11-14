using System.Text;
using UnityEngine;

namespace GravityWells.Services
{
    // Writes events to the console in the Editor and development builds, and drops them in
    // release builds (there is no backend yet, and a release shouldn't spam the device log).
    public sealed class LogAnalyticsService : IAnalyticsService
    {
        public void LogEvent(string eventName, params AnalyticsParam[] parameters)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var line = new StringBuilder("[Analytics] ").Append(eventName);
            foreach (AnalyticsParam parameter in parameters)
            {
                line.Append(' ').Append(parameter.Key).Append('=').Append(parameter.Value);
            }
            Debug.Log(line.ToString());
#endif
        }
    }
}
