using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class TutorialDemoTests
    {
        private static readonly Vector2 Tray = new Vector2(0.1f, -0.2f);

        private static TutorialDemo DemoFor(int number)
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(number), number);
            Assert.IsTrue(TutorialDemo.TryCreate(level, out TutorialDemo demo));
            return demo;
        }

        [Test]
        public void TheDemoOfLevelOne_DropsTheWellWhereTheLineThreadsTheGateAndReachesTheGoal()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(1), 1);
            Assert.IsTrue(TutorialDemo.TryCreate(level, out TutorialDemo demo));

            Assert.IsTrue(WellRules.IsLegal(level, demo.Target), "a spot a player could really drop it");
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, new[] { new Well(demo.Target) }, 1, trajectory);
            Assert.AreEqual(FlightOutcome.Won, trajectory.Outcome);
            Assert.AreEqual(level.Gates.Length, trajectory.GatesLitCount);
        }

        [Test]
        public void TheDemo_IsTheSameEveryTime()
        {
            Assert.AreEqual(DemoFor(1).Target, DemoFor(1).Target);
        }

        [Test]
        public void ALevelThatNeedsMoreThanOneWell_HasNoDemo()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(3), 3);
            Assert.AreEqual(2, level.Par);
            Assert.IsFalse(TutorialDemo.TryCreate(level, out TutorialDemo demo));
            Assert.IsNull(demo);
        }

        [Test]
        public void TheTimeline_AppearsCarriesDropsHoldsAndLeaves_InThatOrder()
        {
            TutorialDemo demo = DemoFor(1);

            DemoFrame start = demo.Sample(0f, Tray);
            Assert.AreEqual(0f, start.HandAlpha, 1e-5f, "it fades in, never pops");
            Assert.IsFalse(start.WellOnField);

            DemoFrame appeared = demo.Sample(TutorialDemo.AppearSeconds * 0.99f, Tray);
            Assert.Greater(appeared.HandAlpha, 0.9f);
            Assert.AreEqual(Tray, appeared.HandPosition);
            Assert.IsFalse(appeared.WellOnField, "the well is picked up only when the drag begins");

            float midDrag = TutorialDemo.AppearSeconds + TutorialDemo.DragSeconds * 0.5f;
            DemoFrame carrying = demo.Sample(midDrag, Tray);
            Assert.IsTrue(carrying.WellOnField);
            Assert.IsTrue(carrying.Carrying);
            Assert.AreNotEqual(Tray, carrying.HandPosition);
            Assert.AreNotEqual(demo.Target, carrying.HandPosition);

            float held = TutorialDemo.AppearSeconds + TutorialDemo.DragSeconds + 0.1f;
            DemoFrame dropped = demo.Sample(held, Tray);
            Assert.IsTrue(dropped.WellOnField);
            Assert.IsFalse(dropped.Carrying, "let go: the line stays bent");
            Assert.AreEqual(demo.Target, dropped.HandPosition);

            float leaving = TutorialDemo.CycleSeconds - 0.05f;
            DemoFrame leaves = demo.Sample(leaving, Tray);
            Assert.Less(leaves.HandAlpha, 0.2f);
        }

        [Test]
        public void TheHand_MovesSmoothlyFromTheTrayToTheTarget_AndNeverBackwards()
        {
            TutorialDemo demo = DemoFor(1);
            float previous = 0f;
            float total = Vector2.Distance(Tray, demo.Target);
            for (float t = TutorialDemo.AppearSeconds; t <= TutorialDemo.AppearSeconds + TutorialDemo.DragSeconds; t += 0.01f)
            {
                float travelled = Vector2.Distance(Tray, demo.Sample(t, Tray).HandPosition);
                Assert.GreaterOrEqual(travelled, previous - 1e-5f);
                previous = travelled;
            }
            Assert.AreEqual(total, previous, total * 0.02f, "it arrives");
        }

        [Test]
        public void TheDemo_RepeatsEveryCycle()
        {
            TutorialDemo demo = DemoFor(1);
            DemoFrame a = demo.Sample(1.7f, Tray);
            DemoFrame b = demo.Sample(1.7f + TutorialDemo.CycleSeconds, Tray);
            DemoFrame c = demo.Sample(1.7f + 5 * TutorialDemo.CycleSeconds, Tray);
            Assert.AreEqual(a.HandPosition.x, b.HandPosition.x, 1e-3f);
            Assert.AreEqual(a.HandPosition.y, c.HandPosition.y, 1e-3f);
            Assert.AreEqual(a.WellOnField, b.WellOnField);
            Assert.AreEqual(demo.Sample(-0.5f, Tray).HandAlpha, demo.Sample(CycleSeconds() - 0.5f, Tray).HandAlpha, 1e-4f, "a negative time wraps");
        }

        private static float CycleSeconds() => TutorialDemo.CycleSeconds;
    }
}
