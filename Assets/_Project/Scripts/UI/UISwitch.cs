using UnityEngine;
using UnityEngine.UI;
using GravityWells.Meta;

namespace GravityWells.UI
{
    // An on/off switch: a pill track with a knob that sits left (off) or right (on). It only
    // shows state; the row it sits in is the thing that is tapped.
    public sealed class UISwitch
    {
        public static readonly Vector2 Size = new Vector2(124f, 68f);
        private const float KnobInset = 7f;

        private readonly Image _track;
        private readonly Image _knob;
        private readonly UIStyle _style;
        private bool _on;

        public UISwitch(RectTransform parent, UIStyle style)
        {
            _style = style;

            _track = UIFactory.CreateRoundedFlat(parent, "Switch", Color.white);
            _track.raycastTarget = false;
            UIFactory.Place(_track.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, Size);

            float knobSize = Size.y - 2f * KnobInset;
            RectTransform knobRect = UIFactory.CreateRect(_track.rectTransform, "Knob");
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(0f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(knobSize, knobSize);
            _knob = knobRect.gameObject.AddComponent<Image>();
            _knob.sprite = UIFactory.DiscSprite;
            _knob.raycastTarget = false;

            style.RegisterCustom(Paint);
        }

        public void Set(bool on)
        {
            _on = on;
            Paint(_style.Palette);
        }

        private void Paint(ThemePalette palette)
        {
            // Off is the page ink at low strength rather than a grey, so it belongs to every theme.
            Color off = Color.Lerp(palette.Surface, palette.Text, 0.28f);
            _track.color = _on ? palette.Accent : off;
            _knob.color = palette.SurfaceElevated;

            float half = Size.y * 0.5f;
            _knob.rectTransform.anchoredPosition = new Vector2(_on ? Size.x - half : half, 0f);
        }
    }
}
