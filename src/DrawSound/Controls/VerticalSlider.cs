namespace DrawSound.Controls;

/// <summary>
/// Custom vertical slider that works properly on Android
/// </summary>
public class VerticalSlider : GraphicsView
{
    private float _value;
    private bool _isDragging;

    public event EventHandler<float>? ValueChanged;

    public float Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0f, 1f);
            Invalidate();
        }
    }

    public Color TrackColor { get; set; } = Color.FromArgb("#333");
    public Color FillColor { get; set; } = Colors.Orange;
    public Color ThumbColor { get; set; } = Colors.White;

    public VerticalSlider()
    {
        Drawable = new VerticalSliderDrawable(this);
        
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(panGesture);

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnTapped;
        GestureRecognizers.Add(tapGesture);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.HasValue)
        {
            UpdateValueFromY((float)position.Value.Y);
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isDragging = true;
                break;
            case GestureStatus.Running:
                if (_isDragging)
                {
                    // Calculate new position based on current thumb position + delta
                    float currentY = (1 - _value) * (float)Height;
                    float newY = currentY + (float)e.TotalY;
                    UpdateValueFromY(newY);
                }
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDragging = false;
                break;
        }
    }

    private void UpdateValueFromY(float y)
    {
        float normalizedY = y / (float)Height;
        float newValue = 1f - Math.Clamp(normalizedY, 0f, 1f);
        
        if (Math.Abs(newValue - _value) > 0.001f)
        {
            _value = newValue;
            Invalidate();
            ValueChanged?.Invoke(this, _value);
        }
    }

    private class VerticalSliderDrawable : IDrawable
    {
        private readonly VerticalSlider _slider;

        public VerticalSliderDrawable(VerticalSlider slider)
        {
            _slider = slider;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float width = dirtyRect.Width;
            float height = dirtyRect.Height;
            float trackWidth = 6;
            float thumbRadius = 10;
            float trackX = (width - trackWidth) / 2;
            float thumbY = (1 - _slider.Value) * (height - thumbRadius * 2) + thumbRadius;

            // Draw track background
            canvas.FillColor = _slider.TrackColor;
            canvas.FillRoundedRectangle(trackX, thumbRadius, trackWidth, height - thumbRadius * 2, 3);

            // Draw filled portion
            canvas.FillColor = _slider.FillColor;
            float fillHeight = _slider.Value * (height - thumbRadius * 2);
            canvas.FillRoundedRectangle(trackX, height - thumbRadius - fillHeight, trackWidth, fillHeight, 3);

            // Draw thumb
            canvas.FillColor = _slider.ThumbColor;
            canvas.FillCircle(width / 2, thumbY, thumbRadius);
            
            // Thumb border
            canvas.StrokeColor = _slider.FillColor;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(width / 2, thumbY, thumbRadius);
        }
    }
}

