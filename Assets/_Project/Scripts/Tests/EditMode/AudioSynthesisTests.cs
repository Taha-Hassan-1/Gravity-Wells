using System;
using System.IO;
using NUnit.Framework;
using GravityWells.Core;

namespace GravityWells.Tests
{
    // The sounds are generated, so the only way to know they are usable is to measure them: audible, never
    // clipping, no click at either end, identical every run, a loop that joins itself without a seam, and music
    // that has a tune rather than a steady hum. (Whether it sounds GOOD needs ears: see docs/features/09-audio.md.)
    public class AudioSynthesisTests
    {
        private static readonly SfxKind[] OneShots = Array.FindAll((SfxKind[])Enum.GetValues(typeof(SfxKind)), k => !SfxLibrary.IsLoop(k));
        private static readonly SfxKind[] AllSfx = (SfxKind[])Enum.GetValues(typeof(SfxKind));

        [TestCaseSource(nameof(OneShots))]
        public void EveryOneShot_IsAudible_AndNeverClips(SfxKind kind)
        {
            float[] samples = SfxLibrary.Generate(kind);

            Assert.Greater(samples.Length, SfxLibrary.SampleRate / 20, "at least 50 ms");
            Assert.Less(samples.Length, SfxLibrary.SampleRate * 2, "no effect is longer than 2 s");
            Assert.AreEqual(SfxLibrary.Peak, Peak(samples), 0.01f, "peak-normalised to the shared level");
            Assert.Greater(Rms(samples), 0.02f, kind + " is too quiet to hear");
        }

        [TestCaseSource(nameof(OneShots))]
        public void EveryOneShot_StartsAndEndsAtSilence_SoItNeverClicks(SfxKind kind)
        {
            float[] samples = SfxLibrary.Generate(kind);
            Assert.Less(Math.Abs(samples[0]), 0.02f, "first sample");
            Assert.Less(Math.Abs(samples[samples.Length - 1]), 0.02f, "last sample");
        }

        [TestCaseSource(nameof(AllSfx))]
        public void EverySound_IsIdenticalEveryTime(SfxKind kind)
        {
            CollectionAssert.AreEqual(SfxLibrary.Generate(kind), SfxLibrary.Generate(kind));
        }

        [TestCaseSource(nameof(AllSfx))]
        public void EverySound_HasNoNaNAndNeverExceedsFullScale(SfxKind kind)
        {
            foreach (float sample in SfxLibrary.Generate(kind))
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                Assert.LessOrEqual(Math.Abs(sample), 1f);
            }
        }

        [Test]
        public void TheOneShots_AreAllAboutTheSameLoudness()
        {
            float quiet = float.MaxValue;
            float loud = 0f;
            foreach (SfxKind kind in OneShots)
            {
                float rms = Rms(SfxLibrary.Generate(kind));
                quiet = Math.Min(quiet, rms);
                loud = Math.Max(loud, rms);
            }
            Assert.Less(loud / quiet, 6f, $"loudest/quietest RMS ratio {loud / quiet:F1}");
        }

        [Test]
        public void TheWhoosh_JoinsItselfWithoutAClick_AndIsAudible()
        {
            float[] w = SfxLibrary.Generate(SfxKind.Whoosh);
            Assert.AreEqual(SfxLibrary.LoopPeak, Peak(w), 0.01f);
            Assert.Greater(Rms(w), 0.04f);
            AssertSeamless(w);
        }

        [Test]
        public void TheWhoosh_IsAnEvenTexture_NotATone()
        {
            // Noise has no steady pitch: successive windows have similar loudness (no notes, no pulses).
            float[] w = SfxLibrary.Generate(SfxKind.Whoosh);
            float[] windows = WindowedRms(w, SfxLibrary.SampleRate / 10);
            Assert.Less(CoefficientOfVariation(windows), 0.35f);
        }

        [Test]
        public void TheGateNote_IsAHighBell_AndRisesWithThePlaybackPitch()
        {
            // The same clip at a higher playback pitch plays shorter: the player raises it up the scale this way.
            Assert.Less(1f / GateScale.PitchFor(5), 1f / GateScale.PitchFor(1));
            Assert.Greater(SfxLibrary.Generate(SfxKind.GateNote).Length, SfxLibrary.SampleRate / 4, "long enough to ring");
        }

        // ---- the music ----

        private static float[] _orbit;
        private static float[] Orbit => _orbit ?? (_orbit = MusicLibrary.Generate(MusicTrack.Orbit));

        [Test]
        public void Orbit_IsAudible_AtTheSharedLoudness()
        {
            Assert.AreEqual(GameConfig.MusicLoudnessRms, Rms(Orbit), 0.03f);
        }

        [Test]
        public void Orbit_NeverClips()
        {
            Assert.LessOrEqual(Peak(Orbit), 0.95f + 1e-4f);
        }

        [Test]
        public void Orbit_HasTheLengthOfEightBarsAt66Bpm()
        {
            double seconds = Orbit.Length / (double)MusicLibrary.SampleRate;
            Assert.AreEqual(8 * 4 * 60.0 / 66.0, seconds, 0.05);
            Assert.AreEqual(OrbitMusic.LoopSamples(MusicLibrary.SampleRate), Orbit.Length);
        }

        [Test]
        public void Orbit_JoinsItselfWithoutAClick()
        {
            AssertSeamless(Orbit);
        }

