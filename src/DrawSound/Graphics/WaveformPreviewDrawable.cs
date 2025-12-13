namespace DrawSound.Graphics;

/// <summary>
/// Read-only waveform preview showing the mixed result
/// </summary>
public class WaveformPreviewDrawable : IDrawable
{
    private float[] _waveTable = Array.Empty<float>();
    private readonly object _lock = new();

    public Color WaveColor { get; set; } = Colors.Lime;
    public Color BackgroundColor { get; set; } = Color.FromArgb("#0a0a15");
    public Color GridColor { get; set; } = Color.FromArgb("#1a1a2a");
    public float StrokeWidth { get; set; } = 2f;

    public void SetWaveTable(float[] waveTable)
    {
        lock (_lock)
        {
            _waveTable = (float[])waveTable.Clone();
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = BackgroundColor;
        canvas.FillRectangle(dirtyRect);

        // Center line
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;
        float centerY = dirtyRect.Height / 2;
        canvas.DrawLine(0, centerY, dirtyRect.Width, centerY);

        float[] samples;
        lock (_lock)
        {
            samples = (float[])_waveTable.Clone();
        }

        if (samples.Length < 2) return;

        // Draw 2 cycles of the waveform
        DrawWaveform(canvas, dirtyRect, samples, 2);
    }

    private void DrawWaveform(ICanvas canvas, RectF rect, float[] samples, int cycles)
    {
        canvas.StrokeColor = WaveColor;
        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        float centerY = rect.Height / 2;
        float amplitude = rect.Height * 0.4f;
        int totalSamples = samples.Length * cycles;
        float xStep = rect.Width / totalSamples;

        var path = new PathF();

        for (int i = 0; i < totalSamples; i++)
        {
            int sampleIndex = i % samples.Length;
            float x = i * xStep;
            float y = centerY - (samples[sampleIndex] * amplitude * 2);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path);
    }
}

