using DrawSound.Core.Audio;

namespace DrawSound.Tests;

public class HarmonicMixerTests
{
    [Fact]
    public void MaxHarmonics_Is12()
    {
        // Assert
        Assert.Equal(12, HarmonicMixer.MaxHarmonics);
    }

    [Fact]
    public void GetDefaultLevels_Returns12Elements()
    {
        // Act
        var levels = HarmonicMixer.GetDefaultLevels();

        // Assert
        Assert.Equal(12, levels.Length);
    }

    [Fact]
    public void GetDefaultLevels_FirstElementIsOne()
    {
        // Act
        var levels = HarmonicMixer.GetDefaultLevels();

        // Assert
        Assert.Equal(1.0f, levels[0]);
    }

    [Fact]
    public void GetDefaultLevels_OtherElementsAreZero()
    {
        // Act
        var levels = HarmonicMixer.GetDefaultLevels();

        // Assert
        for (int i = 1; i < levels.Length; i++)
        {
            Assert.Equal(0f, levels[i]);
        }
    }

    [Fact]
    public void MixHarmonics_EmptyBaseWave_ReturnsSilence()
    {
        // Arrange
        var baseWave = Array.Empty<float>();
        var levels = HarmonicMixer.GetDefaultLevels();

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert
        Assert.Equal(100, result.Length);
        Assert.All(result, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void MixHarmonics_OnlyBaseFrequency_ReturnsBaseWave()
    {
        // Arrange - simple sine wave
        var baseWave = CreateSineWave(100);
        var levels = HarmonicMixer.GetDefaultLevels(); // Only base at 1.0

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert - should match base wave
        Assert.Equal(100, result.Length);
        for (int i = 0; i < result.Length; i++)
        {
            Assert.Equal(baseWave[i], result[i], 4);
        }
    }

    [Fact]
    public void MixHarmonics_AllLevelsZero_ReturnsSilence()
    {
        // Arrange
        var baseWave = CreateSineWave(100);
        var levels = new float[13]; // All zeros

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert
        Assert.All(result, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void MixHarmonics_ReturnsCorrectSampleCount()
    {
        // Arrange
        var baseWave = CreateSineWave(50);
        var levels = HarmonicMixer.GetDefaultLevels();

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 200);

        // Assert
        Assert.Equal(200, result.Length);
    }

    [Fact]
    public void MixHarmonics_SecondHarmonic_DoublesFrequency()
    {
        // Arrange
        var baseWave = CreateSineWave(100);
        var levels = new float[13];
        levels[1] = 1.0f; // Only second harmonic (2x frequency)

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert - second harmonic should cross zero twice as often
        // Count zero crossings
        int zeroCrossings = CountZeroCrossings(result);
        int baseZeroCrossings = CountZeroCrossings(baseWave);
        
        // Second harmonic should have ~2x zero crossings
        Assert.True(zeroCrossings >= baseZeroCrossings * 1.8, 
            $"Expected ~{baseZeroCrossings * 2} crossings, got {zeroCrossings}");
    }

    [Fact]
    public void MixHarmonics_NormalizesToPreventClipping()
    {
        // Arrange - add multiple harmonics at full level
        var baseWave = CreateSineWave(100);
        var levels = new float[13];
        levels[0] = 1.0f;
        levels[1] = 1.0f;
        levels[2] = 1.0f;

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert - all samples should be within -0.5 to 0.5
        Assert.All(result, sample =>
        {
            Assert.True(sample >= -0.51f, $"Sample {sample} below -0.5");
            Assert.True(sample <= 0.51f, $"Sample {sample} above 0.5");
        });
    }

    [Fact]
    public void MixHarmonics_LowLevel_ScalesAmplitude()
    {
        // Arrange
        var baseWave = CreateSineWave(100);
        var levels = new float[13];
        levels[0] = 0.5f; // Half level

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert - amplitude should be half
        float maxResult = result.Max(Math.Abs);
        float maxBase = baseWave.Max(Math.Abs);
        Assert.True(Math.Abs(maxResult - maxBase * 0.5f) < 0.05f,
            $"Expected max ~{maxBase * 0.5f}, got {maxResult}");
    }

    [Fact]
    public void MixHarmonics_MixingMultipleHarmonics_ProducesComplexWave()
    {
        // Arrange
        var baseWave = CreateSineWave(100);
        var levelsSimple = new float[13];
        levelsSimple[0] = 1.0f;
        
        var levelsComplex = new float[13];
        levelsComplex[0] = 1.0f;
        levelsComplex[1] = 0.5f;
        levelsComplex[2] = 0.25f;

        // Act
        var simpleResult = HarmonicMixer.MixHarmonics(baseWave, levelsSimple, 100);
        var complexResult = HarmonicMixer.MixHarmonics(baseWave, levelsComplex, 100);

        // Assert - complex wave should be different from simple
        bool anyDifferent = false;
        for (int i = 0; i < 100; i++)
        {
            if (Math.Abs(simpleResult[i] - complexResult[i]) > 0.01f)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Complex wave should differ from simple wave");
    }

    [Fact]
    public void MixHarmonics_VerySmallLevel_IsIgnored()
    {
        // Arrange
        var baseWave = CreateSineWave(100);
        var levels = new float[13];
        levels[1] = 0.0005f; // Below 0.001 threshold

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert - should be silence since only very small harmonic
        Assert.All(result, sample => Assert.Equal(0f, sample));
    }

    [Theory]
    [InlineData(3)]   // 3rd harmonic
    [InlineData(5)]   // 5th harmonic
    [InlineData(7)]   // 7th harmonic
    [InlineData(12)]  // 12th harmonic
    public void MixHarmonics_HigherHarmonics_IncreaseFrequency(int harmonicIndex)
    {
        // Arrange
        var baseWave = CreateSineWave(100);
        var levels = new float[13];
        levels[harmonicIndex] = 1.0f;

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert - should have more zero crossings than base
        int resultCrossings = CountZeroCrossings(result);
        int baseCrossings = CountZeroCrossings(baseWave);
        
        // Higher harmonics should have more crossings
        Assert.True(resultCrossings > baseCrossings,
            $"Harmonic {harmonicIndex + 1} should have more crossings than base. " +
            $"Got {resultCrossings}, base has {baseCrossings}");
    }

    [Fact]
    public void MixHarmonics_FewerLevelsThanMax_WorksCorrectly()
    {
        // Arrange - only 5 levels provided
        var baseWave = CreateSineWave(100);
        var levels = new float[] { 1.0f, 0.5f, 0.25f, 0.125f, 0.0625f };

        // Act
        var result = HarmonicMixer.MixHarmonics(baseWave, levels, 100);

        // Assert
        Assert.Equal(100, result.Length);
        // Should work without error
    }

    private static float[] CreateSineWave(int samples)
    {
        var wave = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            double phase = 2.0 * Math.PI * i / samples;
            wave[i] = (float)(Math.Sin(phase) * 0.5);
        }
        return wave;
    }

    private static int CountZeroCrossings(float[] samples)
    {
        int crossings = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            if ((samples[i - 1] >= 0 && samples[i] < 0) ||
                (samples[i - 1] < 0 && samples[i] >= 0))
            {
                crossings++;
            }
        }
        return crossings;
    }
}

