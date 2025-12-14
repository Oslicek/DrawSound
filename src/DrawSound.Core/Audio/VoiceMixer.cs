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
        public int ReleaseStartIndex { get; set; }
    }

    private const int AttackLengthSamples = 256; // ~6ms Hann attack
    private const int ReleaseRampSamples = 256;  // ~6ms Hann release
    private const int ExtraReleaseSamples = 0;
    private const int MixCrossfadeSamples = 32;
    private readonly int _sampleRate;
    private readonly int _releaseSamples;
    private readonly int _maxVoices;
    private readonly List<Voice> _voices = new();
    private readonly object _lock = new();
    private float _lastOutput;
    private float _prevOutput;
    private int _mixCrossfadeRemaining;
    private int _lastVoiceCount;

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
                v.ReleaseSamplesRemaining = ReleaseRampSamples + ExtraReleaseSamples;
                v.ReleaseStartIndex = FindNearestZeroCross(v.WaveTable, (int)v.Phase);
            }
            _mixCrossfadeRemaining = MixCrossfadeSamples;
            _lastVoiceCount = _voices.Count;
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
                Phase = FindBestStartPhase(cloned),
                PhaseIncrement = CalcPhaseIncrement(frequency, cloned.Length),
                Releasing = false,
                ReleaseSamplesRemaining = ReleaseRampSamples + ExtraReleaseSamples,
                AttackSamplesRemaining = AttackLengthSamples,
                ReleaseStartIndex = 0
            });
            _mixCrossfadeRemaining = MixCrossfadeSamples;
            _lastVoiceCount = _voices.Count;
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
                voice.ReleaseSamplesRemaining = ReleaseRampSamples + ExtraReleaseSamples;
                voice.ReleaseStartIndex = FindNearestZeroCross(voice.WaveTable, (int)voice.Phase);
            }
            _mixCrossfadeRemaining = MixCrossfadeSamples;
            _lastVoiceCount = _voices.Count;
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

        if (snapshot.Length != _lastVoiceCount)
        {
            _mixCrossfadeRemaining = MixCrossfadeSamples;
            _lastVoiceCount = snapshot.Length;
        }

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

                float gain;
                if (voice.Releasing)
                {
                    // Hann window from 1 -> 0 over ReleaseRampSamples
                    float t = 1f - (voice.ReleaseSamplesRemaining / (float)ReleaseRampSamples);
                    gain = 0.5f * (1f + MathF.Cos(MathF.PI * t)); // cos from 1->0
                }
                else
                {
                    // Hann attack from 0 -> 1 over AttackLengthSamples
                    if (voice.AttackSamplesRemaining > 0)
                    {
                        float t = 1f - (voice.AttackSamplesRemaining / (float)AttackLengthSamples);
                        gain = 0.5f * (1f - MathF.Cos(MathF.PI * t));
                    }
                    else
                    {
                        gain = 1f;
                    }
                }

                if (!voice.Releasing && voice.AttackSamplesRemaining > 0)
                {
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

            // Equal-power headroom with gentle soft clip to avoid resonance/clip spikes
            float mixScale = 0.6f / MathF.Max(1f, MathF.Sqrt(snapshot.Length));
            float driven = sample * mixScale;
            float limited = MathF.Tanh(driven); // smooth limiting

            if (_mixCrossfadeRemaining > 0)
            {
                float t = 1f - (_mixCrossfadeRemaining / (float)MixCrossfadeSamples);
                limited = _prevOutput + t * (limited - _prevOutput);
                _mixCrossfadeRemaining--;
            }

            // Light output smoothing to kill residual clicks between buffers
            float smoothed = _lastOutput + 0.2f * (limited - _lastOutput);
            buffer[i] = smoothed;
            _prevOutput = smoothed;
            _lastOutput = smoothed;
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

    private float FindBestStartPhase(float[] table)
    {
        int bestIndex = 0;
        float bestVal = MathF.Abs(table[0]);
        for (int i = 1; i < table.Length; i++)
        {
            float v = MathF.Abs(table[i]);
            if (v < bestVal)
            {
                bestVal = v;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private int FindNearestZeroCross(float[] table, int startIndex)
    {
        int len = table.Length;
        int best = startIndex;
        float bestVal = MathF.Abs(table[startIndex % len]);
        // search up to one cycle for nearest zero crossing
        for (int i = 1; i < len; i++)
        {
            int idx = (startIndex + i) % len;
            float v = MathF.Abs(table[idx]);
            if (v < bestVal)
            {
                bestVal = v;
                best = idx;
                if (bestVal < 1e-4f) break;
            }
        }
        return best;
    }
}

