using System;

namespace GravityWells.Core
{
    public enum SfxKind
    {
        WellPickUp,   // a well leaves the tray or is picked up
        WellDrop,     // a well is put down: a soft thump and a settling note
        GateNote,     // a gate lights; played at a rising pitch for each gate (GateScale)
        Launch,       // the ship leaves the pad
        Whoosh,       // the flight, looped, with volume and pitch following the ship
        Win,          // rising notes landing on a held chord
        Fail,         // two sagging slides: the flight did not make it
        GoalChord,    // the soft chord that holds when the line under the finger has found the goal
    }

    // Every sound effect, synthesised from maths (no audio files, so no copyright to clear). One-shots have a
    // fast fade at both ends so they never click and are peak-normalised to the same level so one effect is
    // never louder than another; the looped whoosh is levelled to a quieter peak and joins itself without a seam.
    // Everything is in D, with the gate notes on a pentatonic scale, so any combination sounds right together.
    public static class SfxLibrary
    {
        public const int SampleRate = 44100;
        public const float Peak = 0.85f;
        public const float LoopPeak = 0.6f;

        private const double FadeInSeconds = 0.002;
        private const double FadeOutSeconds = 0.010;
        private const double LoopSeconds = 1.4;
        private const double LoopCrossfadeSeconds = 0.25;

        public static bool IsLoop(SfxKind kind) => kind == SfxKind.Whoosh;

        public static float[] Generate(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.WellPickUp: return WellPickUp();
                case SfxKind.WellDrop: return WellDrop();
                case SfxKind.GateNote: return GateNote();
                case SfxKind.Launch: return Launch();
                case SfxKind.Whoosh: return Whoosh();
                case SfxKind.Win: return Win();
                case SfxKind.Fail: return Fail();
                default: return GoalChord();
            }
        }

        // A small upward bloop: the well lifts.
        private static float[] WellPickUp()
        {
            float[] b = Buffer(0.14);
            AddSweep(b, 0, 0.11, 330.0, 660.0, 0.7f, 15.0);
            MusicDsp.AddNoise(b, 0, 0.03, 0.10f, SampleRate, new Random(11), 90.0);
            return Finish(b);
        }

        // A soft thump and a note that settles: the well is down.
        private static float[] WellDrop()
        {
            float[] b = Buffer(0.34);
            AddSweep(b, 0, 0.2, 250.0, 115.0, 0.8f, 11.0);
            MusicDsp.AddPluck(b, 0, MusicDsp.Hz(62), 0.35f, SampleRate, 0.12, 0.3); // D4
            return Finish(b);
        }

        // A bell on D5 with a faint octave above. The player raises it up the scale with the playback pitch.
        private static float[] GateNote()
        {
            float[] b = Buffer(0.5);
            MusicDsp.AddPluck(b, 0, MusicDsp.Hz(74), 0.8f, SampleRate, 0.2, 0.46);
            MusicDsp.AddPluck(b, 0, MusicDsp.Hz(86), 0.12f, SampleRate, 0.09, 0.3);
            return Finish(b);
        }

        // A thump, then a swell of air rising as the ship goes.
        private static float[] Launch()
        {
            float[] b = Buffer(0.75);
            MusicDsp.AddKick(b, 0, SampleRate, 0.55f, 0.18);
            AddSweep(b, 0, 0.6, 130.0, 560.0, 0.5f, 2.2);
            MusicDsp.AddNoise(b, (int)(0.02 * SampleRate), 0.4, 0.3f, SampleRate, new Random(12), 5.0, highpass: false);
            MusicDsp.LowPass(b, SampleRate, 2400.0);
            return Finish(b);
        }

        // Air going past, to be looped under the flight. It is noise, softened, with the end faded into the
        // beginning so the join cannot be heard.
        private static float[] Whoosh()
        {
            int loop = (int)(LoopSeconds * SampleRate);
            int fade = (int)(LoopCrossfadeSeconds * SampleRate);
            float[] raw = new float[loop + fade];
            var random = new Random(13);
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }
            MusicDsp.LowPass(raw, SampleRate, 1500.0);
            MusicDsp.LowPass(raw, SampleRate, 900.0);

