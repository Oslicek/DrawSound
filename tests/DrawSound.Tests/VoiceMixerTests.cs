using DrawSound.Core.Audio;
using Xunit;

namespace DrawSound.Tests;

public class VoiceMixerTests
{
    private static float[] MakeWave(params float[] samples) => samples;

    [Fact]
    public void Mix_SingleVoice_ProducesSamples()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 4, maxVoices: 2);
        mixer.AddVoice(440, MakeWave(1f, 0f, -1f, 0f));

        var buffer = new float[8];
        mixer.Mix(buffer);

        Assert.True(buffer[0] > 0.7f && buffer[0] <= 1f); // scaled by headroom
        Assert.All(buffer, v => Assert.InRange(v, -1f, 1f));
    }

    [Fact]
    public void Release_FadesOutAndRemovesVoice()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 4, maxVoices: 2);
        mixer.AddVoice(440, MakeWave(1f, 1f, 1f, 1f));
        mixer.ReleaseVoice(440);

        var buffer = new float[4];
        mixer.Mix(buffer); // release block

        Assert.True(buffer[0] > buffer[^1]); // fades down

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

        Assert.InRange(buffer[0], 0.3f, 0.5f); // scaled by headroom
    }

    [Fact]
    public void UpdateVoice_ChangesWaveTable()
    {
        var mixer = new VoiceMixer(sampleRate: 44100, releaseSamples: 2, maxVoices: 2);
        mixer.AddVoice(440, MakeWave(1f, 0f));

        var buffer = new float[2];
        mixer.Mix(buffer);
        Assert.InRange(buffer[0], 0.7f, 1f);

        mixer.UpdateVoice(440, MakeWave(0.25f, 0f));
        Array.Clear(buffer, 0, buffer.Length);
        mixer.Mix(buffer);

        Assert.InRange(buffer[0], 0.15f, 0.25f);
    }
}

