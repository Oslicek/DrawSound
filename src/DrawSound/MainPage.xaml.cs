using DrawSound.Core.Audio;
using DrawSound.Graphics;
using DrawSound.Services;

namespace DrawSound;

public partial class MainPage : ContentPage
{
    private const double MiddleC = 261.63;
    private const int SampleRate = 44100;
    private const int WaveTableSamples = 256; // Samples per cycle for editing
    
    private readonly ITonePlayer _tonePlayer;
    private readonly WaveTableGenerator _waveTableGenerator;
    private bool _isPlaying;
    private float _canvasWidth;
    private float _canvasHeight;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;
        _waveTableGenerator = new WaveTableGenerator(SampleRate);

        // Initialize with a sine wave
        var initialWave = _waveTableGenerator.GenerateSineWave(MiddleC);
        
        // Resample to our editing resolution
        var editableWave = ResampleWaveTable(initialWave, WaveTableSamples);
        WaveformDrawable.SetWaveTable(editableWave);

        // Subscribe to wave changes
        WaveformDrawable.WaveTableChanged += OnWaveTableChanged;
    }

    private static float[] ResampleWaveTable(float[] source, int targetLength)
    {
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
            _tonePlayer.UpdateWaveTable(waveTable);
        }
        WaveformView.Invalidate();
    }

    private void OnWaveformStartInteraction(object? sender, TouchEventArgs e)
    {
        UpdateCanvasSize();
        HandleTouch(e.Touches.FirstOrDefault());
    }

    private void OnWaveformDragInteraction(object? sender, TouchEventArgs e)
    {
        HandleTouch(e.Touches.FirstOrDefault());
    }

    private void OnWaveformEndInteraction(object? sender, TouchEventArgs e)
    {
        // Touch ended - no action needed
    }

    private void HandleTouch(PointF? point)
    {
        if (point == null) return;
        
        WaveformDrawable.HandleTouch(
            (float)point.Value.X, 
            (float)point.Value.Y, 
            _canvasWidth, 
            _canvasHeight);
        
        WaveformView.Invalidate();
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

        var waveTable = WaveformDrawable.GetWaveTable();
        _tonePlayer.StartTone(MiddleC, waveTable);
    }

    private void OnToneButtonReleased(object? sender, EventArgs e)
    {
        _isPlaying = false;
        WaveformDrawable.SetPlaying(false);
        WaveformView.Invalidate();

        _tonePlayer.StopTone();
    }
}
