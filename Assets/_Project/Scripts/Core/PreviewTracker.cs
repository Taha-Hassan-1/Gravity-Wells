using UnityEngine;

namespace GravityWells.Core
{
    // What changed on the preview line since the last time it was looked at.
    public readonly struct PreviewChange
    {
        public readonly int NewlyLitMask;     // gates lit now that were not before (bit g = gate g)
        public readonly int LitCount;         // gates lit now, in all
        public readonly bool GoalReached;     // the line has just started to win

        public PreviewChange(int newlyLitMask, int litCount, bool goalReached)
        {
            NewlyLitMask = newlyLitMask;
            LitCount = litCount;
            GoalReached = goalReached;
        }

        public int NewlyLitCount
        {
            get
            {
                int count = 0;
                for (int g = 0; g < GameConfig.MaxGates; g++)
                {
                    count += (NewlyLitMask >> g) & 1;
                }
                return count;
            }
        }
    }

    // Turns the stream of preview lines (one per frame of a drag) into moments worth a sound: a gate that
    // has just lit, and the line that has just started to win. The reverse never makes a sound: a gate going
    // dark, or the line ceasing to win, changes the picture and nothing else. Pure, so it is tested without a scene.
    public sealed class PreviewTracker
    {
        private int _litMask;
        private bool _winning;

        // Adopts a state without reporting it: a new level, a restart, an undo. Nothing is celebrated.
        public void Reset(int litMask, bool winning)
        {
            _litMask = litMask;
            _winning = winning;
        }

        public PreviewChange Update(int litMask, bool winning)
        {
            int newlyLit = litMask & ~_litMask;
            bool goalReached = winning && !_winning;
            _litMask = litMask;
            _winning = winning;
            return new PreviewChange(newlyLit, CountBits(litMask), goalReached);
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (int g = 0; g < GameConfig.MaxGates; g++)
            {
                count += (mask >> g) & 1;
            }
            return count;
        }
    }

    // The notes the gates sing. Each gate lit raises the note one step up a pentatonic scale, so a line that
    // threads three gates plays three rising notes and no combination of them can sound wrong.
    public static class GateScale
    {
        // Semitones above the root for the first gate, the second, and so on (major pentatonic, then up an octave).
        private static readonly int[] Steps = { 0, 2, 4, 7, 9, 12, 14, 16, 19 };

        // The playback pitch multiplier for the gate that makes litCount gates lit (1 = the first gate).
        public static float PitchFor(int litCount)
        {
            int index = Mathf.Clamp(litCount - 1, 0, Steps.Length - 1);
            return Mathf.Pow(2f, Steps[index] / 12f);
        }
    }

    // When things happen in the win sequence, as times from the moment the ship reaches the goal.
    public static class WinSequence
    {
        // Gate i pings at this time.
        public static float PingTime(int gateIndex) => GameConfig.WinPingSpacingSeconds * (gateIndex + 1);

        // The stinger lands just after the last ping, and always inside the hold before the result card.
        public static float StingerTime(int gateCount) =>
            Mathf.Min(PingTime(gateCount - 1) + 0.05f, GameConfig.WinHoldSeconds * 0.8f);
    }

    // The failed-launch feedback decisions.
    public static class FailFeedback
    {
        // The gates the flight did not light.
        public static int MissedMask(int gateCount, int litMask)
        {
            int all = (1 << gateCount) - 1;
            return all & ~litMask;
        }

        // The hint button pulses once, when the third launch on a level has failed (not on the fourth, fifth ...).
        public static bool ShouldPulseHint(int failedLaunches) => failedLaunches == GameConfig.FailsBeforeHintPulse;
    }

    // How the sound of the flight follows the ship.
    public static class WhooshCurve
    {
        // speed in field widths per second; the launch speed is the reference.
        public static float VolumeFor(float speed) =>
            0.12f + 0.55f * Mathf.Clamp01(speed / (GameConfig.LaunchSpeed * 3f));

        public static float PitchFor(float speed) =>
            0.7f + 0.9f * Mathf.Clamp01(speed / (GameConfig.LaunchSpeed * 3f));
    }
}
