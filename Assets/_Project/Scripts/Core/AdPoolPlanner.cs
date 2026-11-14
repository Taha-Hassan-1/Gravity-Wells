using System;

namespace GravityWells.Core
{
    // The bookkeeping for keeping a few ads of one kind loaded ahead of time. It decides when another
    // load may start and never lets loads pile up: at most one request is in flight, and loaded plus
    // in-flight never exceeds the pool size. So when one load fails, the retry waits its turn instead of
    // racing a second one, and two loads can never both succeed beyond the capacity. Pure, so those
    // guarantees are tested without any ad SDK; the adapter just reports what happened.
    public sealed class AdPoolPlanner
    {
        public const float MinRetrySeconds = 5f;
        public const float MaxRetrySeconds = 120f;

        private float _backoff = MinRetrySeconds;

        public AdPoolPlanner(int capacity)
        {
            Capacity = Math.Max(1, capacity);
        }

        public int Capacity { get; }

        // Ads loaded and waiting to be shown.
        public int Ready { get; private set; }

        // Load requests started and not yet answered.
        public int InFlight { get; private set; }

        public float NextTryTime { get; private set; }

        public bool IsFull => Ready >= Capacity;

        // May another load start now? Only when the pool has room counting what is already on its way,
        // nothing is in flight, and any failure's wait is over.
        public bool ShouldStartLoad(float now) =>
            InFlight == 0 && Ready + InFlight < Capacity && now >= NextTryTime;

        public void OnLoadStarted() => InFlight++;

        // True when the new ad was kept; false when the pool was already full (the caller destroys it).
        public bool OnLoaded()
        {
            InFlight = Math.Max(0, InFlight - 1);
            _backoff = MinRetrySeconds;
            NextTryTime = 0f;
            if (Ready >= Capacity)
            {
                return false;
            }
            Ready++;
            return true;
        }

        // The wait before the next try doubles each time, up to a ceiling, and resets on success.
        public void OnLoadFailed(float now)
        {
            InFlight = Math.Max(0, InFlight - 1);
            NextTryTime = now + _backoff;
            _backoff = Math.Min(_backoff * 2f, MaxRetrySeconds);
        }

        // An ad left the pool: shown, expired, or thrown away.
        public void OnConsumed() => Ready = Math.Max(0, Ready - 1);
    }
}
