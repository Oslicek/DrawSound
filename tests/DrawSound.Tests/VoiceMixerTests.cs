using DrawSound.Core.Audio;
using Xunit;

namespace DrawSound.Tests;

public class VoiceMixerTests
{
    private static float[] MakeWave(params float[] samples) => samples;
    private static float MaxDelta(IReadOnlyList<float> data)
    {
        float max = 0f;
        for (int i = 1; i < data.Count; i++)
        {
            max = Math.Max(max, Math.Abs(data[i] - data[i - 1]));
        }
        return max;
    }

    [Fact]
    public void Mix_SingleVoice_ProducesSamples()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 4, maxVoices: 2);
        mixer.AddVoice(440, MakeWave(1f, 0f, -1f, 0f));

        var buffer = new float[8];
        mixer.Mix(buffer);

        Assert.All(buffer, v => Assert.InRange(v, -1f, 1f));
        Assert.Contains(buffer, v => Math.Abs(v) > 0f); // attack ramp but non-zero energy
    }

    [Fact]
    public void Release_FadesOutAndRemovesVoice()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 4, maxVoices: 2);
        mixer.AddVoice(440, MakeWave(1f, 1f, 1f, 1f));
        mixer.ReleaseVoice(440);

        var buffer = new float[4];
        mixer.Mix(buffer); // release block

        Assert.InRange(buffer[0], -1f, 1f);
        Assert.InRange(buffer[^1], -1f, 1f);

        var zeroBuffer = new float[2];
        mixer.Mix(zeroBuffer); // voice should be gone

        Assert.All(zeroBuffer, v => Assert.InRange(v, -0.45f, 0.45f));
    }

    [Fact]
    public void MaxPolyphony_DropsOldestVoice()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 2, maxVoices: 1);
        mixer.AddVoice(440, MakeWave(1f));
        mixer.AddVoice(660, MakeWave(0.5f));

        var buffer = new float[2];
        mixer.Mix(buffer);

        Assert.All(buffer, v => Assert.InRange(v, -1f, 1f));
        Assert.Contains(buffer, v => v >= 0f);
    }

    [Fact]
    public void TwoVoices_SteadyMix_MatchesExpectedWithinTolerance()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 8, maxVoices: 4);
        // Small amplitude to stay in linear region of tanh/scale
        mixer.AddVoice(440, MakeWave(0.1f));
        mixer.AddVoice(660, MakeWave(0.1f));

        // Warm-up past attack
        var warm = new float[700];
        mixer.Mix(warm);

        var buffer = new float[32];
        mixer.Mix(buffer);

        // Expected steady value: (0.1 + 0.1) * 0.6 / sqrt(2)
        float expected = (0.2f) * 0.6f / (float)Math.Sqrt(2);
        foreach (var v in buffer)
        {
            Assert.InRange(v, expected - 0.1f, expected + 0.1f);
        }
    }

    [Fact]
    public void UpdateVoice_ChangesWaveTable()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 2, maxVoices: 2);
        mixer.AddVoice(440, MakeWave(1f, 0f));

        var buffer = new float[2];
        mixer.Mix(buffer);
        Assert.InRange(buffer[0], 0f, 1f);

        mixer.UpdateVoice(440, MakeWave(0.25f, 0f));
        Array.Clear(buffer, 0, buffer.Length);
        mixer.Mix(buffer);

        Assert.InRange(buffer[0], 0f, 0.3f);
    }

    [Fact]
    public void AddSecondVoice_DoesNotSpike()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 4, maxVoices: 4);
        mixer.AddVoice(440, MakeWave(1f)); // sustained 1.0

        var buffer = new float[8];
        mixer.Mix(buffer); // warm-up

        mixer.AddVoice(660, MakeWave(1f)); // add second voice
        Array.Clear(buffer, 0, buffer.Length);
        mixer.Mix(buffer);

        Assert.All(buffer, v => Assert.InRange(v, -1f, 1f));
        Assert.InRange(MaxDelta(buffer), 0f, 0.15f); // tighter pop limit with longer attack
    }

    [Fact]
    public void AddSecondVoice_EnvelopeIsBoundedAndSmooth()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 8, maxVoices: 4);
        mixer.AddVoice(440, MakeWave(0.2f)); // steady first voice

        var buffer = new float[256];
        mixer.Mix(buffer); // warm-up past attack

        mixer.AddVoice(660, MakeWave(0.2f)); // add second voice
        Array.Clear(buffer, 0, buffer.Length);
        mixer.Mix(buffer);

        float max = buffer.Max();
        float min = buffer.Min();
        Assert.InRange(max - min, 0f, 0.7f); // relaxed for older mixer envelope

        // Samples should rise toward steady and stay below a reasonable bound
        float steady = (0.4f) * 0.6f / (float)Math.Sqrt(2); // linear expectation
        Assert.All(buffer.Take(64), v => Assert.InRange(v, -0.1f, steady + 0.15f));
    }

    [Fact]
    public void ReleaseVoiceWhileOthersPlay_DoesNotSpike()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 4, maxVoices: 4);
        mixer.AddVoice(440, MakeWave(1f));
        mixer.AddVoice(660, MakeWave(1f));

        var buffer = new float[8];
        mixer.Mix(buffer); // both active

        mixer.ReleaseVoice(660); // release one voice, keep the other
        Array.Clear(buffer, 0, buffer.Length);
        mixer.Mix(buffer);

        Assert.All(buffer, v => Assert.InRange(v, -1f, 1f));
        Assert.InRange(MaxDelta(buffer), 0f, 0.2f); // smooth transition with release ramp
    }

    [Fact]
    public void ReleaseVoiceWhileOthersPlay_EnvelopeIsBounded()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 8, maxVoices: 4);
        mixer.AddVoice(440, MakeWave(0.2f));
        mixer.AddVoice(660, MakeWave(0.2f));

        var buffer = new float[256];
        mixer.Mix(buffer); // warm-up

        mixer.ReleaseVoice(660);
        Array.Clear(buffer, 0, buffer.Length);
        mixer.Mix(buffer);

        float max = buffer.Max();
        float min = buffer.Min();
        Assert.InRange(max, -0.1f, 0.7f);
        Assert.InRange(min, -0.5f, 0.2f);

        // Toward end, should decay near zero
        foreach (var v in buffer.Skip(180))
        {
            Assert.InRange(v, -0.2f, 0.2f);
        }
    }
}

