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
    private const int PreviewSamples = 64;
    private const int ThrottleMs = 50;
    
    private readonly ITonePlayer _tonePlayer;
    private readonly float[] _harmonicLevels;
    private readonly UpdateThrottler _updateThrottler;
    private readonly HarmonicsView _harmonicsControl;
    private bool _isPlaying;
    private bool _pendingUpdate;
    private float _canvasWidth;
    private float _canvasHeight;
    private DateTime _lastWaveformInvalidate = DateTime.MinValue;

    public MainPage(ITonePlayer tonePlayer)
    {
        InitializeComponent();
        _tonePlayer = tonePlayer;
        _harmonicLevels = HarmonicMixer.GetDefaultLevels();
        _updateThrottler = new UpdateThrottler(ThrottleMs);

        // Create and configure the harmonics control
        _harmonicsControl = new HarmonicsView(HarmonicMixer.MaxHarmonics);
        _harmonicsControl.ValueChanged += OnHarmonicValueChanged;
        HarmonicsView.Drawable = _harmonicsControl.Drawable;
        
        // Forward touch events to harmonics control
        HarmonicsView.StartInteraction += (s, e) => 
        {
            var touch = e.Touches.FirstOrDefault();
            if (touch != default)
            {
                _harmonicsControl.HandleStartTouch((float)touch.X, (float)touch.Y, 
                    (float)HarmonicsView.Width, (float)HarmonicsView.Height);
                HarmonicsView.Invalidate();
            }
        };
        HarmonicsView.DragInteraction += (s, e) =>
        {
            var touch = e.Touches.FirstOrDefault();
            if (touch != default)
            {
                _harmonicsControl.HandleDragTouch((float)touch.X, (float)touch.Y,
                    (float)HarmonicsView.Width, (float)HarmonicsView.Height);
                HarmonicsView.Invalidate();
            }
        };
        HarmonicsView.EndInteraction += (s, e) =>
        {
            _harmonicsControl.HandleEndTouch();
            HarmonicsView.Invalidate();
        };

        WaveformDrawable.WaveTableChanged += OnWaveTableChanged;
        
        // Initial preview update
        Dispatcher.Dispatch(UpdatePreview);
    }

    private void OnHarmonicValueChanged(object? sender, (int Index, float Value) e)
    {
        _harmonicLevels[e.Index] = e.Value;
        ThrottledUpdate();
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
        if (_updateThrottler.ShouldUpdate())
        {
            PerformUpdate();
        }
        else if (!_pendingUpdate)
        {
            _pendingUpdate = true;
            int delay = _updateThrottler.GetDeferredDelayMs();
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), () =>
            {
                _pendingUpdate = false;
                if (_updateThrottler.NeedsDeferredUpdate)
                {
                    _updateThrottler.ShouldUpdate();
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
        ThrottledUpdate();
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
            
            var now = DateTime.UtcNow;
            if ((now - _lastWaveformInvalidate).TotalMilliseconds >= ThrottleMs)
            {
                _lastWaveformInvalidate = now;
                WaveformView.Invalidate();
            }
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
