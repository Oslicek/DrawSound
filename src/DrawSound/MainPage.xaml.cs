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
    private bool _suppressEnvelopeEvents;
    private Slider? _attackLandscape;
    private Slider? _hold1Landscape;
    private Slider? _decayLandscape;
    private Slider? _sustainLandscape;
    private Slider? _hold2Landscape;
    private Slider? _releaseLandscape;
    private Button? _hold2ToggleLandscape;
    
    // Landscape layout elements
    private Grid? _landscapeLayout;
    private GraphicsView? _landscapeWaveformView;
    private GraphicsView? _landscapeHarmonicsView;
    private GraphicsView? _landscapePreviewView;
    private GraphicsView? _landscapePianoView;

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
    }

    private void CreateLandscapeLayout()
    {
        _landscapeLayout = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(70)),
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
        var envelopeRow = BuildEnvelopeRow(isLandscape: true);
        Grid.SetRow(envelopeRow, 1);
        Grid.SetColumn(envelopeRow, 0);
        Grid.SetColumnSpan(envelopeRow, 3);
        _landscapeLayout.Children.Add(envelopeRow);

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
        _suppressEnvelopeEvents = true;
        AttackSlider.Value = _envelope.AttackMs;
        Hold1Slider.Value = _envelope.Hold1Ms;
        DecaySlider.Value = _envelope.DecayMs;
        SustainSlider.Value = _envelope.SustainLevel;
        Hold2Slider.Value = Math.Max(0, _envelope.Hold2Ms);
        Hold2Slider.IsEnabled = !_hold2Infinite;
        ReleaseSlider.Value = _envelope.ReleaseMs;
        UpdateHold2ToggleVisual(Hold2Toggle, _hold2Infinite);
        _suppressEnvelopeEvents = false;
    }

    private View BuildEnvelopeRow(bool isLandscape)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(4, 0),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        Slider NewTimeSlider(out Slider store, double min = 0, double max = 1000)
        {
            var s = new Slider
            {
                Minimum = min,
                Maximum = max,
                MinimumTrackColor = Color.FromArgb("#4CAF50"),
                MaximumTrackColor = Color.FromArgb("#333"),
                ThumbColor = Color.FromArgb("#4CAF50")
            };
            s.ValueChanged += OnEnvelopeSliderChanged;
            store = s;
            return s;
        }

        Slider NewSustainSlider(out Slider store)
        {
            var s = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                MinimumTrackColor = Color.FromArgb("#2196F3"),
                MaximumTrackColor = Color.FromArgb("#333"),
                ThumbColor = Color.FromArgb("#2196F3")
            };
            s.ValueChanged += OnEnvelopeSliderChanged;
            store = s;
            return s;
        }

        Button NewHoldToggle(out Button store)
        {
            var b = new Button
            {
                Text = "∞",
                WidthRequest = 30,
                HeightRequest = 24,
                FontSize = 12,
                BackgroundColor = Color.FromArgb("#333"),
                TextColor = Colors.White
            };
            b.Clicked += OnHold2ToggleClicked;
            store = b;
            return b;
        }

        // Attack
        var atkStack = new VerticalStackLayout { WidthRequest = 64, Spacing = 2 };
        atkStack.Children.Add(new Label { Text = "Atk", FontSize = 10, TextColor = Color.FromArgb("#ccc"), HorizontalOptions = LayoutOptions.Center });
        atkStack.Children.Add(NewTimeSlider(out var atk));
        row.Children.Add(atkStack);

        // Hold1
        var h1Stack = new VerticalStackLayout { WidthRequest = 64, Spacing = 2 };
        h1Stack.Children.Add(new Label { Text = "H1", FontSize = 10, TextColor = Color.FromArgb("#ccc"), HorizontalOptions = LayoutOptions.Center });
        h1Stack.Children.Add(NewTimeSlider(out var h1));
        row.Children.Add(h1Stack);

        // Decay
        var decStack = new VerticalStackLayout { WidthRequest = 64, Spacing = 2 };
        decStack.Children.Add(new Label { Text = "Dec", FontSize = 10, TextColor = Color.FromArgb("#ccc"), HorizontalOptions = LayoutOptions.Center });
        decStack.Children.Add(NewTimeSlider(out var dec));
        row.Children.Add(decStack);

        // Sustain
        var susStack = new VerticalStackLayout { WidthRequest = 64, Spacing = 2 };
        susStack.Children.Add(new Label { Text = "Sus", FontSize = 10, TextColor = Color.FromArgb("#7cc7ff"), HorizontalOptions = LayoutOptions.Center });
        susStack.Children.Add(NewSustainSlider(out var sus));
        row.Children.Add(susStack);

        // Hold2 with toggle
        var h2Stack = new VerticalStackLayout { WidthRequest = 74, Spacing = 2 };
        var h2Header = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        h2Header.Children.Add(new Label { Text = "H2", FontSize = 10, TextColor = Color.FromArgb("#ccc"), HorizontalOptions = LayoutOptions.Center });
        h2Header.Children.Add(NewHoldToggle(out var h2Toggle));
        h2Stack.Children.Add(h2Header);
        h2Stack.Children.Add(NewTimeSlider(out var h2));
        row.Children.Add(h2Stack);

        // Release
        var relStack = new VerticalStackLayout { WidthRequest = 64, Spacing = 2 };
        relStack.Children.Add(new Label { Text = "Rel", FontSize = 10, TextColor = Color.FromArgb("#ccc"), HorizontalOptions = LayoutOptions.Center });
        relStack.Children.Add(NewTimeSlider(out var rel));
        row.Children.Add(relStack);

        if (isLandscape)
        {
            _attackLandscape = atk;
            _hold1Landscape = h1;
            _decayLandscape = dec;
            _sustainLandscape = sus;
            _hold2Landscape = h2;
            _releaseLandscape = rel;
            _hold2ToggleLandscape = h2Toggle;
        }

        return row;
    }

    private void OnEnvelopeSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressEnvelopeEvents) return;

        _suppressEnvelopeEvents = true;

        if (sender == AttackSlider || sender == _attackLandscape)
            _envelope.AttackMs = (float)Math.Clamp(e.NewValue, 0, 1000);
        else if (sender == Hold1Slider || sender == _hold1Landscape)
            _envelope.Hold1Ms = (float)Math.Clamp(e.NewValue, 0, 1000);
        else if (sender == DecaySlider || sender == _decayLandscape)
            _envelope.DecayMs = (float)Math.Clamp(e.NewValue, 0, 1000);
        else if (sender == SustainSlider || sender == _sustainLandscape)
            _envelope.SustainLevel = (float)Math.Clamp(e.NewValue, 0, 1);
        else if ((sender == Hold2Slider || sender == _hold2Landscape) && !_hold2Infinite)
            _envelope.Hold2Ms = (float)Math.Clamp(e.NewValue, 0, 1000);
        else if (sender == ReleaseSlider || sender == _releaseLandscape)
            _envelope.ReleaseMs = (float)Math.Clamp(e.NewValue, 0, 1000);

        SyncEnvelopeUI();
        _suppressEnvelopeEvents = false;
    }

    private void OnHold2ToggleClicked(object? sender, EventArgs e)
    {
        _hold2Infinite = !_hold2Infinite;
        _envelope.Hold2Ms = _hold2Infinite ? -1 : (float)(Hold2Slider.Value);
        SyncEnvelopeUI();
    }

    private void SyncEnvelopeUI()
    {
        _suppressEnvelopeEvents = true;

        // Portrait sliders
        AttackSlider.Value = _envelope.AttackMs;
        Hold1Slider.Value = _envelope.Hold1Ms;
        DecaySlider.Value = _envelope.DecayMs;
        SustainSlider.Value = _envelope.SustainLevel;
        ReleaseSlider.Value = _envelope.ReleaseMs;
        Hold2Slider.Value = Math.Max(0, _envelope.Hold2Ms);
        Hold2Slider.IsEnabled = !_hold2Infinite;
        UpdateHold2ToggleVisual(Hold2Toggle, _hold2Infinite);

        // Landscape sliders (if created)
        if (_attackLandscape != null)
        {
            _attackLandscape.Value = _envelope.AttackMs;
            _hold1Landscape!.Value = _envelope.Hold1Ms;
            _decayLandscape!.Value = _envelope.DecayMs;
            _sustainLandscape!.Value = _envelope.SustainLevel;
            _releaseLandscape!.Value = _envelope.ReleaseMs;
            _hold2Landscape!.Value = Math.Max(0, _envelope.Hold2Ms);
            _hold2Landscape.IsEnabled = !_hold2Infinite;
            UpdateHold2ToggleVisual(_hold2ToggleLandscape!, _hold2Infinite);
        }

        _suppressEnvelopeEvents = false;
    }

    private static void UpdateHold2ToggleVisual(Button toggle, bool infinite)
    {
        toggle.Text = infinite ? "∞" : "↺";
        toggle.BackgroundColor = infinite ? Color.FromArgb("#555") : Color.FromArgb("#2a5a2a");
	}
}
