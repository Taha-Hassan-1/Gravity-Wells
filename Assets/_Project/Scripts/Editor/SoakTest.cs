using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using GravityWells.Core;
using GravityWells.Game;

namespace GravityWells.Editor
{
    // Plays the campaign through the real scene, as fast as the clock allows, with rendering on: for each level
    // it puts the generator's own solution on the field, launches, waits for the result card and taps Next level.
    // A level that does not win, an exception, or a level that never reaches the result stops being "a clean run"
    // and is counted. Menu: GravityWells/Tuning/Run Soak Test. The save goes to a throwaway folder, so the real
    // save is never touched. The report is written to Logs/Soak.txt.
    [InitializeOnLoad]
    public static class SoakTest
    {
        private const string ActiveKey = "GW.Soak.Active";
        private const string TargetKey = "GW.Soak.Target";
        private const string DoneKey = "GW.Soak.Done";
        private const string FailKey = "GW.Soak.Fail";
        private const string ErrorKey = "GW.Soak.Errors";
        private const string StartKey = "GW.Soak.StartTicks";
        private const float TimeScale = 12f;
        private const double LevelTimeoutSeconds = 40;
        private const int DefaultLevels = 1000;

        private static GameManager _game;
        private static int _levelAtStart;
        private static double _levelDeadline;
        private static bool _waitingForNext;
        private static int _launchedLevel = -1;

        static SoakTest()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("GravityWells/Tuning/Run Soak Test (1,000 levels)")]
        public static void Run() => Begin(DefaultLevels);

        // Also callable from the CLI: eval_file with GravityWells.Editor.SoakTest.Begin(100).
        public static void Begin(int levels)
        {
            string dir = Path.Combine(Path.GetTempPath(), "gw-soak-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            Environment.SetEnvironmentVariable(GravityWells.Game.Bootstrap.DataDirectoryVariable, dir);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(TargetKey, levels);
            SessionState.SetInt(DoneKey, 0);
            SessionState.SetInt(FailKey, 0);
            SessionState.SetInt(ErrorKey, 0);
            SessionState.SetString(StartKey, DateTime.UtcNow.Ticks.ToString());
            _game = null;
            _waitingForNext = false;
            _launchedLevel = -1;
            EditorApplication.isPlaying = true;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (SessionState.GetBool(ActiveKey, false) && (type == LogType.Exception || type == LogType.Error))
            {
                SessionState.SetInt(ErrorKey, SessionState.GetInt(ErrorKey, 0) + 1);
                Append("ERROR " + type + ": " + condition);
            }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
            {
                return;
            }

            if (_game == null)
            {
                _game = FindGame();
                if (_game == null)
                {
                    return;
                }
                _game.GetComponent<AppFlow>().Play();
                Time.timeScale = TimeScale;
                _levelAtStart = -1;
            }

            int target = SessionState.GetInt(TargetKey, DefaultLevels);
            if (SessionState.GetInt(DoneKey, 0) >= target)
            {
                Finish();
                return;
            }

            // Real-time deadline per level: a level that stalls is a failure, and the run moves on.
            if (_game.Level != null && _game.Level.Level != _levelAtStart)
            {
                _levelAtStart = _game.Level.Level;
                _levelDeadline = EditorApplication.timeSinceStartup + LevelTimeoutSeconds;
                _launchedLevel = -1;
                _waitingForNext = false;
            }
            else if (EditorApplication.timeSinceStartup > _levelDeadline)
            {
                Fail("timed out in state " + _game.State + " on level " + _game.Level.Level);
                _levelDeadline = EditorApplication.timeSinceStartup + LevelTimeoutSeconds;
                _game.NextLevel();
                return;
            }

            // The interstitial policy may hold the game for a moment; Time.timeScale stays up so flights are quick.
            Time.timeScale = _game.State == GameManager.GameState.Paused ? 1f : TimeScale;

            switch (_game.State)
            {
                case GameManager.GameState.Planning:
                    if (_launchedLevel != _game.Level.Level)
                    {
                        _launchedLevel = _game.Level.Level;
                        foreach (Well well in _game.Level.SolutionWells)
                        {
                            _game.Layout.Place(well.Kind, well.Position);
                        }
                        _game.Resimulate();
                        if (_game.Trajectory.Outcome != FlightOutcome.Won)
                        {
                            Fail("solution does not win on level " + _game.Level.Level + ": " + _game.Trajectory.Outcome);
                        }
                        _game.Launch();
                    }
                    break;
                case GameManager.GameState.Result:
                    if (!_waitingForNext)
                    {
                        _waitingForNext = true;
                        SessionState.SetInt(DoneKey, SessionState.GetInt(DoneKey, 0) + 1);
                        _game.NextLevel();
                    }
                    break;
            }
        }

        private static void Fail(string reason)
        {
            SessionState.SetInt(FailKey, SessionState.GetInt(FailKey, 0) + 1);
            Append("FAIL " + reason);
        }

        private static GameManager FindGame()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                GameManager game = root.GetComponent<GameManager>();
                if (game != null)
                {
                    return game;
                }
            }
            return null;
        }

        private static void Finish()
        {
            long ticks = long.Parse(SessionState.GetString(StartKey, "0"));
            TimeSpan took = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ticks);
            string line = string.Format(
                "SOAK levels={0} failures={1} errors={2} took={3:F0}s", SessionState.GetInt(DoneKey, 0),
                SessionState.GetInt(FailKey, 0), SessionState.GetInt(ErrorKey, 0), took.TotalSeconds);
            Append(line);
            Debug.Log(line);
            SessionState.SetBool(ActiveKey, false);
            Time.timeScale = 1f;
            Environment.SetEnvironmentVariable(GravityWells.Game.Bootstrap.DataDirectoryVariable, null);
            EditorApplication.isPlaying = false;
        }

        private static void Append(string text)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Soak.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine);
        }
    }
}
