using System;
using UnityEngine;
using GravityWells.Core;
using GravityWells.Meta;

namespace GravityWells.Game
{
    // Owns the active palette and the 0.3s cross-fade between palettes. Anything that
    // draws in theme colors (the board, the camera background, the UI) subscribes to
    // Changed, which fires once per frame during a fade and once when it lands.
    public sealed class ThemeController : MonoBehaviour
    {
        private ThemePalette _from;
        private ThemePalette _to;
        private ThemePalette _base; // the theme's own colors; Current is this with the player's line colors on top
        private LineStyleService _lineStyle;
        private float _elapsed;
        private bool _fading;

        public ThemePalette Current { get; private set; }
        public int CurrentIndex { get; private set; }
        public LineStyleService LineStyle => _lineStyle;

        public event Action<ThemePalette> Changed;

        public void Init(int themeIndex, LineStyleService lineStyle)
        {
            _lineStyle = lineStyle;
            CurrentIndex = themeIndex;
            _base = ThemeService.Palettes[themeIndex];
            _to = _base;
            Current = _lineStyle.Apply(_base);
            _lineStyle.Changed += OnLineStyleChanged;
        }

        private void OnDestroy()
        {
            if (_lineStyle != null)
            {
                _lineStyle.Changed -= OnLineStyleChanged;
            }
        }

        private void OnLineStyleChanged()
        {
            Current = _lineStyle.Apply(_base);
            Changed?.Invoke(Current);
        }

        // Re-broadcasts the current palette, for subscribers that attached after Init.
        public void Broadcast() => Changed?.Invoke(Current);

        public void SetTheme(int themeIndex, bool instant = false)
        {
            if (themeIndex == CurrentIndex && !_fading)
            {
                return;
            }

            CurrentIndex = themeIndex;
            _from = _base;
            _to = ThemeService.Palettes[themeIndex];
            _elapsed = 0f;

            if (instant)
            {
                _fading = false;
                _base = _to;
                Current = _lineStyle.Apply(_base);
                Changed?.Invoke(Current);
                return;
            }

            _fading = true;
        }

        private void Update()
        {
            if (!_fading)
            {
                return;
            }

            // Unscaled: themes are changed from menus while Time.timeScale may be 0.
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / GameConfig.ThemeCrossfadeDuration);
            _base = ThemePalette.Lerp(_from, _to, t);
            Current = _lineStyle.Apply(_base);
            Changed?.Invoke(Current);

            if (t >= 1f)
            {
                _fading = false;
            }
        }
    }
}
