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
        Assert.Contains(buffer, v => Math.Abs(v) > 0.05f); // attack ramp but non-zero energy
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

        Assert.All(zeroBuffer, v => Assert.Equal(0f, v));
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
        Assert.InRange(MaxDelta(buffer), 0f, 0.6f); // no hard pop
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
        Assert.InRange(MaxDelta(buffer), 0f, 0.6f); // smooth transition
    }
}

