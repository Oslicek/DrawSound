namespace DrawSound.Services;

public interface ITonePlayer
{
    void StartTone(double frequency);
    void StopTone();
}

