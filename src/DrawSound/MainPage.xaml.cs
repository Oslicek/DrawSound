using DrawSound.Controls;
using DrawSound.Core.Audio;
using DrawSound.Core.Utils;
using DrawSound.Graphics;
using DrawSound.Services;
using Microsoft.Maui.Controls.Shapes;

namespace DrawSound;

public partial class MainPage : ContentPage
{
    private const int SampleRate = 44100;
    private const int EditingSamples = 256;
    private const int PreviewSamples = 64;
    private const int ThrottleMs = 50;
    
    private readonly ITonePlayer _tonePlayer;
    private readonly float[] _harmonicLevels;
    private readonly UpdateThrottler _updateThrottler;
    private readonly HarmonicsView _harmonicsControl;
    private readonly EnvelopeView _envelopeControl;
    private readonly PianoKeyboard _pianoControl;
    private readonly HashSet<double> _activeFrequencies = new();
    private bool _isPlaying;
    private bool _pendingUpdate;
    private float _canvasWidth;
    private float _canvasHeight;
    private DateTime _lastWaveformInvalidate = DateTime.MinValue;
    private bool _isLandscape;
    private readonly AHDSHRSettings _envelope = new()
    {
        AttackMs = 10,
        Hold1Ms = 0,
        DecayMs = 60,
        SustainLevel = 0.7f,
        Hold2Ms = -1,
        ReleaseMs = 120
    };
    private bool _hold2Infinite = true;
    
    // Landscape layout elements
    private Grid? _landscapeLayout;
    private GraphicsView? _landscapeWaveformView;
    private GraphicsView? _landscapeHarmonicsView;
    private GraphicsView? _landscapePreviewView;
    private GraphicsView? _landscapePianoView;
    private GraphicsView? _landscapeEnvelopeView;

    public MainPage(ITonePlayer tonePlayer)
	{
		InitializeComponent();
        _tonePlayer = tonePlayer;
        _harmonicLevels = HarmonicMixer.GetDefaultLevels();
        _updateThrottler = new UpdateThrottler(ThrottleMs);

        // Harmonics sliders
        _harmonicsControl = new HarmonicsView();
        _harmonicsControl.ValueChanged += OnHarmonicValueChanged;
        HarmonicsView.Drawable = _harmonicsControl.Drawable;
        
        SetupHarmonicsTouch(HarmonicsView);

        // Envelope sliders
        _envelopeControl = new EnvelopeView();
        _envelopeControl.ValueChanged += OnEnvelopeValueChanged;
        EnvelopeView.Drawable = _envelopeControl.Drawable;
        SetupEnvelopeTouch(EnvelopeView);

        // Piano keyboard
        _pianoControl = new PianoKeyboard();
        _pianoControl.KeyPressed += OnPianoKeyPressed;
        _pianoControl.KeyReleased += OnPianoKeyReleased;
        PianoView.Drawable = _pianoControl.Drawable;
        
        SetupPianoTouch(PianoView);

        WaveformDrawable.WaveTableChanged += OnWaveTableChanged;
        
        InitEnvelopeUi();

        // Initial preview update
        Dispatcher.Dispatch(UpdatePreview);
    }

    private void SetupHarmonicsTouch(GraphicsView view)
    {
        view.StartInteraction += (s, e) => 
        {
            var touch = e.Touches.FirstOrDefault();
            if (touch != default)
            {
                _harmonicsControl.OnTouch((float)touch.X, (float)touch.Y, 
                    (float)view.Width, (float)view.Height, isStart: true);
                view.Invalidate();
            }
        };
        view.DragInteraction += (s, e) =>
        {
            var touch = e.Touches.FirstOrDefault();
            if (touch != default)
            {
                _harmonicsControl.OnTouch((float)touch.X, (float)touch.Y,
                    (float)view.Width, (float)view.Height, isStart: false);
                view.Invalidate();
            }
        };
        view.EndInteraction += (s, e) =>
        {
            _harmonicsControl.OnTouchEnd();
            view.Invalidate();
        };
    }

