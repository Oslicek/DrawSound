namespace DrawSound.Controls;

/// <summary>
/// Custom vertical slider that works properly on Android
/// Uses StartInteraction/DragInteraction events for touch handling
/// Optimized for performance with throttled updates
/// </summary>
public class VerticalSlider : GraphicsView
{
    private float _value;
    private float _displayValue; // Separate display value for smooth visuals
    private DateTime _lastInvalidate = DateTime.MinValue;
    private const int InvalidateThrottleMs = 16; // ~60 FPS max

    public event EventHandler<float>? ValueChanged;

    public float Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0f, 1f);
            _displayValue = _value;
            Invalidate();
        }
    }

    public Color TrackColor { get; set; } = Color.FromArgb("#333");
    public Color FillColor { get; set; } = Colors.Orange;
    public Color ThumbColor { get; set; } = Colors.White;

    public VerticalSlider()
    {
        Drawable = new VerticalSliderDrawable(this);
        
        // Use GraphicsView touch events - these work on Android
        StartInteraction += OnStartInteraction;
        DragInteraction += OnDragInteraction;
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        var touch = e.Touches.FirstOrDefault();
        if (touch != default)
        {
            UpdateValueFromY((float)touch.Y, forceRedraw: true);
        }
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        var touch = e.Touches.FirstOrDefault();
        if (touch != default)
        {
            UpdateValueFromY((float)touch.Y, forceRedraw: false);
        }
    }

    private void UpdateValueFromY(float y, bool forceRedraw)
    {
        float height = (float)Height;
        if (height <= 0) height = 100; // fallback
        
        float thumbRadius = 10;
        float usableHeight = height - thumbRadius * 2;
        float normalizedY = (y - thumbRadius) / usableHeight;
        float newValue = 1f - Math.Clamp(normalizedY, 0f, 1f);
        
        // Only update if value changed enough (reduces noise)
        if (Math.Abs(newValue - _value) > 0.005f)
        {
            _value = newValue;
            _displayValue = newValue;
            
            // Throttle visual updates
            var now = DateTime.UtcNow;
            if (forceRedraw || (now - _lastInvalidate).TotalMilliseconds >= InvalidateThrottleMs)
            {
                _lastInvalidate = now;
                Invalidate();
            }
            
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
            float usableHeight = height - thumbRadius * 2;
            float thumbY = thumbRadius + (1 - _slider._displayValue) * usableHeight;

            // Draw track background - simple rectangle
            canvas.FillColor = _slider.TrackColor;
            canvas.FillRectangle(trackX, thumbRadius, trackWidth, usableHeight);

            // Draw filled portion (from bottom up)
            if (_slider._displayValue > 0.01f)
            {
                canvas.FillColor = _slider.FillColor;
                float fillHeight = _slider._displayValue * usableHeight;
                canvas.FillRectangle(trackX, height - thumbRadius - fillHeight, trackWidth, fillHeight);
            }

            // Draw thumb - simple circle, no shadow
            canvas.FillColor = _slider.FillColor;
            canvas.FillCircle(width / 2, thumbY, thumbRadius);
            
            // White center dot
            canvas.FillColor = _slider.ThumbColor;
            canvas.FillCircle(width / 2, thumbY, 4);
        }
    }
}
