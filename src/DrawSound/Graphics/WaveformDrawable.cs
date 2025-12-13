namespace DrawSound.Graphics;

public class WaveformDrawable : IDrawable
{
    private float[]? _waveTable;
    private readonly object _lock = new();

    public Color WaveColor { get; set; } = Colors.Cyan;
    public Color BackgroundColor { get; set; } = Color.FromArgb("#0f0f23");
    public float StrokeWidth { get; set; } = 2f;

    public void SetWaveTable(float[]? waveTable)
    {
        lock (_lock)
        {
            _waveTable = waveTable;
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Draw background
        canvas.FillColor = BackgroundColor;
        canvas.FillRectangle(dirtyRect);

        float[]? samples;
        lock (_lock)
        {
            samples = _waveTable;
        }

        if (samples == null || samples.Length < 2)
        {
            DrawCenterLine(canvas, dirtyRect);
            return;
        }

        DrawCenterLine(canvas, dirtyRect);
        DrawWaveform(canvas, dirtyRect, samples);
    }

    private void DrawCenterLine(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = Colors.DimGray;
        canvas.StrokeSize = 1;
        float centerY = rect.Height / 2;
        canvas.DrawLine(0, centerY, rect.Width, centerY);
    }

    private void DrawWaveform(ICanvas canvas, RectF rect, float[] samples)
    {
        canvas.StrokeColor = WaveColor;
        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        float centerY = rect.Height / 2;
        float amplitude = rect.Height * 0.4f; // 80% of half-height
        
        // Draw multiple cycles to fill the canvas
        int totalSamples = samples.Length * 3; // Show 3 cycles
        float xStep = rect.Width / totalSamples;

        var path = new PathF();
        bool first = true;

        for (int i = 0; i < totalSamples; i++)
        {
            int sampleIndex = i % samples.Length;
            float x = i * xStep;
            float y = centerY - (samples[sampleIndex] * amplitude * 2); // Invert Y (screen coords)

            if (first)
            {
                path.MoveTo(x, y);
                first = false;
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        canvas.DrawPath(path);
    }
}

