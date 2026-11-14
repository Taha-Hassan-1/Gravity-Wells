using UnityEngine;

namespace GravityWells.Services
{
    // Thin wrapper so gameplay code depends on an abstraction rather than the platform
    // vibration API directly. Handheld.Vibrate is a fixed ~400 ms buzz that feels like an
    // alarm, so on Android this asks the vibrator for a short, light effect instead.
    public sealed class HapticService
    {
        public bool Enabled { get; set; } = true;

        // The stronger setting: the heaviest built-in click for ticks and a long full-power pulse to confirm.
        public bool Strong { get; set; }

        // A light tick: a crossing cleared, a node grabbed.
        public void Tick()
        {
            if (!Enabled)
            {
                return;
            }
            Play(
                effectId: Strong ? EffectHeavyClick : EffectClick,
                fallbackMillis: Strong ? 45 : 28, fallbackAmplitude: Strong ? 255 : 170);
        }

        // A firmer click for the moment the board is solved.
        public void Confirm()
        {
            if (!Enabled)
            {
                return;
            }
            Play(
                effectId: EffectHeavyClick,
                fallbackMillis: Strong ? 80 : 50, fallbackAmplitude: 255);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // android.os.VibrationEffect predefined ids (API 29+).
        private const int EffectClick = 0;
        private const int EffectHeavyClick = 5;

        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _effectClass;
        private int _sdk;
        private bool _failed;

        private void Play(int effectId, long fallbackMillis, int fallbackAmplitude)
        {
            if (_failed)
            {
                return;
            }

            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        _sdk = version.GetStatic<int>("SDK_INT");
                    }
                    if (_sdk >= 26)
                    {
                        _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    }
                }

                if (_sdk >= 29)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>("createPredefined", effectId))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else if (_sdk >= 26)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", fallbackMillis, fallbackAmplitude))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", fallbackMillis);
                }
            }
            catch (System.Exception)
            {
                // No vibrator or a restricted device: haptics are optional, never break play over them.
                _failed = true;
            }
        }
#else
        private const int EffectClick = 0;
        private const int EffectHeavyClick = 5;

        // Desktop and Editor builds have no motor (Handheld.Vibrate there only logs a stack trace).
        private static void Play(int effectId, long fallbackMillis, int fallbackAmplitude)
        {
        }
#endif
    }
}
