namespace DrawSound.Services;

public interface ITonePlayer
{
    void StartTone(double frequency);
    void StartTone(double frequency, float[] waveTable);
    void UpdateWaveTable(float[] waveTable);
    void StopTone();
}
