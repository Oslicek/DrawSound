namespace DrawSound.Controls;

/// <summary>
/// Single GraphicsView drawable that renders all harmonic sliders
/// Much more efficient than 12 separate GraphicsView instances
/// </summary>
public class HarmonicsView : IDrawable
{
    private readonly float[] _levels;
    private readonly string[] _labels;
    private int _activeSlider = -1;
    private float _viewWidth;
    private float _viewHeight;
    private DateTime _lastUpdate = DateTime.MinValue;
    private const int ThrottleMs = 32;

    public event EventHandler<(int Index, float Value)>? ValueChanged;
    public event EventHandler? NeedsRedraw;

    public int SliderCount { get; }
    public IDrawable Drawable => this;

    public HarmonicsView(int sliderCount = 12)
    {
        SliderCount = sliderCount;
        _levels = new float[sliderCount];
        _levels[0] = 1.0f; // Base frequency at max
        
        _labels = new string[sliderCount];
        _labels[0] = "f";
        for (int i = 1; i < sliderCount; i++)
        {
            _labels[i] = $"{i + 1}f";
        }
    }

    public float GetLevel(int index) => index >= 0 && index < SliderCount ? _levels[index] : 0f;
    
    public void SetLevel(int index, float value)
    {
        if (index >= 0 && index < SliderCount)
        {
            _levels[index] = Math.Clamp(value, 0f, 1f);
        }
    }

    public float[] GetAllLevels() => (float[])_levels.Clone();

    public void HandleStartTouch(float x, float y, float viewWidth, float viewHeight)
    {
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;
        _activeSlider = GetSliderAtX(x);
        if (_activeSlider >= 0)
        {
            UpdateSliderValue(y, forceUpdate: true);
        }
    }

    public void HandleDragTouch(float x, float y, float viewWidth, float viewHeight)
    {
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;
        if (_activeSlider >= 0)
        {
            UpdateSliderValue(y, forceUpdate: false);
        }
    }

    public void HandleEndTouch()
    {
        if (_activeSlider >= 0)
        {
            // Final update and redraw on release
            NeedsRedraw?.Invoke(this, EventArgs.Empty);
            ValueChanged?.Invoke(this, (_activeSlider, _levels[_activeSlider]));
        }
        _activeSlider = -1;
    }

    private int GetSliderAtX(float x)
    {
        if (_viewWidth <= 0) return 0;
        float sliderWidth = _viewWidth / SliderCount;
        int index = (int)(x / sliderWidth);
        return Math.Clamp(index, 0, SliderCount - 1);
    }

    private void UpdateSliderValue(float y, bool forceUpdate)
    {
        if (_activeSlider < 0 || _viewHeight <= 0) return;

        float padding = 20;
        float labelHeight = 18;
        float usableHeight = _viewHeight - padding * 2 - labelHeight;
        
        float normalizedY = (y - padding) / usableHeight;
        float newValue = 1f - Math.Clamp(normalizedY, 0f, 1f);

        if (Math.Abs(newValue - _levels[_activeSlider]) > 0.01f)
        {
            _levels[_activeSlider] = newValue;

            var now = DateTime.UtcNow;
            if (forceUpdate || (now - _lastUpdate).TotalMilliseconds >= ThrottleMs)
            {
                _lastUpdate = now;
                NeedsRedraw?.Invoke(this, EventArgs.Empty);
                ValueChanged?.Invoke(this, (_activeSlider, newValue));
            }
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float width = dirtyRect.Width;
        float height = dirtyRect.Height;
        float sliderWidth = width / SliderCount;
        float labelHeight = 18;
        float padding = 15;
        float trackHeight = height - labelHeight - padding * 2;
        float trackWidth = 5;
        float thumbRadius = 8;

        for (int i = 0; i < SliderCount; i++)
        {
            float x = sliderWidth * i + sliderWidth / 2;
            float trackTop = padding;
            float trackBottom = padding + trackHeight;
            float level = _levels[i];
            float thumbY = trackTop + (1 - level) * trackHeight;

            bool isBase = i == 0;
            bool isActive = i == _activeSlider;
            
            Color fillColor = isBase ? Colors.Cyan : Colors.Orange;
            Color trackColor = Color.FromArgb("#333");

            // Draw track background
            canvas.FillColor = trackColor;
            canvas.FillRectangle(x - trackWidth / 2, trackTop, trackWidth, trackHeight);

            // Draw filled portion
            if (level > 0.01f)
            {
                canvas.FillColor = fillColor;
                float fillHeight = level * trackHeight;
                canvas.FillRectangle(x - trackWidth / 2, trackBottom - fillHeight, trackWidth, fillHeight);
            }

            // Draw thumb
            float currentRadius = isActive ? thumbRadius + 3 : thumbRadius;
            canvas.FillColor = fillColor;
            canvas.FillCircle(x, thumbY, currentRadius);
            
            // White center
            canvas.FillColor = Colors.White;
            canvas.FillCircle(x, thumbY, 3);

            // Draw label
            canvas.FontColor = isBase ? Colors.Cyan : Color.FromArgb("#666");
            canvas.FontSize = 9;
            canvas.DrawString(_labels[i], x - 12, height - labelHeight, 24, labelHeight, 
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
