using UnityEngine;
using UnityEngine.EventSystems;
using GravityWells.Core;
using GravityWells.Meta;
using GravityWells.Services;
using GravityWells.UI;

namespace GravityWells.Game
{
    // Everything GameManager is handed. Bootstrap builds these and passes them in one bundle.
    public sealed class GameDependencies
    {
        public Camera Camera;
        public IGameMode Mode;
        public UIRoot Ui;
        public GameHUD Hud;
        public PausePanel Pause;
        public LevelCompletePanel Result;
        public FieldView Field;
        public PathView Path;
        public WellViews Wells;
        public GateViews Gates;
        public AsteroidViews Asteroids;
        public ShipView Ship;
        public HintView Hint;
        public HapticService Haptics;
        public AudioService Audio;
    }

    // Runs one level at a time: the plan (wells being dragged, the preview line following them), the launch
    // and the flight, the win and the result. The rules of play are in the Core classes and in the IGameMode;
    // this class only moves between the states and tells the views what to show.
    //
    // The flight is the buffer the preview already drew: Resimulate runs on every change to the layout, and
    // Launch plays that same buffer back. Nothing is simulated once the ship is moving.
    public sealed class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Planning,   // wells can be moved; Launch is available
            Flying,     // the ship follows the line
            FailHold,   // a short rest on where the flight ended
            WinHold,    // a short rest on the finished flight
            Result,     // the result card is up
            Paused,
        }

        private const float FailHoldSeconds = 0.6f;

        private GameDependencies _d;
        private readonly WellLayout _layout = new WellLayout();
        private readonly Trajectory _trajectory = new Trajectory();
        private readonly FlightPlayback _playback = new FlightPlayback();
        private readonly Well[] _hintWells = new Well[GameConfig.MaxWells];
        private readonly Trajectory _hintScratch = new Trajectory();
        private WellDragController _drag;
        private LevelDefinition _level;
        private GameState _state;
        private GameState _stateBeforePause;
        private float _holdTimer;
        private int _launches;
        private int _wellsAtLaunch;
        private int _litShown = -1;

        public GameState State => _state;
        public LevelDefinition Level => _level;
        public WellLayout Layout => _layout;
        public Trajectory Trajectory => _trajectory;
        public FlightPlayback Playback => _playback;
        public int Launches => _launches;
        public bool IsLevelComplete => _state == GameState.Result || _state == GameState.WinHold;
        public LevelResult LastResult { get; private set; }
        public Vector2 ShipPosition => _d.Ship.Position;
        public bool IsPaused => _state == GameState.Paused;

        public void Init(GameDependencies dependencies)
        {
            _d = dependencies;
            _drag = new WellDragController(
                _d.Camera, _layout, _d.Hud, _d.Haptics, OnLayoutChanged, () => _d.Ui.ShowToast("No wells left. Take one back first."));
            _d.Hud.SecretTapped = RunDeterminismCheck;
        }

        // The device half of the determinism check: the same number the Editor prints (GravityWells > Tuning >
        // Print Determinism Hash). If they differ, stop: floats are not behaving the same on this device.
        private void RunDeterminismCheck()
        {
            int hash = DeterminismCheck.CombinedHash();
            string line = $"Determinism hash, campaign levels {DeterminismCheck.FirstLevel}-{DeterminismCheck.LastLevel}: {hash} " +
                          $"({Application.platform}, {SystemInfo.deviceModel}, {SystemInfo.processorType})";
            Debug.Log("[Determinism] " + line);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "determinism.txt"), line + "\n");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Determinism] could not write the result file: " + exception.Message);
            }
            _d.Ui.ShowToast("Determinism hash " + hash);
        }

        public void ApplyTheme(ThemePalette palette)
        {
            ViewColors colors = new ViewColors(palette);
            _d.Field.ApplyTheme(colors);
            _d.Path.ApplyTheme(colors);
            _d.Wells.ApplyTheme(colors);
            _d.Gates.ApplyTheme(colors);
            _d.Asteroids.ApplyTheme(colors);
            _d.Ship.ApplyTheme(colors);
            _d.Hint.ApplyTheme(colors);
            if (_level != null)
            {
                _d.Wells.Sync(_layout);
            }
        }

        // ---- levels ----

        public void StartCampaign()
        {
            Load(_d.Mode.Begin());
        }

        public void StartLevel(LevelDefinition level)
        {
            Load(level);
        }

        public void NextLevel() => Load(_d.Mode.Next());

        public void ReplayLevel() => Load(_d.Mode.Replay());

        private void Load(LevelDefinition level)
        {
            Time.timeScale = 1f;
            _level = level;
            _layout.Begin(level);
            _launches = 0;
            _litShown = -1;
            _d.Field.ShowLevel(level);
            _d.Gates.ShowLevel(level);
            _d.Asteroids.ShowLevel(level);
            _d.Ship.PlaceOnPad(level);
            _d.Hint.Hide();
            _d.Path.Visible = true;
            _d.Hud.ShowLevel(_d.Mode.LevelNumber);
            _d.Ui.SwitchTo(_d.Hud);
            EnterPlanning();
            Resimulate();
        }

        // ---- the plan ----

        private void EnterPlanning()
        {
            _state = GameState.Planning;
            _drag.SetEnabled(true);
            _d.Hud.SetControlsEnabled(true);
            _d.Ship.PlaceOnPad(_level);
            RefreshHud();
        }

        // The layout changed (a drag moved, Undo, Restart, a hint was taken): fly the preview again and show it.
        // This is the only place the simulator runs for the player; Launch plays what it left behind.
        public void Resimulate()
        {
            if (_level == null)
            {
                return;
            }

            FlightSimulator.Simulate(_level, _layout.SimulationWells, _layout.SimulationCount, _trajectory);

            bool winning = _trajectory.Outcome == FlightOutcome.Won;
            _d.Path.Show(_trajectory, winning ? PathView.PathState.Winning : PathView.PathState.Neutral);
            _d.Field.SetGoalReached(winning);
            _d.Gates.SetLitMask(_trajectory.GatesLitMask);
            _d.Wells.Sync(_layout);
            if (_d.Hint.Visible)
            {
                _d.Hint.Hide();
            }
            RefreshHud();
        }

        private void OnLayoutChanged() => Resimulate();

        private void RefreshHud()
        {
            if (_level == null)
            {
                return;
            }

            bool flying = _state == GameState.Flying;
            _d.Hud.SetGates(flying ? _playback.GatesLitSoFar() : _trajectory.GatesLitCount, _level.Gates.Length);
            _d.Hud.SetWells(_layout.Count, _level.Par);
            _d.Hud.SetTray(
                _layout.Remaining(WellKind.Attractor), _layout.Remaining(WellKind.Repulsor), _level.RepulsorBudget > 0);
            _d.Hud.SetUndoAvailable(_layout.CanUndo && _state == GameState.Planning);
        }

        public void Undo()
        {
            if (_state != GameState.Planning || !_layout.Undo())
            {
                return;
            }
            Resimulate();
        }

        public void Restart()
        {
            if (_state != GameState.Planning)
            {
                return;
            }
            _layout.Clear();
            Resimulate();
        }

        public void ShowHint()
        {
            if (_state != GameState.Planning || _level == null)
            {
                return;
            }

            for (int i = 0; i < _layout.Count; i++)
            {
                _hintWells[i] = _layout[i];
            }
            var rng = new System.Random(unchecked(_level.Seed + _launches * 977 + _layout.Count));
            if (HintPlanner.TryPlan(_level, _hintWells, _layout.Count, rng, _hintScratch, out int index, out Vector2 position))
            {
                bool moving = index < _layout.Count;
                _d.Hint.Show(position, moving ? _layout[index].Kind : WellKind.Attractor, moving, moving ? _layout[index].Position : default);
            }
            else
            {
                _d.Hint.Hide();
                _d.Ui.ShowToast("No better move found from here.");
            }
        }

        // ---- the flight ----

        public void Launch()
        {
            if (_state != GameState.Planning || _level == null || _layout.IsDragging)
            {
                return;
            }

            _launches++;
            _wellsAtLaunch = _layout.Count;
            _d.Hint.Hide();
            _drag.SetEnabled(false);
            _d.Hud.SetControlsEnabled(false);
            _d.Hud.SetUndoAvailable(false);
            _d.Haptics.Tap();

            // The buffer the preview drew, played back. No simulation here.
            _playback.Begin(_trajectory);
            _state = GameState.Flying;
            _d.Ship.Place(_playback.Position, _playback.Heading);
            RefreshHud();
        }

        private void Update()
        {
            if (_level == null)
            {
                return;
            }

            switch (_state)
            {
                case GameState.Planning:
                    _drag.Tick();
                    break;
                case GameState.Flying:
                    TickFlight();
                    break;
                case GameState.FailHold:
                    _holdTimer -= Time.deltaTime;
                    if (_holdTimer <= 0f)
                    {
                        EnterPlanning();
                    }
                    break;
                case GameState.WinHold:
                    _holdTimer -= Time.deltaTime;
                    if (_holdTimer <= 0f)
                    {
                        ShowResult();
                    }
                    break;
            }
        }

        private void TickFlight()
        {
            if (TapStarted() && !PointerOverUi())
            {
                _playback.SpeedUp();
            }

            _playback.Advance(Time.deltaTime);
            _d.Ship.Place(_playback.Position, _playback.Heading);
            _d.Gates.SetLitMask(_playback.GatesLitMaskSoFar());

            int lit = _playback.GatesLitSoFar();
            if (lit != _litShown)
            {
                _litShown = lit;
                _d.Hud.SetGates(lit, _level.Gates.Length);
            }

            if (!_playback.Finished)
            {
                return;
            }

            if (_playback.Outcome == FlightOutcome.Won)
            {
                _state = GameState.WinHold;
                _holdTimer = GameConfig.WinHoldSeconds;
                _d.Haptics.Confirm();
            }
            else
            {
                // A failed launch costs nothing but the count: the wells stay where they are.
                _state = GameState.FailHold;
                _holdTimer = FailHoldSeconds;
            }
        }

        private void ShowResult()
        {
            LastResult = _d.Mode.OnWon(_wellsAtLaunch, _launches, usedExtraWell: false);
            _d.Result.ShowResult(LastResult);
            _d.Ui.SwitchTo(_d.Result);
            _state = GameState.Result;
        }

        // ---- pause ----

        public void Pause()
        {
            if (_state == GameState.Paused || _state == GameState.Result || _level == null)
            {
                return;
            }
            _stateBeforePause = _state;
            _drag.SetEnabled(false);
            _state = GameState.Paused;
            Time.timeScale = 0f;
            _d.Ui.SwitchTo(_d.Pause);
        }

        public void Resume()
        {
            if (_state != GameState.Paused)
            {
                return;
            }
            Time.timeScale = 1f;
            _state = _stateBeforePause;
            _d.Ui.SwitchTo(_d.Hud);
            if (_state == GameState.Planning)
            {
                _drag.SetEnabled(true);
            }
        }

        public void TogglePause()
        {
            if (_state == GameState.Paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void RestartFromPause()
        {
            Time.timeScale = 1f;
            ReplayLevel();
        }

        // ---- input helpers ----

        private static bool TapStarted()
        {
            return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        }

        private static bool PointerOverUi()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return false;
            }
            return Input.touchCount > 0
                ? events.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                : events.IsPointerOverGameObject();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f; // never leave the editor paused behind us
        }
    }
}
