using UnityEditor.Compilation;

namespace GravityWells.Tests
{
    // The time budgets in the spec are for a phone. The Editor runs scripts on Mono, and usually
    // compiled in Debug mode (the bug icon in the status bar), so a timing test here allows a margin:
    // 20% over the budget in Release mode (measurement noise between runs is about that much) and
    // double in Debug. That still catches an accidental slowdown. It does NOT prove the budget on a
    // device; the Phase 2 device check does.
    public static class TestTiming
    {
        private const double ReleaseSlack = 1.2;
        private const double DebugSlack = 2.0;

        public static bool Optimised => CompilationPipeline.codeOptimization == CodeOptimization.Release;

        public static double Budget(double budgetMilliseconds)
        {
            return budgetMilliseconds * (Optimised ? ReleaseSlack : DebugSlack);
        }

        public static string Describe(double measuredMilliseconds)
        {
            return $"{measuredMilliseconds:F4} ms ({(Optimised ? "Release" : "Debug")} script compilation)";
        }
    }
}
