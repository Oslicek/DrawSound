using DrawSound.Core.Audio;
using Xunit;

namespace DrawSound.Tests;

public class AHDSHREnvelopeTests
{
    private const int SampleRate = 44100;

    private static float[] MakeSine(float frequency, int samples)
    {
        var arr = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            arr[i] = MathF.Sin(2 * MathF.PI * frequency * t);
        }
        return arr;
    }

    private static float MaxAbs(float[] data, int start, int end)
    {
        start = Math.Max(0, start);
        end = Math.Min(data.Length, end);
        float m = 0f;
        for (int i = start; i < end; i++)
        {
            m = Math.Max(m, MathF.Abs(data[i]));
        }
        return m;
    }

    [Fact]
    public void Apply_ShortSound_WithFastEnvelope_ShouldMatchExpectedEnvelope()
    {
        // ~30ms wavetable
        var input = MakeSine(440f, (int)(SampleRate * 0.03f));

        var settings = new AHDSHRSettings
        {
            AttackMs = 2f,
            Hold1Ms = 2f,
            DecayMs = 4f,
            SustainLevel = 0.6f,
            Hold2Ms = 10f,
            ReleaseMs = 5f
        };

        var output = AHDSHREnvelope.Apply(input, settings, SampleRate);

        // Attack should start near zero
        Assert.True(MathF.Abs(output[0]) <= 1e-6f);

        int attackEnd = (int)(settings.AttackMs * SampleRate / 1000f);
        int hold1End = attackEnd + (int)(settings.Hold1Ms * SampleRate / 1000f);
        int decayEnd = hold1End + (int)(settings.DecayMs * SampleRate / 1000f);
        int releaseEnd = decayEnd + (int)(settings.Hold2Ms * SampleRate / 1000f) + (int)(settings.ReleaseMs * SampleRate / 1000f);

        // Peak after attack/hold1 near input peak
        float inPeakAttack = MaxAbs(input, 0, hold1End);
        float outPeakAttack = MaxAbs(output, 0, hold1End);
        Assert.True(outPeakAttack > inPeakAttack * 0.5f);

        // After decay end, level near sustain
        int decaySample = Math.Max(hold1End, decayEnd - 1);
        float absInDecay = MathF.Abs(input[decaySample]);
        float absOutDecay = MathF.Abs(output[decaySample]);
        float sustain = settings.SustainLevel;
        if (absInDecay > 1e-5f)
            Assert.InRange(absOutDecay / absInDecay, sustain - 0.15f, sustain + 0.15f);

        // After release end, samples should be near zero
        float tail = MaxAbs(output, releaseEnd, output.Length);
        Assert.True(tail < 1e-3);
    }

    [Fact]
    public void Apply_ShortSound_WithInfiniteSustain_ShouldHoldUntilRelease()
    {
        // ~25ms wavetable
        var input = MakeSine(330f, (int)(SampleRate * 0.025f));

        var settings = new AHDSHRSettings
        {
            AttackMs = 5f,
            Hold1Ms = 0f,
            DecayMs = 5f,
            SustainLevel = 0.8f,
            Hold2Ms = -1f, // sustain until release
            ReleaseMs = 8f
        };

        var output = AHDSHREnvelope.Apply(input, settings, SampleRate);

        // Attack start near zero
        Assert.True(MathF.Abs(output[0]) <= 1e-6f);

        int decayEnd = (int)((settings.AttackMs + settings.DecayMs) * SampleRate / 1000f);
        float sustain = settings.SustainLevel;
        int decaySample = Math.Max((int)(settings.AttackMs * SampleRate / 1000f), decayEnd - 1);
        float absInDecay = MathF.Abs(input[decaySample]);
        float absOutDecay = MathF.Abs(output[decaySample]);
        if (absInDecay > 1e-5f)
            Assert.InRange(absOutDecay / absInDecay, sustain - 0.15f, sustain + 0.15f);

        // Tail should remain near sustain*input (no release)
        float inTail = MaxAbs(input, decayEnd, input.Length);
        float outTail = MaxAbs(output, decayEnd, output.Length);
        if (inTail > 1e-5f)
            Assert.InRange(outTail / inTail, sustain - 0.15f, sustain + 0.15f);
    }
}

