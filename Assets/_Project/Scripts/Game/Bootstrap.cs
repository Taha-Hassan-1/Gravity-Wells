using System;
using UnityEngine;
using GravityWells.Core;
using GravityWells.Meta;
using GravityWells.Services;
using GravityWells.UI;

namespace GravityWells.Game
{
    // The single component in the single scene. Everything else is built from here in code: the camera, the
    // canvas, the board's views, the services and the screens. This is also the only place concrete service
    // classes are chosen.
    public sealed class Bootstrap : MonoBehaviour
    {
        // In the Editor an environment variable can point the save at a throwaway folder, so scripted play
        // sessions never touch the real save.
        public const string DataDirectoryVariable = "GRAVITYWELLS_DATA_DIR";

        private void Awake()
        {
            // Android defaults to 30 fps; the board is cheap to draw.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            // Only the first finger counts, everywhere (dragging and buttons alike).
            Input.multiTouchEnabled = false;

            Camera camera = BuildCamera();
            gameObject.AddComponent<CameraFitter>().Init(camera);

            string dataDirectory = DataDirectory();
            var save = new SaveService(dataDirectory);
            save.Data.SessionCount++;
            save.Save();
            Func<DateTime> utcNow = () => DateTime.UtcNow;
            var progression = new ProgressionService(save);
            var economy = new EconomyService(save);
            var quests = new QuestService(save, economy, utcNow);
            var rewards = new RewardService(economy, quests);
            var campaign = new CampaignMode(progression, rewards);
            var daily = new DailyMode(save, rewards, utcNow);

            var themes = gameObject.AddComponent<ThemeController>();
            themes.Init(ValidThemeIndex(save.Data), new LineStyleService(save));

            var ui = gameObject.AddComponent<UIRoot>();
            ui.Init(themes.Current);

            // Sound and vibration follow the saved settings.
            var haptics = new HapticService { Enabled = save.Data.HapticsOn, Strong = save.Data.VibrationStrong };
            var audioHost = new GameObject("Audio");
            audioHost.transform.SetParent(transform, false);
            var audio = new AudioService(audioHost.AddComponent<AudioSource>())
            {
                Enabled = save.Data.SoundOn,
                MusicEnabled = save.Data.MusicOn,
            };
            ui.Style.ButtonPressed = audio.PlayClick;

            // External services, all behind interfaces and all registered here and only here. Ad networks are
            // combined by priority (GameConfig.AdProviderOrder); only the ones named in the order are constructed,
            // so an SDK that is not in the order is never touched. On an Android device that is real AdMob plus
            // Google's UMP consent; in the Editor and on any other target the stubs keep every flow testable
            // without a network. The stub is never in the device order: it always "completes", so it would hand out
            // rewards for free in a release build. A mediation or publisher SDK is one more Register line here.
            var adRegistry = new AdProviderRegistry();
            adRegistry.Register(StubAdService.ProviderName, () => new StubAdService());
#if UNITY_ANDROID && !UNITY_EDITOR
            adRegistry.Register(
                AdMobAdService.ProviderName,
                () => new AdMobAdService(GameConfig.AdMobInterstitialUnitId, GameConfig.AdMobRewardedUnitId));
            string adOrder = GameConfig.AdProviderOrder;
            IConsentService consent = new AdMobConsentService();
#else
            string adOrder = GameConfig.EditorAdProviderOrder;
            IConsentService consent = new StubConsentService();
#endif
            IAdService ads = new WaterfallAdService(adRegistry, AdProviderOrder.Parse(adOrder), Debug.LogWarning);

            // Every event goes to the console (development builds) and to a capped file on the device; a real
            // backend, or a publisher's SDK, is one more sink in this list and nothing in gameplay changes.
            IAnalyticsService analytics = new CompositeAnalyticsService(
                new LogAnalyticsService(),
                new FileAnalyticsService(dataDirectory, utcNow, GameConfig.LocalEventLogMaxBytes));
            var exceptionReporter = new ExceptionReporter(analytics);
            Application.logMessageReceived += exceptionReporter.Handle;
            analytics.LogEvent(AnalyticsEvents.SessionStart, new AnalyticsParam("session", save.Data.SessionCount));
            var gameAnalytics = new GameAnalytics(analytics, () => Time.realtimeSinceStartup);
            var monetization = new MonetizationController(
                ads, new StubPurchaseService(), consent, analytics, save, () => Time.realtimeSinceStartup);
            var adFlow = new AdFlow(monetization, ui, () => Time.unscaledTime);
            ui.AdOverlayDismissed = adFlow.Dismiss;

            Transform boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);
            var field = new FieldView(boardRoot);
            var path = new PathView(boardRoot);
            var wells = new WellViews(boardRoot);
            var gates = new GateViews(boardRoot);
            var asteroids = new AsteroidViews(boardRoot);
            var ship = new ShipView(boardRoot);
            var hint = new HintView(boardRoot);
            var links = new WellLinkView(boardRoot);
            var previewCrash = new CrashMarkView(boardRoot);
            var trail = new TrailView(boardRoot);
            var burst = new BurstView(boardRoot);
            var mark = new CrashMarkView(boardRoot);