            // The loop is raw[0..loop); its first `fade` samples are blended with the `fade` samples that would
            // have followed its end, so the end runs straight on into the start.
            float[] b = new float[loop];
            Array.Copy(raw, b, loop);
            for (int i = 0; i < fade; i++)
            {
                double s = 0.5 - 0.5 * Math.Cos(Math.PI * i / fade);
                b[i] = (float)(raw[i] * s + raw[loop + i] * (1.0 - s));
            }
            return Scale(b, LoopPeak);
        }

        // Five rising notes up the scale and then a held chord under them.
        private static float[] Win()
        {
            float[] b = Buffer(1.7);
            double[] notes = { 74, 76, 79, 81, 86 }; // D5 E5 G5 A5 D6
            for (int i = 0; i < notes.Length; i++)
            {
                int start = (int)(i * 0.085 * SampleRate);
                MusicDsp.AddPluck(b, start, MusicDsp.Hz(notes[i]), 0.55f, SampleRate, 0.2, 0.6);
            }
            AddChord(b, (int)(0.3 * SampleRate), 1.3, new double[] { 62, 69, 74, 78 }, 0.2f, 0.01, 0.8);
            return Finish(b);
        }

        // Two sagging slides: it did not make it. Gentle; a miss is free and must not sting.
        private static float[] Fail()
        {
            float[] b = Buffer(0.95);
            AddSweep(b, 0, 0.45, 392.0, 290.0, 0.5f, 3.0);
            AddSweep(b, (int)(0.34 * SampleRate), 0.55, 311.0, 196.0, 0.5f, 3.2);
            MusicDsp.AddKick(b, (int)(0.34 * SampleRate), SampleRate, 0.25f, 0.2);
            MusicDsp.LowPass(b, SampleRate, 1700.0);
            return Finish(b);
        }

        // The "you have it" moment: a soft, slow chord that blooms and then rings out under the finger.
        private static float[] GoalChord()
        {
            float[] b = Buffer(1.6);
            AddChord(b, 0, 1.5, new double[] { 62, 69, 74, 78 }, 0.22f, 0.12, 0.9);
            MusicDsp.AddPluck(b, (int)(0.06 * SampleRate), MusicDsp.Hz(81), 0.16f, SampleRate, 0.25, 0.7);
            return Finish(b);
        }

        private static void AddChord(float[] buffer, int start, double seconds, double[] midi, float amplitude, double attack, double release)
        {
            int length = (int)(seconds * SampleRate);
            for (int i = 0; i < midi.Length; i++)
            {
                MusicDsp.AddTone(buffer, start, length, MusicDsp.Hz(midi[i]), amplitude, MusicDsp.Wave.Sine, SampleRate, attack, release);
                // A touch of the octave and a hair of detune, so the chord shimmers instead of sitting still.
                MusicDsp.AddTone(buffer, start, length, MusicDsp.Hz(midi[i]) * 2.003, amplitude * 0.18f, MusicDsp.Wave.Sine, SampleRate, attack, release);
            }
        }

        // A sine that glides exponentially from f0 to f1 while it decays.
        private static void AddSweep(float[] buffer, int start, double seconds, double f0, double f1, float amplitude, double decayPerSecond)
        {
            int n = buffer.Length;
            int length = (int)(seconds * SampleRate);
            double phase = 0.0;
            for (int k = 0; k < length; k++)
            {
                double t = k / (double)SampleRate;
                double progress = k / (double)length;
                double frequency = f0 * Math.Pow(f1 / f0, progress);
                phase += frequency / SampleRate;
                double envelope = Math.Exp(-t * decayPerSecond) * Math.Min(1.0, k / (0.002 * SampleRate));
                buffer[(start + k) % n] += (float)(Math.Sin(phase * Math.PI * 2.0) * amplitude * envelope);
            }
        }

        private static float[] Buffer(double seconds) => new float[(int)(seconds * SampleRate)];

        // Fades both ends to zero (no click), then scales to the shared peak.
        private static float[] Finish(float[] b)
        {
            int fadeIn = Math.Max(1, (int)(FadeInSeconds * SampleRate));
            int fadeOut = Math.Max(1, (int)(FadeOutSeconds * SampleRate));
            for (int i = 0; i < b.Length; i++)
            {
                double gain = 1.0;
                if (i < fadeIn)
                {
                    gain = Math.Min(gain, (i + 1) / (double)fadeIn);
                }
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < fadeOut)
                {
                    gain = Math.Min(gain, fromEnd / (double)fadeOut);
                }
                b[i] = (float)(b[i] * gain);
            }
            return Scale(b, Peak);
        }

        private static float[] Scale(float[] b, float peakTarget)
        {
            float peak = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                peak = Math.Max(peak, Math.Abs(b[i]));
            }
            if (peak > 1e-6f)
            {
                float scale = peakTarget / peak;
                for (int i = 0; i < b.Length; i++)
                {
                    b[i] *= scale;
                }
            }
            return b;
        }
    }
}
