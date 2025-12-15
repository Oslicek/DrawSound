namespace DrawSound.Controls;

/// <summary>
/// Vertical slider set for AHDSHR envelope. 6 sliders with individual ranges.
/// </summary>
public class EnvelopeView : IDrawable
{
    public const int SliderCount = 6;

    private readonly float[] _values = new float[SliderCount];
    private readonly float[] _mins = new float[SliderCount] { 0, 0, 0, 0, 0, 0 };
    private readonly float[] _maxs = new float[SliderCount] { 1000, 1000, 1000, 1, 1000, 1000 };
    private readonly string[] _labels = new[] { "Atk", "H1", "Dec", "Sus", "H2", "Rel" };
    private readonly bool[] _disabled = new bool[SliderCount];
    private int _active = -1;

    public event EventHandler<(int Index, float Value)>? ValueChanged;

    public IDrawable Drawable => this;

    public void SetValues(float[] values)
    {
        if (values.Length != SliderCount) return;
        Array.Copy(values, _values, SliderCount);
    }

    public void SetDisabled(int index, bool disabled)
    {
        if (index < 0 || index >= SliderCount) return;
        _disabled[index] = disabled;
    }

    public void OnTouch(float x, float y, float width, float height, bool isStart)
    {
        if (width <= 0 || height <= 0) return;

        float colWidth = width / SliderCount;

        if (isStart)
        {
            _active = Math.Clamp((int)(x / colWidth), 0, SliderCount - 1);
            if (_disabled[_active])
                _active = -1;
        }

        if (_active >= 0)
        {
            float norm = 1f - (y / height);
            norm = Math.Clamp(norm, 0f, 1f);
            float val = _mins[_active] + norm * (_maxs[_active] - _mins[_active]);

            if (Math.Abs(val - _values[_active]) > 0.5f * ((_maxs[_active] - _mins[_active]) / 1000f))
            {
                _values[_active] = val;
                ValueChanged?.Invoke(this, (_active, val));
            }
        }
    }

    public void OnTouchEnd()
    {
        _active = -1;
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width;
        float h = rect.Height;
        float col = w / SliderCount;

        canvas.FillColor = Color.FromArgb("#111");
        canvas.FillRectangle(rect);

        for (int i = 0; i < SliderCount; i++)
        {
            float cx = col * i + col / 2;
            float norm = (_values[i] - _mins[i]) / (_maxs[i] - _mins[i]);
            norm = Math.Clamp(norm, 0f, 1f);

            // track
            canvas.StrokeColor = _disabled[i] ? Color.FromArgb("#333") : Color.FromArgb("#444");
            canvas.StrokeSize = 4;
            canvas.DrawLine(cx, 10, cx, h - 24);

            // fill
            float top = h - 24 - norm * (h - 34);
            canvas.StrokeColor = i == 3 ? Color.FromArgb("#2196F3") : Color.FromArgb("#4CAF50");
            if (_disabled[i])
                canvas.StrokeColor = Color.FromArgb("#555");
            canvas.StrokeSize = 6;
            canvas.DrawLine(cx, h - 24, cx, top);

            // thumb
            canvas.FillColor = i == _active ? Colors.White : (_disabled[i] ? Color.FromArgb("#666") : (i == 3 ? Color.FromArgb("#7cc7ff") : Colors.Orange));
            canvas.FillCircle(cx, top, i == _active ? 12 : 9);

            // label
            canvas.FontSize = 10;
            canvas.FontColor = Color.FromArgb("#ccc");
            canvas.DrawString(_labels[i], cx - 14, h - 22, 28, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

