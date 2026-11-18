using UnityEngine;
using GravityWells.Core;
using GravityWells.Meta;
using GravityWells.Services;
using GravityWells.UI;

namespace GravityWells.Game
{
    // How the player moves between the menu, the other screens and a level, and what the screens may ask the
    // app to do. The board and the rules of play stay in GameManager and the modes; this class owns navigation,
    // the settings and the theme choice. Every screen has a way back to the menu, and the menu's own Back asks
    // twice before leaving, so the Android Back button never dead-ends.
    public sealed class AppFlow : MonoBehaviour
    {
        private const float DoubleBackSeconds = 2f;

        private GameManager _game;
        private UIRoot _ui;
        private ThemeController _themes;
        private SaveService _save;
        private ProgressionService _progression;
        private EconomyService _economy;
        private QuestService _quests;
        private CampaignMode _campaign;
        private DailyMode _daily;
        private HapticService _haptics;
        private AudioService _audio;
        private MonetizationController _monetization;

        private UIScreen _menu;
        private UIScreen _settings;
        private UIScreen _stats;
        private UIScreen _themesScreen;
        private HowToPlayScreen _howToPlay;
        private float _lastMenuBack = -10f;

        public void Init(
            GameManager game, UIRoot ui, ThemeController themes, SaveService save, ProgressionService progression,
            EconomyService economy, QuestService quests, CampaignMode campaign, DailyMode daily, HapticService haptics,
            AudioService audio, MonetizationController monetization)
        {
            _game = game;
            _ui = ui;
            _themes = themes;
            _save = save;
            _progression = progression;
            _economy = economy;
            _quests = quests;
            _campaign = campaign;
            _daily = daily;
            _haptics = haptics;
            _audio = audio;
            _monetization = monetization;
        }

        public void SetHowToPlay(HowToPlayScreen screen) => _howToPlay = screen;

        public void SetScreens(UIScreen menu, UIScreen settings, UIScreen stats, UIScreen themes)
        {
            _menu = menu;
            _settings = settings;
            _stats = stats;
            _themesScreen = themes;
        }

        // What the screens read.
        public SaveService Save => _save;
        public ProgressionService Progression => _progression;
        public EconomyService Economy => _economy;
        public QuestService Quests => _quests;
        public DailyMode Daily => _daily;
        public GameManager Game => _game;

        // ---- navigation ----

        public void ShowMainMenu()
        {
            _game.LeaveLevel();
            _quests.EnsureToday();
            _ui.SwitchTo(_menu);
        }

        public void ShowSettings() => _ui.SwitchTo(_settings);

        public void ShowStats() => _ui.SwitchTo(_stats);

        public void ShowThemes() => _ui.SwitchTo(_themesScreen);

        public void ShowHowToPlay() => _ui.SwitchTo(_howToPlay);

        // The menu button: the pages, then back to the menu.
        public void OpenHowToPlay() => _howToPlay.Open(thenPlay: false);

        public void FinishHowToPlay(bool thenPlay)
        {
            if (!_save.Data.HowToPlaySeen)
            {
                _save.Data.HowToPlaySeen = true;
                _save.Save();
            }
            if (thenPlay)
            {
                _game.StartMode(_campaign);
            }
            else
            {
                ShowMainMenu();
            }
        }

        // Continue the campaign from the level the player has reached.
        // A brand-new player reads the pages once first; everyone else goes straight to the level.
        public void Play()
        {
            if (!_save.Data.TutorialSeen && !_save.Data.HowToPlaySeen)
            {
                _howToPlay.Open(thenPlay: true);
                return;
            }
            _game.StartMode(_campaign);
        }

        public void PlayDaily() => _game.StartMode(_daily);

        // The menu's Back: the first press says what the second will do.
        public void OnMenuBack()
        {
            float now = Time.unscaledTime;
            if (now - _lastMenuBack <= DoubleBackSeconds)
            {
                Application.Quit();
                return;
            }
            _lastMenuBack = now;
            _ui.ShowToast(Strings.Menu.PressBackAgain);
        }

        // ---- settings ----

        public bool SoundOn => _save.Data.SoundOn;
        public bool MusicOn => _save.Data.MusicOn;

        public void SetSoundOn(bool on)
        {
            _save.Data.SoundOn = on;
            _audio.Enabled = on;
            _save.Save();
        }

        public void SetMusicOn(bool on)
        {
            _save.Data.MusicOn = on;
            _audio.MusicEnabled = on;
            _save.Save();
        }

        // Off, then On, then Strong, then Off again.
        public void CycleVibrations()
        {
            SaveData data = _save.Data;
            if (!data.HapticsOn)
            {
                data.HapticsOn = true;
                data.VibrationStrong = false;
            }
            else if (!data.VibrationStrong)
            {
                data.VibrationStrong = true;
            }
            else
            {
                data.HapticsOn = false;
                data.VibrationStrong = false;
            }
            _haptics.Enabled = data.HapticsOn;
            _haptics.Strong = data.VibrationStrong;
            _save.Save();
            _haptics.Grab(); // a sample of what was just chosen (silent when switched off)
        }

        // ---- themes ----

        public int SelectedTheme => _save.Data.SelectedThemeId;

        public bool IsThemeUnlocked(int index) => ThemeService.IsUnlocked(index, _progression.HighestLevelUnlocked);

        public bool SelectTheme(int index)
        {
            if (index < 0 || index >= ThemeService.Palettes.Length || !IsThemeUnlocked(index))
            {
                return false;
            }
            _save.Data.SelectedThemeId = index;
            _save.Save();
            _themes.SetTheme(index);
            return true;
        }

        // ---- the store ----

        public bool AdsRemoved => _monetization.AdsRemoved;
        public string RemoveAdsPrice => _monetization.RemoveAdsPrice;
        public bool PrivacyOptionsRequired => _monetization.PrivacyOptionsRequired;

        public void PurchaseRemoveAds()
        {
            _monetization.PurchaseRemoveAds(outcome =>
            {
                if (outcome != PurchaseOutcome.Success)
                {
                    _ui.ShowToast(Strings.Settings.PurchaseUnavailable);
                }
            });
        }

        public void RestorePurchases()
        {
            _monetization.RestorePurchases(found =>
                _ui.ShowToast(found ? Strings.Settings.RestoreDone : Strings.Settings.RestoreNothing));
        }

        public void ShowPrivacyOptions() => _monetization.ShowPrivacyOptions(() => { });

        public void OpenPrivacyPolicy()
        {
            if (string.IsNullOrEmpty(GameConfig.PrivacyPolicyUrl))
            {
                _ui.ShowToast(Strings.Settings.PrivacyPolicyNotPublished);
                return;
            }
            Application.OpenURL(GameConfig.PrivacyPolicyUrl);
        }
    }
}

