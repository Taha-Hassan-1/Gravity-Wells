using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using GravityWells.Core;

namespace GravityWells.Editor
{
    // Generates thousands of levels across the curve and reports, per band of ten levels, how hard
    // the generator had to work and how hard the levels are. This table is what the difficulty and
    // physics constants are tuned from (IMPLEMENTATION_PROMPT 1.9).
    //
    //   Unity.exe -batchmode -nographics -projectPath D:/Projects/GravityWells
    //     -executeMethod GravityWells.Editor.LevelBatchTester.RunFromCommandLine -logFile batch.log
    public static class LevelBatchTester
    {
        public const string ReportPath = "Logs/LevelBatchReport.txt"; // Logs/ is git-ignored

        private const int LastLevel = 250;
        private const int BandSize = 10;
        private const int SeedsPerLevel = 40;          // 250 levels x 40 seeds = 10,000 levels
        private const int SolverSeedsPerLevel = 2;     // the solver is far slower than the generator, so it samples
        private const int SolverMaxEvaluations = 20000;

        [MenuItem("GravityWells/Tuning/Run Level Batch (10,000 levels)")]
        public static void RunFromMenu()
        {
            string report = Run(LastLevel, SeedsPerLevel, SolverSeedsPerLevel);
            UnityEngine.Debug.Log("[Batch] written to " + ReportPath + "\n" + report);
        }

        public static void RunFromCommandLine()
        {
            string report = Run(LastLevel, SeedsPerLevel, SolverSeedsPerLevel);
            UnityEngine.Debug.Log("[Batch]\n" + report);
            EditorApplication.Exit(0);
        }

        // Levels 1..lastLevel, each generated from seedsPerLevel seeds; the first solverSeedsPerLevel
        // of them are also handed to the solver.
        public static string Run(int lastLevel, int seedsPerLevel, int solverSeedsPerLevel)
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"Level batch: levels 1-{lastLevel}, {seedsPerLevel} seeds each ({lastLevel * seedsPerLevel} levels), " +
                $"solver on {solverSeedsPerLevel} per level, up to {SolverMaxEvaluations} flights.");
            text.AppendLine(
                $"Constants: LaunchSpeed {GameConfig.LaunchSpeed}, WellStrength {GameConfig.WellStrength}, Softening {GameConfig.Softening}, " +
                $"WellCoreRadius {GameConfig.WellCoreRadius}, ForgivenessRadius {GameConfig.ForgivenessRadius}, MinForgiveness {GameConfig.MinForgiveness}.");
            text.AppendLine();
            text.AppendLine("levels    wells gates  width  attempts  maxAtt  dropped%  flight s  forgive  solved%  solver flights  gen ms");

            var wells = new Well[GameConfig.MaxWells];
            var scratch = new Trajectory();
            var watch = new Stopwatch();
            long totalAttempts = 0;
            int totalDropped = 0, totalLevels = 0, totalSolved = 0, totalSolverRuns = 0;

            for (int bandStart = 1; bandStart <= lastLevel; bandStart += BandSize)
            {
                int bandEnd = System.Math.Min(bandStart + BandSize - 1, lastLevel);
                long attempts = 0;
                int maxAttempts = 0, dropped = 0, levels = 0, solved = 0, solverRuns = 0;
                long solverFlights = 0;
                double flightSeconds = 0, forgiveness = 0, generateMs = 0;

                for (int level = bandStart; level <= bandEnd; level++)
                {
                    for (int s = 0; s < seedsPerLevel; s++)
                    {
                        int seed = unchecked(DifficultyCurve.SeedForLevel(level) + s * 7919);
                        watch.Restart();
                        LevelDefinition definition = LevelGenerator.Generate(seed, level);
                        watch.Stop();

                        levels++;
                        generateMs += watch.Elapsed.TotalMilliseconds;
                        attempts += definition.Attempts;
                        maxAttempts = System.Math.Max(maxAttempts, definition.Attempts);
                        dropped += definition.GatesDropped > 0 ? 1 : 0;
                        flightSeconds += definition.SolutionFlightSeconds;
                        forgiveness += definition.Forgiveness;

                        if (s < solverSeedsPerLevel)
                        {
                            SolverResult result = Solver.Solve(definition, seed, SolverMaxEvaluations, wells, scratch);
                            solverRuns++;
                            solverFlights += result.Evaluations;
                            solved += result.Solved ? 1 : 0;
                        }
                    }
                }

                LevelPlan plan = DifficultyCurve.PlanForLevel(bandEnd);
                text.AppendLine(
                    $"{bandStart,3}-{bandEnd,-3}   {plan.Wells,3}   {plan.Gates,3}   {plan.GateWidth:F3}  {(double)attempts / levels,8:F1}  {maxAttempts,6}  " +
                    $"{100.0 * dropped / levels,7:F2}  {flightSeconds / levels,8:F2}  {forgiveness / levels,7:F2}  " +
                    $"{(solverRuns > 0 ? 100.0 * solved / solverRuns : 0),7:F1}  {(solverRuns > 0 ? (double)solverFlights / solverRuns : 0),14:F0}  {generateMs / levels,6:F2}");

                totalAttempts += attempts;
                totalDropped += dropped;
                totalLevels += levels;
                totalSolved += solved;
                totalSolverRuns += solverRuns;
            }

            text.AppendLine();
            text.AppendLine(
                $"Total: {totalLevels} levels, {(double)totalAttempts / totalLevels:F1} attempts each on average, " +
                $"gate dropped on {100.0 * totalDropped / totalLevels:F2}%, solver {(totalSolverRuns > 0 ? 100.0 * totalSolved / totalSolverRuns : 0):F1}% " +
                $"of {totalSolverRuns}. The wells/gates/width columns are those of the last level in the band.");

            string report = text.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report);
            return report;
        }
    }
}
