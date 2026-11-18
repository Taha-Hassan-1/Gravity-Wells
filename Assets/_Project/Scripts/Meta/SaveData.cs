using System.Collections.Generic;

namespace GravityWells.Meta
{
    // [Preserve] so IL2CPP's managed stripping (Medium on Android) can never drop a field that
    // JsonUtility reads and writes only by reflection. Additive fields with defaults only, so
    // older saves always load; anything else needs a step in SaveMigrator.
    [System.Serializable]
    [UnityEngine.Scripting.Preserve]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int HighestLevelUnlocked = 1;
        public int CurrentLevel = 1;
        public List<byte> StarsByLevel = new List<byte>(); // index = level-1
        public float TotalPlaySeconds;
        public List<int> UnlockedThemeIds = new List<int> { 0 };
        public int SelectedThemeId;
        public int DailyStreak;
        public int BestDailyStreak;
        public string LastDailyDateUtc = "";
        public bool StreakFreezeAvailable;                         // one forgiven missed day, held until a gap uses it
        public List<string> FrozenDatesUtc = new List<string>();   // days a freeze covered, for the calendar
        public int SessionCount;                                   // launches so far; ad pacing treats session 1 more gently
        public bool TutorialSeen;                                  // the wordless level-1 demo has been dismissed
        public bool HowToPlaySeen;                                 // the How to play pages have been read once
        public bool RemoveAdsPurchased;
        public bool SoundOn = true;
        public bool HapticsOn = true;                              // shown to players as "Vibrations"
        public bool VibrationStrong;                               // the stronger buzz, chosen in Settings
        public bool MusicOn = true;
        public int Coins;
        public int QuestDay;                                       // QuestBoard.DayNumber the progress below belongs to
        public List<int> QuestProgress = new List<int>();          // one counter per quest of that day, capped at its target
        public bool QuestBonusPaid;                                // the all-quests bonus for that day has been paid
        // Colour overrides for the theme (packed 0xAARRGGBB, 0 = use the theme) and the thicker-crossings cue,
        // read by LineStyleService; the contrast checks around them are shared with Untangle.
        public int LineColorClear;
        public int LineColorCrossing;
        public bool ThickCrossings;

        // --- Gravity Wells ---
        public int UnderParCount;                                  // wins that used fewer wells than par
        public int TotalLaunches;
        public int TotalWellsPlaced;
    }
}
