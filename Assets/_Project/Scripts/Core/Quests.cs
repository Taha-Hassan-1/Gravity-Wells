using System;

namespace GravityWells.Core
{
    public enum QuestKind
    {
        SolveLevels,      // solve N levels
        EarnStars,        // earn N stars in all
        SolveUnderPar,    // solve N levels with fewer wells than par
        SolveFirstTry,    // solve N levels on the first launch
        SolveWithoutHint, // solve N levels without taking a hint
        PlayDaily,        // finish the daily challenge
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
                case QuestKind.EarnStars: return $"Earn {Target} stars";
                case QuestKind.SolveUnderPar: return Target == 1 ? "Solve a level under par" : $"Solve {Target} levels under par";
                case QuestKind.SolveFirstTry: return Target == 1 ? "Solve a level on the first launch" : $"Solve {Target} levels on the first launch";
                case QuestKind.SolveWithoutHint: return $"Solve {Target} levels without a hint";
                default: return "Finish the daily challenge";
            }
        }
    }

    // What the player just did, reduced to the facts quests care about.
    public readonly struct LevelOutcome
    {
        public readonly int Stars;
        public readonly bool UnderPar;
        public readonly int Launches;
        public readonly bool IsDaily;
        public readonly bool UsedHint;

        public LevelOutcome(int stars, bool underPar, int launches, bool isDaily, bool usedHint)
        {
            Stars = stars;
            UnderPar = underPar;
            Launches = launches;
            IsDaily = isDaily;
            UsedHint = usedHint;
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
            QuestKind.SolveLevels, QuestKind.EarnStars, QuestKind.SolveUnderPar,
            QuestKind.SolveFirstTry, QuestKind.SolveWithoutHint, QuestKind.PlayDaily,
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
                while (used[pick]);

                used[pick] = true;
                chosen[i] = Build(Pool[pick], random);
            }
            return chosen;
        }

        private static QuestDef Build(QuestKind kind, Random random)
        {
            switch (kind)
            {
                case QuestKind.SolveLevels: return new QuestDef(kind, random.Next(3, 6), 30);
                case QuestKind.EarnStars: return new QuestDef(kind, random.Next(4, 8), 35);
                case QuestKind.SolveUnderPar: return new QuestDef(kind, random.Next(1, 3), 40);
                case QuestKind.SolveFirstTry: return new QuestDef(kind, random.Next(2, 4), 30);
                case QuestKind.SolveWithoutHint: return new QuestDef(kind, random.Next(2, 5), 25);
                default: return new QuestDef(kind, 1, 35);
            }
        }

        // How much a finished level moves this quest.
        public static int ProgressFor(QuestKind kind, LevelOutcome outcome)
        {
            switch (kind)
            {
                case QuestKind.SolveLevels: return 1;
                case QuestKind.EarnStars: return outcome.Stars;
                case QuestKind.SolveUnderPar: return outcome.UnderPar ? 1 : 0;
                case QuestKind.SolveFirstTry: return outcome.Launches == 1 ? 1 : 0;
                case QuestKind.SolveWithoutHint: return outcome.UsedHint ? 0 : 1;
                default: return outcome.IsDaily ? 1 : 0;
            }
        }

        public static int DayNumber(DateTime utcNow)
        {
            return (int)(utcNow.Date - new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays + 1;
        }
    }
}
