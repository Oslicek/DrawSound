namespace DrawSound.Core.Audio;

/// <summary>
/// Samples a Bezier curve defined by nodes into a wavetable
/// </summary>
public static class BezierWaveSampler
{
    /// <summary>
    /// Sample the Bezier curve into a wavetable
    /// </summary>
    public static float[] SampleToWaveTable(List<BezierNode> nodes, int samples)
    {
        var waveTable = new float[samples];

        if (nodes.Count == 0)
        {
            // Empty - return silence
            return waveTable;
        }

        if (nodes.Count == 1)
        {
            // Single point - flat line at that Y
            Array.Fill(waveTable, nodes[0].Y);
            return waveTable;
        }

        // Sort nodes by X position
        var sortedNodes = nodes.OrderBy(n => n.X).ToList();

        for (int i = 0; i < samples; i++)
        {
            float x = (float)i / (samples - 1);
            waveTable[i] = SampleAtX(sortedNodes, x);
        }

        return waveTable;
    }

    private static float SampleAtX(List<BezierNode> nodes, float x)
    {
        // Find which segment this x falls into
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var n0 = nodes[i];
            var n1 = nodes[i + 1];

            if (x >= n0.X && x <= n1.X)
            {
                return SampleBezierSegment(n0, n1, x);
            }
        }

        // Before first node
        if (x < nodes[0].X)
        {
            return nodes[0].Y;
        }

        // After last node
        return nodes[^1].Y;
    }

    private static float SampleBezierSegment(BezierNode n0, BezierNode n1, float x)
    {
        if (Math.Abs(n1.X - n0.X) < 0.0001f)
        {
            return (n0.Y + n1.Y) / 2;
        }

        // Find t parameter for this x using Newton's method
        float t = (x - n0.X) / (n1.X - n0.X); // Initial guess

        // Control points for cubic Bezier
        float x0 = n0.X;
        float x1 = n0.X + n0.HandleOut.X;
        float x2 = n1.X + n1.HandleIn.X;
        float x3 = n1.X;

        // Newton's method to find t for given x
        for (int iter = 0; iter < 10; iter++)
        {
            float bx = CubicBezier(x0, x1, x2, x3, t);
            float dx = CubicBezierDerivative(x0, x1, x2, x3, t);
            
            if (Math.Abs(dx) < 0.0001f) break;
            
            float error = bx - x;
            if (Math.Abs(error) < 0.0001f) break;
            
            t -= error / dx;
            t = Math.Clamp(t, 0f, 1f);
        }

        // Now get Y at this t
        float y0 = n0.Y;
        float y1 = n0.Y + n0.HandleOut.Y;
        float y2 = n1.Y + n1.HandleIn.Y;
        float y3 = n1.Y;

        return CubicBezier(y0, y1, y2, y3, t);
    }

    private static float CubicBezier(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 +
               3 * u * u * t * p1 +
               3 * u * t * t * p2 +
               t * t * t * p3;
    }

    private static float CubicBezierDerivative(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return 3 * u * u * (p1 - p0) +
               6 * u * t * (p2 - p1) +
               3 * t * t * (p3 - p2);
    }

    /// <summary>
    /// Create default sine wave nodes
    /// </summary>
    public static List<BezierNode> CreateSineWaveNodes()
    {
        // Create a sine wave using 5 nodes
        var nodes = new List<BezierNode>
        {
            new(0f, 0f, new PointF(-0.05f, 0.15f), new PointF(0.05f, 0.15f)),
            new(0.25f, 0.5f, new PointF(-0.08f, 0f), new PointF(0.08f, 0f)),
            new(0.5f, 0f, new PointF(-0.05f, 0.15f), new PointF(0.05f, -0.15f)),
            new(0.75f, -0.5f, new PointF(-0.08f, 0f), new PointF(0.08f, 0f)),
            new(1f, 0f, new PointF(-0.05f, -0.15f), new PointF(0.05f, 0.15f))
        };
        return nodes;
    }
}

