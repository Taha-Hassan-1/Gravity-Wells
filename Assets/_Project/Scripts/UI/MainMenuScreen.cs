using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Core;
using GravityWells.Game;

namespace GravityWells.UI
{
    // The first screen: the title, Play (with the level the player has reached), the daily challenge with its
    // streak, today's three quests with their progress, the coin balance, and the way to Themes, Stats and
    // Settings. Back asks twice before the app closes.
    public sealed class MainMenuScreen : UIScreen
    {
        private const float TileWidth = 860f;

        private readonly AppFlow _flow;
        private readonly UIStyle _style;
        private readonly TMP_Text _playLabel;
        private readonly TMP_Text _dailyLabel;
        private readonly TextMeshProUGUI _coins;
        private readonly TextMeshProUGUI[] _questText = new TextMeshProUGUI[QuestBoard.QuestsPerDay];
        private readonly TextMeshProUGUI[] _questValue = new TextMeshProUGUI[QuestBoard.QuestsPerDay];

        public MainMenuScreen(AppFlow flow, UIRoot ui, UIStyle style)
        {
            _flow = flow;
            _style = style;
            BuildRoot(ui.CanvasTransform, "MainMenu", ui, style, opaque: true);

            // The coin balance, top right.
            Image pill = UIFactory.CreateSurface(Content, "CoinPill", style);
            UIFactory.Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(-UITokens.SpaceL, -UITokens.SpaceL), new Vector2(250f, 88f));
            Image coin = UIFactory.CreateIconImage(pill.rectTransform, "Coin", UIIcons.Get("coin"), 56f);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(56f, 56f));
            _coins = UIFactory.CreateText(pill.rectTransform, "Coins", "0", 48f, TextAlignmentOptions.MidlineRight, style, bold: true);
            UIFactory.Place(_coins.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(150f, 80f));

            TextMeshProUGUI title = UIFactory.CreateText(Content, "Title", Strings.Menu.Title, 120f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1000f, 150f));
            TextMeshProUGUI tagline = UIFactory.CreateText(Content, "Tagline", Strings.Menu.Tagline, 48f, TextAlignmentOptions.Center, style, dim: true);
            UIFactory.Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(1000f, 70f));

            // Play and the daily.
            Button play = UIFactory.CreateTile(
                Content, "Play", Strings.Menu.Play, string.Empty, new Vector2(TileWidth, 190f), style,
                () => _flow.Play(), primary: true, UIIcons.Get("play"), 72f);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(TileWidth, 190f));
            _playLabel = UIFactory.LabelOf(play);

            Button daily = UIFactory.CreateTile(
                Content, "Daily", Strings.Menu.DailyTitle, string.Empty, new Vector2(TileWidth, 160f), style,
                () => _flow.PlayDaily(), primary: false, UIIcons.Get("daily"), 52f);
            UIFactory.Place((RectTransform)daily.transform, new Vector2(0.5f, 1f), new Vector2(0f, -690f), new Vector2(TileWidth, 160f));
            _dailyLabel = UIFactory.LabelOf(daily);

            // Today's quests.
            TextMeshProUGUI header = UIFactory.CreateSectionHeader(Content, "QuestsHeader", Strings.Menu.QuestsTitle, style);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -900f), new Vector2(TileWidth, 50f));

            Image card = UIFactory.CreateSurface(Content, "QuestCard", style);
            const float rowHeight = 120f;
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -960f), new Vector2(TileWidth, rowHeight * QuestBoard.QuestsPerDay));
            for (int i = 0; i < QuestBoard.QuestsPerDay; i++)
            {
                TextMeshProUGUI text = UIFactory.CreateText(card.rectTransform, "Quest" + i, string.Empty, 40f, TextAlignmentOptions.MidlineLeft, style);
                UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -i * rowHeight), new Vector2(560f, rowHeight));
                TextMeshProUGUI value = UIFactory.CreateText(card.rectTransform, "QuestValue" + i, string.Empty, 40f, TextAlignmentOptions.MidlineRight, style, accent: true, bold: true);
                UIFactory.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -i * rowHeight), new Vector2(240f, rowHeight));
                _questText[i] = text;
                _questValue[i] = value;
            }

            // The way to the other screens.
            const float navWidth = 270f;
            AddNav("Themes", Strings.Menu.Themes, "themes", -navWidth - 20f, () => _flow.ShowThemes(), style);
            AddNav("Stats", Strings.Menu.Stats, "stats", 0f, () => _flow.ShowStats(), style);
            AddNav("Settings", Strings.Menu.Settings, "settings", navWidth + 20f, () => _flow.ShowSettings(), style);

            Button how = UIFactory.CreateButton(Content, "HowToPlay", Strings.Tutorial.HowToPlay, new Vector2(420f, 96f), style, () => _flow.OpenHowToPlay(), fontSize: 40f);
            UIFactory.Place((RectTransform)how.transform, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(420f, 96f));
        }

        private void AddNav(string name, string label, string icon, float x, UnityEngine.Events.UnityAction onClick, UIStyle style)
        {
            Button nav = UIFactory.CreateNavItem(Content, name, label, UIIcons.Get(icon), new Vector2(270f, 160f), style, onClick);
            UIFactory.Place((RectTransform)nav.transform, new Vector2(0.5f, 0f), new Vector2(x, 40f), new Vector2(270f, 160f));
        }

        protected override void OnShow() => Refresh();

        public void Refresh()
        {
            _coins.text = _flow.Economy.Coins.ToString();
            _playLabel.text = UIFactory.TileText(Strings.Menu.Play, Strings.Menu.PlayLevel(_flow.Progression.CurrentLevel));

            string dailyLine = _flow.Daily.CompletedToday ? Strings.Menu.DailyDone : Strings.Menu.DailyReady;
            int streak = _flow.Daily.Streak;
            if (streak > 0)
            {
                dailyLine = (_flow.Daily.CompletedToday ? Strings.Menu.DailyDone : Strings.Menu.DailyReady) + "  ·  " + Strings.Menu.Streak(streak);
            }
            _dailyLabel.text = UIFactory.TileText(Strings.Menu.DailyTitle, dailyLine);

            QuestDef[] quests = _flow.Quests.Today;
            for (int i = 0; i < _questText.Length; i++)
            {
                bool done = _flow.Quests.IsDone(i);
                _questText[i].text = quests[i].Describe();
                _questValue[i].text = done ? "Done" : Strings.Menu.QuestProgress(_flow.Quests.Progress(i), quests[i].Target);
            }
        }

        public override void OnBack() => _flow.OnMenuBack();
    }
}
