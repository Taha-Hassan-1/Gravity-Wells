namespace GravityWells.Core
{
    // The device determinism check (CLAUDE.md, third rule): one number for the solutions of campaign
    // levels 1 to 100. It is computed on the Editor and on a phone; if the two differ, floats are not
    // behaving identically on the phone (IL2CPP fusing a multiply and an add, for instance) and the daily
    // puzzle and share codes cannot be trusted until that is understood.
    public static class DeterminismCheck
    {
        public const int FirstLevel = 1;
        public const int LastLevel = 100;

        // Every level is generated from its campaign seed, its solution is flown, and the 32-bit hash of each
        // flight (every point, the outcome and the lit gates) is folded into one number. The generator's own
        // randomness is part of what is checked, because a level that comes out differently flies differently.
        public static int CombinedHash()
        {
            var trajectory = new Trajectory();
            unchecked
            {
                uint combined = 2166136261u;
                for (int number = FirstLevel; number <= LastLevel; number++)
                {
                    LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(number), number);
                    FlightSimulator.Simulate(level, level.SolutionWells, level.SolutionWells.Length, trajectory);
                    combined = (combined ^ (uint)trajectory.Hash()) * 16777619u;
                }
                return (int)combined;
            }
        }
    }
}
