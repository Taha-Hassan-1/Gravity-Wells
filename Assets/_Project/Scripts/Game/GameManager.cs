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
        public WellLinkView Links;
        public CrashMarkView PreviewCrash;   // where the line on screen ends in a crash, shown while planning
        public FeelController Feel;
        public HapticService Haptics;
        public AudioService Audio;
        public EconomyService Economy;
        public AdFlow AdFlow;
        public GameAnalytics Analytics;      // optional: tests that do not care about events leave it null
        public SaveService Save;
        public System.Action ShowMainMenu;   // set after the app flow exists: the pause card and the result card lead there
    }

    // Runs one level at a time: the plan (wells being dragged, the preview line following them), the launch
    // and the flight, the win and the result. The rules of play are in the Core classes and in the IGameMode;
    // the sounds and animations are FeelController's; this class only moves between the states and tells the
    // views what to show.
    //
    // The flight is the buffer the preview already drew: Refresh runs on every change to the layout, and
    // Launch plays that same buffer back. Nothing is simulated once the ship is moving.
    public sealed class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Planning,   // wells can be moved; Launch is available
            Charging,   // the ship gathers on the pad
            Flying,     // the ship follows the line
            FailHold,   // a short rest on where the flight ended
            WinHold,    // the win sequence plays out on the finished flight
            Result,     // the result card is up
            Paused,
            Menu,       // the player is on a menu screen; no level is being played
        }

        private const float FailHoldSeconds = 0.7f;

        private GameDependencies _d;
        private readonly WellLayout _layout = new WellLayout();
        private readonly Trajectory _trajectory = new Trajectory();
        private readonly FlightPlayback _playback = new FlightPlayback();
        private readonly PathEase _ease = new PathEase();
        private readonly Well[] _hintWells = new Well[GameConfig.MaxWells];
        private readonly Trajectory _hintScratch = new Trajectory();
        private WellDragController _drag;
        private LevelDefinition _level;
        private GameState _state;
        private GameState _stateBeforePause;
        private float _holdTimer;
        private float _chargeTimer;
        private int _launches;
        private int _failedLaunches;
        private bool _coinsDoubled;

        private bool _crashBanner;      // the banner currently says "too close" instead of the level's lesson
        private int _wellsAtLaunch;
        private int _litShown = -1;
        private IGameMode _mode;
        private bool _usedHint;
        private bool _usedExtraWell;
        private bool _extraWellOffered;

        // The wordless level-1 lesson: a ghost finger drags a well across until the player touches the screen.
        private TutorialDemo _demo;
        private bool _demoActive;
        private bool _demoWellOnField;
        private bool _demoStoppedByPlayer;
        private float _demoClock;
        private PendingHint _pendingHint;

        private struct PendingHint
        {
            public bool Valid;
            public int Index;
            public Vector2 Position;
        }

        public GameState State => _state;
        public LevelDefinition Level => _level;
        public WellLayout Layout => _layout;
        public Trajectory Trajectory => _trajectory;
        public FlightPlayback Playback => _playback;
        public int Launches => _launches;
        public bool PreviewCrashVisible => _d.PreviewCrash.Visible;
        public int LinksShown => _d.Links.Shown;
        public bool IsLevelComplete => _state == GameState.Result || _state == GameState.WinHold;
        public LevelResult LastResult { get; private set; }
        public Vector2 ShipPosition => _d.Ship.Position;
        public bool IsPaused => _state == GameState.Paused;

        public void Init(GameDependencies dependencies)
        {
            _d = dependencies;
            _mode = _d.Mode;
            _state = GameState.Menu;
            _drag = new WellDragController(
                _d.Camera, _layout, _d.Hud, _d.Haptics, OnLayoutChanged,
                () => _d.Ui.ShowToast(Strings.Game.NoWellsLeft),
                _d.Feel.WellPickedUp, _d.Feel.WellDropped);
            _d.Hud.SecretTapped = RunDeterminismCheck;
            _d.Hud.ExtraWellPressed = OnExtraWellPressed;
        }

        public IGameMode Mode => _mode;
        public bool UsedHint => _usedHint;
        public bool UsedExtraWell => _usedExtraWell;
        public bool DemoActive => _demoActive;

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
            _d.Links.ApplyTheme(colors);
            _d.PreviewCrash.ApplyTheme(colors);
            _d.Feel.ApplyTheme(colors);
            if (_level != null)
            {
                _d.Wells.Sync(_layout);
                _d.Links.Sync(_layout, _trajectory);
            }
        }

        // ---- levels ----

        public void StartCampaign()
        {
            StartMode(_d.Mode);
        }

        // Begins a session of a mode (the campaign from where the player stopped, or today's daily).
        public void StartMode(IGameMode mode)
        {
            _mode = mode;
            Load(_mode.Begin());
        }

        public void StartLevel(LevelDefinition level)
        {
            Load(level);
        }

        // The one place an interstitial can appear: the player has just tapped "Next level" on a won level. The
        // policy (levels and seconds since the last ad, level 5+, first-session grace, Remove ads) decides; when
        // it says no, this loads the next level at once.
        public void NextLevel()
        {
            if (_d.AdFlow == null || LastResult.IsDaily)
            {
                Load(_mode.Next());
                return;
            }
            _d.AdFlow.ShowInterstitialThen(LastResult.Level, SecondsOnLevel(), () => Load(_mode.Next()));
        }

        public void ReplayLevel()
        {
            _d.Analytics?.LevelAbandoned(_level != null ? _level.Level : 0, _launches);
            Load(_mode.Replay());
        }

        private float SecondsOnLevel() => _d.Analytics != null ? _d.Analytics.SecondsOnLevel : 0f;

        // "Double your coins" on the result card: the same win coins again for a whole rewarded ad.
        public void DoubleCoins()
        {
            if (_state != GameState.Result || _coinsDoubled || LastResult.Coins <= 0 || _d.AdFlow == null || _d.Economy == null)
            {
                return;
            }
            _d.AdFlow.WatchRewarded(GrantDoubleCoins, Strings.Game.DoubleCoinsNeedsAd);
        }

        private void GrantDoubleCoins()
        {
            if (_coinsDoubled || _state != GameState.Result)
            {
                return;
            }
            _coinsDoubled = true;
            _d.Economy.Earn(LastResult.Coins);
            _d.Analytics?.CoinsDoubled(LastResult.Coins);
            _d.Result.ShowDoubled(LastResult.Coins * 2);
        }

        // The player went to a menu: whatever was in progress is dropped (a level never resumes), and nothing runs.
        public void LeaveLevel()
        {
            _d.Analytics?.LevelAbandoned(_level != null ? _level.Level : 0, _launches);
            Time.timeScale = 1f;
            StopDemo(clearWell: true);
            if (_layout.IsDragging)
            {
                _layout.CancelDrag();
            }
            _drag?.SetEnabled(false);
            _d.Audio.StopWhoosh();
            _d.Hud.HideHand();
            _d.Links.SetVisible(false);
            _d.PreviewCrash.Hide();
            _d.Hud.SetLesson(string.Empty);
            _state = GameState.Menu;
        }

        private void Load(LevelDefinition level)
        {
            Time.timeScale = 1f;
            _d.Analytics?.LevelAbandoned(_level != null ? _level.Level : 0, _launches);
            _level = level;
            _d.Analytics?.LevelStarted(_mode != null ? _mode.Key : "none", level.Level, level.Par);
            if (_mode != null && _mode.Key == "daily")
            {
                _d.Analytics?.DailyStarted(level.Level);
            }
            _layout.Begin(level);
            _coinsDoubled = false;
            _launches = 0;
            _failedLaunches = 0;
            _usedHint = false;
            _usedExtraWell = false;
            _extraWellOffered = false;
            _pendingHint = default;
            _litShown = -1;
            _ease.Cancel();
            _d.Hud.SetExtraWellOffer(false);
            _d.Hud.HideHand();
            _d.Field.ShowLevel(level);
            _crashBanner = false;
            _d.Hud.SetLesson(_mode != null && _mode.Key == "campaign" ? Strings.Tutorial.ForLevel(level.Level) : string.Empty);
            _d.Gates.ShowLevel(level);
            _d.Asteroids.ShowLevel(level);
            _d.Ship.PlaceOnPad(level);
            _d.Hint.Hide();
            _d.Path.Visible = true;
            _d.Hud.ShowLevel(_mode.LevelNumber, _mode.Key == "daily");
            _d.Ui.SwitchTo(_d.Hud);
            EnterPlanning();
            Refresh(celebrate: false, ease: false);
            _d.Feel.LevelLoaded(level, _trajectory);
            BeginDemoIfNeeded();
        }

        // ---- the level-1 lesson ----

        private void BeginDemoIfNeeded()
        {
            _demoActive = false;
            _demoWellOnField = false;
            _demoStoppedByPlayer = false;
            _demo = null;

            bool wanted = _mode.Key == "campaign" && _level.Level == 1 && _d.Save != null && !_d.Save.Data.TutorialSeen;
            if (wanted && TutorialDemo.TryCreate(_level, out _demo))
            {
                _demoActive = true;
                _demoClock = 0f;
            }
        }

        private void TickDemo(float dt)
        {
            // Any touch on the field or the tray ends the lesson; the touch itself is then the player's own.
            if (TapStarted() && !PointerOverUi())
            {
                StopDemo(clearWell: true);
                _demoStoppedByPlayer = true;
                return;
            }

            _demoClock += dt;
            Vector2 trayWorld = ScreenToWorld(_d.Hud.TrayScreenCenter());
            DemoFrame frame = _demo.Sample(_demoClock, trayWorld);
            _d.Hud.ShowHand(_d.Camera.WorldToScreenPoint(frame.HandPosition), frame.HandAlpha, frame.Carrying);

            // The demo moves the well through the same calls a finger would, so the line bends live.
            if (frame.WellOnField && !_demoWellOnField && _layout.Count == 0)
            {
                if (_layout.BeginDragNew(WellKind.Attractor, frame.HandPosition))
                {
                    _demoWellOnField = true;
                }
            }
            if (_demoWellOnField && frame.Carrying && _layout.IsDragging)
            {
                _layout.DragTo(frame.HandPosition, removeZone: false);
                OnLayoutChanged(false);
            }
            else if (_demoWellOnField && !frame.Carrying && _layout.IsDragging)
            {
                _layout.EndDrag();
                OnLayoutChanged(true);
            }
            else if (_demoWellOnField && !frame.WellOnField)
            {
                ClearDemoWell();
            }
        }

        private void ClearDemoWell()
        {
            if (_layout.IsDragging)
            {
                _layout.CancelDrag();
            }
            _layout.Clear();
            _demoWellOnField = false;
            Refresh(celebrate: false, ease: true);
            BeginEase();
        }

        private void StopDemo(bool clearWell)
        {
            if (!_demoActive)
            {
                return;
            }
            _demoActive = false;
            _d.Hud.HideHand();
            if (clearWell && _demoWellOnField)
            {
                ClearDemoWell();
            }
        }

        private Vector2 ScreenToWorld(Vector2 screen)
        {
            Vector3 p = _d.Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            return new Vector2(p.x, p.y);
        }

        // ---- the plan ----

        private void EnterPlanning()
        {
            _state = GameState.Planning;
            _drag.SetEnabled(true);
            _d.Hud.SetControlsEnabled(true);
            _d.Ship.PlaceOnPad(_level);
            _d.Links.SetVisible(true);
            ShowPreviewCrash();
            RefreshHud();
        }

        // Fly the preview again and show it. Public for tests and tools: it adopts the layout as it is, silently.
        public void Resimulate() => Refresh(celebrate: false, ease: false);

        // The layout changed (a drag moved, a drop, Undo, Restart): fly the preview again and show it. This is the
        // only place the simulator runs for the player; Launch plays what it left behind.
        //   celebrate: a gate that lit or a goal found by this change gets its sound (the player did it)
        //   ease: the line glides to its new shape over a moment instead of snapping (a jump, not a drag step)
        private void Refresh(bool celebrate, bool ease)
        {
            if (_level == null)
            {
                return;
            }

            if (ease)
            {
                _ease.Capture(_trajectory);
            }
            FlightSimulator.Simulate(_level, _layout.SimulationWells, _layout.SimulationCount, _trajectory);

            bool winning = _trajectory.Outcome == FlightOutcome.Won;
            ShowPath();
            _d.Field.SetGoalReached(winning);
            _d.Gates.SetLitMask(_trajectory.GatesLitMask);
            _d.Wells.Sync(_layout);
            _d.Links.Sync(_layout, _trajectory);
            ShowPreviewCrash();
            if (_d.Hint.Visible)
            {
                _d.Hint.Hide();
            }
            _d.Feel.ClearMarks();
            _d.Feel.PreviewChanged(_trajectory, celebrate);
            RefreshHud();
        }

        // A line that runs into a well, a rock or off the field ends in a cross while planning, so the player
        // sees "this crashes" before launching rather than a line that just stops. The first time it is a well,
        // a short message says why (a well placed on the line pulls the ship straight into itself).
        private void ShowPreviewCrash()
        {
            bool planning = _state == GameState.Planning || _state == GameState.Menu;
            FlightOutcome outcome = _trajectory.Outcome;
            bool crashed = outcome == FlightOutcome.HitWell || outcome == FlightOutcome.HitAsteroid;
            if (!planning || !crashed || _trajectory.Count == 0)
            {
                _d.PreviewCrash.Hide();
                RestoreLesson();
                return;
            }

            Vector2 end = _trajectory.Points[_trajectory.Count - 1];
            if (!_d.PreviewCrash.Visible)
            {
                _d.PreviewCrash.Show(end);
            }
            else
            {
                _d.PreviewCrash.MoveTo(end);
            }

            if (outcome == FlightOutcome.HitWell && _layout.Count > 0)
            {
                _crashBanner = true;
                _d.Hud.SetLesson(Strings.Tutorial.TooClose); // the same place the lesson line lives, so the two never overlap
            }
        }

        // Puts the level's own lesson line back once the line stops crashing.
        private void RestoreLesson()
        {
            if (!_crashBanner || _level == null)
            {
                return;
            }
            _crashBanner = false;
            _d.Hud.SetLesson(_mode != null && _mode.Key == "campaign" ? Strings.Tutorial.ForLevel(_level.Level) : string.Empty);
        }

        // The line on screen: the real buffer, or its glide from the previous one.
        private void ShowPath()
        {
            PathView.PathState state = _trajectory.Outcome == FlightOutcome.Won ? PathView.PathState.Winning : PathView.PathState.Neutral;
            _ease.Cancel();
            _d.Path.Show(_trajectory, state);
        }

        // Starts the glide from the captured line to the buffer now holding the new one.
        private void BeginEase()
        {
            _ease.Begin(_trajectory);
            if (_ease.Active)
            {
                _d.Path.Show(_ease.Points, _ease.Count, _trajectory.Outcome, PathStateNow());
            }
        }

        private PathView.PathState PathStateNow() =>
            _trajectory.Outcome == FlightOutcome.Won ? PathView.PathState.Winning : PathView.PathState.Neutral;

        private void TickEase(float seconds)
        {
            if (!_ease.Tick(seconds))
            {
                return;
            }
            if (_ease.Active)
            {
                _d.Path.Show(_ease.Points, _ease.Count, _trajectory.Outcome, PathStateNow());
            }
            else
            {
                _d.Path.Show(_trajectory, PathStateNow()); // arrived: the real buffer, exactly
            }
        }

        // The layout was changed from outside the plan loop (the drag controller calls this; tests and tools may too).
        // settled = a drop or a jump, not one step of a drag: it eases the line and settles the well.
        public void OnLayoutChanged(bool settled)
        {
            Refresh(celebrate: true, ease: settled);
            if (settled)
            {
                BeginEase();

                // The player's own first drop ends the lesson for good: they have shown they know how.
                if (_demoStoppedByPlayer && _layout.Count > 0 && _d.Save != null && !_d.Save.Data.TutorialSeen)
                {
                    _d.Save.Data.TutorialSeen = true;
                    _d.Save.Save();
                }
            }
        }

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

            // What a hint would cost right now: coins if there are enough, otherwise a rewarded ad.
            _d.Hud.SetHintPrice(_d.Economy != null && _d.Economy.CanAfford(Economy.HintCost));
            _d.Hud.SetExtraWellOffer(_extraWellOffered && !_usedExtraWell && _state == GameState.Planning);
        }

        public void Undo()
        {
            if (_state != GameState.Planning || !_layout.Undo())
            {
                return;
            }
            Refresh(celebrate: false, ease: true);
            BeginEase();
        }

        public void Restart()
        {
            if (_state != GameState.Planning)
            {
                return;
            }
            _layout.Clear();
            Refresh(celebrate: false, ease: true);
            BeginEase();
        }

        public void ShowHint()
        {
            if (_state != GameState.Planning || _level == null)
            {
                return;
            }

            StopDemo(clearWell: true);
            for (int i = 0; i < _layout.Count; i++)
            {
                _hintWells[i] = _layout[i];
            }
            var rng = new System.Random(unchecked(_level.Seed + _launches * 977 + _layout.Count));
            if (!HintPlanner.TryPlan(
                    _level, _hintWells, _layout.Count, rng, _hintScratch, out int index, out Vector2 position, _layout.BonusWells))
            {
                // Nothing to show means nothing to pay for.
                _d.Hint.Hide();
                _d.Ui.ShowToast(Strings.Game.NoBetterMove);
                return;
            }

            _pendingHint = new PendingHint { Valid = true, Index = index, Position = position };
            if (_d.Economy != null && _d.Economy.TrySpend(Economy.HintCost))
            {
                _d.Analytics?.HintUsed(_level.Level, paidWithAd: false);
                RevealHint();
            }
            else if (_d.AdFlow != null)
            {
                // Short of coins: the same hint for a whole rewarded ad.
                _d.AdFlow.WatchRewarded(
                    () =>
                    {
                        _d.Analytics?.HintUsed(_level.Level, paidWithAd: true);
                        RevealHint();
                    },
                    Strings.Game.HintNeedsAd);
            }
        }

        private void RevealHint()
        {
            if (!_pendingHint.Valid || _state != GameState.Planning)
            {
                return;
            }
            _usedHint = true;
            bool moving = _pendingHint.Index < _layout.Count;
            _d.Hint.Show(
                _pendingHint.Position, moving ? _layout[_pendingHint.Index].Kind : WellKind.Attractor, moving,
                moving ? _layout[_pendingHint.Index].Position : default);
            _pendingHint = default;
            RefreshHud();
        }

        // ---- the rewarded Extra Well ----

        // Offered after a run of failed launches: watch an ad for one more attractor on this level. A win that
        // used it earns one star (CampaignMode / Scoring).
        private void OnExtraWellPressed()
        {
            if (_state != GameState.Planning || _usedExtraWell || _d.AdFlow == null)
            {
                return;
            }
            _d.AdFlow.WatchRewarded(GrantExtraWell, Strings.Game.ExtraWellNeedsAd);
        }

        private void GrantExtraWell()
        {
            if (_usedExtraWell)
            {
                return;
            }
            _usedExtraWell = true;
            _d.Analytics?.ExtraWellUsed(_level.Level);
            _layout.GrantBonusWell();
            RefreshHud();
        }

        // ---- the launch and the flight ----

        public void Launch()
        {
            if (_state != GameState.Planning || _level == null || _layout.IsDragging)
            {
                return;
            }

            StopDemo(clearWell: false);
            _launches++;
            _wellsAtLaunch = _layout.Count;
            _d.Links.SetVisible(false);
            _d.PreviewCrash.Hide();
            _d.Hud.SetLesson(string.Empty);
            _d.Hint.Hide();
            _drag.SetEnabled(false);
            _d.Hud.SetControlsEnabled(false);
            _d.Hud.SetUndoAvailable(false);

            // The ship gathers on the pad for a moment, then goes. Nothing about the line changes in between.
            _state = GameState.Charging;
            _chargeTimer = 0f;
            _d.Feel.ChargeBegan();
        }

        private void StartFlight()
        {
            // The buffer the preview drew, played back. No simulation here.
            _playback.Begin(_trajectory);
            _state = GameState.Flying;
            _litShown = -1;
            _d.Ship.Place(_playback.Position, _playback.Heading);
            _d.Feel.FlightStarted();
            RefreshHud();
        }

        private void Update()
        {
            _d.Audio.Tick(Time.unscaledDeltaTime);
            _d.Hud.Tick(Time.unscaledDeltaTime);
            _d.AdFlow?.Tick(Time.unscaledDeltaTime);
            if (_level == null || _state == GameState.Menu)
            {
                return;
            }

            float dt = Time.deltaTime;
            _d.Feel.Tick(dt);
            _d.Links.Tick(dt);
            _d.PreviewCrash.Tick(dt);
            TickEase(dt);

            switch (_state)
            {
                case GameState.Planning:
                    // While an ad is up the board is held still, and a finger belongs to the overlay.
                    if (_d.AdFlow != null && (_d.AdFlow.Busy || _d.Ui.AdOverlayVisible))
                    {
                        break;
                    }
                    if (_demoActive)
                    {
                        TickDemo(dt);
                    }
                    _drag.Tick();
                    break;
                case GameState.Charging:
                    _chargeTimer += dt;
                    _d.Feel.Charging(_chargeTimer / GameConfig.LaunchChargeSeconds);
                    if (_chargeTimer >= GameConfig.LaunchChargeSeconds)
                    {
                        StartFlight();
                    }
                    break;
                case GameState.Flying:
                    TickFlight(dt);
                    break;
                case GameState.FailHold:
                    _holdTimer -= dt;
                    if (_holdTimer <= 0f)
                    {
                        EnterPlanning();
                    }
                    break;
                case GameState.WinHold:
                    _holdTimer -= dt;
                    if (_holdTimer <= 0f)
                    {
                        ShowResult();
                    }
                    break;
            }
        }

        private void TickFlight(float dt)
        {
            if (TapStarted() && !PointerOverUi())
            {
                _playback.SpeedUp();
            }

            _playback.Advance(dt);
            _d.Ship.Place(_playback.Position, _playback.Heading);
            _d.Gates.SetLitMask(_playback.GatesLitMaskSoFar());
            _d.Feel.FlightTick(_playback, dt);

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

            bool won = _playback.Outcome == FlightOutcome.Won;
            _d.Analytics?.Launched(_level.Level, _wellsAtLaunch, _playback.GatesLitSoFar(), won);
            if (won)
            {
                _d.Analytics?.StopClock();
                _state = GameState.WinHold;
                _holdTimer = GameConfig.WinHoldSeconds;
                _d.Feel.Won();
            }
            else
            {
                // A failed launch costs nothing but the count: the wells stay where they are.
                _failedLaunches++;
                _state = GameState.FailHold;
                _holdTimer = FailHoldSeconds;
                _d.Feel.Failed(_trajectory, _playback.GatesLitMaskSoFar(), _failedLaunches);
                if (_failedLaunches >= GameConfig.FailsBeforeExtraWellOffer)
                {
                    _extraWellOffered = true; // shown again whenever planning resumes, until it is taken
                }
            }
        }

        private void ShowResult()
        {
            LastResult = _mode.OnWon(_wellsAtLaunch, _launches, _usedExtraWell, _usedHint);
            _d.Analytics?.LevelCompleted(_mode.Key, LastResult.Level, LastResult.WellsUsed, LastResult.Par, LastResult.Launches, LastResult.Stars);
            if (LastResult.IsDaily)
            {
                _d.Analytics?.DailyCompleted(LastResult.Level, LastResult.Streak);
            }
            _d.Result.ShowResult(LastResult);
            _d.Ui.SwitchTo(_d.Result);
            _state = GameState.Result;
        }

        // ---- pause ----

        public void Pause()
        {
            if (_state == GameState.Paused || _state == GameState.Result || _state == GameState.Menu || _level == null)
            {
                return;
            }
            _stateBeforePause = _state;
            _drag.SetEnabled(false);
            _d.Audio.StopWhoosh();
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
            else if (_state == GameState.Flying)
            {
                _d.Audio.StartWhoosh();
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

        // The Menu button on the pause card and on the result card.
        public void GoToMenu()
        {
            Time.timeScale = 1f;
            _d.ShowMainMenu?.Invoke();
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
