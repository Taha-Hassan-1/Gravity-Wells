using System.IO;
using UnityEditor;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Editor
{
    // The Editor half of the device determinism check (see DeterminismCheck). Run it here, run the hidden action
    // on a phone (seven taps on the level title in a development build), and compare the two numbers.
    public static class DeterminismTool
    {
        [MenuItem("GravityWells/Tuning/Print Determinism Hash")]
        public static void Print()
        {
            int hash = DeterminismCheck.CombinedHash();
            string line = $"Determinism hash, campaign levels {DeterminismCheck.FirstLevel}-{DeterminismCheck.LastLevel}: {hash} " +
                          $"(Editor, {Application.platform}, {SystemInfo.processorType})";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/determinism-editor.txt", line + "\n");
            Debug.Log("[Determinism] " + line);
        }
    }
}
