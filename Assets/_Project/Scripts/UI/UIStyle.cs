using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Meta;

namespace GravityWells.UI
{
    // Tracks every themed UI element by role so a palette change (including each frame
    // of the 0.3s cross-fade) recolors the whole UI in one pass. Screens build their
    // widgets once and update them in place, so entries never go stale.
    public sealed class UIStyle
    {
        private struct ButtonEntry
        {
            public Image Fill;
            public TMP_Text Label;
            public bool Primary;
            public Image Icon;
        }

        private readonly List<Image> _backgrounds = new List<Image>();
        private readonly List<Image> _surfaces = new List<Image>();
        private readonly List<Image> _cards = new List<Image>();
        private readonly List<TMP_Text> _texts = new List<TMP_Text>();
        private readonly List<TMP_Text> _dimTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> _accentTexts = new List<TMP_Text>();
        private readonly List<ButtonEntry> _buttons = new List<ButtonEntry>();
        private readonly List<Image> _icons = new List<Image>();
        private readonly List<Image> _accentIcons = new List<Image>();

        public ThemePalette Palette { get; private set; }

        // Set by UIRoot; PressFeedback calls it so every button taps with the same sound.
        public System.Action ButtonPressed { get; set; }

        public UIStyle(ThemePalette palette)
        {
            Palette = palette;
        }

        public Color Surface => SurfaceColor(Palette);

        public void RegisterBackground(Image image)
        {
            _backgrounds.Add(image);
            image.color = Palette.Background;
        }

        public void RegisterCard(Image image)
        {
            _cards.Add(image);
            image.color = CardColor(Palette);
        }

        public void RegisterSurface(Image image)
        {
            _surfaces.Add(image);
            image.color = SurfaceColor(Palette);
        }

        public void RegisterText(TMP_Text text, bool dim = false, bool accent = false)
        {
            if (accent)
            {
                _accentTexts.Add(text);
            }
            else if (dim)
            {
                _dimTexts.Add(text);
            }
            else
            {
                _texts.Add(text);
            }
            text.color = TextColor(text, dim, accent);
        }

        public void RegisterIcon(Image icon, bool accent = false)
        {
            (accent ? _accentIcons : _icons).Add(icon);
            icon.color = accent ? Palette.Accent : Palette.Text;
        }

        public void RegisterButton(Image fill, TMP_Text label, bool primary, Image icon = null)
        {
            _buttons.Add(new ButtonEntry { Fill = fill, Label = label, Primary = primary, Icon = icon });
            ApplyButton(_buttons[_buttons.Count - 1]);
        }

        public void Apply(ThemePalette palette)
        {
            Palette = palette;
            foreach (Image image in _backgrounds)
            {
                image.color = palette.Background;
            }
            foreach (Image image in _surfaces)
            {
                image.color = SurfaceColor(palette);
            }
            foreach (Image image in _cards)
            {
                image.color = CardColor(palette);
            }
            foreach (TMP_Text text in _texts)
            {
                text.color = palette.Text;
            }
            foreach (TMP_Text text in _dimTexts)
            {
                text.color = Dim(palette.Text);
            }
            foreach (TMP_Text text in _accentTexts)
            {
                text.color = palette.Accent;
            }
            foreach (ButtonEntry entry in _buttons)
            {
                ApplyButton(entry);
            }
            foreach (Image icon in _icons)
            {
                icon.color = palette.Text;
            }
            foreach (Image icon in _accentIcons)
            {
                icon.color = palette.Accent;
            }
        }

        private void ApplyButton(ButtonEntry entry)
        {
            Color fill = entry.Primary ? Palette.Accent : SurfaceColor(Palette);
            entry.Fill.color = fill;
            entry.Label.color = ReadableOn(fill, Palette);
            if (entry.Icon != null)
            {
                entry.Icon.color = entry.Label.color;
            }
        }

        private Color TextColor(TMP_Text text, bool dim, bool accent)
        {
            if (accent)
            {
                return Palette.Accent;
            }
            return dim ? Dim(Palette.Text) : Palette.Text;
        }

        // ---- pure, testable ----

        // A subtle raised panel: the background nudged toward the text color.
        public static Color SurfaceColor(ThemePalette palette)
        {
            return Color.Lerp(palette.Background, palette.Text, 0.12f);
        }

        public static Color CardColor(ThemePalette palette)
        {
            return Color.Lerp(palette.Background, palette.Text, 0.06f);
        }

        public static Color Dim(Color color)
        {
            return new Color(color.r, color.g, color.b, 0.62f);
        }

        // Picks whichever of the palette's text/background colors contrasts more with
        // the fill, so a pastel accent never ends up with pale-on-pale labels.
        public static Color ReadableOn(Color fill, ThemePalette palette)
        {
            float fillLum = Luminance(fill);
            float textContrast = Mathf.Abs(Luminance(palette.Text) - fillLum);
            float backgroundContrast = Mathf.Abs(Luminance(palette.Background) - fillLum);
            return textContrast >= backgroundContrast ? palette.Text : palette.Background;
        }

        public static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }
    }
}
