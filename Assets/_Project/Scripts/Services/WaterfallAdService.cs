using System;
using System.Collections.Generic;

namespace GravityWells.Services
{
    // Several ad networks behind the one IAdService the game already uses. Providers are tried in
    // priority order: the first that has an ad ready serves it, and if it then fails the next ready
    // one is tried, so a single network's outage or empty inventory doesn't leave the player with
    // nothing. The order can be changed at runtime (SetOrder), which is the hook for remote config,
    // regional choices or A/B tests — no gameplay code changes.
    //
    // This is a client-side fallback chain, not real-time bidding. For the best revenue use a
    // mediation SDK (AppLovin MAX, LevelPlay) as ONE provider here and keep this for fallback.
    public sealed class WaterfallAdService : IAdService
    {
        private readonly AdProviderRegistry _registry;
        private readonly Action<string> _log;
        private readonly Dictionary<string, IAdService> _created =
            new Dictionary<string, IAdService>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IAdService> _active = new List<IAdService>();

        private bool _initialized;

        public WaterfallAdService(AdProviderRegistry registry, IEnumerable<string> order, Action<string> log = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _log = log;
            SetOrder(order);
        }

        public string Name => "waterfall";

        // The provider that served the most recent ad, for analytics.
        public string LastServedBy { get; private set; }

        public IReadOnlyList<IAdService> Active => _active;

        // Replaces the active providers and their priority. Names that aren't registered are
        // skipped with a log line; providers left out stop being asked (ads they hold are kept).
        public void SetOrder(IEnumerable<string> names)
        {
            _active.Clear();
            if (names != null)
            {
                foreach (string name in names)
                {
                    IAdService provider = Resolve(name);
                    if (provider != null && !_active.Contains(provider))
                    {
                        _active.Add(provider);
                    }
                }
            }

            if (_initialized)
            {
                InitializeActive();
            }
        }

        public void Initialize()
        {
            _initialized = true;
            InitializeActive();
        }

        public void Tick()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].Tick();
            }
        }

        // Every active provider is polled, even after one answers yes, because polling is what
        // keeps a provider's own next ad loading in the background.
        public bool IsInterstitialReady
        {
            get
            {
                bool ready = false;
                for (int i = 0; i < _active.Count; i++)
                {
                    ready |= _active[i].IsInterstitialReady;
                }
                return ready;
            }
        }

        public bool IsRewardedReady
        {
            get
            {
                bool ready = false;
                for (int i = 0; i < _active.Count; i++)
                {
                    ready |= _active[i].IsRewardedReady;
                }
                return ready;
            }
        }

        // Ready beats Loading beats Idle; it is Failed only when every provider has failed.
        public AdLoadState RewardedState
        {
            get
            {
                if (_active.Count == 0)
                {
                    return AdLoadState.Failed;
                }

                bool loading = false, idle = false;
                for (int i = 0; i < _active.Count; i++)
                {
                    switch (_active[i].RewardedState)
                    {
                        case AdLoadState.Ready: return AdLoadState.Ready;
                        case AdLoadState.Loading: loading = true; break;
                        case AdLoadState.Idle: idle = true; break;
                    }
                }
                return loading ? AdLoadState.Loading : idle ? AdLoadState.Idle : AdLoadState.Failed;
            }
        }

        // When everything failed, report the reason that tells the player the most: a problem
        // with their connection outranks "this one network had no ad".
        public AdFailure RewardedFailure
        {
            get
            {
                if (_active.Count == 0)
                {
                    return AdFailure.Other;
                }

                AdFailure best = AdFailure.None;
                for (int i = 0; i < _active.Count; i++)
                {
                    if (_active[i].RewardedState != AdLoadState.Failed)
                    {
                        continue;
                    }
                    AdFailure failure = _active[i].RewardedFailure;
                    if (Rank(failure) > Rank(best))
                    {
                        best = failure;
                    }
                }
                return best;
            }
        }

        public void RequestRewarded()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].RequestRewarded(); // each provider ignores it when it already has or is loading one
            }
        }

        public void ShowInterstitial(Action<AdOutcome> onComplete) => Show(false, 0, AdOutcome.NotReady, onComplete);

        public void ShowRewarded(Action<AdOutcome> onComplete) => Show(true, 0, AdOutcome.NotReady, onComplete);

        // Skipped is final (the player chose to close it), so only NotReady/Failed move on to the next provider.
        private void Show(bool rewarded, int from, AdOutcome ifExhausted, Action<AdOutcome> onComplete)
        {
            for (int i = from; i < _active.Count; i++)
            {
                IAdService provider = _active[i];
                if (!(rewarded ? provider.IsRewardedReady : provider.IsInterstitialReady))
                {
                    continue;
                }

                int next = i + 1;
                Action<AdOutcome> done = outcome =>
                {
                    if (outcome == AdOutcome.Failed || outcome == AdOutcome.NotReady)
                    {
                        _log?.Invoke($"[Ads] {provider.Name} could not show the ad ({outcome}); trying the next provider");
                        Show(rewarded, next, outcome, onComplete);
                        return;
                    }
                    LastServedBy = provider.Name;
                    onComplete?.Invoke(outcome);
                };

                if (rewarded)
                {
                    provider.ShowRewarded(done);
                }
                else
                {
                    provider.ShowInterstitial(done);
                }
                return;
            }

            onComplete?.Invoke(ifExhausted);
        }

        private IAdService Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            if (_created.TryGetValue(name.Trim(), out IAdService existing))
            {
                return existing;
            }
            if (!_registry.IsRegistered(name))
            {
                _log?.Invoke($"[Ads] provider '{name}' is not registered in this build; skipped");
                return null;
            }

            IAdService created = _registry.Create(name);
            _created[name.Trim()] = created;
            return created;
        }

        private void InitializeActive()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].Initialize();
            }
        }

        private static int Rank(AdFailure failure)
        {
            switch (failure)
            {
                case AdFailure.Offline: return 5;
                case AdFailure.Blocked: return 4;
                case AdFailure.NoConsent: return 3;
                case AdFailure.NoFill: return 2;
                case AdFailure.Other: return 1;
                default: return 0;
            }
        }
    }
}
