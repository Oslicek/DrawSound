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
        public int Position { get; set; }
        public bool Releasing { get; set; }
        public int ReleaseSamplesRemaining { get; set; }
    }

    private readonly int _releaseSamples;
    private readonly int _maxVoices;
    private readonly List<Voice> _voices = new();
    private readonly object _lock = new();

    public VoiceMixer(int releaseSamples, int maxVoices)
    {
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
                Position = 0,
                Releasing = false,
                ReleaseSamplesRemaining = _releaseSamples
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
                    if (voice.Position >= cloned.Length)
                    {
                        voice.Position %= cloned.Length;
                    }
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

                float gain = voice.Releasing
                    ? Math.Max(0f, voice.ReleaseSamplesRemaining / (float)_releaseSamples)
                    : 1f;

                sample += table[voice.Position % len] * gain;
                voice.Position = (voice.Position + 1) % len;

                if (voice.Releasing && voice.ReleaseSamplesRemaining > 0)
                {
                    voice.ReleaseSamplesRemaining--;
                }
            }

            buffer[i] = Math.Clamp(sample, -1f, 1f);
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
}

