using GravityWells.Services;

namespace GravityWells.Game
{
    // The one sanctioned singleton-shaped container in this codebase (CLAUDE.md) — a
    // single instance created by Bootstrap and handed explicitly to whatever needs it,
    // never reached through a static/global.
    public sealed class ServiceLocator
    {
        public HapticService Haptics { get; }
        public AudioService Audio { get; }
        public IAdService Ads { get; }
        public IPurchaseService Purchases { get; }
        public IConsentService Consent { get; }
        public IAnalyticsService Analytics { get; }
        public IShareService Share { get; }

        public ServiceLocator(
            HapticService haptics, AudioService audio, IAdService ads,
            IPurchaseService purchases, IConsentService consent, IAnalyticsService analytics,
            IShareService share)
        {
            Haptics = haptics;
            Audio = audio;
            Ads = ads;
            Purchases = purchases;
            Consent = consent;
            Analytics = analytics;
            Share = share;
        }
    }
}
