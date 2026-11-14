using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class DifficultyCurveTests
    {
        [TestCase(1, 1)]
        [TestCase(8, 1)]
        [TestCase(9, 2)]
        [TestCase(32, 2)]
        [TestCase(33, 3)]
        [TestCase(73, 3)]
        [TestCase(74, 4)]
        [TestCase(1000, 4)]
        public void Wells_GrowWithTheSquareRootOfTheLevel_AndStopAtFour(int level, int wells)
        {
            Assert.AreEqual(wells, DifficultyCurve.WellsForLevel(level));
        }

        [TestCase(1, 1)]
        [TestCase(7, 1)]
        [TestCase(8, 2)]
        [TestCase(31, 4)]
        [TestCase(32, 5)]
        [TestCase(500, 5)]
        public void Gates_GainOneEveryEightLevels_AndStopAtFive(int level, int gates)
        {
            Assert.AreEqual(gates, DifficultyCurve.GatesForLevel(level));
        }

        [Test]
        public void GatesAndGoal_NarrowUntilLevel151_ThenHold()
        {
            Assert.AreEqual(0.16f, DifficultyCurve.GateWidthForLevel(1), 1e-6f);
            Assert.AreEqual(0.07f, DifficultyCurve.GateWidthForLevel(151), 1e-6f);
            Assert.AreEqual(0.07f, DifficultyCurve.GateWidthForLevel(900), 1e-6f);
            Assert.AreEqual(0.07f, DifficultyCurve.GoalRadiusForLevel(1), 1e-6f);
            Assert.AreEqual(0.04f, DifficultyCurve.GoalRadiusForLevel(151), 1e-6f);

            for (int level = 2; level <= 300; level++)
            {
                Assert.LessOrEqual(DifficultyCurve.GateWidthForLevel(level), DifficultyCurve.GateWidthForLevel(level - 1));
                Assert.LessOrEqual(DifficultyCurve.GoalRadiusForLevel(level), DifficultyCurve.GoalRadiusForLevel(level - 1));
            }
        }

        [TestCase(1, 0)]
        [TestCase(11, 0)]
        [TestCase(12, 1)]
        [TestCase(26, 1)]
        [TestCase(27, 2)]
        [TestCase(57, 4)]
        [TestCase(400, 4)]
        public void Asteroids_StartAtLevel12_AndStopAtFour(int level, int asteroids)
        {
            Assert.AreEqual(asteroids, DifficultyCurve.AsteroidsForLevel(level));
        }

        [Test]
        public void NothingEverGetsEasierAsLevelsRise()
        {
            for (int level = 2; level <= 400; level++)
            {
                Assert.GreaterOrEqual(DifficultyCurve.WellsForLevel(level), DifficultyCurve.WellsForLevel(level - 1));
                Assert.GreaterOrEqual(DifficultyCurve.GatesForLevel(level), DifficultyCurve.GatesForLevel(level - 1));
                Assert.GreaterOrEqual(DifficultyCurve.AsteroidsForLevel(level), DifficultyCurve.AsteroidsForLevel(level - 1));
            }
        }

        [Test]
        public void ThePlan_FollowsTheCurve_ExceptForTheFirstThreeLevels()
        {
            LevelPlan one = DifficultyCurve.PlanForLevel(1);
            Assert.AreEqual(1, one.Wells);
            Assert.AreEqual(1, one.Gates);
            Assert.AreEqual(GameConfig.FirstLevelGoalRadius, one.GoalRadius);

            LevelPlan two = DifficultyCurve.PlanForLevel(2);
            Assert.AreEqual(1, two.Wells);
            Assert.AreEqual(2, two.Gates);

            LevelPlan three = DifficultyCurve.PlanForLevel(3);
            Assert.AreEqual(2, three.Wells);
            Assert.AreEqual(2, three.Gates);

            for (int level = 4; level <= 300; level++)
            {
                LevelPlan plan = DifficultyCurve.PlanForLevel(level);
                Assert.AreEqual(DifficultyCurve.WellsForLevel(level), plan.Wells);
                Assert.AreEqual(DifficultyCurve.GatesForLevel(level), plan.Gates);
                Assert.AreEqual(DifficultyCurve.GateWidthForLevel(level), plan.GateWidth);
                Assert.AreEqual(DifficultyCurve.GoalRadiusForLevel(level), plan.GoalRadius);
                Assert.AreEqual(DifficultyCurve.AsteroidsForLevel(level), plan.Asteroids);
            }
        }

        [Test]
        public void TheCurveNeverAsksForMoreThanTheBuffersHold()
        {
            for (int level = 1; level <= 1000; level++)
            {
                LevelPlan plan = DifficultyCurve.PlanForLevel(level);
                Assert.LessOrEqual(plan.Gates, GameConfig.MaxGates);
                Assert.LessOrEqual(plan.Wells + GameConfig.ExtraWellBudget + 1, GameConfig.MaxWells, "par, the spares and the rewarded Extra Well");
            }
        }

        // ---- stars ----

        [TestCase(1, 2, 3)] // under par
        [TestCase(2, 2, 3)]
        [TestCase(3, 2, 2)]
        [TestCase(4, 2, 1)]
        [TestCase(5, 2, 1)]
        public void Stars_FallAsMoreWellsAreUsed(int wellsUsed, int par, int stars)
        {
            Assert.AreEqual(stars, Scoring.StarsFor(wellsUsed, par, usedExtraWell: false));
        }

        [Test]
        public void AWinWithTheExtraWell_EarnsOneStar_HoweverFewWells()
        {
            Assert.AreEqual(1, Scoring.StarsFor(1, 3, usedExtraWell: true));
            Assert.AreEqual(1, Scoring.StarsFor(6, 3, usedExtraWell: true));
        }

        [Test]
        public void UnderPar_MeansFewerWellsThanTheGeneratorUsed()
        {
            Assert.IsTrue(Scoring.IsUnderPar(1, 2));
            Assert.IsFalse(Scoring.IsUnderPar(2, 2));
            Assert.IsFalse(Scoring.IsUnderPar(3, 2));
        }

        // ---- where a well may go ----

        private static LevelDefinition LevelWithStartAt(Vector2 start)
        {
            return new LevelDefinition(1, 1, start, new Vector2(0f, GameConfig.LaunchSpeed), null, new Vector2(0.5f, 1.3f), 0.05f, null, 1, 0, null);
        }

        [Test]
        public void AWell_MayGoAnywhereOnTheField_ExceptNearTheStart()
        {
            LevelDefinition level = LevelWithStartAt(new Vector2(0.5f, 0.2f));

            Assert.IsTrue(WellRules.IsLegal(level, new Vector2(0f, 0f)));
            Assert.IsTrue(WellRules.IsLegal(level, new Vector2(1f, 1.5f)));
            Assert.IsFalse(WellRules.IsLegal(level, new Vector2(1.01f, 0.5f)));
            Assert.IsFalse(WellRules.IsLegal(level, new Vector2(0.5f, -0.01f)));
            Assert.IsFalse(WellRules.IsLegal(level, new Vector2(0.5f, 0.2f + GameConfig.MinWellDistanceFromStart * 0.9f)));
            Assert.IsTrue(WellRules.IsLegal(level, new Vector2(0.5f, 0.2f + GameConfig.MinWellDistanceFromStart * 1.1f)));
        }

        [Test]
        public void Clamp_AlwaysGivesALegalPosition_AndLeavesALegalOneAlone()
        {
            LevelDefinition level = LevelWithStartAt(new Vector2(0.15f, 0.13f));
            var rng = new System.Random(5);
            for (int i = 0; i < 2000; i++)
            {
                var anywhere = new Vector2(SeededMath.Range(rng, -0.5f, 1.5f), SeededMath.Range(rng, -0.5f, 2f));
                Vector2 clamped = WellRules.Clamp(level, anywhere);
                Assert.IsTrue(WellRules.IsLegal(level, clamped), anywhere + " -> " + clamped);
                if (WellRules.IsLegal(level, anywhere))
                {
                    Assert.AreEqual(anywhere, clamped);
                }
            }
            Assert.IsTrue(WellRules.IsLegal(level, WellRules.Clamp(level, level.Start)), "a well dropped exactly on the start");
        }

        // ---- the score that steers the solver and the hint ----

        private static LevelDefinition TwoGateLevel()
        {
            var gates = new[]
            {
                new Gate(new Vector2(0.4f, 0.6f), new Vector2(0.6f, 0.6f)),
                new Gate(new Vector2(0.7f, 1.0f), new Vector2(0.9f, 1.0f)),
            };
            return new LevelDefinition(
                1, 1, new Vector2(0.5f, 0.2f), new Vector2(0f, GameConfig.LaunchSpeed), gates, new Vector2(0.8f, 1.3f), 0.05f, null, 1, 0, null);
        }

        private static float Score(LevelDefinition level, params Well[] wells)
        {
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, wells, wells.Length, trajectory);
            return PathScore.Evaluate(level, trajectory);
        }

        [Test]
        public void AWin_ScoresZero_AndNothingElseDoes()
        {
            var gate = new Gate(new Vector2(0.4f, 0.6f), new Vector2(0.6f, 0.6f));
            var winnable = new LevelDefinition(
                1, 1, new Vector2(0.5f, 0.2f), new Vector2(0f, GameConfig.LaunchSpeed), new[] { gate }, new Vector2(0.5f, 1.0f), 0.05f, null, 1, 0, null);
            Assert.AreEqual(0f, Score(winnable));

            // The path runs straight through the goal before the gate that would let it count.
            var gateAfterGoal = new Gate(new Vector2(0.4f, 1.2f), new Vector2(0.6f, 1.2f));
            var flownOver = new LevelDefinition(
                1, 1, new Vector2(0.5f, 0.2f), new Vector2(0f, GameConfig.LaunchSpeed), new[] { gateAfterGoal }, new Vector2(0.5f, 0.8f), 0.05f, null, 1, 0, null);
            Assert.Greater(Score(flownOver), 0.3f, "only the path after the last gate can still reach the goal");
        }

        [Test]
        public void EachUnlitGate_CostsAThousand_PlusHowFarThePathIsFromIt()
        {
            LevelDefinition level = TwoGateLevel();
            float score = Score(level); // straight up: lights the first gate, misses the second

            Assert.That(score, Is.InRange(1000f, 1001f));
            float fromGate = 0.3f;                       // the path x = 0.5 against the gate middle at x = 0.8
            float fromGoal = 0.3f;                       // and against the goal at x = 0.8
            Assert.AreEqual(1000f + fromGate + fromGoal, score, 0.01f);
        }

        [Test]
        public void LightingOneMoreGate_AlwaysBeatsGettingCloserToTwo()
        {
            LevelDefinition level = TwoGateLevel();
            float oneLit = Score(level);

            // Launched sideways: nothing lit, however near it passes.
            var sideways = new LevelDefinition(
                1, 1, new Vector2(0.3f, 0.55f), new Vector2(GameConfig.LaunchSpeed, 0f), level.Gates, level.Goal, 0.05f, null, 1, 0, null);
            float noneLit = Score(sideways);

            Assert.Greater(noneLit, 2000f);
            Assert.Less(oneLit, noneLit);
        }

        [Test]
        public void ACrash_CostsFiveHundredMore()
        {
            LevelDefinition level = TwoGateLevel();
            var onThePath = new Well(new Vector2(0.5f, 0.9f));

            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, new[] { onThePath }, 1, trajectory);
            Assert.AreEqual(FlightOutcome.HitWell, trajectory.Outcome);
            Assert.Greater(PathScore.Evaluate(level, trajectory), 1000f + PathScore.CrashPenalty);
        }
    }
}
