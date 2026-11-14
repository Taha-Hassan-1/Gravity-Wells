using System;
using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    // The daily puzzle and share codes depend on a seed meaning the same level everywhere, so
    // "the same" here means the same bits, not "close".
    public class DeterminismTests
    {
        // The Editor's value of DeterminismCheck.CombinedHash. If a deliberate change to the generator or the
        // simulator moves it, record the new value here AND re-run the check on a device (docs/TEST_CASES.md, DET-05).
        // History: -159271037 (Phase 2, recorded before any phone had run it); 69262906 from Phase 4, when level 35
        // (the one that introduces repulsors) was made a lighter level that always contains one. Only level 35 of
        // levels 1-100 changed. No phone has recorded either value yet, so nothing on a device was invalidated.
        private const int ExpectedEditorHash = 69262906;

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        private static void AssertSameBits(Vector2 a, Vector2 b, string what)
        {
            Assert.AreEqual(Bits(a.x), Bits(b.x), what);
            Assert.AreEqual(Bits(a.y), Bits(b.y), what);
        }

        [Test]
        public void TheSameSeedAndLevel_GiveBitIdenticalData()
        {
            for (int i = 0; i < 60; i++)
            {
                int seed = 77 + i * 104729;
                int number = 1 + i * 5;
                LevelDefinition a = LevelGenerator.Generate(seed, number);
                LevelDefinition b = LevelGenerator.Generate(seed, number);
                string what = $"seed {seed} level {number}";

                AssertSameBits(a.Start, b.Start, what);
                AssertSameBits(a.LaunchVelocity, b.LaunchVelocity, what);
                AssertSameBits(a.Goal, b.Goal, what);
                Assert.AreEqual(Bits(a.GoalRadius), Bits(b.GoalRadius), what);
                Assert.AreEqual(a.Par, b.Par, what);
                Assert.AreEqual(a.RepulsorBudget, b.RepulsorBudget, what);
                Assert.AreEqual(a.Attempts, b.Attempts, what);

                Assert.AreEqual(a.Gates.Length, b.Gates.Length, what);
                for (int g = 0; g < a.Gates.Length; g++)
                {
                    AssertSameBits(a.Gates[g].A, b.Gates[g].A, what);
                    AssertSameBits(a.Gates[g].B, b.Gates[g].B, what);
                }
                Assert.AreEqual(a.Asteroids.Length, b.Asteroids.Length, what);
                for (int k = 0; k < a.Asteroids.Length; k++)
                {
                    AssertSameBits(a.Asteroids[k].Centre, b.Asteroids[k].Centre, what);
                    Assert.AreEqual(Bits(a.Asteroids[k].Radius), Bits(b.Asteroids[k].Radius), what);
                }
                Assert.AreEqual(a.SolutionWells.Length, b.SolutionWells.Length, what);
                for (int w = 0; w < a.SolutionWells.Length; w++)
                {
                    AssertSameBits(a.SolutionWells[w].Position, b.SolutionWells[w].Position, what);
                    Assert.AreEqual(a.SolutionWells[w].Kind, b.SolutionWells[w].Kind, what);
                }
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentLevels()
        {
            LevelDefinition a = LevelGenerator.Generate(1, 20);
            LevelDefinition b = LevelGenerator.Generate(2, 20);
            Assert.AreNotEqual(a.Start, b.Start);
        }

        [Test]
        public void SimulatingTheSameLayoutTwice_GivesABitIdenticalBuffer()
        {
            LevelDefinition level = LevelGenerator.Generate(4242, 60);
            var first = new Trajectory();
            var second = new Trajectory();
            FlightSimulator.Simulate(level, level.SolutionWells, level.SolutionWells.Length, first);
            FlightSimulator.Simulate(level, level.SolutionWells, level.SolutionWells.Length, second);

            Assert.AreEqual(first.Count, second.Count);
            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.GatesLitMask, second.GatesLitMask);
            for (int i = 0; i < first.Count; i++)
            {
                AssertSameBits(first.Points[i], second.Points[i], "step " + i);
            }
            Assert.AreEqual(first.Hash(), second.Hash());
        }

        [Test]
        public void TheHash_ChangesWhenAWellMovesByAHair()
        {
            LevelDefinition level = LevelGenerator.Generate(4242, 60);
            var wells = (Well[])level.SolutionWells.Clone();
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, wells, wells.Length, trajectory);
            int before = trajectory.Hash();

            wells[0] = new Well(wells[0].Position + new Vector2(1e-5f, 0f), wells[0].Kind);
            FlightSimulator.Simulate(level, wells, wells.Length, trajectory);

            Assert.AreNotEqual(before, trajectory.Hash());
        }

        [Test]
        public void TheCombinedHashOfTheCampaign_IsStableFromRunToRun()
        {
            int first = DeterminismCheck.CombinedHash();
            Assert.AreEqual(first, DeterminismCheck.CombinedHash());
            Assert.AreEqual(ExpectedEditorHash, first, "the campaign now flies differently from when the device check was recorded: re-run it on a phone");
            Debug.Log("[Determinism] combined trajectory hash, campaign levels 1-100 (Editor): " + first);
        }

        [Test]
        public void SeedForLevel_IsStable_AndSpreadsNeighbouringLevelsApart()
        {
            Assert.AreEqual(unchecked((int)(1u * 2654435761u ^ 0x85EBCA6Bu)), DifficultyCurve.SeedForLevel(1));
            Assert.AreNotEqual(DifficultyCurve.SeedForLevel(1), DifficultyCurve.SeedForLevel(2));
            Assert.Greater(Math.Abs((long)DifficultyCurve.SeedForLevel(2) - DifficultyCurve.SeedForLevel(1)), 1000);
        }
    }
}
