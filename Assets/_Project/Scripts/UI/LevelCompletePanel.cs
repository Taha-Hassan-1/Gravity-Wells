using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Game;

namespace GravityWells.UI
{
    // The card shown over the finished board: stars, wells against par, launches, and Next / Replay. A
    // plain version; Phase 4 gives it the count-up, coins and the share of progress.
    public sealed class LevelCompletePanel : UIScreen
    {
        private readonly UIStyle _style;
        private readonly Action _onNext;
        private readonly Action _onReplay;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _wells;
        private readonly TextMeshProUGUI _launches;
        private readonly TextMeshProUGUI _badge;
        private readonly Image[] _stars = new Image[3];

        public LevelCompletePanel(UIRoot ui, UIStyle style, Action onNext, Action onReplay)
        {
            _style = style;
            _onNext = onNext;
            _onReplay = onReplay;
            BuildRoot(ui.CanvasTransform, "LevelComplete", ui, style, opaque: false);

            Image dim = UIFactory.CreateFlat(Root.transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Stretch(dim.rectTransform);
            dim.transform.SetSiblingIndex(0);

            Image card = UIFactory.CreateCard(Content, "Card", style);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 980f));

            _title = UIFactory.CreateText(card.rectTransform, "Title", string.Empty, 76f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 110f));

            for (int i = 0; i < _stars.Length; i++)
            {
                Image star = UIFactory.CreateIconImage(card.rectTransform, "Star" + i, UIFactory.StarSprite, 190f);
                star.raycastTarget = false;
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 220f, -190f), new Vector2(190f, 190f));
                _stars[i] = star;
            }

            _wells = UIFactory.CreateText(card.rectTransform, "Wells", string.Empty, 52f, TextAlignmentOptions.Center, style);
            UIFactory.Place(_wells.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(820f, 80f));
            _launches = UIFactory.CreateText(card.rectTransform, "Launches", string.Empty, 44f, TextAlignmentOptions.Center, style, dim: true);
            UIFactory.Place(_launches.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -490f), new Vector2(820f, 70f));
            _badge = UIFactory.CreateText(card.rectTransform, "Badge", string.Empty, 46f, TextAlignmentOptions.Center, style, accent: true, bold: true);
            UIFactory.Place(_badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -565f), new Vector2(820f, 70f));

            Button next = UIFactory.CreateButton(card.rectTransform, "Next", "Next level", new Vector2(700f, 128f), style, () => _onNext(), primary: true, fontSize: 56f);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(700f, 128f));
            Button replay = UIFactory.CreateButton(card.rectTransform, "Replay", "Replay", new Vector2(700f, 112f), style, () => _onReplay(), fontSize: 48f, icon: UIIcons.Get("restart"));
            UIFactory.Place((RectTransform)replay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(700f, 112f));
        }

        public void ShowResult(LevelResult result)
        {
            _title.text = "Level " + result.Level;
            Color lit = _style.Palette.Accent;
            Color unlit = new Color(_style.Palette.Text.r, _style.Palette.Text.g, _style.Palette.Text.b, 0.18f);
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = i < result.Stars ? lit : unlit;
            }

            _wells.text = "Wells " + result.WellsUsed + "  (par " + result.Par + ")";
            _launches.text = result.Launches == 1 ? "First launch" : result.Launches + " launches";
            _badge.text = result.UnderPar ? "Under par!" : result.UsedExtraWell ? "Extra well used" : result.NewBest ? "New best" : string.Empty;
        }

        // Back never advances: it replays, so a stray press cannot skip a level.
        public override void OnBack() => _onReplay();
    }
}
