using System;
using System.Collections.Generic;
using System.Globalization;

namespace GravityWells.Meta
{
    // How one calendar day shows on the menu's 7-day strip.
    public enum DayState { None, Completed, Frozen }

    // Seeds the daily puzzle from the UTC date so every player worldwide gets the same one. Date is always passed in explicitly rather than
    // read from DateTime.UtcNow internally, so this is testable without the clock.
    public static class DailyChallengeService
    {
        // Frozen dates older than this many entries can no longer appear on a 7-day strip.
        private const int MaxStoredFrozenDates = 30;

        public static int SeedForDate(DateTime utcDate) => int.Parse(utcDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        public static string DateKey(DateTime utcDate) => utcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static bool HasPlayedToday(SaveData data, DateTime utcNow)
        {
            return data.LastDailyDateUtc == DateKey(utcNow);
        }

        // Returns true when this completion was rescued by a streak freeze (so the caller can
        // say so). A freeze forgives exactly one missed day: the day before yesterday was the
        // last completion, yesterday was missed, and a freeze is held.
        public static bool RecordCompletion(SaveData data, DateTime utcNow)
        {
            string today = DateKey(utcNow);
            if (data.LastDailyDateUtc == today)
            {
                return false; // already recorded today
            }

            string yesterday = DateKey(utcNow.AddDays(-1));
            string twoDaysAgo = DateKey(utcNow.AddDays(-2));
            bool freezeUsed = false;

            if (data.LastDailyDateUtc == yesterday)
            {
                data.DailyStreak++;
            }
            else if (data.LastDailyDateUtc == twoDaysAgo && data.StreakFreezeAvailable && data.DailyStreak > 0)
            {
                data.StreakFreezeAvailable = false;
                AddFrozenDate(data, yesterday);
                data.DailyStreak++;
                freezeUsed = true;
            }
            else
            {
                data.DailyStreak = 1;
            }

            data.LastDailyDateUtc = today;
            if (data.DailyStreak > data.BestDailyStreak)
            {
                data.BestDailyStreak = data.DailyStreak;
            }
            return freezeUsed;
        }

        // ---- streak freeze ----

        public static bool CanEarnFreeze(SaveData data) => !data.StreakFreezeAvailable;

        public static void GrantFreeze(SaveData data) => data.StreakFreezeAvailable = true;

        // True when the next completion would be saved by the held freeze.
        public static bool FreezeWouldSaveStreak(SaveData data, DateTime utcNow)
        {
            return data.StreakFreezeAvailable && data.DailyStreak > 0
                && DaysSinceLast(data, utcNow) == 2;
        }

        // The streak the player can actually still continue: a lapsed run counts as 0 even
        // though the number stays in the save until the next completion resets it.
        public static int EffectiveStreak(SaveData data, DateTime utcNow)
        {
            int days = DaysSinceLast(data, utcNow);
            if (days < 0 || data.DailyStreak <= 0)
            {
                return 0;
            }
            bool alive = days <= 1 || (days == 2 && data.StreakFreezeAvailable);
            return alive ? data.DailyStreak : 0;
        }

        private static void AddFrozenDate(SaveData data, string dateKey)
        {
            if (data.FrozenDatesUtc == null)
            {
                data.FrozenDatesUtc = new List<string>();
            }
            if (!data.FrozenDatesUtc.Contains(dateKey))
            {
                data.FrozenDatesUtc.Add(dateKey);
            }
            while (data.FrozenDatesUtc.Count > MaxStoredFrozenDates)
            {
                data.FrozenDatesUtc.RemoveAt(0);
            }
        }

        // Whole days from the last completion to today; -1 when there has never been one.
        private static int DaysSinceLast(SaveData data, DateTime utcNow)
        {
            if (!TryParseDate(data.LastDailyDateUtc, out DateTime last))
            {
                return -1;
            }
            return (int)(utcNow.Date - last.Date).TotalDays;
        }

        private static bool TryParseDate(string text, out DateTime date)
        {
            return DateTime.TryParseExact(
                text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        // ---- calendar ----

        // The save stores only the last completed date, the running streak and the dates a
        // freeze covered, not a per-day history, so the strip is derived: walk back from the
        // last completion; a frozen day is shown as frozen and does not use up the streak,
        // every other day in the run was completed. Index 0 is the oldest day shown, the last
        // index is today. A streak that has already lapsed shows nothing.
        public static DayState[] DayStates(SaveData data, DateTime utcNow, int dayCount)
        {
            var result = new DayState[dayCount];
            if (!TryParseDate(data.LastDailyDateUtc, out DateTime last))
            {
                return result;
            }

            int daysSinceLast = (int)(utcNow.Date - last.Date).TotalDays;
            bool alive = daysSinceLast <= 1 || (daysSinceLast == 2 && data.StreakFreezeAvailable);
            if (!alive)
            {
                return result; // streak already broken
            }

            var frozen = new HashSet<string>(data.FrozenDatesUtc ?? new List<string>());
            DateTime day = last.Date;
            int remaining = data.DailyStreak;
            while (remaining > 0)
            {
                int index = dayCount - 1 - (int)(utcNow.Date - day).TotalDays;
                if (index < 0)
                {
                    break; // past the oldest day on the strip
                }

                bool isFrozen = frozen.Contains(DateKey(day));
                if (index < dayCount)
                {
                    result[index] = isFrozen ? DayState.Frozen : DayState.Completed;
                }
                if (!isFrozen)
                {
                    remaining--;
                }
                day = day.AddDays(-1);
            }
            return result;
        }

        // Completed days only, kept for callers (and tests) that don't care about freezes.
        public static bool[] CompletedDays(SaveData data, DateTime utcNow, int dayCount)
        {
            DayState[] states = DayStates(data, utcNow, dayCount);
            var result = new bool[dayCount];
            for (int i = 0; i < dayCount; i++)
            {
                result[i] = states[i] == DayState.Completed;
            }
            return result;
        }
    }
}
