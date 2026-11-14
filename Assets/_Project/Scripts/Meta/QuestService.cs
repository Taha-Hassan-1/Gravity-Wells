using System;
using GravityWells.Core;

namespace GravityWells.Meta
{
    public readonly struct QuestUpdate
    {
        public readonly int CoinsAwarded;
        public readonly int QuestsCompleted;
        public readonly bool BonusAwarded;

        public QuestUpdate(int coinsAwarded, int questsCompleted, bool bonusAwarded)
        {
            CoinsAwarded = coinsAwarded;
            QuestsCompleted = questsCompleted;
            BonusAwarded = bonusAwarded;
        }

        public bool Any => QuestsCompleted > 0;

        public string Summary()
        {
            if (!Any)
            {
                return string.Empty;
            }
            string what = BonusAwarded ? "All daily quests done!"
                : QuestsCompleted == 1 ? "Quest complete!"
                : $"{QuestsCompleted} quests complete!";
            return $"{what}  +{CoinsAwarded} coins";
        }
    }

    // Today's quests and the player's progress on them. Rewards are paid the moment a quest
    // completes (no claim button), and everything resets when the UTC day changes.
    public sealed class QuestService
    {
        private readonly SaveService _save;
        private readonly EconomyService _economy;
        private readonly Func<DateTime> _utcNow;

        public QuestService(SaveService save, EconomyService economy, Func<DateTime> utcNow)
        {
            _save = save;
            _economy = economy;
            _utcNow = utcNow;
            EnsureToday();
        }

        public QuestDef[] Today { get; private set; }

        public int Progress(int index) => _save.Data.QuestProgress[index];
        public bool IsDone(int index) => Progress(index) >= Today[index].Target;
        public bool BonusPaid => _save.Data.QuestBonusPaid;

        public int DoneCount
        {
            get
            {
                int done = 0;
                for (int i = 0; i < Today.Length; i++)
                {
                    if (IsDone(i))
                    {
                        done++;
                    }
                }
                return done;
            }
        }

        // Safe to call any time; only does work when the day has rolled over (or on first use).
        public void EnsureToday()
        {
            SaveData data = _save.Data;
            int day = QuestBoard.DayNumber(_utcNow());
            if (Today != null && data.QuestDay == day && data.QuestProgress.Count == Today.Length)
            {
                return;
            }

            if (data.QuestDay != day || data.QuestProgress.Count != QuestBoard.QuestsPerDay)
            {
                data.QuestDay = day;
                data.QuestBonusPaid = false;
                data.QuestProgress.Clear();
                for (int i = 0; i < QuestBoard.QuestsPerDay; i++)
                {
                    data.QuestProgress.Add(0);
                }
                _save.Save();
            }
            Today = QuestBoard.ForDay(day);
        }

        public QuestUpdate OnLevelSolved(LevelOutcome outcome)
        {
            EnsureToday();
            int coins = 0;
            int completed = 0;
            for (int i = 0; i < Today.Length; i++)
            {
                int gain = QuestBoard.ProgressFor(Today[i].Kind, outcome);
                Advance(i, gain, ref coins, ref completed);
            }
            return Finish(coins, completed);
        }

        private void Advance(int index, int gain, ref int coins, ref int completed)
        {
            if (gain <= 0 || IsDone(index))
            {
                return;
            }

            SaveData data = _save.Data;
            int target = Today[index].Target;
            data.QuestProgress[index] = Math.Min(target, data.QuestProgress[index] + gain);
            if (data.QuestProgress[index] >= target)
            {
                coins += Today[index].Reward;
                completed++;
            }
        }

        private QuestUpdate Finish(int coins, int completed)
        {
            bool bonus = false;
            if (!_save.Data.QuestBonusPaid && DoneCount == Today.Length)
            {
                _save.Data.QuestBonusPaid = true;
                coins += QuestBoard.AllDoneBonus;
                bonus = true;
            }

            _economy.Earn(coins);
            _save.Save();
            return new QuestUpdate(coins, completed, bonus);
        }
    }
}
