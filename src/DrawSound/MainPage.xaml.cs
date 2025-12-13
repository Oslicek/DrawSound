using DrawSound.Core.Audio;
using DrawSound.Graphics;
using DrawSound.Services;

namespace DrawSound;

public partial class MainPage : ContentPage
{
    private const double MiddleC = 261.63;
    private const int SampleRate = 44100;
    private const int EditingSamples = 256;
    
    private readonly ITonePlayer _tonePlayer;
    private bool _isPlaying;
    private float _canvasWidth;
    private float _canvasHeight;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;

        WaveformDrawable.WaveTableChanged += OnWaveTableChanged;
    }

    private float[] GetPlaybackWaveTable(float[] editingWaveTable, double frequency)
    {
        int targetSamples = (int)Math.Round(SampleRate / frequency);
        return ResampleWaveTable(editingWaveTable, targetSamples);
    }

    private static float[] ResampleWaveTable(float[] source, int targetLength)
    {
        if (source.Length == 0)
        {
            return new float[targetLength];
        }

        var result = new float[targetLength];
        for (int i = 0; i < targetLength; i++)
        {
            float sourceIndex = (float)i / targetLength * source.Length;
            int index = (int)sourceIndex;
            float fraction = sourceIndex - index;
            
            int nextIndex = (index + 1) % source.Length;
            result[i] = source[index] * (1 - fraction) + source[nextIndex] * fraction;
        }
        return result;
    }

    private void OnWaveTableChanged(object? sender, float[] waveTable)
    {
        if (_isPlaying)
        {
            var playbackWave = GetPlaybackWaveTable(waveTable, MiddleC);
            _tonePlayer.UpdateWaveTable(playbackWave);
        }
        WaveformView.Invalidate();
    }

    private void OnGenerateSineClicked(object? sender, EventArgs e)
    {
        WaveformDrawable.GenerateSineWave();
        WaveformView.Invalidate();
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        WaveformDrawable.ClearWave();
        WaveformView.Invalidate();
    }

    private void OnDeleteNodeClicked(object? sender, EventArgs e)
    {
        WaveformDrawable.DeleteSelectedNode();
        WaveformView.Invalidate();
    }

    private void OnWaveformStartInteraction(object? sender, TouchEventArgs e)
    {
        UpdateCanvasSize();
        var point = e.Touches.FirstOrDefault();
        if (point != default)
        {
            WaveformDrawable.StartTouch(point.X, point.Y, _canvasWidth, _canvasHeight);
            WaveformView.Invalidate();
        }
    }

    private void OnWaveformDragInteraction(object? sender, TouchEventArgs e)
    {
        var point = e.Touches.FirstOrDefault();
        if (point != default)
        {
            WaveformDrawable.DragTouch(point.X, point.Y, _canvasWidth, _canvasHeight);
            WaveformView.Invalidate();
        }
    }

    private void OnWaveformEndInteraction(object? sender, TouchEventArgs e)
    {
        WaveformDrawable.EndTouch();
    }

    private void UpdateCanvasSize()
    {
        _canvasWidth = (float)WaveformView.Width;
        _canvasHeight = (float)WaveformView.Height;
    }

    private void OnToneButtonPressed(object? sender, EventArgs e)
    {
        _isPlaying = true;
        WaveformDrawable.SetPlaying(true);
        WaveformView.Invalidate();

        var editingWave = WaveformDrawable.GetWaveTable(EditingSamples);
        var playbackWave = GetPlaybackWaveTable(editingWave, MiddleC);
        _tonePlayer.StartTone(MiddleC, playbackWave);
    }

    private void OnToneButtonReleased(object? sender, EventArgs e)
    {
        _isPlaying = false;
        WaveformDrawable.SetPlaying(false);
        WaveformView.Invalidate();

        _tonePlayer.StopTone();
    }
}
