namespace DrawSound.Core.Audio;

/// <summary>
/// Placeholder for AHDSHR envelope application. Not implemented yet.
/// </summary>
public static class AHDSHREnvelope
{
    public static float[] Apply(float[] samples, AHDSHRSettings settings, int sampleRate)
    {
        var output = new float[samples.Length];
        if (samples.Length == 0) return output;

        int AttackSamples(float ms) => ms <= 0 ? 0 : Math.Max(1, (int)Math.Round(ms * sampleRate / 1000f));

        int attack = AttackSamples(settings.AttackMs);
        int hold1 = AttackSamples(settings.Hold1Ms);
        int decay = AttackSamples(settings.DecayMs);
        int hold2 = settings.Hold2Ms < 0 ? samples.Length : AttackSamples(settings.Hold2Ms);
        int release = AttackSamples(settings.ReleaseMs);

        float sustain = Math.Clamp(settings.SustainLevel, 0f, 1f);

        int idx = 0;
        // Attack: 0 -> 1
        for (; idx < attack && idx < output.Length; idx++)
        {
            float t = attack == 0 ? 1f : (idx + 1) / (float)attack;
            output[idx] = samples[idx] * t;
        }

        // Hold1: keep 1
        for (int h = 0; h < hold1 && idx < output.Length; h++, idx++)
        {
            output[idx] = samples[idx];
        }

        // Decay: 1 -> sustain
        for (int d = 0; d < decay && idx < output.Length; d++, idx++)
        {
            float t = decay == 0 ? 1f : (d + 1) / (float)decay;
            float gain = 1f + (sustain - 1f) * t;
            output[idx] = samples[idx] * gain;
        }

        // Hold2/Sustain: sustain level until release or end
        bool infiniteHold2 = settings.Hold2Ms < 0;
        int sustainSamples = infiniteHold2 ? int.MaxValue : hold2;
        for (int s = 0; s < sustainSamples && idx < output.Length; s++, idx++)
        {
            // If we will run out of buffer before release, just sustain
            if (!infiniteHold2 && s >= hold2) break;
            output[idx] = samples[idx] * sustain;
        }

        // Release: sustain -> 0
        for (int r = 0; r < release && idx < output.Length; r++, idx++)
        {
            float t = release == 0 ? 1f : (r + 1) / (float)release;
            float gain = sustain * (1f - t);
            output[idx] = samples[idx] * gain;
        }

        // Any remaining samples -> 0
        for (; idx < output.Length; idx++)
        {
            output[idx] = 0f;
        }

        return output;
    }
}

