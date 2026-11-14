using System;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Meta
{
    // The player's own line colours and the thicker-crossings cue, layered over whichever theme is
    // active. Kept in the save; ThemeController applies it so every screen and the board see one
    // effective palette, and Changed tells them to redraw.
    public sealed class LineStyleService
    {
        private readonly SaveService _save;

        public LineStyleService(SaveService save)
        {
            _save = save;
        }

        public event Action Changed;

        public int ClearColor => _save.Data.LineColorClear;
        public int CrossingColor => _save.Data.LineColorCrossing;
        public bool ThickCrossings => _save.Data.ThickCrossings;

        public bool HasOverrides => LineColors.IsSet(ClearColor) || LineColors.IsSet(CrossingColor);

        public void SetClear(int packed)
        {
            _save.Data.LineColorClear = packed;
            Commit();
        }

        public void SetCrossing(int packed)
        {
            _save.Data.LineColorCrossing = packed;
            Commit();
        }

        public void SetThickCrossings(bool thick)
        {
            _save.Data.ThickCrossings = thick;
            Commit();
        }

        public void Reset()
        {
            _save.Data.LineColorClear = LineColors.Unset;
            _save.Data.LineColorCrossing = LineColors.Unset;
            _save.Data.ThickCrossings = false;
            Commit();
        }

        // A colour the player did not pick stays the theme's.
        public ThemePalette Apply(ThemePalette palette)
        {
            if (!HasOverrides)
            {
                return palette;
            }
            return palette.WithEdgeColors(
                LineColors.IsSet(ClearColor) ? ToColor(ClearColor) : palette.EdgeClear,
                LineColors.IsSet(CrossingColor) ? ToColor(CrossingColor) : palette.EdgeCrossing);
        }

        public static Color ToColor(int packed)
        {
            LineColors.Unpack(packed, out int r, out int g, out int b);
            return new Color32((byte)r, (byte)g, (byte)b, 255);
        }

        public static int ToPacked(Color color)
        {
            Color32 c = color;
            return LineColors.Pack(c.r, c.g, c.b);
        }

        private void Commit()
        {
            _save.Save();
            Changed?.Invoke();
        }
    }
}
