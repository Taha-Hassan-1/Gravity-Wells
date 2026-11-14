using System;

namespace GravityWells.Core
{
    public enum QuestKind
    {
        SolveLevels,         // solve N levels
        EarnThreeStars,      // N solves at par or better
        SolveWithoutUndo,    // N solves where Undo was never used
        SolveWithoutPowerUp, // N solves with no power-up or Assist
        MakeMoves,           // N moves across solved levels
        UsePowerUp,          // use any power-up or Assist N times
        PlayDaily,           // finish the daily challenge
    }

    public readonly struct QuestDef
    {
        public readonly QuestKind Kind;
        public readonly int Target;
        public readonly int Reward;

        public QuestDef(QuestKind kind, int target, int reward)
        {
            Kind = kind;
            Target = target;
            Reward = reward;
        }

        public string Describe()
        {
            switch (Kind)
            {
                case QuestKind.SolveLevels: return $"Solve {Target} levels";
                case QuestKind.EarnThreeStars: return Target == 1 ? "Earn 3 stars on a level" : $"Earn 3 stars on {Target} levels";
                case QuestKind.SolveWithoutUndo: return $"Solve {Target} levels without Undo";
                case QuestKind.SolveWithoutPowerUp: return $"Solve {Target} levels without power-ups";
                case QuestKind.MakeMoves: return $"Make {Target} moves";
                case QuestKind.UsePowerUp: return Target == 1 ? "Use a power-up" : $"Use {Target} power-ups";
                default: return "Finish the daily challenge";
            }
        }
    }

    // What the player just did, reduced to the facts quests care about.
    public readonly struct LevelOutcome
    {
        public readonly int Stars;
        public readonly int Moves;
        public readonly bool IsDaily;
        public readonly bool UsedUndo;
        public readonly bool UsedPowerUp;

        public LevelOutcome(int stars, int moves, bool isDaily, bool usedUndo, bool usedPowerUp)
        {
            Stars = stars;
            Moves = moves;
            IsDaily = isDaily;
            UsedUndo = usedUndo;
            UsedPowerUp = usedPowerUp;
        }
    }

    // Today's quests are a pure function of the date, so every install sees the same set
    // for a given day and nothing needs storing beyond progress.
    public static class QuestBoard
    {
        public const int QuestsPerDay = 3;
        public const int AllDoneBonus = 50;

        private static readonly QuestKind[] Pool =
        {
            QuestKind.SolveLevels, QuestKind.EarnThreeStars, QuestKind.SolveWithoutUndo,
            QuestKind.SolveWithoutPowerUp, QuestKind.MakeMoves, QuestKind.UsePowerUp, QuestKind.PlayDaily,
        };

        public static QuestDef[] ForDay(int dayNumber)
        {
            var random = new Random(unchecked(dayNumber * 7919 + 13));
            var chosen = new QuestDef[QuestsPerDay];
            var used = new bool[Pool.Length];

            for (int i = 0; i < QuestsPerDay; i++)
            {
                int pick;
                do
                {
                    pick = random.Next(Pool.Length);
                }
                while (used[pick] || ConflictsWithChosen(Pool[pick], chosen, i));

                used[pick] = true;
                chosen[i] = Build(Pool[pick], random);
            }
            return chosen;
        }

        // "Use a power-up" and "solve without power-ups" would ask for opposite things.
        private static bool ConflictsWithChosen(QuestKind kind, QuestDef[] chosen, int count)
        {
            for (int i = 0; i < count; i++)
            {
                QuestKind other = chosen[i].Kind;
                bool opposed = (kind == QuestKind.UsePowerUp && other == QuestKind.SolveWithoutPowerUp)
                    || (kind == QuestKind.SolveWithoutPowerUp && other == QuestKind.UsePowerUp);
                if (opposed)
                {
                    return true;
                }
            }
            return false;
        }

        private static QuestDef Build(QuestKind kind, Random random)
        {
            switch (kind)
            {
                case QuestKind.SolveLevels: return new QuestDef(kind, random.Next(3, 6), 30);
                case QuestKind.EarnThreeStars: return new QuestDef(kind, random.Next(1, 3), 40);
                case QuestKind.SolveWithoutUndo: return new QuestDef(kind, random.Next(2, 4), 30);
                case QuestKind.SolveWithoutPowerUp: return new QuestDef(kind, random.Next(2, 4), 30);
                case QuestKind.MakeMoves: return new QuestDef(kind, random.Next(5, 9) * 5, 25);
                case QuestKind.UsePowerUp: return new QuestDef(kind, random.Next(1, 3), 25);
                default: return new QuestDef(kind, 1, 35);
            }
        }

        // How much a finished level moves this quest.
        public static int ProgressFor(QuestKind kind, LevelOutcome outcome)
        {
            switch (kind)
            {
                case QuestKind.SolveLevels: return 1;
                case QuestKind.EarnThreeStars: return outcome.Stars >= 3 ? 1 : 0;
                case QuestKind.SolveWithoutUndo: return outcome.UsedUndo ? 0 : 1;
                case QuestKind.SolveWithoutPowerUp: return outcome.UsedPowerUp ? 0 : 1;
                case QuestKind.MakeMoves: return outcome.Moves;
                case QuestKind.PlayDaily: return outcome.IsDaily ? 1 : 0;
                default: return 0; // UsePowerUp advances when one is used, not when a level ends
            }
        }

        public static int DayNumber(DateTime utcNow)
        {
            return (int)(utcNow.Date - new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays + 1;
        }
    }
}
