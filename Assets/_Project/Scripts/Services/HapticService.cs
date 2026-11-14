using System;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Services
{
    // Touch feedback, named by the moment (Tap, Grab, Cross, Solve) so gameplay never deals
    // with a platform API. On Android it plays the system's own touch-feedback effects through
    // View.performHapticFeedback: the same crisp ticks the keyboard and system buttons give,
    // with no permission needed. If the phone refuses that (touch feedback switched off in
    // system settings), it falls back to the vibrator with a predefined effect, which needs the
    // VIBRATE permission the build adds (Editor/AndroidPermissionPatcher). Handheld.Vibrate is
    // never used: it is a fixed ~400 ms buzz that feels like an alarm. Nothing runs off Android.
    public sealed class HapticService
    {
        private const int FlagIgnoreViewSetting = 1; // HapticFeedbackConstants.FLAG_IGNORE_VIEW_SETTING

        public bool Enabled { get; set; } = true;

        // The stronger setting picks the heavier effect for every moment.
        public bool Strong { get; set; }

        private readonly bool _onAndroid = Application.platform == RuntimePlatform.Android;
        private readonly jvalue[] _viewArgs = new jvalue[2]; // reused: a tick during a drag allocates nothing
        private float _lastCrossTime = -1f;

        private bool _ready;
        private bool _viewFailed;
        private bool _vibratorFailed;
        private int _sdk;
        private AndroidJavaObject _view;
        private IntPtr _performMethod;
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _effectClass;

        // A button was pressed.
        public void Tap() => Play(HapticKind.Tap);

        // A dot was picked up.
        public void Grab() => Play(HapticKind.Grab);

        // A line of the dragged dot crossed, or stopped crossing, another line.
        public void Cross() => Play(HapticKind.Cross);

        // The board came clean (also played once when the strength setting changes, as a sample).
        public void Confirm() => Play(HapticKind.Solve);

        private void Play(HapticKind kind)
        {
            if (!Enabled || !_onAndroid)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!HapticPlan.IsDue(kind, now, _lastCrossTime))
            {
                return;
            }
            if (kind == HapticKind.Cross)
            {
                _lastCrossTime = now;
            }

            EnsureReady();
            if (!PlayOnView(kind))
            {
                PlayOnVibrator(kind);
            }
        }

        private void EnsureReady()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;

            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdk = version.GetStatic<int>("SDK_INT");
                }
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    {
                        _view = window.Call<AndroidJavaObject>("getDecorView");
                        _performMethod = AndroidJNIHelper.GetMethodID(_view.GetRawClass(), "performHapticFeedback", "(II)Z");
                    }
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (_sdk >= 26)
                {
                    _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (Exception)
            {
                // Whatever was not reached stays null and its path reports failure below.
            }
        }

        // True when the system accepted the effect.
        private bool PlayOnView(HapticKind kind)
        {
            if (_viewFailed || _view == null || _performMethod == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                _viewArgs[0].i = HapticPlan.FeedbackConstant(kind, _sdk, Strong);
                _viewArgs[1].i = FlagIgnoreViewSetting;
                bool played = AndroidJNI.CallBooleanMethod(_view.GetRawObject(), _performMethod, _viewArgs);
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                {
                    AndroidJNI.ExceptionClear();
                    _viewFailed = true;
                    return false;
                }
                return played;
            }
            catch (Exception)
            {
                _viewFailed = true;
                return false;
            }
        }

        private void PlayOnVibrator(HapticKind kind)
        {
            if (_vibratorFailed || _vibrator == null)
            {
                return;
            }

            try
            {
                if (_sdk >= 29)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>(
                        "createPredefined", HapticPlan.FallbackEffect(kind, Strong)))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else if (_sdk >= 26)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", HapticPlan.FallbackMillis(kind, Strong), HapticPlan.FallbackAmplitude(kind, Strong)))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", HapticPlan.FallbackMillis(kind, Strong));
                }
            }
            catch (Exception)
            {
                // No motor, or the permission is missing: feedback is optional, never break play over it.
                _vibratorFailed = true;
            }
        }
    }
}
