namespace DrawSound.Core.Audio;

public class WaveTableGenerator
{
    private readonly int _sampleRate;

    public WaveTableGenerator(int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive");
        
        _sampleRate = sampleRate;
    }

    public float[] GenerateSineWave(double frequency, float amplitude = 0.5f)
    {
        if (frequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be positive");
        
        if (amplitude < 0 || amplitude > 1)
            throw new ArgumentOutOfRangeException(nameof(amplitude), "Amplitude must be between 0 and 1");

        // Calculate samples per cycle
        int samplesPerCycle = (int)Math.Round(_sampleRate / frequency);
        
        // Ensure at least 2 samples
        samplesPerCycle = Math.Max(samplesPerCycle, 2);

        var waveTable = new float[samplesPerCycle];
        
        for (int i = 0; i < samplesPerCycle; i++)
        {
            double phase = 2.0 * Math.PI * i / samplesPerCycle;
            waveTable[i] = (float)(Math.Sin(phase) * amplitude);
        }

        return waveTable;
    }
}

