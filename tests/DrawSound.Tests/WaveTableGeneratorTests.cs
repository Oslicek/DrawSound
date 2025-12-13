using DrawSound.Core.Audio;

namespace DrawSound.Tests;

public class WaveTableGeneratorTests
{
    private const int SampleRate = 44100;
    private const double Tolerance = 0.0001;

    [Fact]
    public void GenerateSineWave_MiddleC_ProducesCorrectWaveShape()
    {
        // Arrange
        const double frequency = 261.63; // Middle C (C4)
        var generator = new WaveTableGenerator(SampleRate);

        // Act
        var waveTable = generator.GenerateSineWave(frequency);

        // Assert
        // For 261.63 Hz at 44100 Hz: 44100 / 261.63 ≈ 169 samples
        Assert.True(waveTable.Length > 0, "Wave table should not be empty");
        
        // Verify sine wave properties:
        // 1. Starts at zero (sin(0) = 0)
        Assert.True(Math.Abs(waveTable[0]) < Tolerance, 
            $"Wave should start near zero, but was {waveTable[0]}");
        
        // 2. Peak at 1/4 of the cycle (sin(π/2) = 1)
        int quarterIndex = waveTable.Length / 4;
        Assert.True(waveTable[quarterIndex] > 0.4f, 
            $"Wave should be positive at quarter point, but was {waveTable[quarterIndex]}");
        
        // 3. Back to zero at 1/2 of the cycle (sin(π) = 0)
        int halfIndex = waveTable.Length / 2;
        Assert.True(Math.Abs(waveTable[halfIndex]) < 0.1f, 
            $"Wave should be near zero at half point, but was {waveTable[halfIndex]}");
        
        // 4. Negative peak at 3/4 of the cycle (sin(3π/2) = -1)
        int threeQuarterIndex = (waveTable.Length * 3) / 4;
        Assert.True(waveTable[threeQuarterIndex] < -0.4f, 
            $"Wave should be negative at three-quarter point, but was {waveTable[threeQuarterIndex]}");
    }

    [Fact]
    public void GenerateSineWave_MiddleC_HasCorrectSampleCount()
    {
        // Arrange
        const double frequency = 261.63;
        var generator = new WaveTableGenerator(SampleRate);
        int expectedSamples = (int)Math.Round(SampleRate / frequency); // ~169

        // Act
        var waveTable = generator.GenerateSineWave(frequency);

        // Assert
        Assert.Equal(expectedSamples, waveTable.Length);
    }

    [Fact]
    public void GenerateSineWave_AmplitudeWithinRange()
    {
        // Arrange
        const double frequency = 440.0; // A4
        const float expectedAmplitude = 0.5f;
        var generator = new WaveTableGenerator(SampleRate);

        // Act
        var waveTable = generator.GenerateSineWave(frequency, expectedAmplitude);

        // Assert
        float maxValue = waveTable.Max();
        float minValue = waveTable.Min();
        
        Assert.True(maxValue <= expectedAmplitude + Tolerance, 
            $"Max value {maxValue} exceeds amplitude {expectedAmplitude}");
        Assert.True(minValue >= -expectedAmplitude - Tolerance, 
            $"Min value {minValue} exceeds negative amplitude {-expectedAmplitude}");
        Assert.True(maxValue > expectedAmplitude - 0.05f, 
            $"Max value {maxValue} should be close to amplitude {expectedAmplitude}");
    }

    [Theory]
    [InlineData(261.63)]  // C4
    [InlineData(440.0)]   // A4
    [InlineData(880.0)]   // A5
    [InlineData(1000.0)]  // 1kHz
    public void GenerateSineWave_VariousFrequencies_ProducesValidWaveTable(double frequency)
    {
        // Arrange
        var generator = new WaveTableGenerator(SampleRate);

        // Act
        var waveTable = generator.GenerateSineWave(frequency);

        // Assert
        Assert.NotNull(waveTable);
        Assert.True(waveTable.Length >= 2, "Wave table must have at least 2 samples");
        Assert.True(waveTable.All(s => s >= -1.0f && s <= 1.0f), 
            "All samples must be within [-1, 1] range");
    }
}

