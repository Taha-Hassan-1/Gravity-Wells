using System;
using System.Collections.Generic;

namespace GravityWells.Meta
{
    // Brings a save written by an older build up to SaveData.CurrentVersion, one version at a
    // time. Steps[n] upgrades a version n+1 save to version n+2, so a save several versions
    // behind runs every step in order.
    //
    // To change the schema: bump SaveData.CurrentVersion, append one step here, add a test.
    // A field that is merely new needs no step (JsonUtility gives it its initializer value);
    // a step is for renamed, re-encoded or re-derived data. Steps must be safe to run twice,
    // because a save a newer build wrote is stamped back to this build's version.
    public static class SaveMigrator
    {
        private static readonly Action<SaveData>[] Steps =
        {
            // v1 -> v2 goes here.
        };

        public static void Migrate(SaveData data)
        {
            Migrate(data, Steps, SaveData.CurrentVersion);
        }

        // The table is a parameter so tests can exercise the stepping without a real v2.
        public static void Migrate(SaveData data, IReadOnlyList<Action<SaveData>> steps, int targetVersion)
        {
            for (int version = Math.Max(1, data.Version); version < targetVersion; version++)
            {
                int index = version - 1;
                if (index < steps.Count)
                {
                    steps[index]?.Invoke(data);
                }
            }
            data.Version = targetVersion;
            Sanitize(data);
        }

        // A hand-edited or half-written file can null out a collection that every reader
        // assumes exists.
        private static void Sanitize(SaveData data)
        {
            data.StarsByLevel ??= new List<byte>();
            data.UnlockedThemeIds ??= new List<int> { 0 };
            data.FrozenDatesUtc ??= new List<string>();
            data.QuestProgress ??= new List<int>();
            data.LastDailyDateUtc ??= "";
        }
    }
}
