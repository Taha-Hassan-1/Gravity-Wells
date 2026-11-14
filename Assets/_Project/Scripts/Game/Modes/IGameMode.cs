using GravityWells.Core;

namespace GravityWells.Game
{
    // What a finished level came to. Immutable; the result card reads it.
    public readonly struct LevelResult
    {
        public readonly int Level;
        public readonly int Stars;
        public readonly int WellsUsed;
        public readonly int Par;
        public readonly int Launches;
        public readonly bool UnderPar;
        public readonly bool NewBest;
        public readonly bool UsedExtraWell;

        public LevelResult(int level, int stars, int wellsUsed, int par, int launches, bool underPar, bool newBest, bool usedExtraWell)
        {
            Level = level;
            Stars = stars;
            WellsUsed = wellsUsed;
            Par = par;
            Launches = launches;
            UnderPar = underPar;
            NewBest = newBest;
            UsedExtraWell = usedExtraWell;
        }
    }

    // One way of playing: where its levels come from and what a win means. GameManager runs the board, the
    // flight and the screens; a mode only decides which puzzle comes next and records what a win earned. No
    // engine types appear here, so a mode's rules are tested without a scene. Campaign and Daily (Phase 4)
    // are the two v1.0 modes, written as classes from the start so GameManager never absorbs their rules.
    public interface IGameMode
    {
        // A stable lowercase id for analytics.
        string Key { get; }

        // Campaign-level number of the puzzle being played (a daily borrows its difficulty from one).
        int LevelNumber { get; }

        // Starts a session and returns its first puzzle.
        LevelDefinition Begin();

        // The same puzzle again, from scratch.
        LevelDefinition Replay();

        // The puzzle after this one.
        LevelDefinition Next();

        // The player won this puzzle. Records it and returns what to show.
        LevelResult OnWon(int wellsUsed, int launches, bool usedExtraWell);
    }
}