    private void SetupPianoTouch(GraphicsView view)
    {
        view.StartInteraction += (s, e) =>
        {
            var touches = e.Touches.Select((t, i) => ((long)i, (float)t.X, (float)t.Y)).ToList();
            _pianoControl.OnTouches(touches, (float)view.Width, (float)view.Height, isStart: true);
            view.Invalidate();
        };
        view.DragInteraction += (s, e) =>
        {
            var touches = e.Touches.Select((t, i) => ((long)i, (float)t.X, (float)t.Y)).ToList();
            _pianoControl.OnTouches(touches, (float)view.Width, (float)view.Height, isStart: false);
            view.Invalidate();
        };
        view.EndInteraction += (s, e) =>
        {
            var touches = e.Touches.Select((t, i) => ((long)i, (float)t.X, (float)t.Y)).ToList();
            _pianoControl.OnTouchesEnd(touches);
            view.Invalidate();
        };
    }

    private void SetupEnvelopeTouch(GraphicsView view)
    {
        view.StartInteraction += (s, e) =>
        {
            var touch = e.Touches.FirstOrDefault();
            if (touch != default)
            {
                _envelopeControl.OnTouch((float)touch.X, (float)touch.Y, (float)view.Width, (float)view.Height, true);
                view.Invalidate();
            }
        };
        view.DragInteraction += (s, e) =>
        {
            var touch = e.Touches.FirstOrDefault();
            if (touch != default)
            {
                _envelopeControl.OnTouch((float)touch.X, (float)touch.Y, (float)view.Width, (float)view.Height, false);
                view.Invalidate();
            }
        };
        view.EndInteraction += (s, e) =>
        {
            _envelopeControl.OnTouchEnd();
            view.Invalidate();
        };
    }

    private void SetupWaveformTouch(GraphicsView view)
    {
        view.StartInteraction += (s, e) =>
        {
            var point = e.Touches.FirstOrDefault();
            if (point != default)
            {
                WaveformDrawable.StartTouch(point.X, point.Y, (float)view.Width, (float)view.Height);
                view.Invalidate();
            }
        };
        view.DragInteraction += (s, e) =>
        {
            var point = e.Touches.FirstOrDefault();
            if (point != default)
            {
                WaveformDrawable.DragTouch(point.X, point.Y, (float)view.Width, (float)view.Height);
                var now = DateTime.UtcNow;
                if ((now - _lastWaveformInvalidate).TotalMilliseconds >= ThrottleMs)
                {
                    _lastWaveformInvalidate = now;
                    view.Invalidate();
                }
            }
        };
        view.EndInteraction += (s, e) =>
        {
            WaveformDrawable.EndTouch();
        };
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        bool isLandscape = Width > Height;
        
        if (isLandscape != _isLandscape)
        {
            _isLandscape = isLandscape;
            UpdateLayout();
        }
    }

    private void UpdateLayout()
    {
        if (_isLandscape)
        {
            ShowLandscapeLayout();
        }
        else
        {
            ShowPortraitLayout();
        }
    }

    private void ShowPortraitLayout()
    {
        PortraitLayout.IsVisible = true;
        if (_landscapeLayout != null)
        {
            _landscapeLayout.IsVisible = false;
        }
        
        // Reassign drawables to portrait views
        HarmonicsView.Drawable = _harmonicsControl.Drawable;
        PianoView.Drawable = _pianoControl.Drawable;
        PreviewView.Drawable = PreviewDrawable;
        WaveformView.Drawable = WaveformDrawable;
    }

    private void ShowLandscapeLayout()
    {
        PortraitLayout.IsVisible = false;
        
        if (_landscapeLayout == null)
        {
            CreateLandscapeLayout();
        }
        
        _landscapeLayout!.IsVisible = true;
        
        // Reassign drawables to landscape views
        _landscapeWaveformView!.Drawable = WaveformDrawable;
        _landscapeHarmonicsView!.Drawable = _harmonicsControl.Drawable;
        _landscapePreviewView!.Drawable = PreviewDrawable;
        _landscapePianoView!.Drawable = _pianoControl.Drawable;
        _landscapeEnvelopeView!.Drawable = _envelopeControl.Drawable;
    }

