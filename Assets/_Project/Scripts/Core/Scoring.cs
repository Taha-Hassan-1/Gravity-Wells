namespace GravityWells.Core
{
    // Stars are about how few wells the winning layout used.
    public static class Scoring
    {
        public const int MaxStars = 3;

        // 3 for par or fewer, 2 for one more, 1 for two more. A win that needed the rewarded
        // Extra Well always earns 1, whatever the count.
        public static int StarsFor(int wellsUsed, int par, bool usedExtraWell)
        {
            if (usedExtraWell || wellsUsed >= par + 2)
            {
                return 1;
            }
            return wellsUsed <= par ? 3 : 2;
        }

        // Beating the generator: fewer wells than it used.
        public static bool IsUnderPar(int wellsUsed, int par)
        {
            return wellsUsed < par;
        }
    }
}
