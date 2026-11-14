using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GravityWells.UI
{
    // Shown over the board while the game is paused. Back resumes.
    public sealed class PausePanel : UIScreen
    {
        private readonly Action _onResume;

        public PausePanel(UIRoot ui, UIStyle style, Action onResume, Action onRestart)
        {
            _onResume = onResume;
            BuildRoot(ui.CanvasTransform, "Pause", ui, style, opaque: false);

            Image dim = UIFactory.CreateFlat(Root.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);
            dim.transform.SetSiblingIndex(0);

            Image card = UIFactory.CreateCard(Content, "Card", style);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 620f));

            TextMeshProUGUI title = UIFactory.CreateText(card.rectTransform, "Title", "Paused", 80f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 110f));

            Button resume = UIFactory.CreateButton(card.rectTransform, "Resume", "Resume", new Vector2(660f, 128f), style, () => onResume(), primary: true, fontSize: 56f, icon: UIIcons.Get("play"));
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(660f, 128f));
            Button restart = UIFactory.CreateButton(card.rectTransform, "Restart", "Restart level", new Vector2(660f, 112f), style, () => onRestart(), fontSize: 48f, icon: UIIcons.Get("restart"));
            UIFactory.Place((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(660f, 112f));
        }

        public override void OnBack() => _onResume();
    }
}
