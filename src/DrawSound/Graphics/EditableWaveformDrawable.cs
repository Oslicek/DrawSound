namespace DrawSound.Graphics;

public class EditableWaveformDrawable : IDrawable
{
    private float[] _waveTable;
    private readonly object _lock = new();
    private bool _isPlaying;

    public Color WaveColor { get; set; } = Colors.Cyan;
    public Color WaveColorPlaying { get; set; } = Colors.Lime;
    public Color BackgroundColor { get; set; } = Color.FromArgb("#0f0f23");
    public Color GridColor { get; set; } = Color.FromArgb("#2a2a4a");
    public float StrokeWidth { get; set; } = 3f;

    public event EventHandler<float[]>? WaveTableChanged;

    public EditableWaveformDrawable()
    {
        // Initialize with a default sine wave (will be set properly later)
        _waveTable = GenerateDefaultSineWave(128);
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

    public void HandleTouch(float x, float y, float canvasWidth, float canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        lock (_lock)
        {
            // Convert touch position to wave table index and value
            float normalizedX = Math.Clamp(x / canvasWidth, 0f, 1f);
            int index = (int)(normalizedX * (_waveTable.Length - 1));
            
            // Convert Y to amplitude (-0.5 to 0.5)
            float centerY = canvasHeight / 2;
            float amplitude = canvasHeight * 0.4f;
            float normalizedY = (centerY - y) / (amplitude * 2);
            normalizedY = Math.Clamp(normalizedY, -0.5f, 0.5f);

            // Update the sample and neighbors for smooth editing
            UpdateSampleWithSmoothing(index, normalizedY);
        }

        WaveTableChanged?.Invoke(this, GetWaveTable());
    }

    private void UpdateSampleWithSmoothing(int centerIndex, float value)
    {
        // Update center sample
        _waveTable[centerIndex] = value;

        // Smooth neighboring samples (3 samples on each side)
        int smoothRadius = 3;
        for (int offset = 1; offset <= smoothRadius; offset++)
        {
            float weight = 1f - (offset / (float)(smoothRadius + 1));
            
            int leftIndex = centerIndex - offset;
            int rightIndex = centerIndex + offset;

            if (leftIndex >= 0)
            {
                _waveTable[leftIndex] = Lerp(_waveTable[leftIndex], value, weight * 0.5f);
            }
            if (rightIndex < _waveTable.Length)
            {
                _waveTable[rightIndex] = Lerp(_waveTable[rightIndex], value, weight * 0.5f);
            }
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Draw background
        canvas.FillColor = BackgroundColor;
        canvas.FillRectangle(dirtyRect);

        // Draw grid
        DrawGrid(canvas, dirtyRect);

        float[] samples;
        lock (_lock)
        {
            samples = (float[])_waveTable.Clone();
        }

        // Draw the waveform
        DrawWaveform(canvas, dirtyRect, samples);
    }

    private void DrawGrid(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;

        float centerY = rect.Height / 2;
        
        // Center line (zero)
        canvas.DrawLine(0, centerY, rect.Width, centerY);

        // Quarter lines
        float quarterHeight = rect.Height / 4;
        canvas.StrokeDashPattern = new float[] { 5, 5 };
        canvas.DrawLine(0, quarterHeight, rect.Width, quarterHeight);
        canvas.DrawLine(0, rect.Height - quarterHeight, rect.Width, rect.Height - quarterHeight);
        
        // Vertical lines (quarters of the cycle)
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
            float x = i * xStep;
            float y = centerY - (samples[i] * amplitude * 2);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path);
    }
}

