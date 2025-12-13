using DrawSound.Controls;
using DrawSound.Core.Audio;
using DrawSound.Core.Utils;
using DrawSound.Graphics;
using DrawSound.Services;

namespace DrawSound;

public partial class MainPage : ContentPage
{
    private const double MiddleC = 261.63;
    private const int SampleRate = 44100;
    private const int EditingSamples = 256;
    private const int PreviewSamples = 64; // Reduced for performance
    private const int ThrottleMs = 50; // Max 20 updates/second
    
    private readonly ITonePlayer _tonePlayer;
    private readonly float[] _harmonicLevels;
    private readonly VerticalSlider[] _harmonicSliders;
    private readonly UpdateThrottler _sliderThrottler;
    private bool _isPlaying;
    private bool _pendingUpdate;
    private float _canvasWidth;
    private float _canvasHeight;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;
        _harmonicLevels = HarmonicMixer.GetDefaultLevels();
        _harmonicSliders = new VerticalSlider[HarmonicMixer.MaxHarmonics];
        _sliderThrottler = new UpdateThrottler(ThrottleMs);

        CreateHarmonicSliders();
        WaveformDrawable.WaveTableChanged += OnWaveTableChanged;
        
        // Initial preview update
        Dispatcher.Dispatch(UpdatePreview);
    }

    private void CreateHarmonicSliders()
    {
        // Create column definitions
        for (int i = 0; i < HarmonicMixer.MaxHarmonics; i++)
        {
            HarmonicsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }
        HarmonicsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        HarmonicsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Labels: f, 2f, 3f, ... 12f
        string[] labels = { "f", "2f", "3f", "4f", "5f", "6f", "7f", "8f", "9f", "10f", "11f", "12f" };

        for (int i = 0; i < HarmonicMixer.MaxHarmonics; i++)
        {
            int index = i; // Capture for closure

            var slider = new VerticalSlider
            {
                Value = _harmonicLevels[i],
                WidthRequest = 28,
                HeightRequest = 110,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Fill,
                FillColor = i == 0 ? Colors.Cyan : Colors.Orange,
                ThumbColor = Colors.White,
                TrackColor = Color.FromArgb("#333")
            };

            slider.ValueChanged += (s, value) =>
            {
                _harmonicLevels[index] = value;
                ThrottledUpdate();
            };

            _harmonicSliders[i] = slider;
            
            Grid.SetRow(slider, 0);
            Grid.SetColumn(slider, i);
            HarmonicsGrid.Add(slider);

            // Label
            var label = new Label
            {
                Text = labels[i],
                TextColor = i == 0 ? Colors.Cyan : Colors.Gray,
                FontSize = 9,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            };
            Grid.SetRow(label, 1);
            Grid.SetColumn(label, i);
            HarmonicsGrid.Add(label);
        }
    }

    private float[] GetMixedWaveTable(int samples)
    {
        var baseWave = WaveformDrawable.GetWaveTable(samples);
        return HarmonicMixer.MixHarmonics(baseWave, _harmonicLevels, samples);
    }

    private float[] GetPlaybackWaveTable(double frequency)
    {
        int targetSamples = (int)Math.Round(SampleRate / frequency);
        var baseWave = WaveformDrawable.GetWaveTable(EditingSamples);
        return HarmonicMixer.MixHarmonics(baseWave, _harmonicLevels, targetSamples);
    }

    private void ThrottledUpdate()
    {
        if (_sliderThrottler.ShouldUpdate())
        {
            PerformUpdate();
        }
        else if (!_pendingUpdate)
        {
            _pendingUpdate = true;
            int delay = _sliderThrottler.GetDeferredDelayMs();
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), () =>
            {
                _pendingUpdate = false;
                if (_sliderThrottler.NeedsDeferredUpdate)
                {
                    _sliderThrottler.ShouldUpdate(); // Reset the throttle
                    PerformUpdate();
                }
            });
        }
    }

    private void PerformUpdate()
    {
        UpdatePreview();
        if (_isPlaying)
        {
            UpdatePlayback();
        }
    }

    private void UpdatePreview()
    {
        var mixedWave = GetMixedWaveTable(PreviewSamples);
        PreviewDrawable.SetWaveTable(mixedWave);
        PreviewView.Invalidate();
    }

    private void UpdatePlayback()
    {
        var playbackWave = GetPlaybackWaveTable(MiddleC);
        _tonePlayer.UpdateWaveTable(playbackWave);
    }

    private void OnWaveTableChanged(object? sender, float[] waveTable)
    {
        UpdatePreview();
        if (_isPlaying)
        {
            UpdatePlayback();
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

        var playbackWave = GetPlaybackWaveTable(MiddleC);
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
