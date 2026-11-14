using TMPro;
using UnityEngine;
using UnityEngine.Events;
using GravityWells.Core;

namespace GravityWells.UI
{
    public abstract class UIScreen
    {
        public GameObject Root { get; private set; }

        // Everything a screen draws goes here, inside the device safe area (notches,
        // rounded corners, gesture bars); the opaque background behind it does not.
        protected RectTransform Content { get; private set; }

        protected void BuildRoot(Transform parent, string name, UIRoot ui, UIStyle style, bool opaque)
        {
            RectTransform root = UIFactory.CreateRect(parent, name);
            UIFactory.Stretch(root);
            Root = root.gameObject;

            if (opaque)
            {
                UIFactory.CreateBackground(root, "Background", style);
            }

            Content = UIFactory.CreateRect(root, "Content");
            UIFactory.Stretch(Content);
            ui.RegisterSafeArea(Content);
        }

        // Back button top-left plus a centered title: the shared header of the
        // Settings / Stats / Themes sub-screens.
        protected void AddHeader(string title, UIStyle style, UnityAction onBack)
        {
            // A round glyph, like every other piece of navigation chrome; the word "Back" added nothing.
            const float backSize = 92f;
            var back = UIFactory.CreateRoundButton(Content, "BackButton", UIIcons.Get("back"), backSize, style, onBack);
            UIFactory.Place(
                (RectTransform)back.transform, new Vector2(0f, 1f),
                new Vector2(UITokens.SpaceL, -UITokens.SpaceL), new Vector2(backSize, backSize));

            TextMeshProUGUI heading = UIFactory.CreateText(
                Content, "Title", title, 90f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(980f, 120f));
        }

        public void Show()
        {
            Root.SetActive(true);
            OnShow();
        }

        public void Hide() => Root.SetActive(false);

        protected virtual void OnShow() { }

        // Android back button / Escape. Every screen answers it, so it never dead-ends.
        public abstract void OnBack();
    }
}
