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
        public EnvelopeStage Stage { get; set; }
        public int StageSamplesRemaining { get; set; }
        public float CurrentGain { get; set; }
    }

    private enum EnvelopeStage
    {
        Attack,
        Hold1,
        Decay,
        Sustain,
        Hold2,
        Release
    }

    private const int AttackLengthSamplesDefault = 128; // fallback attack if envelope not set
    private readonly int _sampleRate;
    private readonly int _releaseSamples;
    private readonly int _maxVoices;
    private readonly List<Voice> _voices = new();
    private readonly object _lock = new();
    private int _envAttack;
    private int _envHold1;
    private int _envDecay;
    private float _envSustain = 1f;
    private int _envHold2; // -1 => infinite
    private int _envRelease;

    public VoiceMixer(int sampleRate, int releaseSamples, int maxVoices)
    {
        _sampleRate = sampleRate;
        _releaseSamples = Math.Max(1, releaseSamples);
        _maxVoices = Math.Max(1, maxVoices);
        SetEnvelope(new AHDSHRSettings
        {
            AttackMs = 0,
            Hold1Ms = 0,
            DecayMs = 0,
            SustainLevel = 1f,
            Hold2Ms = -1,
            ReleaseMs = releaseSamples * 1000f / sampleRate
        });
    }

    public void SetEnvelope(AHDSHRSettings settings)
    {
        _envAttack = MsToSamples(settings.AttackMs);
        _envHold1 = MsToSamples(settings.Hold1Ms);
        _envDecay = MsToSamples(settings.DecayMs);
        _envSustain = Math.Clamp(settings.SustainLevel, 0f, 1f);
        _envHold2 = settings.Hold2Ms < 0 ? -1 : MsToSamples(settings.Hold2Ms);
        _envRelease = MsToSamples(settings.ReleaseMs);
        if (_envRelease <= 0) _envRelease = _releaseSamples;
    }

    private int MsToSamples(float ms) => ms <= 0 ? 0 : Math.Max(1, (int)Math.Round(ms * _sampleRate / 1000f));

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
                v.Stage = EnvelopeStage.Release;
                v.StageSamplesRemaining = _envRelease > 0 ? _envRelease : _releaseSamples;
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
                Stage = EnvelopeStage.Attack,
                StageSamplesRemaining = _envAttack > 0 ? _envAttack : AttackLengthSamplesDefault,
                CurrentGain = 0f
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
                voice.Stage = EnvelopeStage.Release;
                voice.StageSamplesRemaining = _envRelease > 0 ? _envRelease : _releaseSamples;
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

                float gain = AdvanceEnvelope(voice);
                sample += waveSample * gain;
                voice.Phase += voice.PhaseIncrement;
                if (voice.Phase >= len)
                    voice.Phase -= len;
            }

            // Pure linear mix (no scaling/clamping) to match ideal sums
            buffer[i] = sample;
        }

        lock (_lock)
        {
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                var voice = _voices[v];
                if (voice.Stage == EnvelopeStage.Release && voice.StageSamplesRemaining <= 0 && voice.CurrentGain <= 0f)
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

    private float AdvanceEnvelope(Voice voice)
    {
        int attackSamples = _envAttack > 0 ? _envAttack : AttackLengthSamplesDefault;
        int releaseSamples = _envRelease > 0 ? _envRelease : _releaseSamples;

        switch (voice.Stage)
        {
            case EnvelopeStage.Attack:
                voice.CurrentGain += 1f / Math.Max(1, attackSamples);
                voice.StageSamplesRemaining--;
                if (voice.StageSamplesRemaining <= 0)
                {
                    voice.Stage = EnvelopeStage.Hold1;
                    voice.StageSamplesRemaining = _envHold1;
                }
                break;
            case EnvelopeStage.Hold1:
                if (_envHold1 == 0)
                {
                    voice.Stage = EnvelopeStage.Decay;
                    voice.StageSamplesRemaining = _envDecay;
                }
                else if (voice.StageSamplesRemaining <= 0)
                {
                    voice.Stage = EnvelopeStage.Decay;
                    voice.StageSamplesRemaining = _envDecay;
                }
                else
                {
                    voice.StageSamplesRemaining--;
                }
                break;
            case EnvelopeStage.Decay:
                if (_envDecay == 0)
                {
                    voice.CurrentGain = _envSustain;
                    voice.Stage = EnvelopeStage.Hold2;
                    voice.StageSamplesRemaining = _envHold2;
                }
                else
                {
                    float step = (voice.CurrentGain - _envSustain) / Math.Max(1, _envDecay);
                    voice.CurrentGain = Math.Max(_envSustain, voice.CurrentGain - step);
                    voice.StageSamplesRemaining--;
                    if (voice.StageSamplesRemaining <= 0)
                    {
                        voice.Stage = EnvelopeStage.Hold2;
                        voice.StageSamplesRemaining = _envHold2;
                    }
                }
                break;
            case EnvelopeStage.Sustain:
                voice.CurrentGain = _envSustain;
                break;
            case EnvelopeStage.Hold2:
                voice.CurrentGain = _envSustain;
                if (_envHold2 >= 0)
                {
                    voice.StageSamplesRemaining--;
                    if (voice.StageSamplesRemaining <= 0)
                    {
                        voice.Stage = EnvelopeStage.Release;
                        voice.StageSamplesRemaining = releaseSamples;
                    }
                }
                break;
            case EnvelopeStage.Release:
                if (releaseSamples == 0)
                {
                    voice.CurrentGain = 0f;
                    voice.StageSamplesRemaining = 0;
                }
                else
                {
                    float step = voice.CurrentGain / Math.Max(1, releaseSamples);
                    voice.CurrentGain = Math.Max(0f, voice.CurrentGain - step);
                    voice.StageSamplesRemaining--;
                }
                break;
        }

        if (voice.Stage == EnvelopeStage.Hold1 && _envHold1 == 0)
        {
            voice.Stage = EnvelopeStage.Decay;
            voice.StageSamplesRemaining = _envDecay;
        }
        if (voice.Stage == EnvelopeStage.Hold2 && _envHold2 < 0)
        {
            // infinite sustain until release
            voice.StageSamplesRemaining = int.MaxValue;
        }

        voice.CurrentGain = Math.Clamp(voice.CurrentGain, 0f, 1f);
        return voice.CurrentGain;
    }
}

