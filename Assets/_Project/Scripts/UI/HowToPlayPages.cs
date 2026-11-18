using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Core;
using GravityWells.Game;

namespace GravityWells.UI
{
    // Five short pages, one idea each (see Strings.Tutorial). Opened from the menu at any time, and once before
    // a new player's first level. Next goes on, Back goes to the page before (or out on the first page), and the
    // last page's button says "Got it" and leads on: into the first level when it was opened by Play, otherwise
    // back to the menu.
    public sealed class HowToPlayScreen : UIScreen
    {
        private readonly AppFlow _flow;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _body;
        private readonly TextMeshProUGUI _counter;
        private readonly TMP_Text _nextLabel;
        private int _page;
        private bool _thenPlay;

        public int Page => _page;

        public HowToPlayScreen(AppFlow flow, UIRoot ui, UIStyle style)
        {
            _flow = flow;
            BuildRoot(ui.CanvasTransform, "HowToPlay", ui, style, opaque: true);
            AddHeader(Strings.Tutorial.HowToPlay, style, () => OnBack());

            Image card = UIFactory.CreateCard(Content, "Card", style);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(920f, 900f));

            _counter = UIFactory.CreateText(card.rectTransform, "Counter", string.Empty, 38f, TextAlignmentOptions.Center, style, dim: true);
            UIFactory.Place(_counter.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 60f));

            _title = UIFactory.CreateText(card.rectTransform, "Title", string.Empty, 66f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(820f, 200f));

            _body = UIFactory.CreateText(card.rectTransform, "Body", string.Empty, 48f, TextAlignmentOptions.Center, style);
            UIFactory.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(800f, 340f));

            Button next = UIFactory.CreateButton(
                card.rectTransform, "Next", Strings.Tutorial.Next, new Vector2(620f, 120f), style, () => Advance(), primary: true, fontSize: 54f);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(620f, 120f));
            _nextLabel = UIFactory.LabelOf(next);
        }

        // thenPlay: leave into the first level when finished (the first-launch route) instead of the menu.
        public void Open(bool thenPlay)
        {
            _thenPlay = thenPlay;
            _page = 0;
            _flow.ShowHowToPlay();
        }

        protected override void OnShow() => Render();

        private void Render()
        {
            int last = Strings.Tutorial.PageTitles.Length - 1;
            _page = Mathf.Clamp(_page, 0, last);
            _counter.text = (_page + 1) + " / " + (last + 1);
            _title.text = Strings.Tutorial.PageTitles[_page];
            _body.text = Strings.Tutorial.PageBodies[_page];
            _nextLabel.text = _page == last ? Strings.Tutorial.GotIt : Strings.Tutorial.Next;
        }

        private void Advance()
        {
            if (_page < Strings.Tutorial.PageTitles.Length - 1)
            {
                _page++;
                Render();
                return;
            }
            _flow.FinishHowToPlay(_thenPlay);
        }

        public override void OnBack()
        {
            if (_page > 0)
            {
                _page--;
                Render();
                return;
            }
            _flow.FinishHowToPlay(false);
        }
    }
}