    private void CreateLandscapeLayout()
    {
        _landscapeLayout = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(130)),
                new RowDefinition(new GridLength(80))
            },
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                // Narrow the editor canvas and give noticeably more room to sliders/preview
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star))
            },
            Padding = new Thickness(8),
            BackgroundColor = Color.FromArgb("#1a1a2e")
        };

        // Wave canvas (top-left)
        var waveformBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#512BD4"),
            StrokeThickness = 2,
            Margin = new Thickness(0, 0, 4, 4)
        };
        _landscapeWaveformView = new GraphicsView();
        SetupWaveformTouch(_landscapeWaveformView);
        waveformBorder.Content = _landscapeWaveformView;
        Grid.SetRow(waveformBorder, 0);
        Grid.SetColumn(waveformBorder, 0);
        _landscapeLayout.Children.Add(waveformBorder);

        // Harmonics sliders (top-middle)
        var harmonicsBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#333"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#0f0f1a"),
            Margin = new Thickness(0, 0, 4, 4),
            MinimumWidthRequest = 240
        };
        _landscapeHarmonicsView = new GraphicsView();
        SetupHarmonicsTouch(_landscapeHarmonicsView);
        harmonicsBorder.Content = _landscapeHarmonicsView;
        Grid.SetRow(harmonicsBorder, 0);
        Grid.SetColumn(harmonicsBorder, 1);
        _landscapeLayout.Children.Add(harmonicsBorder);

        // Preview (top-right)
        var previewBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#2a5a2a"),
            StrokeThickness = 1,
            Margin = new Thickness(0, 0, 0, 4),
            MinimumWidthRequest = 260
        };
        _landscapePreviewView = new GraphicsView();
        previewBorder.Content = _landscapePreviewView;
        Grid.SetRow(previewBorder, 0);
        Grid.SetColumn(previewBorder, 2);
        _landscapeLayout.Children.Add(previewBorder);

        // Envelope (middle row, full width)
        var envelopeBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#333"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#0f0f1a"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        _landscapeEnvelopeView = new GraphicsView();
        SetupEnvelopeTouch(_landscapeEnvelopeView);
        envelopeBorder.Content = _landscapeEnvelopeView;
        Grid.SetRow(envelopeBorder, 1);
        Grid.SetColumn(envelopeBorder, 0);
        Grid.SetColumnSpan(envelopeBorder, 3);
        _landscapeLayout.Children.Add(envelopeBorder);

        // Piano (bottom, full width)
        var pianoBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#444"),
            StrokeThickness = 1
        };
        _landscapePianoView = new GraphicsView();
        SetupPianoTouch(_landscapePianoView);
        pianoBorder.Content = _landscapePianoView;
        Grid.SetRow(pianoBorder, 2);
        Grid.SetColumn(pianoBorder, 0);
        Grid.SetColumnSpan(pianoBorder, 3);
        _landscapeLayout.Children.Add(pianoBorder);

        SyncEnvelopeUI();

        // Add control buttons overlay (top-left corner)
        var buttonsStack = new HorizontalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(8),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start
        };
        
        var sineBtn = new Button { Text = "∿", FontSize = 12, BackgroundColor = Color.FromArgb("#2a5a2a"), 
            TextColor = Colors.White, CornerRadius = 4, WidthRequest = 35, HeightRequest = 28 };
        sineBtn.Clicked += OnGenerateSineClicked;
        
        var clearBtn = new Button { Text = "⌫", FontSize = 12, BackgroundColor = Color.FromArgb("#5a2a2a"),
            TextColor = Colors.White, CornerRadius = 4, WidthRequest = 35, HeightRequest = 28 };
        clearBtn.Clicked += OnClearClicked;
        
        var deleteBtn = new Button { Text = "✕", FontSize = 12, BackgroundColor = Color.FromArgb("#4a3a2a"),
            TextColor = Colors.White, CornerRadius = 4, WidthRequest = 35, HeightRequest = 28 };
        deleteBtn.Clicked += OnDeleteNodeClicked;
        
        buttonsStack.Children.Add(sineBtn);
        buttonsStack.Children.Add(clearBtn);
        buttonsStack.Children.Add(deleteBtn);
        
        Grid.SetRow(buttonsStack, 0);
        Grid.SetColumn(buttonsStack, 0);
        _landscapeLayout.Children.Add(buttonsStack);

        // Add to page
        if (Content is Grid mainGrid)
        {
            // Replace content with a new container
            var container = new Grid();
            container.Children.Add(PortraitLayout);
            container.Children.Add(_landscapeLayout);
            Content = container;
        }
    }

    private void OnPianoKeyPressed(object? sender, double frequency)
    {
        _activeFrequencies.Add(frequency);
        _isPlaying = true;
        WaveformDrawable.SetPlaying(true);
        InvalidateWaveformViews();

        var playbackWave = GetPlaybackWaveTable(frequency);
        _tonePlayer.StartTone(frequency, playbackWave);
    }

    private void OnPianoKeyReleased(object? sender, double frequency)
    {
        _activeFrequencies.Remove(frequency);
        _isPlaying = _activeFrequencies.Count > 0;
        if (!_isPlaying)
        {
            WaveformDrawable.SetPlaying(false);
            _tonePlayer.StopAll(); // ensure no stuck voices
        }
        InvalidateWaveformViews();

        _tonePlayer.StopTone(frequency);
    }

    private void InvalidateWaveformViews()
    {
        WaveformView.Invalidate();
        _landscapeWaveformView?.Invalidate();
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
        _landscapePreviewView?.Invalidate();
    }

    private void UpdatePlayback()
    {
        if (!_isPlaying) return;

        foreach (var freq in _activeFrequencies)
        {
            var playbackWave = GetPlaybackWaveTable(freq);
            _tonePlayer.UpdateWaveTable(freq, playbackWave);
        }
    }

    private void OnWaveTableChanged(object? sender, float[] waveTable)
    {
        ThrottledUpdate();
    }

    private void OnGenerateSineClicked(object? sender, EventArgs e)
    {
        WaveformDrawable.GenerateSineWave();
        InvalidateWaveformViews();
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        WaveformDrawable.ClearWave();
        InvalidateWaveformViews();
    }

    private void OnDeleteNodeClicked(object? sender, EventArgs e)
    {
        WaveformDrawable.DeleteSelectedNode();
        InvalidateWaveformViews();
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

    private void InitEnvelopeUi()
    {
        SyncEnvelopeUI();
    }

    private void OnEnvelopeValueChanged(object? sender, (int Index, float Value) e)
    {
        switch (e.Index)
        {
            case 0: _envelope.AttackMs = Math.Clamp(e.Value, 0, 1000); break;
            case 1: _envelope.Hold1Ms = Math.Clamp(e.Value, 0, 1000); break;
            case 2: _envelope.DecayMs = Math.Clamp(e.Value, 0, 1000); break;
            case 3: _envelope.SustainLevel = Math.Clamp(e.Value, 0, 1); break;
            case 4:
                if (!_hold2Infinite)
                    _envelope.Hold2Ms = Math.Clamp(e.Value, 0, 1000);
                break;
            case 5: _envelope.ReleaseMs = Math.Clamp(e.Value, 0, 1000); break;
        }
        SyncEnvelopeUI();
    }

    private void OnHold2ToggleClicked(object? sender, EventArgs e)
    {
        _hold2Infinite = !_hold2Infinite;
        _envelope.Hold2Ms = _hold2Infinite ? -1 : Math.Max(0, _envelope.Hold2Ms);
        SyncEnvelopeUI();
    }

    private void SyncEnvelopeUI()
    {
        _envelopeControl.SetValues(new[]
        {
            _envelope.AttackMs,
            _envelope.Hold1Ms,
            _envelope.DecayMs,
            _envelope.SustainLevel,
            Math.Max(0, _envelope.Hold2Ms),
            _envelope.ReleaseMs
        });
        _envelopeControl.SetDisabled(4, _hold2Infinite);
        UpdateHold2ToggleVisual(Hold2Toggle, _hold2Infinite);
        _landscapeEnvelopeView?.Invalidate();
        EnvelopeView.Invalidate();
    }

    private static void UpdateHold2ToggleVisual(Button toggle, bool infinite)
    {
        toggle.Text = infinite ? "∞" : "↺";
        toggle.BackgroundColor = infinite ? Color.FromArgb("#555") : Color.FromArgb("#2a5a2a");
	}
}
