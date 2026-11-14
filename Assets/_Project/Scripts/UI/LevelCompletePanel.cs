using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Core;
using GravityWells.Game;

namespace GravityWells.UI
{
    // The card shown over the finished board: stars, wells against par, launches, what it paid, and where to go
    // next. After a campaign level the main button is "Next level"; after the daily, which is one puzzle, it is
    // "Menu". Back replays, so a stray press can never skip a level.
    public sealed class LevelCompletePanel : UIScreen
    {
        private readonly UIStyle _style;
        private readonly Action _onNext;
        private readonly Action _onReplay;
        private readonly Action _onMenu;
        private readonly Action _onDouble;
        private readonly Button _double;
        private readonly TMP_Text _doubleLabel;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _wells;
        private readonly TextMeshProUGUI _launches;
        private readonly TextMeshProUGUI _badge;
        private readonly TextMeshProUGUI _coins;
        private readonly TextMeshProUGUI _quest;
        private readonly Button _main;
        private readonly TMP_Text _mainLabel;
        private readonly Button _menu;
        private readonly Image[] _stars = new Image[3];
        private bool _mainIsMenu;

        public LevelCompletePanel(UIRoot ui, UIStyle style, Action onNext, Action onReplay, Action onMenu, Action onDouble)
        {
            _style = style;
            _onNext = onNext;
            _onReplay = onReplay;
            _onMenu = onMenu;
            _onDouble = onDouble;
            BuildRoot(ui.CanvasTransform, "LevelComplete", ui, style, opaque: false);

            Image dim = UIFactory.CreateFlat(Root.transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Stretch(dim.rectTransform);
            dim.transform.SetSiblingIndex(0);

            Image card = UIFactory.CreateCard(Content, "Card", style);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 1260f));

            _title = UIFactory.CreateText(card.rectTransform, "Title", string.Empty, 76f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 110f));

            for (int i = 0; i < _stars.Length; i++)
            {
                Image star = UIFactory.CreateIconImage(card.rectTransform, "Star" + i, UIFactory.StarSprite, 190f);
                star.raycastTarget = false;
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 220f, -190f), new Vector2(190f, 190f));
                _stars[i] = star;
            }

            _wells = Line(card.rectTransform, "Wells", -410f, 52f, dim: false, accent: false, bold: false);
            _launches = Line(card.rectTransform, "Launches", -485f, 44f, dim: true, accent: false, bold: false);
            _badge = Line(card.rectTransform, "Badge", -555f, 46f, dim: false, accent: true, bold: true);
            _coins = Line(card.rectTransform, "Coins", -625f, 50f, dim: false, accent: false, bold: true);
            _quest = Line(card.rectTransform, "Quest", -695f, 38f, dim: true, accent: false, bold: false);

            _double = UIFactory.CreateButton(card.rectTransform, "DoubleCoins", Strings.Game.DoubleCoins(0), new Vector2(700f, 104f), style, () => _onDouble(), fontSize: 44f);
            UIFactory.Place((RectTransform)_double.transform, new Vector2(0.5f, 1f), new Vector2(0f, -815f), new Vector2(700f, 104f));
            _doubleLabel = UIFactory.LabelOf(_double);
            _double.gameObject.SetActive(false);

            _main = UIFactory.CreateButton(card.rectTransform, "Main", Strings.Game.NextLevel, new Vector2(700f, 128f), style, OnMain, primary: true, fontSize: 56f);
            UIFactory.Place((RectTransform)_main.transform, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(700f, 128f));
            _mainLabel = UIFactory.LabelOf(_main);

            Button replay = UIFactory.CreateButton(card.rectTransform, "Replay", Strings.Game.Replay, new Vector2(700f, 112f), style, () => _onReplay(), fontSize: 48f, icon: UIIcons.Get("restart"));
            UIFactory.Place((RectTransform)replay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(700f, 112f));

            _menu = UIFactory.CreateButton(card.rectTransform, "Menu", Strings.Game.Menu, new Vector2(700f, 112f), style, () => _onMenu(), fontSize: 48f, icon: UIIcons.Get("home"));
            UIFactory.Place((RectTransform)_menu.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(700f, 112f));
        }

        private TextMeshProUGUI Line(RectTransform parent, string name, float y, float size, bool dim, bool accent, bool bold)
        {
            TextMeshProUGUI text = UIFactory.CreateText(parent, name, string.Empty, size, TextAlignmentOptions.Center, _style, dim: dim, accent: accent, bold: bold);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(820f, 70f));
            return text;
        }

        private void OnMain()
        {
            if (_mainIsMenu)
            {
                _onMenu();
            }
            else
            {
                _onNext();
            }
        }

        public void ShowResult(LevelResult result)
        {
            _title.text = result.IsDaily ? Strings.Game.DailyDone : Strings.Game.Level(result.Level);

            Color lit = _style.Palette.Accent;
            Color unlit = new Color(_style.Palette.Text.r, _style.Palette.Text.g, _style.Palette.Text.b, 0.18f);
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = i < result.Stars ? lit : unlit;
            }

            _wells.text = Strings.Game.WellsAgainstPar(result.WellsUsed, result.Par);
            _launches.text = Strings.Game.Launches(result.Launches);
            _badge.text = result.UnderPar ? Strings.Game.UnderParBadge
                : result.UsedExtraWell ? Strings.Game.ExtraWellBadge
                : result.IsDaily && result.Streak > 0 ? Strings.Game.Streak(result.Streak)
                : result.NewBest ? Strings.Game.NewBestBadge
                : string.Empty;
            _coins.text = result.Coins > 0 ? Strings.Game.Coins(result.Coins) : string.Empty;
            _quest.text = result.QuestText;

            // Offered whenever the win paid something; one tap on a rewarded ad pays the same again.
            _double.gameObject.SetActive(result.Coins > 0);
            _doubleLabel.text = Strings.Game.DoubleCoins(result.Coins);

            // The daily is one puzzle: its main button leads home, and the separate Menu button is not needed.
            _mainIsMenu = !result.CanContinue;
            _mainLabel.text = _mainIsMenu ? Strings.Game.Menu : Strings.Game.NextLevel;
            _menu.gameObject.SetActive(!_mainIsMenu);
        }

        public void ShowDoubled(int totalCoins)
        {
            _coins.text = Strings.Game.Coins(totalCoins);
            _double.gameObject.SetActive(false);
        }

        public bool DoubleVisible => _double.gameObject.activeSelf;

        // Back never advances: it replays, so a stray press cannot skip a level.
        public override void OnBack() => _onReplay();
    }
}
