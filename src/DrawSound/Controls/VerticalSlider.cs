namespace DrawSound.Controls;

/// <summary>
/// Custom vertical slider that works properly on Android
/// Uses StartInteraction/DragInteraction events for touch handling
/// </summary>
public class VerticalSlider : GraphicsView
{
    private float _value;

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
        
        // Use GraphicsView touch events - these work on Android
        StartInteraction += OnStartInteraction;
        DragInteraction += OnDragInteraction;
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        var touch = e.Touches.FirstOrDefault();
        if (touch != default)
        {
            UpdateValueFromY((float)touch.Y);
        }
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        var touch = e.Touches.FirstOrDefault();
        if (touch != default)
        {
            UpdateValueFromY((float)touch.Y);
        }
    }

    private void UpdateValueFromY(float y)
    {
        float height = (float)Height;
        if (height <= 0) height = 100; // fallback
        
        float thumbRadius = 10;
        float usableHeight = height - thumbRadius * 2;
        float normalizedY = (y - thumbRadius) / usableHeight;
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
            float trackWidth = 8;
            float thumbRadius = 12;
            float trackX = (width - trackWidth) / 2;
            float usableHeight = height - thumbRadius * 2;
            float thumbY = thumbRadius + (1 - _slider.Value) * usableHeight;

            // Draw track background
            canvas.FillColor = _slider.TrackColor;
            canvas.FillRoundedRectangle(trackX, thumbRadius, trackWidth, usableHeight, 4);

            // Draw filled portion (from bottom up)
            if (_slider.Value > 0.01f)
            {
                canvas.FillColor = _slider.FillColor;
                float fillHeight = _slider.Value * usableHeight;
                float fillY = height - thumbRadius - fillHeight;
                canvas.FillRoundedRectangle(trackX, fillY, trackWidth, fillHeight, 4);
            }

            // Draw thumb shadow
            canvas.FillColor = Colors.Black.WithAlpha(0.3f);
            canvas.FillCircle(width / 2 + 1, thumbY + 2, thumbRadius);

            // Draw thumb
            canvas.FillColor = _slider.ThumbColor;
            canvas.FillCircle(width / 2, thumbY, thumbRadius);
            
            // Thumb border
            canvas.StrokeColor = _slider.FillColor;
            canvas.StrokeSize = 3;
            canvas.DrawCircle(width / 2, thumbY, thumbRadius - 1);
        }
    }
}
