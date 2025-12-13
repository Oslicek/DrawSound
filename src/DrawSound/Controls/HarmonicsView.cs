namespace DrawSound.Controls;

/// <summary>
/// Minimal vertical sliders - 6 sliders for harmonics control
/// </summary>
public class HarmonicsView : IDrawable
{
    public const int SliderCount = 6;
    
    private readonly float[] _levels = new float[SliderCount];
    private int _activeSlider = -1;

    public event EventHandler<(int Index, float Value)>? ValueChanged;
    
    public IDrawable Drawable => this;

    public HarmonicsView()
    {
        _levels[0] = 1.0f; // First slider (base frequency) at max
    }

    public float[] GetLevels() => (float[])_levels.Clone();

    public void OnTouch(float x, float y, float width, float height, bool isStart)
    {
        if (width <= 0 || height <= 0) return;
        
        if (isStart)
        {
            // Determine which slider column was touched
            float colWidth = width / SliderCount;
            _activeSlider = Math.Clamp((int)(x / colWidth), 0, SliderCount - 1);
        }
        
        if (_activeSlider >= 0)
        {
            // Simple Y to value mapping (0 at bottom, 1 at top)
            float value = 1.0f - (y / height);
            float newValue = Math.Clamp(value, 0f, 1f);
            
            if (Math.Abs(newValue - _levels[_activeSlider]) > 0.005f)
            {
                _levels[_activeSlider] = newValue;
                ValueChanged?.Invoke(this, (_activeSlider, newValue));
            }
        }
    }

    public void OnTouchEnd()
    {
        _activeSlider = -1;
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        float width = rect.Width;
        float height = rect.Height;
        float colWidth = width / SliderCount;
        
        // Background
        canvas.FillColor = Color.FromArgb("#111");
        canvas.FillRectangle(rect);

        for (int i = 0; i < SliderCount; i++)
        {
            float centerX = colWidth * i + colWidth / 2;
            float level = _levels[i];
            
            // Track (vertical line in center of column)
            canvas.StrokeColor = Color.FromArgb("#444");
            canvas.StrokeSize = 4;
            canvas.DrawLine(centerX, 10, centerX, height - 10);
            
            // Filled portion (from bottom up)
            float fillTop = height - 10 - (level * (height - 20));
            canvas.StrokeColor = i == 0 ? Colors.Cyan : Colors.Orange;
            canvas.StrokeSize = 6;
            canvas.DrawLine(centerX, height - 10, centerX, fillTop);
            
            // Thumb
            float thumbY = fillTop;
            canvas.FillColor = i == _activeSlider ? Colors.White : (i == 0 ? Colors.Cyan : Colors.Orange);
            canvas.FillCircle(centerX, thumbY, i == _activeSlider ? 14 : 10);
        }
    }
}
