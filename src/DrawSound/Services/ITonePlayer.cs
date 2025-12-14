namespace DrawSound.Services;

public interface ITonePlayer
{
    void StartTone(double frequency);
    void StartTone(double frequency, float[] waveTable);
    void UpdateWaveTable(double frequency, float[] waveTable);
    void StopTone(double frequency);
    void StopAll();
}
