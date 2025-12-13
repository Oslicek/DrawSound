namespace DrawSound.Core.Audio;

/// <summary>
/// Mixes a base waveform with its overtones/harmonics
/// </summary>
public static class HarmonicMixer
{
    public const int MaxHarmonics = 12; // Base (f) + 11 overtones (2f to 12f)

    /// <summary>
    /// Mix the base waveform with overtones
    /// </summary>
    /// <param name="baseWaveTable">The base waveform shape (one cycle)</param>
    /// <param name="harmonicLevels">Array of 13 levels (0.0 to 1.0) for base and overtones</param>
    /// <param name="outputSamples">Number of samples in output</param>
    /// <returns>Mixed waveform</returns>
    public static float[] MixHarmonics(float[] baseWaveTable, float[] harmonicLevels, int outputSamples)
    {
        if (baseWaveTable.Length == 0)
        {
            return new float[outputSamples];
        }

        var result = new float[outputSamples];
        float maxAmplitude = 0f;

        // Add each harmonic
        for (int h = 0; h < Math.Min(harmonicLevels.Length, MaxHarmonics); h++)
        {
            float level = harmonicLevels[h];
            if (level <= 0.001f) continue; // Skip silent harmonics

            int harmonicMultiplier = h + 1; // 1 for base, 2 for first overtone, etc.

            for (int i = 0; i < outputSamples; i++)
            {
                // Sample from base waveform at multiplied frequency
                float phase = (float)i / outputSamples * harmonicMultiplier;
                phase = phase - MathF.Floor(phase); // Wrap to 0-1

                // Linear interpolation for smooth sampling
                float samplePos = phase * baseWaveTable.Length;
                int index = (int)samplePos;
                float fraction = samplePos - index;
                int nextIndex = (index + 1) % baseWaveTable.Length;

                float sample = baseWaveTable[index] * (1 - fraction) + 
                               baseWaveTable[nextIndex] * fraction;

                result[i] += sample * level;
            }
        }

        // Find max amplitude for normalization
        for (int i = 0; i < outputSamples; i++)
        {
            float abs = MathF.Abs(result[i]);
            if (abs > maxAmplitude) maxAmplitude = abs;
        }

        // Normalize to prevent clipping (max 0.5 amplitude)
        if (maxAmplitude > 0.5f)
        {
            float scale = 0.5f / maxAmplitude;
            for (int i = 0; i < outputSamples; i++)
            {
                result[i] *= scale;
            }
        }

        return result;
    }

    /// <summary>
    /// Get default harmonic levels (base at max, others at zero)
    /// </summary>
    public static float[] GetDefaultLevels()
    {
        var levels = new float[MaxHarmonics];
        levels[0] = 1.0f; // Base frequency at max
        return levels;
    }
}

