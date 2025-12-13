namespace DrawSound.Graphics;

public class EditableWaveformDrawable : IDrawable
{
    private float[] _waveTable;
    private readonly object _lock = new();
    private bool _isPlaying;
    
    // For smooth line drawing
    private int _lastTouchIndex = -1;
    private float _lastTouchValue;

    public Color WaveColor { get; set; } = Colors.Cyan;
    public Color WaveColorPlaying { get; set; } = Colors.Lime;
    public Color BackgroundColor { get; set; } = Color.FromArgb("#0f0f23");
    public Color GridColor { get; set; } = Color.FromArgb("#2a2a4a");
    public float StrokeWidth { get; set; } = 3f;

    public event EventHandler<float[]>? WaveTableChanged;

    public EditableWaveformDrawable()
    {
        _waveTable = GenerateDefaultSineWave(256);
    }

    private static float[] GenerateDefaultSineWave(int samples)
    {
        var wave = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            double phase = 2.0 * Math.PI * i / samples;
            wave[i] = (float)(Math.Sin(phase) * 0.5);
        }
        return wave;
    }

    public void SetWaveTable(float[] waveTable)
    {
        lock (_lock)
        {
            _waveTable = (float[])waveTable.Clone();
        }
    }

    public float[] GetWaveTable()
    {
        lock (_lock)
        {
            return (float[])_waveTable.Clone();
        }
    }

    public void SetPlaying(bool isPlaying)
    {
        _isPlaying = isPlaying;
    }

    public void GenerateSineWave()
    {
        lock (_lock)
        {
            _waveTable = GenerateDefaultSineWave(_waveTable.Length);
        }
        WaveTableChanged?.Invoke(this, GetWaveTable());
    }

    public void ClearWave()
    {
        lock (_lock)
        {
            for (int i = 0; i < _waveTable.Length; i++)
            {
                _waveTable[i] = 0f;
            }
        }
        WaveTableChanged?.Invoke(this, GetWaveTable());
    }

    public void StartTouch(float x, float y, float canvasWidth, float canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var (index, value) = ScreenToWave(x, y, canvasWidth, canvasHeight);
        
        lock (_lock)
        {
            _waveTable[index] = value;
            _lastTouchIndex = index;
            _lastTouchValue = value;
        }

        WaveTableChanged?.Invoke(this, GetWaveTable());
    }

    public void DragTouch(float x, float y, float canvasWidth, float canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var (index, value) = ScreenToWave(x, y, canvasWidth, canvasHeight);

        lock (_lock)
        {
            if (_lastTouchIndex >= 0)
            {
                // Interpolate between last point and current point for smooth line
                InterpolateLine(_lastTouchIndex, _lastTouchValue, index, value);
            }
            else
            {
                _waveTable[index] = value;
            }

            _lastTouchIndex = index;
            _lastTouchValue = value;
        }

        WaveTableChanged?.Invoke(this, GetWaveTable());
    }

    public void EndTouch()
    {
        _lastTouchIndex = -1;
    }

    private (int index, float value) ScreenToWave(float x, float y, float canvasWidth, float canvasHeight)
    {
        float normalizedX = Math.Clamp(x / canvasWidth, 0f, 1f);
        int index = (int)(normalizedX * (_waveTable.Length - 1));
        index = Math.Clamp(index, 0, _waveTable.Length - 1);

        float centerY = canvasHeight / 2;
        float amplitude = canvasHeight * 0.4f;
        float normalizedY = (centerY - y) / (amplitude * 2);
        normalizedY = Math.Clamp(normalizedY, -0.5f, 0.5f);

        return (index, normalizedY);
    }

    private void InterpolateLine(int fromIndex, float fromValue, int toIndex, float toValue)
    {
        if (fromIndex == toIndex)
        {
            _waveTable[toIndex] = toValue;
            return;
        }

        int startIndex = Math.Min(fromIndex, toIndex);
        int endIndex = Math.Max(fromIndex, toIndex);
        
        float startValue = fromIndex < toIndex ? fromValue : toValue;
        float endValue = fromIndex < toIndex ? toValue : fromValue;

        for (int i = startIndex; i <= endIndex; i++)
        {
            float t = (float)(i - startIndex) / (endIndex - startIndex);
            _waveTable[i] = Lerp(startValue, endValue, t);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = BackgroundColor;
        canvas.FillRectangle(dirtyRect);

        DrawGrid(canvas, dirtyRect);

        float[] samples;
        lock (_lock)
        {
            samples = (float[])_waveTable.Clone();
        }

        DrawWaveform(canvas, dirtyRect, samples);
    }

    private void DrawGrid(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;

        float centerY = rect.Height / 2;
        canvas.DrawLine(0, centerY, rect.Width, centerY);

        float quarterHeight = rect.Height / 4;
        canvas.StrokeDashPattern = new float[] { 5, 5 };
        canvas.DrawLine(0, quarterHeight, rect.Width, quarterHeight);
        canvas.DrawLine(0, rect.Height - quarterHeight, rect.Width, rect.Height - quarterHeight);
        
        float quarterWidth = rect.Width / 4;
        for (int i = 1; i < 4; i++)
        {
            canvas.DrawLine(quarterWidth * i, 0, quarterWidth * i, rect.Height);
        }
        
        canvas.StrokeDashPattern = null;
    }

    private void DrawWaveform(ICanvas canvas, RectF rect, float[] samples)
    {
        canvas.StrokeColor = _isPlaying ? WaveColorPlaying : WaveColor;
        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        float centerY = rect.Height / 2;
        float amplitude = rect.Height * 0.4f;
        float xStep = rect.Width / (samples.Length - 1);

        var path = new PathF();

        for (int i = 0; i < samples.Length; i++)
        {
            float xPos = i * xStep;
            float yPos = centerY - (samples[i] * amplitude * 2);

            if (i == 0)
                path.MoveTo(xPos, yPos);
            else
                path.LineTo(xPos, yPos);
        }

        canvas.DrawPath(path);
    }
}
