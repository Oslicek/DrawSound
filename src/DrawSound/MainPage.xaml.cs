using DrawSound.Services;

namespace DrawSound;

public partial class MainPage : ContentPage
{
    private const double MiddleC = 261.63;
    private readonly ITonePlayer _tonePlayer;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;
    }

    private void OnToneButtonPressed(object? sender, EventArgs e)
    {
        _tonePlayer.StartTone(MiddleC);
    }

    private void OnToneButtonReleased(object? sender, EventArgs e)
    {
        _tonePlayer.StopTone();
    }
}
