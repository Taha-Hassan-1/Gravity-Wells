using System;
using UnityEngine;

namespace GravityWells.Core
{
    // One puzzle. Always regenerated from (Seed, Level), never written to disk.
    public sealed class LevelDefinition
    {
        private static readonly Gate[] NoGates = new Gate[0];
        private static readonly Asteroid[] NoAsteroids = new Asteroid[0];
        private static readonly Well[] NoWells = new Well[0];

        public LevelDefinition(
            int seed, int level, Vector2 start, Vector2 launchVelocity, Gate[] gates, Vector2 goal, float goalRadius,
            Asteroid[] asteroids, int par, int repulsorBudget, Well[] solutionWells,
            int attempts = 1, int gatesDropped = 0, float forgiveness = 0f, float solutionFlightSeconds = 0f)
        {
            Seed = seed;
            Level = level;
            Start = start;
            LaunchVelocity = launchVelocity;
            Gates = gates ?? NoGates;
            Goal = goal;
            GoalRadius = goalRadius;
            Asteroids = asteroids ?? NoAsteroids;
            Par = par;
            RepulsorBudget = repulsorBudget;
            SolutionWells = solutionWells ?? NoWells;
            Attempts = attempts;
            GatesDropped = gatesDropped;
            Forgiveness = forgiveness;
            SolutionFlightSeconds = solutionFlightSeconds;

            if (Gates.Length > GameConfig.MaxGates)
            {
                throw new ArgumentException("A level holds at most " + GameConfig.MaxGates + " gates.", nameof(gates));
            }
        }

        public int Seed { get; }
        public int Level { get; }
        public Vector2 Start { get; }
        public Vector2 LaunchVelocity { get; }
        public Gate[] Gates { get; }
        public Vector2 Goal { get; }
        public float GoalRadius { get; }
        public Asteroid[] Asteroids { get; }

        // Wells the generator used. Three stars means matching it.
        public int Par { get; }

        // Wells the player may have on the field at once, of any kind.
        public int WellBudget => Par + GameConfig.ExtraWellBudget;

        // An attractor can stand in for any well, so attractors are limited only by the total.
        public int AttractorBudget => WellBudget;

        // How many of the budget may be repulsors (zero before repulsors are introduced).
        public int RepulsorBudget { get; }

        // Generator and tests only; never read by Game/. The hint and the solver must not use it.
        internal Well[] SolutionWells { get; }

        // --- how generation went, for the batch tool and tuning; play never depends on these ---
        public int Attempts { get; }
        public int GatesDropped { get; }
        public float Forgiveness { get; }
        public float SolutionFlightSeconds { get; }
    }
}
