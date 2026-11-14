using GravityWells.Core;
using System;
using GravityWells.Services;

namespace GravityWells.Game
{
    // What AdFlow needs from the screen: the spinner and message overlay and the toast. UIRoot implements it; a test
    // can record the calls instead.
    public interface IAdPresenter
    {
        void ShowAdLoading(string title, string body, bool cancellable);
        void ShowAdMessage(string title, string body);
        void HideAdOverlay();
        void ShowToast(string message);
    }

    // Everything that happens around an ad, kept out of GameManager: the rewarded ad the player asked for (a spinner
    // while it loads, an honest message when none can be shown, a timeout, Cancel) and the policy-gated interstitial
    // between levels (with a watchdog so a hung SDK can never strand the player). While Busy the world holds still.
    public sealed class AdFlow
    {
        private const float RewardedLoadTimeoutSeconds = 20f;
        private const float InterstitialWatchdogSeconds = 60f;
        private const float MonetizationTickSeconds = 0.25f;

        private enum RewardedPhase { None, Waiting, Opening }

        private readonly MonetizationController _monetization;
        private readonly IAdPresenter _ui;
        private readonly Func<float> _now;

        private RewardedPhase _phase;
        private Action _onReward;
        private string _skippedMessage;
        private float _rewardedDeadline;

        private int _interstitialToken;
        private bool _awaitingInterstitial;
        private float _interstitialDeadline;
        private Action _interstitialFallback;

        private float _tickTimer;

        // now is a clock in seconds that keeps running while the game is paused (Time.unscaledTime in the game).
        public AdFlow(MonetizationController monetization, IAdPresenter ui, Func<float> now)
        {
            _monetization = monetization;
            _ui = ui;
            _now = now;
        }

        public bool Busy => _phase != RewardedPhase.None || _awaitingInterstitial;

        // ---- rewarded ----

        // Shows a spinner from the tap until the ad is up, and a real explanation when none can be shown.
        // onReward runs only when the whole ad was watched.
        public void WatchRewarded(Action onReward, string skippedMessage)
        {
            if (_phase != RewardedPhase.None)
            {
                return;
            }

            _onReward = onReward;
            _skippedMessage = skippedMessage;
            _monetization.RequestRewarded();

            AdLoadState state = _monetization.RewardedState;
            if (state == AdLoadState.Ready)
            {
                OpenRewarded();
            }
            else if (state == AdLoadState.Failed)
            {
                ExplainNoAd(timedOut: false);
            }
            else
            {
                _phase = RewardedPhase.Waiting;
                _rewardedDeadline = _now() + RewardedLoadTimeoutSeconds;
                _ui.ShowAdLoading(AdMessages.LoadingTitle, Strings.Ads.LoadingBody, cancellable: true);
            }
        }

        private void OpenRewarded()
        {
            _phase = RewardedPhase.Opening;
            _ui.ShowAdLoading(AdMessages.OpeningTitle, string.Empty, cancellable: false);

            _monetization.ShowRewarded(outcome =>
            {
                Action onReward = _onReward;
                _onReward = null;

                if (outcome == AdOutcome.Completed || outcome == AdOutcome.Skipped)
                {
                    _phase = RewardedPhase.None;
                    _ui.HideAdOverlay();
                    if (outcome == AdOutcome.Completed)
                    {
                        onReward?.Invoke();
                    }
                    else
                    {
                        _ui.ShowToast(_skippedMessage);
                    }
                    return;
                }

                ExplainNoAd(timedOut: false);
            });
        }

        private void ExplainNoAd(bool timedOut)
        {
            AdFailure failure = _monetization.RewardedFailure;
            AdMessages.ForFailure(failure, out string title, out string body);
            if (timedOut && failure != AdFailure.Blocked && failure != AdFailure.Offline)
            {
                body = AdMessages.TimedOutBody;
            }

            _phase = RewardedPhase.None;
            _onReward = null;
            _ui.ShowAdMessage(title, body); // stays up until the player taps OK
        }

        // The overlay's button, and Android Back while it is up. Not allowed while the ad itself is opening.
        public void Dismiss()
        {
            if (_phase == RewardedPhase.Opening)
            {
                return;
            }
            _phase = RewardedPhase.None;
            _onReward = null;
            _ui.HideAdOverlay();
        }

        // ---- interstitial ----

        // Runs proceed exactly once when the player may carry on: at once when the policy blocks an ad, or after the
        // ad closes, or when the watchdog gives up on it.
        public void ShowInterstitialThen(int level, float secondsSinceLevelStart, Action proceed)
        {
            int token = ++_interstitialToken;
            _awaitingInterstitial = true;
            _interstitialDeadline = _now() + InterstitialWatchdogSeconds;

            Action once = () =>
            {
                if (token != _interstitialToken || !_awaitingInterstitial)
                {
                    return; // already continued (or the player left): never run twice
                }
                _awaitingInterstitial = false;
                _interstitialFallback = null;
                proceed?.Invoke();
            };
            _interstitialFallback = once;

            _monetization.TryShowInterstitial(level, isDragging: false, secondsSinceLevelStart, once);
        }

        // ---- every frame ----

        public void Tick(float unscaledDeltaTime)
        {
            _tickTimer -= unscaledDeltaTime;
            if (_tickTimer <= 0f)
            {
                _tickTimer = MonetizationTickSeconds;
                _monetization.Tick(); // lets the ad network keep its next ads loaded
            }

            if (_awaitingInterstitial && _now() > _interstitialDeadline)
            {
                _interstitialFallback?.Invoke();
            }

            if (_phase != RewardedPhase.Waiting)
            {
                return;
            }

            AdLoadState state = _monetization.RewardedState;
            if (state == AdLoadState.Ready)
            {
                OpenRewarded();
            }
            else if (state == AdLoadState.Failed)
            {
                ExplainNoAd(timedOut: false);
            }
            else if (_now() > _rewardedDeadline)
            {
                ExplainNoAd(timedOut: true);
            }
        }

        // The player left the level (menu, restart): forget anything in flight.
        public void Cancel()
        {
            _phase = RewardedPhase.None;
            _onReward = null;
            _awaitingInterstitial = false;
            _interstitialFallback = null;
            _interstitialToken++;
            _ui.HideAdOverlay();
        }
    }
}
