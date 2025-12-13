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
    private readonly float[] _harmonicLevels;
    private readonly Slider[] _harmonicSliders;
    private bool _isPlaying;
    private float _canvasWidth;
    private float _canvasHeight;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;
        _harmonicLevels = HarmonicMixer.GetDefaultLevels();
        _harmonicSliders = new Slider[HarmonicMixer.MaxHarmonics];

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

        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13" };

        for (int i = 0; i < HarmonicMixer.MaxHarmonics; i++)
        {
            int index = i; // Capture for closure

            // Create vertical slider container
            var sliderContainer = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Star) }
            };

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = _harmonicLevels[i],
                Rotation = 270,
                WidthRequest = 120,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                ThumbColor = i == 0 ? Colors.Cyan : Colors.Orange,
                MinimumTrackColor = i == 0 ? Colors.Cyan : Colors.Orange,
                MaximumTrackColor = Color.FromArgb("#333")
            };

            slider.ValueChanged += (s, e) =>
            {
                _harmonicLevels[index] = (float)e.NewValue;
                UpdatePreview();
                if (_isPlaying)
                {
                    UpdatePlayback();
                }
            };

            _harmonicSliders[i] = slider;
            sliderContainer.Add(slider);
            
            Grid.SetRow(sliderContainer, 0);
            Grid.SetColumn(sliderContainer, i);
            HarmonicsGrid.Add(sliderContainer);

            // Label
            var label = new Label
            {
                Text = labels[i],
                TextColor = i == 0 ? Colors.Cyan : Colors.Gray,
                FontSize = 10,
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

    private void UpdatePreview()
    {
        var mixedWave = GetMixedWaveTable(EditingSamples);
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
