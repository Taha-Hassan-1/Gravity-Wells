using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Core;
using GravityWells.Game;
using GravityWells.Meta;

namespace GravityWells.UI
{
    // The nine palettes as a list: four swatches of the palette's own colours, its name, and either "In use",
    // a tick-less way to pick it, or the level it unlocks at. Tapping an unlocked one switches to it at once
    // (the whole game cross-fades); tapping a locked one says when it opens. The state is also in words, never
    // in colour alone.
    public sealed class ThemesScreen : UIScreen
    {
        private const float RowHeight = 136f;
        private const float RowStep = 148f;

        private readonly AppFlow _flow;
        private readonly UIStyle _style;
        private readonly UIRoot _ui;
        private readonly TextMeshProUGUI[] _status;
        private readonly Image[] _lock;

        public ThemesScreen(AppFlow flow, UIRoot ui, UIStyle style)
        {
            _flow = flow;
            _ui = ui;
            _style = style;
            BuildRoot(ui.CanvasTransform, "Themes", ui, style, opaque: true);
            AddHeader(Strings.Themes.Title, style, () => _flow.ShowMainMenu());

            int count = ThemeService.Palettes.Length;
            _status = new TextMeshProUGUI[count];
            _lock = new Image[count];

            for (int i = 0; i < count; i++)
            {
                int index = i;
                ThemePalette palette = ThemeService.Palettes[i];
                var size = new Vector2(UITokens.ContentWidth, RowHeight);
                Button row = UIFactory.CreateButton(Content, "Theme" + i, string.Empty, size, style, () => OnPressed(index));
                UIFactory.Place((RectTransform)row.transform, new Vector2(0.5f, 1f), new Vector2(0f, -330f - RowStep * i), size);

                // Four swatches in the palette's own colours: the page, the lit colour, the clear path, the warning.
                Color[] colours = { palette.Background, palette.Accent, palette.EdgeClear, palette.EdgeCrossing };
                for (int s = 0; s < colours.Length; s++)
                {
                    Image swatch = UIFactory.CreateRoundedFlat(row.transform, "Swatch" + s, colours[s]);
                    swatch.raycastTarget = false;
                    UIFactory.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f + s * 62f, 0f), new Vector2(54f, 54f));
                    swatch.rectTransform.pivot = new Vector2(0f, 0.5f);
                }

                TMP_Text label = UIFactory.LabelOf(row);
                label.text = palette.Name;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = 44f;
                label.rectTransform.offsetMin = new Vector2(28f + 4 * 62f + 24f, 0f);
                label.rectTransform.offsetMax = new Vector2(-300f, 0f);

                _status[i] = UIFactory.CreateText(row.transform, "Status", string.Empty, 34f, TextAlignmentOptions.MidlineRight, style, dim: true);
                UIFactory.Place(_status[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(420f, 90f));
                _status[i].rectTransform.pivot = new Vector2(1f, 0.5f);

                _lock[i] = UIFactory.CreateIcon(row.transform, "Lock", UIIcons.Get("lock"), 44f, style);
                _lock[i].raycastTarget = false;
                UIFactory.Place(_lock[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(-30f - 410f, 0f), new Vector2(44f, 44f));
                _lock[i].rectTransform.pivot = new Vector2(1f, 0.5f);
            }
        }

        protected override void OnShow() => Refresh();

        public void Refresh()
        {
            for (int i = 0; i < _status.Length; i++)
            {
                bool unlocked = _flow.IsThemeUnlocked(i);
                bool selected = unlocked && i == _flow.SelectedTheme;
                _status[i].text = selected ? Strings.Themes.Selected
                    : unlocked ? string.Empty
                    : Strings.Themes.RowLocked(ThemeService.Palettes[i].UnlockLevel);
                _lock[i].gameObject.SetActive(!unlocked);
                // The lock sits just left of its caption, so it shrinks away from the text it belongs to.
                _lock[i].rectTransform.anchoredPosition = new Vector2(-30f - _status[i].preferredWidth - 14f, 0f);
            }
        }

        private void OnPressed(int index)
        {
            if (_flow.SelectTheme(index))
            {
                Refresh();
            }
            else
            {
                _ui.ShowToast(Strings.Themes.UnlocksAt(ThemeService.Palettes[index].UnlockLevel));
            }
        }

        public override void OnBack() => _flow.ShowMainMenu();
    }
}