            var gameManager = gameObject.AddComponent<GameManager>();
            var appFlow = gameObject.AddComponent<AppFlow>();
            var hud = new GameHUD(
                ui, ui.Style, gameManager.TogglePause, gameManager.Undo, gameManager.Restart, gameManager.ShowHint,
                gameManager.Launch);
            var pause = new PausePanel(ui, ui.Style, gameManager.Resume, gameManager.RestartFromPause, gameManager.GoToMenu);
            var result = new LevelCompletePanel(ui, ui.Style, gameManager.NextLevel, gameManager.ReplayLevel, gameManager.GoToMenu, gameManager.DoubleCoins);
            var menu = new MainMenuScreen(appFlow, ui, ui.Style);
            var settings = new SettingsScreen(appFlow, ui, ui.Style);
            var stats = new StatsScreen(appFlow, ui, ui.Style);
            var themesScreen = new ThemesScreen(appFlow, ui, ui.Style);
            var howToPlay = new HowToPlayScreen(appFlow, ui, ui.Style);
            var feel = new FeelController(audio, haptics, field, path, wells, gates, ship, trail, burst, mark, hud);
            foreach (UIScreen screen in new UIScreen[] { hud, pause, result, menu, settings, stats, themesScreen, howToPlay })
            {
                ui.AddScreen(screen);
            }

            gameManager.Init(
                new GameDependencies
                {
                    Camera = camera,
                    Mode = campaign,
                    Ui = ui,
                    Hud = hud,
                    Pause = pause,
                    Result = result,
                    Field = field,
                    Path = path,
                    Wells = wells,
                    Gates = gates,
                    Asteroids = asteroids,
                    Ship = ship,
                    Hint = hint,
                    Links = links,
                    PreviewCrash = previewCrash,
                    Feel = feel,
                    Haptics = haptics,
                    Audio = audio,
                    Economy = economy,
                    AdFlow = adFlow,
                    Analytics = gameAnalytics,
                    Save = save,
                    ShowMainMenu = appFlow.ShowMainMenu,
                });
            appFlow.Init(gameManager, ui, themes, save, progression, economy, quests, campaign, daily, haptics, audio, monetization);
            appFlow.SetScreens(menu, settings, stats, themesScreen);
            appFlow.SetHowToPlay(howToPlay);

            ui.FinishBuild(); // the toast and the ad overlay go on top of every screen

            // Everything that draws in theme colours listens to one controller, so a theme change (and its
            // cross-fade) reaches the camera, the board and the UI together.
            themes.Changed += palette =>
            {
                camera.backgroundColor = palette.Background;
                ui.ApplyTheme(palette);
                gameManager.ApplyTheme(palette);
            };
            themes.Broadcast();

            monetization.Initialize();
            audio.PlayMusic(MusicTrack.Orbit);
            appFlow.ShowMainMenu();
        }

        // A save from a future or corrupt build could name a theme that does not exist or is not unlocked yet;
        // fall back to the first palette instead of throwing.
        private static int ValidThemeIndex(SaveData data)
        {
            int index = data.SelectedThemeId;
            bool exists = index >= 0 && index < ThemeService.Palettes.Length;
            return exists && ThemeService.IsUnlocked(index, data.HighestLevelUnlocked) ? index : 0;
        }

        private static string DataDirectory()
        {
#if UNITY_EDITOR
            string overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
            if (!string.IsNullOrEmpty(overridden))
            {
                return overridden;
            }
#endif
            return Application.persistentDataPath;
        }

        private Camera BuildCamera()
        {
            var cameraGO = new GameObject("Main Camera");
            cameraGO.transform.SetParent(transform, false);
            // Board content sits at z = 0; the camera sits in front of it (negative z) or nothing renders.
            // CameraFitter only ever rewrites x and y, keeping this z.
            cameraGO.transform.localPosition = new Vector3(0f, 0f, -10f);
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camera.orthographic = true;
            camera.tag = "MainCamera";
            cameraGO.AddComponent<AudioListener>();
            return camera;
        }
    }
}
