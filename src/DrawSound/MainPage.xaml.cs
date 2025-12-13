using DrawSound.Core.Audio;
using DrawSound.Graphics;
using DrawSound.Services;

namespace DrawSound;

public partial class MainPage : ContentPage
{
    private const double MiddleC = 261.63;
    private const int SampleRate = 44100;
    
    private readonly ITonePlayer _tonePlayer;
    private readonly WaveTableGenerator _waveTableGenerator;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;
        _waveTableGenerator = new WaveTableGenerator(SampleRate);
    }

    private void OnToneButtonPressed(object? sender, EventArgs e)
    {
        // Generate and display the waveform
        var waveTable = _waveTableGenerator.GenerateSineWave(MiddleC);
        WaveformDrawable.SetWaveTable(waveTable);
        WaveformView.Invalidate();

        // Play the tone
        _tonePlayer.StartTone(MiddleC);
    }

    private void OnToneButtonReleased(object? sender, EventArgs e)
    {
        // Clear the waveform display
        WaveformDrawable.SetWaveTable(null);
        WaveformView.Invalidate();

        // Stop the tone
        _tonePlayer.StopTone();
    }
}