        [Test]
        public void Orbit_IsIdenticalEveryTime()
        {
            CollectionAssert.AreEqual(Orbit, MusicLibrary.Generate(MusicTrack.Orbit));
        }

        [Test]
        public void Orbit_HasATuneInIt_NotASteadyHum()
        {
            // A sustained pad has almost the same loudness in every tenth of a second. A plucked motif has attacks
            // and decays, so the loudness moves a lot from window to window.
            float[] windows = WindowedRms(Orbit, MusicLibrary.SampleRate / 10);
            Assert.Greater(CoefficientOfVariation(windows), 0.25f, "the music is too flat to carry a motif");
        }

        [Test]
        public void Orbit_HasRoomInIt_QuietMomentsBetweenTheNotes()
        {
            float[] windows = WindowedRms(Orbit, MusicLibrary.SampleRate / 4);
            float mean = 0f;
            foreach (float w in windows)
            {
                mean += w;
            }
            mean /= windows.Length;
            int quiet = 0;
            foreach (float w in windows)
            {
                quiet += w < mean * 0.8f ? 1 : 0;
            }
            Assert.Greater(quiet, windows.Length / 5, "spacious means space");
        }

        [Test]
        public void Orbit_HasNothingBelowTheFloor_ItIsNotSilentAnywhereLong()
        {
            // No stretch of a full second is empty: the drone holds the room.
            float[] windows = WindowedRms(Orbit, MusicLibrary.SampleRate);
            foreach (float w in windows)
            {
                Assert.Greater(w, 0.03f);
            }
        }

        [Test]
        public void Orbit_DoesNotRespondToPressure_ItIsTheCalmUnderAPuzzleWithNoClock()
        {
            Assert.IsFalse(MusicLibrary.RespondsToPressure(MusicTrack.Orbit));
            Assert.AreEqual(1f, MusicLibrary.PitchFor(MusicTrack.Orbit, 1f));
            Assert.AreEqual(MusicLibrary.BaseVolume(MusicTrack.Orbit), MusicLibrary.VolumeFor(MusicTrack.Orbit, 1f));
            Assert.Greater(MusicLibrary.BaseVolume(MusicTrack.Orbit), 0f);
            Assert.AreEqual(0, MusicLibrary.Generate(MusicTrack.None).Length);
        }

        // ---- a way to listen ----

        // Not an assertion: writes every sound and the music as WAV files into Logs/Audio (git-ignored) so a person
        // can play them. Runs with the suite so the files are always from the current code.
        [Test]
        public void TheSoundsCanBeExportedAsWavFiles_ToBeListenedTo()
        {
            string folder = Path.Combine("Logs", "Audio");
            Directory.CreateDirectory(folder);
            foreach (SfxKind kind in AllSfx)
            {
                float[] samples = SfxLibrary.Generate(kind);
                if (SfxLibrary.IsLoop(kind))
                {
                    samples = Repeat(samples, 3); // three passes, so the seam is in the file for the ear to find
                }
                string path = Path.Combine(folder, kind + ".wav");
                WriteWav(path, samples, SfxLibrary.SampleRate);
                Assert.Greater(new FileInfo(path).Length, 1000);
            }
            WriteWav(Path.Combine(folder, "Orbit-music.wav"), Repeat(Orbit, 2), MusicLibrary.SampleRate);
            Assert.IsTrue(File.Exists(Path.Combine(folder, "Orbit-music.wav")));
        }

        // ---- helpers ----

        // The step across the join (last sample -> first sample) must look like any other step in the loop.
        private static void AssertSeamless(float[] samples)
        {
            float joinStep = Math.Abs(samples[0] - samples[samples.Length - 1]);
            float maxStep = 0f;
            for (int i = 1; i < samples.Length; i++)
            {
                maxStep = Math.Max(maxStep, Math.Abs(samples[i] - samples[i - 1]));
            }
            Assert.LessOrEqual(joinStep, maxStep, $"join step {joinStep:F4} vs the loop's largest step {maxStep:F4}");
            Assert.Less(joinStep, 0.05f);
        }

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            return peak;
        }

        private static float Rms(float[] samples)
        {
            double sum = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * (double)samples[i];
            }
            return (float)Math.Sqrt(sum / Math.Max(1, samples.Length));
        }

        private static float[] WindowedRms(float[] samples, int window)
        {
            int count = samples.Length / window;
            var result = new float[count];
            for (int w = 0; w < count; w++)
            {
                double sum = 0.0;
                for (int i = 0; i < window; i++)
                {
                    float s = samples[w * window + i];
                    sum += s * (double)s;
                }
                result[w] = (float)Math.Sqrt(sum / window);
            }
            return result;
        }

        private static float CoefficientOfVariation(float[] values)
        {
            double mean = 0.0;
            foreach (float v in values)
            {
                mean += v;
            }
            mean /= values.Length;
            double variance = 0.0;
            foreach (float v in values)
            {
                variance += (v - mean) * (v - mean);
            }
            return (float)(Math.Sqrt(variance / values.Length) / Math.Max(1e-9, mean));
        }

        private static float[] Repeat(float[] samples, int times)
        {
            var result = new float[samples.Length * times];
            for (int i = 0; i < times; i++)
            {
                Array.Copy(samples, 0, result, i * samples.Length, samples.Length);
            }
            return result;
        }

        private static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                foreach (float sample in samples)
                {
                    writer.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, sample)) * 32767f));
                }
            }
        }
    }
}
