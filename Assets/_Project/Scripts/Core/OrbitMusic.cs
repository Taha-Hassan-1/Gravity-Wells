using System;

namespace GravityWells.Core
{
    // The game's own track, "Orbit": slow and spacious, built around one five-note motif that rises a fifth
    // and then circles back, like a body in orbit, answered a bar later and ending each pair of phrases on
    // a falling slide (the "gravity" sound of the game). A plucked bell carries the motif, with a long echo
    // that is part of the tune; a low drone moves between D and G and A under it; the pad is only a whisper.
    // D Dorian, 66 beats a minute, eight bars, written into a circular buffer so the loop has no seam.
    // Pure maths on float arrays, so it is measured by tests (loudness, clipping, the join) and generated
    // on a background thread the first time it is asked for.
    public static class OrbitMusic
    {
        public const int BarsInLoop = 8;
        public const int BeatsPerBar = 4;
        public const double BeatsPerMinute = 66.0;

        private const double EchoBeats = 0.75;       // a dotted-eighth echo
        private const int EchoCount = 3;
        private const float EchoGain = 0.46f;

        public static int LoopSamples(int sampleRate) => (int)Math.Round(BarsInLoop * BeatsPerBar * 60.0 / BeatsPerMinute * sampleRate);

        public static float[] Generate(int sampleRate)
        {
            int length = LoopSamples(sampleRate);
            double beat = 60.0 / BeatsPerMinute;
            int At(double beats) => (int)Math.Round(beats * beat * sampleRate);

            var melody = new float[length];
            var bass = new float[length];
            var pad = new float[length];
            var sweeps = new float[length];

            // The motif, as (beat in the loop, midi note, loudness). Bars are four beats long.
            // Phrase A: rises a fifth, steps down, comes home.
            Motif(melody, sampleRate, At, 0 * 4, 69, 74, 72, 69, 67, 1.0f);
            // Phrase B: the same start, and then it climbs instead of falling: the question.
            Motif(melody, sampleRate, At, 2 * 4, 69, 74, 76, 77, 76, 0.95f);
            // Phrase A again, an octave up and quieter, with the answer a beat behind it.
            Motif(melody, sampleRate, At, 4 * 4, 81, 86, 84, 81, 79, 0.55f);
            Motif(melody, sampleRate, At, 4 * 4 + 1, 69, 74, 72, 69, 67, 0.7f);
            // Phrase B once more, resolving: the climb falls back to the root.
            Motif(melody, sampleRate, At, 6 * 4, 69, 74, 76, 74, 72, 0.9f);
            MusicDsp.AddPluck(melody, At(7 * 4 + 2), MusicDsp.Hz(62), 0.8f, sampleRate, 1.1, 2.4); // D4, a long settle

            // A few high drops of light between the phrases.
            double[] sparkle = { 5 * 4 + 3, 6 * 4 + 2.5, 7 * 4 + 0.5 };
            double[] sparkleNotes = { 93, 98, 91 }; // A6 D7 G6
            for (int i = 0; i < sparkle.Length; i++)
            {
                MusicDsp.AddPluck(melody, At(sparkle[i]), MusicDsp.Hz(sparkleNotes[i]), 0.16f, sampleRate, 0.35, 1.0);
            }

            // The echo is part of the tune: delayed copies of the melody, wrapped round the loop.
            AddEcho(melody, At(EchoBeats));

            // The drone: D for two bars, G for two, D for two, A for two (which leads back to D).
            double[] droneNotes = { 38, 43, 38, 45 };
            for (int i = 0; i < droneNotes.Length; i++)
            {
                int start = At(i * 8);
                int span = At(8) + At(0.6); // each note overlaps the next, so there is never a gap
                MusicDsp.AddTone(bass, start, span, MusicDsp.Hz(droneNotes[i]), 0.34f, MusicDsp.Wave.Sine, sampleRate, 0.5, 1.4);
                MusicDsp.AddTone(bass, start, span, MusicDsp.Hz(droneNotes[i] + 12), 0.08f, MusicDsp.Wave.Triangle, sampleRate, 0.8, 1.4);
            }

            // The pad: a whisper of triangle chords, softened, so the plucks have somewhere to sit.
            double[][] chords =
            {
                new double[] { 50, 53, 57 }, // D F A
                new double[] { 55, 59, 62 }, // G B D
                new double[] { 50, 53, 57 },
                new double[] { 57, 60, 64 }, // A C E
            };
            for (int i = 0; i < chords.Length; i++)
            {
                int start = At(i * 8);
                int span = At(8) + At(0.5);
                for (int n = 0; n < chords[i].Length; n++)
                {
                    MusicDsp.AddTone(pad, start, span, MusicDsp.Hz(chords[i][n]), 0.05f, MusicDsp.Wave.Triangle, sampleRate, 1.2, 1.6);
                    MusicDsp.AddTone(pad, start, span, MusicDsp.Hz(chords[i][n]) * 1.004, 0.03f, MusicDsp.Wave.Triangle, sampleRate, 1.2, 1.6);
                }
            }
            MusicDsp.LowPass(pad, sampleRate, 900.0);

            // The gravity sound: a slide falling out of the high register at the start of each half.
            AddFall(sweeps, sampleRate, At(0), 1.6, 1100.0, 220.0, 0.10f);
            AddFall(sweeps, sampleRate, At(4 * 4), 1.6, 1100.0, 196.0, 0.09f);
            AddFall(sweeps, sampleRate, At(7 * 4 + 3), 1.3, 880.0, 147.0, 0.07f);

            var mix = new float[length];
            MusicDsp.Mix(mix, melody, 1.0f);
            MusicDsp.Mix(mix, bass, 0.8f);
            MusicDsp.Mix(mix, pad, 0.9f);
            MusicDsp.Mix(mix, sweeps, 1.0f);

            // One shared loudness for every track, rounded off rather than cut if a peak is above the ceiling.
            MusicDsp.Normalize(mix, GameConfig.MusicLoudnessRms);
            return mix;
        }

