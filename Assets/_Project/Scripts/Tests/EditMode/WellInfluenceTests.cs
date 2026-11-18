using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class WellInfluenceTests
    {
        private static LevelDefinition Straight() => new LevelDefinition(
            seed: 1, level: 1, start: new Vector2(0.5f, 0.2f), launchVelocity: new Vector2(0f, GameConfig.LaunchSpeed),
            gates: null, goal: new Vector2(-10f, -10f), goalRadius: 0.05f, asteroids: null,
            par: 1, repulsorBudget: 0, solutionWells: null);

        private static Influence Of(params Well[] wells)
        {
            var t = new Trajectory();
            FlightSimulator.Simulate(Straight(), wells, wells.Length, t);
            var into = new Influence[wells.Length];
            WellInfluence.Compute(t, wells, wells.Length, into);
            return into[0];
        }

        [Test]
        public void ANearWell_IsStrongerThanAFarOne()
        {
            Influence near = Of(new Well(new Vector2(0.58f, 0.5f)));
            Influence far = Of(new Well(new Vector2(0.95f, 0.5f)));
            Assert.Greater(near.Strength, far.Strength);
            Assert.Less(near.Distance, far.Distance);
        }

        [Test]
        public void TheLinkPointIsOnTheLine_AtTheNearestApproach()
        {
            var well = new Well(new Vector2(0.6f, 0.5f));
            var t = new Trajectory();
            FlightSimulator.Simulate(Straight(), new[] { well }, 1, t);
            var into = new Influence[1];
            WellInfluence.Compute(t, new[] { well }, 1, into);

            float best = float.MaxValue;
            for (int i = 0; i < t.Count; i++)
            {
                best = Mathf.Min(best, Vector2.Distance(t.Points[i], well.Position));
            }
            Assert.AreEqual(best, Vector2.Distance(into[0].Point, well.Position), 1e-5f);
            Assert.AreEqual(best, into[0].Distance, 1e-5f);
        }

        [Test]
        public void StrengthStaysBetweenTheFloorAndOne()
        {
            Influence veryFar = Of(new Well(new Vector2(-5f, 5f)));
            Influence veryNear = Of(new Well(new Vector2(0.5f, 0.45f)));
            Assert.AreEqual(GameConfig.InfluenceFloor, veryFar.Strength, 1e-6f);
            Assert.LessOrEqual(veryNear.Strength, 1f);
            Assert.Greater(veryNear.Strength, veryFar.Strength);
        }

        [Test]
        public void EveryWellGetsItsOwnLink()
        {
            var wells = new[] { new Well(new Vector2(0.3f, 0.5f)), new Well(new Vector2(0.7f, 0.6f), WellKind.Repulsor) };
            var t = new Trajectory();
            FlightSimulator.Simulate(Straight(), wells, 2, t);
            var into = new Influence[2];
            WellInfluence.Compute(t, wells, 2, into);
            Assert.IsTrue(into[0].Valid && into[1].Valid);
            Assert.AreNotEqual(into[0].Point, into[1].Point);
        }
    }
}
