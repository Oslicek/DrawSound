namespace DrawSound.Graphics;

/// <summary>
/// Read-only waveform preview showing the mixed result
/// Optimized for performance
/// </summary>
public class WaveformPreviewDrawable : IDrawable
{
    private float[] _waveTable = Array.Empty<float>();

    public Color WaveColor { get; set; } = Colors.Lime;
    public Color BackgroundColor { get; set; } = Color.FromArgb("#0a0a15");
    public Color GridColor { get; set; } = Color.FromArgb("#1a1a2a");

    public void SetWaveTable(float[] waveTable)
    {
        _waveTable = waveTable; // Direct reference, no clone needed
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Background
        canvas.FillColor = BackgroundColor;
        canvas.FillRectangle(dirtyRect);

        var samples = _waveTable;
        if (samples.Length < 2) return;

        // Center line
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;
        float centerY = dirtyRect.Height / 2;
        canvas.DrawLine(0, centerY, dirtyRect.Width, centerY);

        // Draw waveform using simple lines (faster than path)
        canvas.StrokeColor = WaveColor;
        canvas.StrokeSize = 2;

        float amplitude = dirtyRect.Height * 0.4f;
        int cycles = 2;
        int totalPoints = samples.Length * cycles;
        float xStep = dirtyRect.Width / totalPoints;

        float prevX = 0;
        float prevY = centerY - (samples[0] * amplitude * 2);

        for (int i = 1; i < totalPoints; i++)
        {
            int sampleIndex = i % samples.Length;
            float x = i * xStep;
            float y = centerY - (samples[sampleIndex] * amplitude * 2);

            canvas.DrawLine(prevX, prevY, x, y);
            prevX = x;
            prevY = y;
        }
    }
}
