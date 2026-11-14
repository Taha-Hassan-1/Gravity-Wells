using System;

namespace GravityWells.Core
{
    // The small synthesis toolkit the generated tracks are built from: tones, drum hits, a pluck and
    // noise, each added into a circular buffer (anything that runs past the end wraps to the start, so
    // loops have no seam), plus loudness normalisation. Pure maths on float arrays, no engine types.
    public static class MusicDsp
    {
        public enum Wave
        {
            Sine,
            Triangle,
            Saw,
            Square,
        }

        public static double Hz(double midiNote) => 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);

        // One cycle of a wave at phase (in cycles), -1..1.
        public static double Osc(Wave wave, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (wave)
            {
                case Wave.Sine: return Math.Sin(p * Math.PI * 2.0);
                case Wave.Triangle: return 4.0 * Math.Abs(p - 0.5) - 1.0;
                case Wave.Saw: return 2.0 * p - 1.0;
                default: return p < 0.5 ? 1.0 : -1.0;
            }
        }

        // A held note with a short attack and release so it never clicks.
        public static void AddTone(
            float[] buffer, int start, int length, double frequency, float amplitude, Wave wave, int sampleRate,
            double attackSeconds = 0.004, double releaseSeconds = 0.03)
        {
            int n = buffer.Length;
            int attack = Math.Max(1, (int)(attackSeconds * sampleRate));
            int release = Math.Max(1, (int)(releaseSeconds * sampleRate));
            double step = frequency / sampleRate;
            double phase = 0.0;

            for (int k = 0; k < length; k++)
            {
                double env = Math.Min(1.0, (k + 1) / (double)attack) * Math.Min(1.0, (length - k) / (double)release);
                buffer[(start + k) % n] += (float)(Osc(wave, phase) * amplitude * env);
                phase += step;
            }
        }

        // A kick: a sine that falls from a thump to a low note while it fades.
        public static void AddKick(float[] buffer, int start, int sampleRate, float amplitude, double seconds = 0.2)
        {
            int n = buffer.Length;
            int length = (int)(seconds * sampleRate);
            double phase = 0.0;
            for (int k = 0; k < length; k++)
            {
                double t = k / (double)sampleRate;
                double frequency = 42.0 + 110.0 * Math.Exp(-t * 28.0);
                phase += frequency / sampleRate;
                double env = Math.Exp(-t * 14.0) * Math.Min(1.0, k / 24.0);
                buffer[(start + k) % n] += (float)(Math.Sin(phase * Math.PI * 2.0) * amplitude * env);
            }
        }

        // A burst of noise with an exponential decay; highpassed it reads as a hat or a snare's snap.
        public static void AddNoise(
            float[] buffer, int start, double seconds, float amplitude, int sampleRate, Random random,
            double decayPerSecond, bool highpass = true)
        {
            int n = buffer.Length;
            int length = (int)(seconds * sampleRate);
            double low = 0.0;
            for (int k = 0; k < length; k++)
            {
                double t = k / (double)sampleRate;
                double x = random.NextDouble() * 2.0 - 1.0;
                low += 0.3 * (x - low);
                double value = highpass ? x - low : x;
                buffer[(start + k) % n] += (float)(value * amplitude * Math.Exp(-t * decayPerSecond));
            }
        }

        // A plucked bell: a few partials that die away at different rates, like a kalimba tine.
        public static void AddPluck(
            float[] buffer, int start, double frequency, float amplitude, int sampleRate, double decaySeconds, double lengthSeconds)
        {
            int n = buffer.Length;
            int length = (int)(lengthSeconds * sampleRate);
            double twoPiOverRate = Math.PI * 2.0 / sampleRate;
            double nyquist = sampleRate * 0.5;

            for (int k = 0; k < length; k++)
            {
                double t = k / (double)sampleRate;
                double attack = Math.Min(1.0, t / 0.003);
                double value = Math.Sin(twoPiOverRate * frequency * k) * Math.Exp(-t / decaySeconds);
                if (frequency * 2.0 < nyquist)
                {
                    value += 0.32 * Math.Sin(twoPiOverRate * frequency * 2.0 * k) * Math.Exp(-t / (decaySeconds * 0.55));
                }
                if (frequency * 5.4 < nyquist)
                {
                    value += 0.09 * Math.Sin(twoPiOverRate * frequency * 5.4 * k) * Math.Exp(-t / (decaySeconds * 0.16));
                }
                buffer[(start + k) % n] += (float)(value * amplitude * attack);
            }
        }

        // A one-pole lowpass over a whole buffer (cutoff in Hz), to take the buzz off a raw saw or square.
        public static void LowPass(float[] buffer, int sampleRate, double cutoffHz)
        {
            double alpha = 1.0 - Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
            double state = buffer.Length > 0 ? buffer[buffer.Length - 1] : 0.0; // start from the loop's end so it joins smoothly
            for (int i = 0; i < buffer.Length; i++)
            {
                state += alpha * (buffer[i] - state);
                buffer[i] = (float)state;
            }
        }

        // Adds source into target, scaled.
        public static void Mix(float[] target, float[] source, float gain)
        {
            for (int i = 0; i < target.Length; i++)
            {
                target[i] += source[i] * gain;
            }
        }

        // Scales the music to a target loudness (RMS) and then rounds off any peak above the ceiling with
        // a soft curve instead of cutting it, so it is loud enough to hear over a phone speaker yet never
        // clips. This is what keeps a quiet, sparse track from sounding faint beside a dense one.
        public static void Normalize(float[] samples, float targetRms = 0.2f, float ceiling = 0.95f)
        {
            double sum = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * (double)samples[i];
            }
            double rms = Math.Sqrt(sum / Math.Max(1, samples.Length));
            if (rms <= 1e-9)
            {
                return;
            }

            float gain = (float)(targetRms / rms);
            for (int i = 0; i < samples.Length; i++)
            {
                float x = samples[i] * gain;
                samples[i] = (float)(ceiling * Math.Tanh(x / ceiling));
            }
        }
    }
}
