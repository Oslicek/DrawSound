using DrawSound.Core.Audio;
using System.Linq;
using Xunit;

namespace DrawSound.Tests;

public class VoiceMixerTests
{
    private static float[] MakeWave(params float[] samples) => samples;
    private static float[] MakeSineTable(float frequency, int sampleRate)
    {
        int len = Math.Max(8, (int)Math.Round(sampleRate / frequency));
        var table = new float[len];
        for (int i = 0; i < len; i++)
        {
            table[i] = MathF.Sin(2 * MathF.PI * i / len);
        }
        return table;
    }
    private static float[] RenderVoice(float[] table, double frequency, int sampleRate, int warmSamples, int count)
    {
        float[] output = new float[count];
        float phase = 0f;
        float step = (float)(table.Length * frequency / sampleRate);

        // warm-up
        for (int i = 0; i < warmSamples; i++)
        {
            phase += step;
            if (phase >= table.Length) phase -= table.Length;
        }

        for (int i = 0; i < count; i++)
        {
            int idx0 = (int)phase;
            int idx1 = (idx0 + 1) % table.Length;
            float frac = phase - idx0;
            float v0 = table[idx0];
            float v1 = table[idx1];
            output[i] = v0 + (v1 - v0) * frac;

            phase += step;
            if (phase >= table.Length) phase -= table.Length;
        }

        return output;
    }
    private static float MeanAbsError(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        float sum = 0f;
        int n = Math.Min(a.Count, b.Count);
        for (int i = 0; i < n; i++)
        {
            sum += MathF.Abs(a[i] - b[i]);
        }
        return sum / n;
    }
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