        // The five notes of the motif starting at a beat: long, then quick, then a held end.
        private static void Motif(float[] buffer, int sampleRate, Func<double, int> at, double startBeat, int n1, int n2, int n3, int n4, int n5, float gain)
        {
            double[] offsets = { 0.0, 1.5, 2.5, 3.0, 4.5 };
            int[] notes = { n1, n2, n3, n4, n5 };
            for (int i = 0; i < notes.Length; i++)
            {
                float amplitude = (i == 1 ? 0.95f : 0.8f) * gain;
                MusicDsp.AddPluck(buffer, at(startBeat + offsets[i]), MusicDsp.Hz(notes[i]), amplitude, sampleRate, 0.55, 2.2);
            }
        }

        // Adds delayed, quieter copies of a buffer back into itself, wrapping round the end.
        private static void AddEcho(float[] buffer, int delaySamples)
        {
            int n = buffer.Length;
            var original = (float[])buffer.Clone();
            float gain = 1f;
            for (int echo = 1; echo <= EchoCount; echo++)
            {
                gain *= EchoGain;
                int shift = (delaySamples * echo) % n;
                for (int i = 0; i < n; i++)
                {
                    buffer[(i + shift) % n] += original[i] * gain;
                }
            }
        }

        // A sine gliding down in pitch while it swells in and fades out.
        private static void AddFall(float[] buffer, int sampleRate, int start, double seconds, double f0, double f1, float amplitude)
        {
            int n = buffer.Length;
            int length = (int)(seconds * sampleRate);
            double phase = 0.0;
            for (int k = 0; k < length; k++)
            {
                double progress = k / (double)length;
                double frequency = f0 * Math.Pow(f1 / f0, progress);
                phase += frequency / sampleRate;
                double envelope = Math.Sin(Math.PI * progress);
                buffer[(start + k) % n] += (float)(Math.Sin(phase * Math.PI * 2.0) * amplitude * envelope * envelope);
            }
        }
    }
}
