#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Services
{
    // Real interstitial and rewarded ads through Google AdMob. The SDK may call back on a
    // non-Unity thread and nothing in the game is thread-safe, so every callback body is
    // marshalled onto the main thread with MobileAdsEventExecutor.ExecuteInUpdate.
    //
    // A small pool of each kind is kept loaded ahead of time (PoolSize), so showing an ad is just taking
    // one that is already waiting; the player rarely sees a loading spinner. Tick (a few times a second)
    // tops the pool up. AdPoolPlanner owns the rules: one load in flight at a time and loaded plus
    // in-flight never above the pool size, so a failed load is retried after a back-off rather than
    // alongside another request, and two loads can never both land beyond the pool size.
    public sealed class AdMobAdService : IAdService
    {
        public const string ProviderName = "admob";

        private const int PoolSize = 2;

        private readonly string _interstitialUnitId;
        private readonly string _rewardedUnitId;

        private readonly AdPoolPlanner _interstitialPool = new AdPoolPlanner(PoolSize);
        private readonly AdPoolPlanner _rewardedPool = new AdPoolPlanner(PoolSize);
        private readonly List<InterstitialAd> _interstitials = new List<InterstitialAd>();
        private readonly List<RewardedAd> _rewardeds = new List<RewardedAd>();

        private bool _initialized;
        private bool _sdkStarting; // Initialize called, its completion callback not yet received

        private InterstitialAd _showingInterstitial;
        private Action<AdOutcome> _pendingInterstitial;

        private RewardedAd _showingRewarded;
        private Action<AdOutcome> _pendingRewarded;
        private bool _rewardEarned;
        private bool _rewardedFailed;
        private AdFailure _rewardedFailure;

        // From the DNS probe; wins over the SDK's code because the SDK reports a blocked
        // network as an opaque internal error.
        private AdFailure _networkFailure;

        public AdMobAdService(string interstitialUnitId, string rewardedUnitId)
        {
            _interstitialUnitId = interstitialUnitId;
            _rewardedUnitId = rewardedUnitId;
        }

        public string Name => ProviderName;

        // Only called once consent allows ad requests.
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            MobileAdsEventExecutor.Initialize();
            Debug.Log("[AdMob] initializing SDK");

            _sdkStarting = true;
            MobileAds.Initialize(_ => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.Log("[AdMob] SDK initialized, filling the ad pools");
                _sdkStarting = false;
                Tick();
            }));
        }

        // Called a few times a second by the game: drops ads that expired while waiting (they last about an
        // hour) and starts the next load if a pool has room.
        public void Tick()
        {
            if (!_initialized || _sdkStarting)
            {
                return;
            }

            DropExpiredInterstitials();
            DropExpiredRewardeds();

            float now = Time.realtimeSinceStartup;
            if (_interstitialPool.ShouldStartLoad(now))
            {
                LoadInterstitial();
            }
            if (_rewardedPool.ShouldStartLoad(now))
            {
                LoadRewarded();
            }
        }

        public bool IsInterstitialReady
        {
            get
            {
                if (!_initialized)
                {
                    return false;
                }
                DropExpiredInterstitials();
                return _interstitials.Count > 0;
            }
        }

        public bool IsRewardedReady
        {
            get
            {
                if (!_initialized)
                {
                    return false;
                }
                DropExpiredRewardeds();
                return _rewardeds.Count > 0;
            }
        }

        public AdLoadState RewardedState
        {
            get
            {
                if (_rewardeds.Count > 0)
                {
                    return AdLoadState.Ready;
                }
                if (_rewardedPool.InFlight > 0 || _sdkStarting)
                {
                    return AdLoadState.Loading;
                }
                return _rewardedFailed ? AdLoadState.Failed : AdLoadState.Idle;
            }
        }

        public AdFailure RewardedFailure => _networkFailure != AdFailure.None ? _networkFailure : _rewardedFailure;

        // The player asked for a rewarded ad and none is ready: skip the back-off and try now, unless a
        // load is already on its way (which then simply finishes).
        public void RequestRewarded()
        {
            if (!_initialized || _sdkStarting || _rewardeds.Count > 0 || _rewardedPool.InFlight > 0)
            {
                return;
            }
            LoadRewarded();
        }

        // ---- interstitial ----

        public void ShowInterstitial(Action<AdOutcome> onComplete)
        {
            if (_pendingInterstitial != null || !IsInterstitialReady)
            {
                onComplete?.Invoke(AdOutcome.NotReady);
                return;
            }

            // Take the oldest waiting ad out of the pool; Tick will start filling its place.
            _showingInterstitial = _interstitials[0];
            _interstitials.RemoveAt(0);
            _interstitialPool.OnConsumed();

            _pendingInterstitial = onComplete;
            try
            {
                _showingInterstitial.Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AdMob] interstitial Show threw: " + e.Message);
                FinishInterstitial(AdOutcome.Failed);
            }
        }

        private void LoadInterstitial()
        {
            _interstitialPool.OnLoadStarted();
            InterstitialAd.Load(_interstitialUnitId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogWarning("[AdMob] interstitial failed to load: " + error);
                        _interstitialPool.OnLoadFailed(Time.realtimeSinceStartup);
                        ProbeNetwork();
                        return;
                    }

                    if (!_interstitialPool.OnLoaded())
                    {
                        ad.Destroy(); // the pool filled while this was loading; never hold more than the pool size
                        return;
                    }

                    Debug.Log("[AdMob] interstitial loaded (" + (_interstitials.Count + 1) + " waiting)");
                    ad.OnAdFullScreenContentClosed += () =>
                        MobileAdsEventExecutor.ExecuteInUpdate(() => FinishInterstitial(AdOutcome.Completed));
                    ad.OnAdFullScreenContentFailed += (AdError showError) =>
                        MobileAdsEventExecutor.ExecuteInUpdate(() =>
                        {
                            Debug.LogWarning("[AdMob] interstitial failed to show: " + showError);
                            FinishInterstitial(AdOutcome.Failed);
                        });
                    _interstitials.Add(ad);
                }));
        }

        // Order matters: drop the used ad, then tell the game, so the game's reaction (e.g. loading a
        // level) can't race a half-finished ad state. The pool refills on the next Tick.
        private void FinishInterstitial(AdOutcome outcome)
        {
            Action<AdOutcome> callback = _pendingInterstitial;
            _pendingInterstitial = null;
            if (_showingInterstitial != null)
            {
                _showingInterstitial.Destroy();
                _showingInterstitial = null;
            }
            callback?.Invoke(outcome);
        }

        private void DropExpiredInterstitials()
        {
            for (int i = _interstitials.Count - 1; i >= 0; i--)
            {
                if (!_interstitials[i].CanShowAd())
                {
                    _interstitials[i].Destroy();
                    _interstitials.RemoveAt(i);
                    _interstitialPool.OnConsumed();
                }
            }
        }

        // ---- rewarded ----

        public void ShowRewarded(Action<AdOutcome> onComplete)
        {
            if (_pendingRewarded != null || !IsRewardedReady)
            {
                onComplete?.Invoke(AdOutcome.NotReady);
                return;
            }

            _showingRewarded = _rewardeds[0];
            _rewardeds.RemoveAt(0);
            _rewardedPool.OnConsumed();

            _pendingRewarded = onComplete;
            _rewardEarned = false;
            try
            {
                // The reward callback fires before the ad closes; both are queued onto the main
                // thread in order, so by the time "closed" runs the flag is already set.
                _showingRewarded.Show(_ => MobileAdsEventExecutor.ExecuteInUpdate(() => _rewardEarned = true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AdMob] rewarded Show threw: " + e.Message);
                FinishRewarded(AdOutcome.Failed);
            }
        }

        private void LoadRewarded()
        {
            _rewardedPool.OnLoadStarted();
            RewardedAd.Load(_rewardedUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogWarning("[AdMob] rewarded failed to load: " + error);
                        _rewardedPool.OnLoadFailed(Time.realtimeSinceStartup);
                        _rewardedFailed = _rewardeds.Count == 0; // a spare ad in the pool means the player is not stuck
                        _rewardedFailure = FailureFromCode(error);
                        ProbeNetwork();
                        return;
                    }

                    if (!_rewardedPool.OnLoaded())
                    {
                        ad.Destroy();
                        return;
                    }

                    Debug.Log("[AdMob] rewarded loaded");
                    _rewardedFailed = false;
                    _rewardedFailure = AdFailure.None;
                    _networkFailure = AdFailure.None;
                    ad.OnAdFullScreenContentClosed += () =>
                        MobileAdsEventExecutor.ExecuteInUpdate(() =>
                            FinishRewarded(_rewardEarned ? AdOutcome.Completed : AdOutcome.Skipped));
                    ad.OnAdFullScreenContentFailed += (AdError showError) =>
                        MobileAdsEventExecutor.ExecuteInUpdate(() =>
                        {
                            Debug.LogWarning("[AdMob] rewarded failed to show: " + showError);
                            FinishRewarded(AdOutcome.Failed);
                        });
                    _rewardeds.Add(ad);
                }));
        }

        private void FinishRewarded(AdOutcome outcome)
        {
            Action<AdOutcome> callback = _pendingRewarded;
            _pendingRewarded = null;
            if (_showingRewarded != null)
            {
                _showingRewarded.Destroy();
                _showingRewarded = null;
            }
            callback?.Invoke(outcome);
        }

        private void DropExpiredRewardeds()
        {
            for (int i = _rewardeds.Count - 1; i >= 0; i--)
            {
                if (!_rewardeds[i].CanShowAd())
                {
                    _rewardeds[i].Destroy();
                    _rewardeds.RemoveAt(i);
                    _rewardedPool.OnConsumed();
                }
            }
        }

        // Codes from the Google Mobile Ads SDK: 2 = network error, 3 = no fill.
        private static AdFailure FailureFromCode(LoadAdError error)
        {
            switch (error?.GetCode())
            {
                case 2: return AdFailure.Offline;
                case 3: return AdFailure.NoFill;
                default: return AdFailure.Other;
            }
        }

        private void ProbeNetwork()
        {
            AdNetworkProbe.Run(result =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    _networkFailure = result;
                    Debug.Log("[AdMob] network probe: " + result);
                }));
        }
    }
}
#endif