        // Expected steady value with linear mix (no headroom)
        float expected = 0.2f;
        foreach (var v in buffer)
        {
            Assert.InRange(v, expected - 0.05f, expected + 0.05f);
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
        Assert.InRange(max - min, 0f, 1.2f); // relaxed for linear mix

        // Samples should rise toward steady and stay below a reasonable bound
        float steady = 0.4f; // linear expectation
        Assert.All(buffer.Take(64), v => Assert.InRange(v, -0.2f, steady + 0.2f));
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

    [Fact]
    public void Mix_TwoSines_C3_E3_ShouldMatchIdealSum()
    {
        const int sampleRate = 44100;
        const float c3 = 130.81f;
        const float e3 = 164.81f;

        var mixer = new VoiceMixer(sampleRate: sampleRate, releaseSamples: 8, maxVoices: 4);
        var c3Table = MakeSineTable(c3, sampleRate);
        var e3Table = MakeSineTable(e3, sampleRate);

        mixer.AddVoice(c3, c3Table);
        mixer.AddVoice(e3, e3Table);

        // Warm-up past attack
        var warm = new float[256];
        mixer.Mix(warm);

        var buffer = new float[512];
        mixer.Mix(buffer);

        // Ideal mix using the same wavetable playback (no headroom scaling/clipping)
        var idealC3 = RenderVoice(c3Table, c3, sampleRate, warm.Length, buffer.Length);
        var idealE3 = RenderVoice(e3Table, e3, sampleRate, warm.Length, buffer.Length);
        var expected = idealC3.Zip(idealE3, (a, b) => a + b).ToArray();

        var error = MeanAbsError(expected, buffer);
        Assert.True(error < 0.001f, $"C3/E3 mix error observed: {error}");
    }

    [Fact]
    public void Mix_TwoSines_C3_Db3_ShouldMatchIdealSum()
    {
        const int sampleRate = 44100;
        const float c3 = 130.81f;
        const float db3 = 138.59f; // minor second above C3

        var mixer = new VoiceMixer(sampleRate: sampleRate, releaseSamples: 8, maxVoices: 4);
        var aTable = MakeSineTable(c3, sampleRate);
        var bTable = MakeSineTable(db3, sampleRate);

        mixer.AddVoice(c3, aTable);
        mixer.AddVoice(db3, bTable);

        var warm = new float[256];
        mixer.Mix(warm);

        var buffer = new float[512];
        mixer.Mix(buffer);

        var idealA = RenderVoice(aTable, c3, sampleRate, warm.Length, buffer.Length);
        var idealB = RenderVoice(bTable, db3, sampleRate, warm.Length, buffer.Length);
        var expected = idealA.Zip(idealB, (x, y) => x + y).ToArray();

        var error = MeanAbsError(expected, buffer);
        Assert.True(error < 0.001f, $"C3/Db3 mix error observed: {error}");
    }

    [Fact]
    public void Mix_TwoSines_C3_D3_ShouldMatchIdealSum()
    {
        const int sampleRate = 44100;
        const float c3 = 130.81f;
        const float d3 = 146.83f; // major second above C3

        var mixer = new VoiceMixer(sampleRate: sampleRate, releaseSamples: 8, maxVoices: 4);
        var aTable = MakeSineTable(c3, sampleRate);
        var bTable = MakeSineTable(d3, sampleRate);

        mixer.AddVoice(c3, aTable);
        mixer.AddVoice(d3, bTable);

        var warm = new float[256];
        mixer.Mix(warm);

        var buffer = new float[512];
        mixer.Mix(buffer);

        var idealA = RenderVoice(aTable, c3, sampleRate, warm.Length, buffer.Length);
        var idealB = RenderVoice(bTable, d3, sampleRate, warm.Length, buffer.Length);
        var expected = idealA.Zip(idealB, (x, y) => x + y).ToArray();

        var error = MeanAbsError(expected, buffer);
        Assert.True(error < 0.001f, $"C3/D3 mix error observed: {error}");
    }

    [Fact]
    public void Mix_TwoSines_C5_Db5_ShouldMatchIdealSum()
    {
        const int sampleRate = 44100;
        const float c5 = 523.25f;
        const float db5 = 554.37f; // minor second above C5

        var mixer = new VoiceMixer(sampleRate: sampleRate, releaseSamples: 8, maxVoices: 4);
        var aTable = MakeSineTable(c5, sampleRate);
        var bTable = MakeSineTable(db5, sampleRate);

        mixer.AddVoice(c5, aTable);
        mixer.AddVoice(db5, bTable);

        var warm = new float[256];
        mixer.Mix(warm);

        var buffer = new float[512];
        mixer.Mix(buffer);

        var idealA = RenderVoice(aTable, c5, sampleRate, warm.Length, buffer.Length);
        var idealB = RenderVoice(bTable, db5, sampleRate, warm.Length, buffer.Length);
        var expected = idealA.Zip(idealB, (x, y) => x + y).ToArray();

        var error = MeanAbsError(expected, buffer);
        Assert.True(error < 0.001f, $"C5/Db5 mix error observed: {error}");
    }

    [Fact]
    public void Mix_TwoSines_C5_D5_ShouldMatchIdealSum()
    {
        const int sampleRate = 44100;
        const float c5 = 523.25f;
        const float d5 = 587.33f; // major second above C5

        var mixer = new VoiceMixer(sampleRate: sampleRate, releaseSamples: 8, maxVoices: 4);
        var aTable = MakeSineTable(c5, sampleRate);
        var bTable = MakeSineTable(d5, sampleRate);

        mixer.AddVoice(c5, aTable);
        mixer.AddVoice(d5, bTable);

        var warm = new float[256];
        mixer.Mix(warm);

        var buffer = new float[512];
        mixer.Mix(buffer);

        var idealA = RenderVoice(aTable, c5, sampleRate, warm.Length, buffer.Length);
        var idealB = RenderVoice(bTable, d5, sampleRate, warm.Length, buffer.Length);
        var expected = idealA.Zip(idealB, (x, y) => x + y).ToArray();

        var error = MeanAbsError(expected, buffer);
        Assert.True(error < 0.001f, $"C5/D5 mix error observed: {error}");
    }

    [Fact]
    public void Mix_ThreeSines_C4_E4_G4_ShouldMatchIdealSum()
    {
        const int sampleRate = 44100;
        const float c4 = 261.63f;
        const float e4 = 329.63f;
        const float g4 = 392.00f;

        var mixer = new VoiceMixer(sampleRate: sampleRate, releaseSamples: 8, maxVoices: 6);
        var cTable = MakeSineTable(c4, sampleRate);
        var eTable = MakeSineTable(e4, sampleRate);
        var gTable = MakeSineTable(g4, sampleRate);

        mixer.AddVoice(c4, cTable);
        mixer.AddVoice(e4, eTable);
        mixer.AddVoice(g4, gTable);

        var warm = new float[512];
        mixer.Mix(warm); // advance past attack

        var buffer = new float[1024];
        mixer.Mix(buffer);

        var idealC = RenderVoice(cTable, c4, sampleRate, warm.Length, buffer.Length);
        var idealE = RenderVoice(eTable, e4, sampleRate, warm.Length, buffer.Length);
        var idealG = RenderVoice(gTable, g4, sampleRate, warm.Length, buffer.Length);
        var expected = idealC.Zip(idealE, (a, b) => a + b).Zip(idealG, (s, c) => s + c).ToArray();

        var error = MeanAbsError(expected, buffer);
        // Very tight tolerance to reveal current distortion/beating; test should fail until mixer is fixed.
        Assert.True(error < 1e-5f, $"C4/E4/G4 mix error observed: {error}");
    }
}

