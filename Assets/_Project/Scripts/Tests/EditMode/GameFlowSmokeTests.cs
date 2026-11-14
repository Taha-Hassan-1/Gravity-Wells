using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GravityWells.Core;
using GravityWells.Game;
using GravityWells.UI;

namespace GravityWells.Tests
{
    // Boots the real Bootstrap scene in Play Mode and plays it: the only tests that exercise GameManager, the
    // views and the screens together. They live in the EditMode assembly (entering Play Mode from there) so the
    // project keeps its four asmdefs.
    //
    // The save goes to a throwaway folder (Bootstrap.DataDirectoryVariable), so the real save is never touched.
    // Any error or exception logged while playing fails the test. Nothing is kept in fields across Play Mode:
    // entering it reloads the domain.
    public class GameFlowSmokeTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string ShotFolder = "Logs/Screens";
        private const double WaitSeconds = 60.0;

        private static string DataDir => Path.Combine(Path.GetTempPath(), "GravityWellsGameFlowSmokeTests");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Directory.Exists(DataDir))
            {
                Directory.Delete(DataDir, recursive: true);
            }
            Directory.CreateDirectory(DataDir);
            Environment.SetEnvironmentVariable(Bootstrap.DataDirectoryVariable, DataDir);

            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null; // Bootstrap.Awake has run; let one frame pass
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying)
            {
                yield return new ExitPlayMode();
            }
            Environment.SetEnvironmentVariable(Bootstrap.DataDirectoryVariable, null);
        }

        private static GameManager Game()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                GameManager game = root.GetComponent<GameManager>();
                if (game != null)
                {
                    return game;
                }
            }
            Assert.Fail("Bootstrap did not build a GameManager");
            return null;
        }

        private static UIRoot Ui(GameManager game) => game.GetComponent<UIRoot>();

        // Frames until this much real time has passed (screens fade in over a fraction of a second).
        private static IEnumerator WaitRealtime(double seconds)
        {
            double until = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static double Deadline() => EditorApplication.timeSinceStartup + WaitSeconds;
        private static bool Expired(double deadline) => EditorApplication.timeSinceStartup > deadline;

        // Puts the generator's own solution on the field the way a player's drags would, as placed edits.
        private static void PlaceTheSolution(GameManager game)
        {
            foreach (Well well in game.Level.SolutionWells)
            {
                Assert.GreaterOrEqual(game.Layout.Place(well.Kind, well.Position), 0);
            }
            game.Resimulate();
        }

        [UnityTest]
        public IEnumerator Level1_SolvedWithItsWells_FliesExactlyTheShownLine_AndWins()
        {
            GameManager game = Game();
            Assert.IsInstanceOf<GameHUD>(Ui(game).Current);
            Assert.AreEqual(GameManager.GameState.Planning, game.State);
            Assert.AreEqual(1, game.Level.Level);
            Assert.AreEqual(FlightOutcome.LeftField, game.Trajectory.Outcome, "with no wells the line leaves the field");
            SaveShot("01-level1-empty");

            PlaceTheSolution(game);
            Assert.AreEqual(FlightOutcome.Won, game.Trajectory.Outcome);
            SaveShot("02-level1-solved-preview");

            // The preview line, copied now: the flight must be this very buffer.
            Vector2[] shown = new Vector2[game.Trajectory.Count];
            Array.Copy(game.Trajectory.Points, shown, shown.Length);
            int shownHash = game.Trajectory.Hash();

            game.Launch();
            Assert.AreEqual(GameManager.GameState.Flying, game.State);

            double deadline = Deadline();
            int frames = 0;
            bool shot = false;
            while (game.State == GameManager.GameState.Flying && !Expired(deadline))
            {
                yield return null;
                frames++;
                Vector2 ship = game.ShipPosition;
                Assert.AreEqual(game.Playback.Position.x, ship.x, 1e-4f, "the ship view is where the playback says");
                Assert.AreEqual(game.Playback.Position.y, ship.y, 1e-4f);

                int step = Mathf.Min(game.Playback.Step, shown.Length - 2);
                float sqr = GeometryUtils.SqrDistancePointToSegment(game.Playback.Position, shown[step], shown[step + 1]);
                Assert.Less(sqr, 1e-9f, "the ship left the line that was shown, at step " + step);

                if (!shot && game.Playback.Step > shown.Length / 2)
                {
                    SaveShot("03-level1-midflight");
                    shot = true;
                }
            }

            Assert.AreEqual(shownHash, game.Trajectory.Hash(), "nothing re-simulated during the flight");
            Assert.Greater(frames, 5, "the flight took real frames, it was not skipped");
            Assert.IsTrue(game.Playback.Finished);

            while (game.State != GameManager.GameState.Result && !Expired(deadline))
            {
                yield return null;
            }
            Assert.AreEqual(GameManager.GameState.Result, game.State);
            Assert.IsInstanceOf<LevelCompletePanel>(Ui(game).Current);
            Assert.AreEqual(3, game.LastResult.Stars, "par wells or fewer is three stars");
            Assert.AreEqual(1, game.LastResult.Launches);
            Assert.AreEqual(game.Level.Par, game.LastResult.WellsUsed);
            yield return WaitRealtime(0.5); // the card fades in over 0.18 s
            SaveShot("04-level1-result");

            game.NextLevel();
            yield return null;
            Assert.AreEqual(2, game.Level.Level);
            Assert.AreEqual(GameManager.GameState.Planning, game.State);
            Assert.IsInstanceOf<GameHUD>(Ui(game).Current);
            Assert.AreEqual(0, game.Layout.Count, "a new level starts with an empty field");
        }

        [UnityTest]
        public IEnumerator AFailedLaunch_KeepsTheWells_AndCostsOnlyTheCount()
        {
            GameManager game = Game();
            game.Layout.Place(WellKind.Attractor, new Vector2(0.85f, 1.35f)); // far from anything useful
            game.Resimulate();
            Assert.AreNotEqual(FlightOutcome.Won, game.Trajectory.Outcome);
            Vector2 where = game.Layout[0].Position;

            game.Launch();
            double deadline = Deadline();
            while (game.State != GameManager.GameState.Planning && !Expired(deadline))
            {
                yield return null;
            }

            Assert.AreEqual(GameManager.GameState.Planning, game.State);
            Assert.AreEqual(1, game.Launches);
            Assert.AreEqual(1, game.Layout.Count);
            Assert.AreEqual(where, game.Layout[0].Position, "the well stays exactly where it was");
            Assert.IsTrue(game.Layout.CanUndo);
        }

        [UnityTest]
        public IEnumerator UndoRestartAndPause_BehaveAndNeverStrandTheGame()
        {
            GameManager game = Game();
            UIRoot ui = Ui(game);

            game.Layout.Place(WellKind.Attractor, new Vector2(0.3f, 0.9f));
            game.Layout.Place(WellKind.Attractor, new Vector2(0.7f, 0.9f));
            game.Resimulate();
            Assert.AreEqual(2, game.Layout.Count);

            game.Undo();
            Assert.AreEqual(1, game.Layout.Count);
            game.Restart();
            Assert.AreEqual(0, game.Layout.Count);
            Assert.IsFalse(game.Layout.CanUndo);

            game.Pause();
            yield return null;
            Assert.IsTrue(game.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsInstanceOf<PausePanel>(ui.Current);
            ui.Current.OnBack(); // the Android back button resumes
            yield return null;
            Assert.IsFalse(game.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsInstanceOf<GameHUD>(ui.Current);

            game.ShowHint();
            yield return null;
            SaveShot("05-hint");
            game.Pause();
            yield return WaitRealtime(0.5);
            SaveShot("06-pause");
            game.Resume();
        }

        [UnityTest]
        public IEnumerator ALaterLevel_WithEveryPiece_PlaysThroughToo()
        {
            GameManager game = Game();
            var level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(60), 60);
            game.StartLevel(level);
            yield return null;
            Assert.AreEqual(level.Gates.Length, 5);
            Assert.Greater(level.Asteroids.Length, 0);
            SaveShot("07-level60-empty");

            PlaceTheSolution(game);
            Assert.AreEqual(FlightOutcome.Won, game.Trajectory.Outcome);
            SaveShot("08-level60-solved");
            game.Launch();

            double deadline = Deadline();
            while (game.State != GameManager.GameState.Result && !Expired(deadline))
            {
                yield return null;
            }
            Assert.AreEqual(GameManager.GameState.Result, game.State);
        }

        private static void SaveShot(string name)
        {
            // Rendering needs the Game view's camera; a failure here must not fail a gameplay test.
            try
            {
                Directory.CreateDirectory(ShotFolder);
                ScreenShot.Save(Path.Combine(ShotFolder, name + ".png"));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Shot] " + name + " not saved: " + exception.Message);
            }
        }
    }

    // Renders the board and the canvas offscreen at 1080x1920 (portrait), the way every screen is checked.
    public static class ScreenShot
    {
        public static void Save(string path)
        {
            const int width = 1080, height = 1920;
            Camera camera = Camera.main;
            Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();

            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            float previousSize = camera.orthographicSize;
            Vector3 previousPosition = camera.transform.position;

            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            CameraFitResult fit = CameraFit.ComputeForField((float)width / height);
            camera.orthographicSize = fit.OrthographicSize;
            camera.transform.position = new Vector3(fit.Center.x, fit.Center.y, previousPosition.z);

            // The canvas is drawn by the same camera for this one frame, on top of the board.
            RenderMode mode = canvas.renderMode;
            Camera worldCamera = canvas.worldCamera;
            int order = canvas.sortingOrder;
            float plane = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            canvas.renderMode = mode;
            canvas.worldCamera = worldCamera;
            canvas.sortingOrder = order;
            canvas.planeDistance = plane;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previousActive;

            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            camera.orthographicSize = previousSize;
            camera.transform.position = previousPosition;
            UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
