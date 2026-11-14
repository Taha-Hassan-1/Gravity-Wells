using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Services
{
    // Procedurally synthesized sounds — no imported audio assets, matching the
    // project's near-zero-art-cost approach. AudioSource.pitch shifts the tick's
    // perceived frequency per play rather than resynthesizing a clip each call.
    public sealed class AudioService
    {
        private readonly AudioSource _source;
        private readonly AudioClip _tickClip;
        private readonly AudioClip _chordClip;
        private readonly AudioClip _clickClip;

        // The game's sound effects are generated on a background thread at start-up (a second or so of maths
        // that must not stall the first frame) and turned into clips as they finish. A sound asked for before
        // it is ready is simply skipped.
        private const int VoiceCount = 6;
        private readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        private readonly AudioSource _whooshSource;
        private readonly Dictionary<SfxKind, Task<float[]>> _sfxTasks = new Dictionary<SfxKind, Task<float[]>>();
        private readonly Dictionary<SfxKind, AudioClip> _sfx = new Dictionary<SfxKind, AudioClip>();
        private int _nextVoice;
        private bool _whooshWanted;
        private float _whooshSpeed;

        private const float MusicFadeSeconds = 1.2f;

        private readonly AudioSource _musicSource;
        private readonly Dictionary<MusicTrack, Task<float[]>> _musicTasks = new Dictionary<MusicTrack, Task<float[]>>();
        private readonly Dictionary<MusicTrack, AudioClip> _musicClips = new Dictionary<MusicTrack, AudioClip>();
        private MusicTrack _wanted = MusicTrack.None;
        private MusicTrack _playing = MusicTrack.None;
        private float _pressure;
        private float _musicLevel;

        public AudioService(AudioSource source)
        {
            _source = source;
            _musicSource = source.gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = 0f;
            _tickClip = BuildTickClip();
            _chordClip = BuildChordClip();
            _clickClip = BuildClickClip();

            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = source.gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _whooshSource = source.gameObject.AddComponent<AudioSource>();
            _whooshSource.loop = true;
            _whooshSource.playOnAwake = false;
            _whooshSource.volume = 0f;

            foreach (SfxKind kind in System.Enum.GetValues(typeof(SfxKind)))
            {
                SfxKind captured = kind;
                _sfxTasks[kind] = Task.Run(() => SfxLibrary.Generate(captured));
            }
        }

        public bool Enabled { get; set; } = true;

        // The background music has its own switch; Enabled (the Sound toggle) silences everything.
        public bool MusicEnabled { get; set; } = true;

        // Asks for a track. Each loop is generated on a background thread the first time it is wanted and
        // kept; the game never waits for it. Changing track fades the old one out first.
        public void PlayMusic(MusicTrack track)
        {
            _wanted = track;
            if (track != MusicTrack.None && !_musicClips.ContainsKey(track) && !_musicTasks.ContainsKey(track))
            {
                _musicTasks[track] = Task.Run(() => MusicLibrary.Generate(track));
            }
        }

        public void StopMusic() => _wanted = MusicTrack.None;

        // How tense the moment is, 0..1. The tense tracks play faster and louder as it rises.
        public void SetMusicPressure(float pressure) => _pressure = Mathf.Clamp01(pressure);

        // Call every frame: finishes any loop that has been generated, and fades and switches the music.
        public void Tick(float unscaledDeltaTime)
        {
            CollectGeneratedSfx();
            TickWhoosh();
            CollectGeneratedMusic();

            bool wantedReady = _wanted != MusicTrack.None && _musicClips.ContainsKey(_wanted);
            bool audible = Enabled && MusicEnabled && wantedReady;

            if (_playing != _wanted)
            {
                // Fade what is playing out, then switch.
                _musicLevel = Mathf.MoveTowards(_musicLevel, 0f, unscaledDeltaTime / MusicFadeSeconds);
                _musicSource.volume = _musicLevel;
                if (_musicLevel > 0.001f && _musicSource.isPlaying)
                {
                    return;
                }

                _musicSource.Stop();
                _musicLevel = 0f; // the next track always fades in from silence
                _playing = MusicTrack.None;
                if (audible)
                {
                    _musicSource.clip = _musicClips[_wanted];
                    _musicSource.Play();
                    _playing = _wanted;
                }
                return;
            }

            if (_playing == MusicTrack.None)
            {
                return;
            }

            float target = audible ? MusicLibrary.VolumeFor(_playing, _pressure) : 0f;
            _musicLevel = Mathf.MoveTowards(_musicLevel, target, unscaledDeltaTime / MusicFadeSeconds);
            _musicSource.volume = _musicLevel;
            _musicSource.pitch = MusicLibrary.PitchFor(_playing, _pressure);

            if (!audible && _musicLevel <= 0f && _musicSource.isPlaying)
            {
                _musicSource.Stop(); // sound or music was switched off; it restarts when they come back
                _playing = MusicTrack.None;
            }
        }

        private void CollectGeneratedMusic()
        {
            if (_musicTasks.Count == 0)
            {
                return;
            }

            List<MusicTrack> done = null;
            foreach (KeyValuePair<MusicTrack, Task<float[]>> entry in _musicTasks)
            {
                if (!entry.Value.IsCompleted)
                {
                    continue;
                }
                (done ?? (done = new List<MusicTrack>())).Add(entry.Key);
                if (!entry.Value.IsFaulted)
                {
                    float[] samples = entry.Value.Result;
                    AudioClip clip = AudioClip.Create("Music" + entry.Key, samples.Length, 1, MusicLibrary.SampleRate, false);
                    clip.SetData(samples, 0);
                    _musicClips[entry.Key] = clip;
                }
                // A failed build just means that track has no music; nothing else depends on it.
            }

            if (done != null)
            {
                foreach (MusicTrack track in done)
                {
                    _musicTasks.Remove(track);
                }
            }
        }

        // Plays a game sound effect. pitch 1 is the generated pitch (the gate notes are raised up a scale this way);
        // volume 0..1. Silent while Sound is off, and skipped if the sound has not finished generating.
        public void PlaySfx(SfxKind kind, float pitch = 1f, float volume = 1f)
        {
            if (!Enabled || !_sfx.TryGetValue(kind, out AudioClip clip))
            {
                return;
            }
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % VoiceCount;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.clip = clip;
            voice.Play();
        }

        // The sound of the flight: air going past, louder and higher the faster the ship goes. Start when the
        // ship leaves, feed it the speed every frame, stop when the flight ends.
        public void StartWhoosh()
        {
            _whooshWanted = true;
            _whooshSpeed = 0f;
        }

        public void SetWhooshSpeed(float speed) => _whooshSpeed = speed;

        public void StopWhoosh() => _whooshWanted = false;

        private void TickWhoosh()
        {
            bool audible = Enabled && _whooshWanted && _sfx.ContainsKey(SfxKind.Whoosh);
            if (!audible)
            {
                if (_whooshSource.isPlaying)
                {
                    _whooshSource.volume = Mathf.MoveTowards(_whooshSource.volume, 0f, 0.15f);
                    if (_whooshSource.volume <= 0.001f)
                    {
                        _whooshSource.Stop();
                    }
                }
                return;
            }

            if (!_whooshSource.isPlaying)
            {
                _whooshSource.clip = _sfx[SfxKind.Whoosh];
                _whooshSource.volume = 0f;
                _whooshSource.Play();
            }
            _whooshSource.pitch = WhooshCurve.PitchFor(_whooshSpeed);
            _whooshSource.volume = Mathf.MoveTowards(_whooshSource.volume, WhooshCurve.VolumeFor(_whooshSpeed), 0.2f);
        }

        private void CollectGeneratedSfx()
        {
            if (_sfxTasks.Count == 0)
            {
                return;
            }

            List<SfxKind> done = null;
            foreach (KeyValuePair<SfxKind, Task<float[]>> entry in _sfxTasks)
            {
                if (!entry.Value.IsCompleted)
                {
                    continue;
                }
                (done ?? (done = new List<SfxKind>())).Add(entry.Key);
                if (!entry.Value.IsFaulted)
                {
                    float[] samples = entry.Value.Result;
                    AudioClip clip = AudioClip.Create("Sfx" + entry.Key, samples.Length, 1, SfxLibrary.SampleRate, false);
                    clip.SetData(samples, 0);
                    _sfx[entry.Key] = clip;
                }
                // A failed build just means that sound is missing; nothing else depends on it.
            }

            if (done != null)
            {
                foreach (SfxKind kind in done)
                {
                    _sfxTasks.Remove(kind);
                }
            }
        }

        public void PlayTick(float pitch)
        {
            if (!Enabled)
            {
                return;
            }
            _source.pitch = pitch;
            _source.PlayOneShot(_tickClip);
        }

        public const float ClickStartHz = 1500f;
        public const float ClickEndHz = 950f;
        public const float ClickVolume = 0.55f;

        // The UI tap: a soft pop, quieter than the board tick but in a range a phone speaker plays.
        public void PlayClick()
        {
            if (!Enabled)
            {
                return;
            }
            _source.pitch = 1f;
            _source.PlayOneShot(_clickClip, ClickVolume);
        }

        public void PlayChord()
        {
            if (!Enabled)
            {
                return;
            }
            _source.pitch = 1f;
            _source.PlayOneShot(_chordClip);
        }

        private static AudioClip BuildTickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.06f;
            const float frequency = 880f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount; // linear decay
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            var clip = AudioClip.Create("Tick", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildClickClip()
        {
            const int sampleRate = 44100;
            // A short "pop": the pitch falls from ClickStartHz to ClickEndHz as it dies away. The old
            // click was a 520 Hz sine, which a phone speaker (weak below about 700 Hz) barely plays.
            const float duration = 0.05f;
            const float attack = 0.002f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float progress = (float)i / sampleCount;
                float t = (float)i / sampleRate;
                float frequency = Mathf.Lerp(ClickStartHz, ClickEndHz, Mathf.Sqrt(progress));
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float envelope = Mathf.Min(1f, t / attack) * Mathf.Pow(1f - progress, 2.5f);
                samples[i] = Mathf.Sin(phase) * envelope;
            }

            var clip = AudioClip.Create("Click", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildChordClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.8f;
            float[] frequencies = { 261.63f, 329.63f, 392.00f }; // C4 E4 G4 major chord
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-3f * t);
                float sum = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * frequencies[f] * t);
                }
                samples[i] = (sum / frequencies.Length) * envelope;
            }

            var clip = AudioClip.Create("Chord", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
