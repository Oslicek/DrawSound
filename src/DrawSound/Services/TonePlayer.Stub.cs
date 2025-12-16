#if !ANDROID
using DrawSound.Core.Audio;
using DrawSound.Services;

namespace DrawSound.Services;

/// <summary>
/// No-op TonePlayer for non-Android targets to satisfy multi-target build.
/// </summary>
public class TonePlayer : ITonePlayer
{
    public void StartTone(double frequency) { }
    public void StartTone(double frequency, float[] waveTable) { }
    public void UpdateWaveTable(double frequency, float[] waveTable) { }
    public void UpdateEnvelope(AHDSHRSettings settings) { }
    public void StopTone(double frequency) { }
    public void StopAll() { }
}
#endif

