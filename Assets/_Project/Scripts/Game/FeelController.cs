using UnityEngine;
using GravityWells.Core;
using GravityWells.Services;
using GravityWells.UI;

namespace GravityWells.Game
{
    // Everything that makes the board feel like something: it listens for the moments (a gate lit under the
    // finger, a well put down, the launch, the ship passing a gate, a win, a miss) and answers each with the
    // right sound, vibration and animation. Which moments count is decided by the pure classes in Core
    // (PreviewTracker, FailFeedback, WinSequence, GateScale); this class only performs them. GameManager
    // never plays a sound or starts an animation itself.
    public sealed class FeelController
    {
        private readonly AudioService _audio;
        private readonly HapticService _haptics;
        private readonly FieldView _field;
        private readonly PathView _path;
        private readonly WellViews _wells;
        private readonly GateViews _gates;
        private readonly ShipView _ship;
        private readonly TrailView _trail;
        private readonly BurstView _burst;
        private readonly CrashMarkView _mark;
        private readonly GameHUD _hud;
        private readonly PreviewTracker _preview = new PreviewTracker();

        private ViewColors _colors;
        private int _gateCount;
        private Vector2 _goal;
        private int _flightLitMask;

        private bool _winActive;
        private float _winClock;
        private int _winPingsDone;
        private bool _winStingerDone;

        public FeelController(
            AudioService audio, HapticService haptics, FieldView field, PathView path, WellViews wells, GateViews gates, ShipView ship,
            TrailView trail, BurstView burst, CrashMarkView mark, GameHUD hud)
        {
            _audio = audio;
            _haptics = haptics;
            _field = field;
            _path = path;
            _wells = wells;
            _gates = gates;
            _ship = ship;
            _trail = trail;
            _burst = burst;
            _mark = mark;
            _hud = hud;
        }

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            _trail.ApplyTheme(colors);
            _mark.ApplyTheme(colors);
        }

        // ---- a level ----

        public void LevelLoaded(LevelDefinition level, Trajectory preview)
        {
            _gateCount = level.Gates.Length;
            _goal = level.Goal;
            _winActive = false;
            _flightLitMask = 0;
            _preview.Reset(preview.GatesLitMask, preview.Outcome == FlightOutcome.Won);
            ClearMarks();
            _audio.StopWhoosh();
        }

        // Hides what a failed flight left behind (the cross and the burst's last bits fade by themselves).
        public void ClearMarks()
        {
            _mark.Hide();
            _trail.Clear();
        }

        // ---- the plan ----

        // The preview line changed. When it is the player's own doing (a drag step, a drop) a gate that has just
        // lit pops and sings its note, and a line that has just found the goal strikes the soft chord. When it
        // is not (a new level, Undo, Restart) the state is simply adopted: nothing is celebrated.
        public void PreviewChanged(Trajectory preview, bool celebrate)
        {
            bool winning = preview.Outcome == FlightOutcome.Won;
            if (!celebrate)
            {
                _preview.Reset(preview.GatesLitMask, winning);
                return;
            }

            PreviewChange change = _preview.Update(preview.GatesLitMask, winning);
            if (change.NewlyLitMask != 0)
            {
                for (int g = 0; g < _gateCount; g++)
                {
                    if ((change.NewlyLitMask & (1 << g)) != 0)
                    {
                        _gates.Pop(g);
                    }
                }
                _audio.PlaySfx(SfxKind.GateNote, GateScale.PitchFor(change.LitCount), 0.9f);
                _haptics.Cross();
            }

            if (change.GoalReached)
            {
                // The "you have it" moment, under the finger. It never launches by itself.
                _audio.PlaySfx(SfxKind.GoalChord, 1f, 0.8f);
            }
        }

        public void WellPickedUp()
        {
            _audio.PlaySfx(SfxKind.WellPickUp, 1f, 0.7f);
        }

        // index is the well that was put down, or -1 if it was taken off the field.
        public void WellDropped(int index)
        {
            if (index >= 0)
            {
                _wells.Settle(index);
                _audio.PlaySfx(SfxKind.WellDrop, 1f, 0.85f);
            }
            else
            {
                _audio.PlaySfx(SfxKind.WellPickUp, 0.7f, 0.5f);
            }
        }

