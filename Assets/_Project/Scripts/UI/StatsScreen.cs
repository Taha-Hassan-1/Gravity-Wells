using TMPro;
using UnityEngine;
using GravityWells.Core;
using GravityWells.Game;
using GravityWells.Meta;

namespace GravityWells.UI
{
    public sealed class StatsScreen : UIScreen
    {
        private readonly AppFlow _flow;
        private readonly TextMeshProUGUI _levelsSolved;
        private readonly TextMeshProUGUI _stars;
        private readonly TextMeshProUGUI _underPar;
        private readonly TextMeshProUGUI _launches;
        private readonly TextMeshProUGUI _wells;
        private readonly TextMeshProUGUI _bestStreak;
        private readonly TextMeshProUGUI _playTime;

        public StatsScreen(AppFlow flow, UIRoot ui, UIStyle style)
        {
            _flow = flow;
            BuildRoot(ui.CanvasTransform, "Stats", ui, style, opaque: true);
            AddHeader(Strings.Stats.Title, style, () => _flow.ShowMainMenu());

            _levelsSolved = AddRow(Strings.Stats.LevelsSolved, 0, style);
            _stars = AddRow(Strings.Stats.StarsEarned, 1, style);
            _underPar = AddRow(Strings.Stats.UnderPar, 2, style);
            _launches = AddRow(Strings.Stats.Launches, 3, style);
            _wells = AddRow(Strings.Stats.WellsPlaced, 4, style);
            _bestStreak = AddRow(Strings.Stats.BestStreak, 5, style);
            _playTime = AddRow(Strings.Stats.TimePlayed, 6, style);
        }

        private TextMeshProUGUI AddRow(string label, int index, UIStyle style)
        {
            const float firstY = -340f;
            const float step = 140f;

            var rowSize = new Vector2(900f, 116f);
            var row = UIFactory.CreateSurface(Content, "Row" + index, style);
            UIFactory.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, firstY - step * index), rowSize);

            TextMeshProUGUI left = UIFactory.CreateText(
                row.rectTransform, "Label", label, 42f, TextAlignmentOptions.Left, style);
            UIFactory.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(560f, 100f));
            left.rectTransform.pivot = new Vector2(0f, 0.5f);

            TextMeshProUGUI value = UIFactory.CreateText(
                row.rectTransform, "Value", "0", 54f, TextAlignmentOptions.Right, style, accent: true, bold: true);
            UIFactory.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(280f, 100f));
            value.rectTransform.pivot = new Vector2(1f, 0.5f);
            return value;
        }

        protected override void OnShow()
        {
            SaveData data = _flow.Save.Data;
            int solved = 0;
            foreach (byte stars in data.StarsByLevel)
            {
                solved += stars > 0 ? 1 : 0;
            }

            _levelsSolved.text = solved.ToString();
            _stars.text = _flow.Progression.TotalStars.ToString();
            _underPar.text = data.UnderParCount.ToString();
            _launches.text = data.TotalLaunches.ToString();
            _wells.text = data.TotalWellsPlaced.ToString();
            _bestStreak.text = data.BestDailyStreak.ToString();
            _playTime.text = FormatDuration(data.TotalPlaySeconds);
        }

        // "42s", "7m 05s", "2h 03m": whichever unit is the largest meaningful one.
        public static string FormatDuration(float totalSeconds)
        {
            int seconds = Mathf.Max(0, Mathf.FloorToInt(totalSeconds));
            if (seconds < 60)
            {
                return Strings.Stats.DurationSeconds(seconds);
            }
            if (seconds < 3600)
            {
                return Strings.Stats.DurationMinutes(seconds / 60, seconds % 60);
            }
            return Strings.Stats.DurationHours(seconds / 3600, seconds % 3600 / 60);
        }

        public override void OnBack() => _flow.ShowMainMenu();
    }
}
