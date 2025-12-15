using System.Collections.Concurrent;

namespace DrawSound.Core.Audio;

/// <summary>
/// Simple polyphonic voice mixer with per-voice release handling.
/// </summary>
public class VoiceMixer
{
    private class Voice
    {
        public double Frequency { get; set; }
        public float[] WaveTable { get; set; } = Array.Empty<float>();
        public float Phase { get; set; }
        public float PhaseIncrement { get; set; }
        public bool Releasing { get; set; }
        public int ReleaseSamplesRemaining { get; set; }
        public int AttackSamplesRemaining { get; set; }
    }

    private const int AttackLengthSamples = 64;
    private readonly int _sampleRate;
    private readonly int _releaseSamples;
    private readonly int _maxVoices;
    private readonly List<Voice> _voices = new();
    private readonly object _lock = new();

    public VoiceMixer(int sampleRate, int releaseSamples, int maxVoices)
    {
        _sampleRate = sampleRate;
        _releaseSamples = Math.Max(1, releaseSamples);
        _maxVoices = Math.Max(1, maxVoices);
    }

    public int ActiveVoiceCount
    {
        get
        {
            lock (_lock) return _voices.Count;
        }
    }

    public void ReleaseAll()
    {
        lock (_lock)
        {
            foreach (var v in _voices)
            {
                v.Releasing = true;
                v.ReleaseSamplesRemaining = _releaseSamples;
            }
        }
    }

    public void AddVoice(double frequency, float[] waveTable)
    {
        var cloned = (float[])waveTable.Clone();
        lock (_lock)
        {
            if (_voices.Count >= _maxVoices)
            {
                _voices.RemoveAt(0);
            }

            _voices.Add(new Voice
            {
                Frequency = frequency,
                WaveTable = cloned,
                Phase = 0f,
                PhaseIncrement = CalcPhaseIncrement(frequency, cloned.Length),
                Releasing = false,
                ReleaseSamplesRemaining = _releaseSamples,
                AttackSamplesRemaining = AttackLengthSamples
            });
        }
    }

    public void UpdateVoice(double frequency, float[] waveTable)
    {
        var cloned = (float[])waveTable.Clone();
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (Math.Abs(voice.Frequency - frequency) < 0.0001)
                {
                    voice.WaveTable = cloned;
                    voice.PhaseIncrement = CalcPhaseIncrement(frequency, cloned.Length);
                    if (voice.Phase >= cloned.Length)
                        voice.Phase %= cloned.Length;
                }
            }
        }
    }

    public void ReleaseVoice(double frequency)
    {
        lock (_lock)
        {
            var voice = _voices.FirstOrDefault(v => Math.Abs(v.Frequency - frequency) < 0.0001);
            if (voice != null)
            {
                voice.Releasing = true;
                voice.ReleaseSamplesRemaining = _releaseSamples;
            }
        }
    }

    public void Mix(float[] buffer)
    {
        Array.Clear(buffer, 0, buffer.Length);

        Voice[] snapshot;
        lock (_lock)
        {
            snapshot = _voices.ToArray();
        }

        if (snapshot.Length == 0)
            return;

        for (int i = 0; i < buffer.Length; i++)
        {
            float sample = 0f;

            foreach (var voice in snapshot)
            {
                var table = voice.WaveTable;
                int len = table.Length;
                if (len == 0) continue;

                int idx0 = (int)voice.Phase;
                int idx1 = (idx0 + 1) % len;
                float frac = voice.Phase - idx0;

                float v0 = table[idx0];
                float v1 = table[idx1];
                float waveSample = v0 + (v1 - v0) * frac;

                float gain = voice.Releasing
                    ? Math.Max(0f, voice.ReleaseSamplesRemaining / (float)_releaseSamples)
                    : 1f;

                if (voice.AttackSamplesRemaining > 0)
                {
                    float attackGain = 1f - (voice.AttackSamplesRemaining / (float)AttackLengthSamples);
                    gain *= attackGain;
                    voice.AttackSamplesRemaining--;
                }

                sample += waveSample * gain;
                voice.Phase += voice.PhaseIncrement;
                if (voice.Phase >= len)
                    voice.Phase -= len;

                if (voice.Releasing && voice.ReleaseSamplesRemaining > 0)
                {
                    voice.ReleaseSamplesRemaining--;
                }
            }

            // Pure linear mix (no headroom scaling/clipping) to preserve phase accuracy
            buffer[i] = sample;
        }

        lock (_lock)
        {
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                var voice = _voices[v];
                if (voice.Releasing && voice.ReleaseSamplesRemaining <= 0)
                {
                    _voices.RemoveAt(v);
                }
            }
        }
    }

    private float CalcPhaseIncrement(double frequency, int tableLength)
    {
        return (float)(tableLength * frequency / _sampleRate);
    }
}