        // ---- the launch ----

        public void ChargeBegan()
        {
            ClearMarks();
            _audio.PlaySfx(SfxKind.Launch, 1f, 0.9f);
        }

        public void Charging(float progress) => _ship.SetCharge(progress);

        public void FlightStarted()
        {
            _ship.SetCharge(0f);
            _flightLitMask = 0;
            _haptics.Grab();   // the medium one: the ship is gone
            _audio.StartWhoosh();
        }

        // Every frame of the flight: the trail, the whoosh, a flash and a note for each gate the ship goes through.
        public void FlightTick(FlightPlayback playback, float seconds)
        {
            float flightSeconds = seconds * playback.Speed * (playback.InFinalApproach ? GameConfig.FinalApproachSpeedFactor : 1f);
            _trail.Drop(playback.Position, flightSeconds);
            _audio.SetWhooshSpeed(playback.Velocity);

            int mask = playback.GatesLitMaskSoFar();
            int fresh = mask & ~_flightLitMask;
            if (fresh != 0)
            {
                int count = 0;
                for (int g = 0; g < _gateCount; g++)
                {
                    count += (mask >> g) & 1;
                    if ((fresh & (1 << g)) != 0)
                    {
                        _gates.Flash(g);
                    }
                }
                _audio.PlaySfx(SfxKind.GateNote, GateScale.PitchFor(count), 0.9f);
                _haptics.Cross();
            }
            _flightLitMask = mask;
        }

        // ---- the end of a flight ----

        public void Won()
        {
            _audio.StopWhoosh();
            _winActive = true;
            _winClock = 0f;
            _winPingsDone = 0;
            _winStingerDone = false;

            // The burst at the goal, in the two colours the goal is drawn in.
            _burst.Emit(_goal, _colors.Lit, _colors.Good, 44, 0.55f);
            _haptics.Confirm();   // the strong one
        }

        // A flight that did not win: a cross where it ended, the missed gates blink, the sound sags, and after
        // enough misses the hint button pulses.
        public void Failed(Trajectory flight, int litMask, int failedLaunches)
        {
            _audio.StopWhoosh();
            // A ship that left the field ended a little outside it, under the HUD: the cross goes on the edge it left by.
            Vector2 end = flight.Points[flight.Count - 1];
            const float edge = 0.03f;
            end = new Vector2(
                Mathf.Clamp(end.x, edge, GameConfig.FieldWidth - edge),
                Mathf.Clamp(end.y, edge, GameConfig.FieldHeight - edge));
            _mark.Show(end);
            _burst.Emit(end, _colors.Warning, _colors.Neutral, 14, 0.25f);
            _gates.Blink(FailFeedback.MissedMask(_gateCount, litMask));
            _audio.PlaySfx(SfxKind.Fail, 1f, 0.8f);
            if (FailFeedback.ShouldPulseHint(failedLaunches))
            {
                _hud.PulseHint();
            }
        }

        // ---- every frame ----

        public void Tick(float seconds)
        {
            // The decorations that move by themselves: the dots drifting along the line, the rings round the
            // wells, the pops and flashes, the trail, the burst, the stars.
            _path.Tick(seconds);
            _field.Tick(seconds);
            _wells.Tick(seconds);
            _gates.Tick(seconds);
            _trail.Tick(seconds);
            _burst.Tick(seconds);
            _mark.Tick(seconds);

            if (!_winActive)
            {
                return;
            }

            _winClock += seconds;
            while (_winPingsDone < _gateCount && _winClock >= WinSequence.PingTime(_winPingsDone))
            {
                _gates.Flash(_winPingsDone);
                _audio.PlaySfx(SfxKind.GateNote, GateScale.PitchFor(_winPingsDone + 1), 0.55f);
                _winPingsDone++;
            }

            if (!_winStingerDone && _winClock >= WinSequence.StingerTime(_gateCount))
            {
                _winStingerDone = true;
                _audio.PlaySfx(SfxKind.Win, 1f, 0.9f);
                _field.SetGoalReached(true);
            }

            if (_winClock > GameConfig.WinHoldSeconds + 0.5f)
            {
                _winActive = false;
            }
        }
    }
}
