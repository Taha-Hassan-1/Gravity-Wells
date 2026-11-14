using System;
using UnityEngine;

namespace GravityWells.Core
{
    public enum FlightOutcome
    {
        Won,
        LeftField,
        HitWell,
        HitAsteroid,
        TimedOut,
    }

    // One simulated flight: the points the ship passes through, how it ended and which gates it lit.
    // Allocated once and refilled by every FlightSimulator.Simulate; the preview draws it and the
    // ship plays it back, so the two can never disagree.
    public sealed class Trajectory
    {
        private readonly int[] _gateLitStep = new int[GameConfig.MaxGates];

        // The simulator's unpacked copy of the wells and asteroids for the flight in progress: plain
        // float arrays are what its 960-step loop reads fastest. Owned here only so they are allocated once.
        internal readonly float[] WellX = new float[GameConfig.MaxWells];
        internal readonly float[] WellY = new float[GameConfig.MaxWells];
        internal readonly float[] WellSign = new float[GameConfig.MaxWells];   // +1 attractor, -1 repulsor
        internal readonly float[] RockX = new float[16];
        internal readonly float[] RockY = new float[16];
        internal readonly float[] RockRadiusSqr = new float[16];

        public Trajectory() : this(GameConfig.TrajectoryCapacity)
        {
        }

        public Trajectory(int capacity)
        {
            Points = new Vector2[capacity];
            Clear();
        }

        public Vector2[] Points { get; }               // Points[0] is the start
        public int Count { get; private set; }
        public int Capacity => Points.Length;
        public FlightOutcome Outcome { get; private set; }
        public int GatesLitMask { get; private set; }  // bit i = gate i
        public int GatesLitCount { get; private set; }

        // The step that lit the last gate; 0 when no gate has been lit.
        public int LastGateLitStep { get; private set; }

        // The step whose segment (Points[step - 1] to Points[step]) crossed the gate; -1 if never lit.
        public int GateLitStep(int gateIndex) => _gateLitStep[gateIndex];

        // Every bit of the flight folded into one number, so two runs (or the Editor and a phone)
        // can be compared without shipping the buffer.
        public int Hash()
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)Count) * 16777619u;
                hash = (hash ^ (uint)Outcome) * 16777619u;
                hash = (hash ^ (uint)GatesLitMask) * 16777619u;
                for (int i = 0; i < Count; i++)
                {
                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(Points[i].x)) * 16777619u;
                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(Points[i].y)) * 16777619u;
                }
                return (int)hash;
            }
        }

        internal void Begin(Vector2 start)
        {
            Clear();
            Points[0] = start;
            Count = 1;
        }

        // The simulator writes Points directly (this is its hot loop) and reports the total at the end.
        internal void Complete(int count, FlightOutcome outcome)
        {
            Count = count;
            Outcome = outcome;
        }

        internal void LightGate(int gateIndex, int step)
        {
            _gateLitStep[gateIndex] = step;
            GatesLitMask |= 1 << gateIndex;
            GatesLitCount++;
            LastGateLitStep = step;
        }

        private void Clear()
        {
            Count = 0;
            Outcome = FlightOutcome.TimedOut;
            GatesLitMask = 0;
            GatesLitCount = 0;
            LastGateLitStep = 0;
            for (int i = 0; i < _gateLitStep.Length; i++)
            {
                _gateLitStep[i] = -1;
            }
        }
    }
}
