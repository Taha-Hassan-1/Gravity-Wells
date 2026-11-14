using System.Collections.Generic;

namespace GravityWells.Meta
{
    // [Preserve] so IL2CPP's managed stripping (Medium on Android) can never drop a field that
    // JsonUtility reads and writes only by reflection. Additive fields with defaults only, so
    // older saves always load.
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
        public bool StreakFreezeAvailable;
        public List<string> FrozenDatesUtc = new List<string>();
        public int SessionCount; // launches so far; ad pacing treats session 1 more gently
        public bool HowToPlaySeen;
        public bool RemoveAdsPurchased;
        public bool SoundOn = true;
        public bool HapticsOn = true;               // shown to players as "Vibrations"
        public bool VibrationStrong;
        public bool MusicOn = true;
        public int Coins;
        // Colour overrides for the theme (packed 0xAARRGGBB, 0 = use the theme) and the thicker-crossings cue,
        // read by LineStyleService; the contrast checks around them are shared with Untangle.
        public int LineColorClear;
        public int LineColorCrossing;
        public bool ThickCrossings;
    }
}
