namespace DrawSound.Core.Audio;

/// <summary>
/// Represents a control point on a Bezier curve with two handles
/// </summary>
public class BezierNode
{
    /// <summary>
    /// X position normalized (0.0 to 1.0 across the wave cycle)
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y position normalized (-0.5 to 0.5 amplitude)
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Left handle offset (relative to node position)
    /// </summary>
    public PointF HandleIn { get; set; }

    /// <summary>
    /// Right handle offset (relative to node position)
    /// </summary>
    public PointF HandleOut { get; set; }

    public BezierNode(float x, float y)
    {
        X = x;
        Y = y;
        // Default handles are horizontal
        HandleIn = new PointF(-0.05f, 0);
        HandleOut = new PointF(0.05f, 0);
    }

    public BezierNode(float x, float y, PointF handleIn, PointF handleOut)
    {
        X = x;
        Y = y;
        HandleIn = handleIn;
        HandleOut = handleOut;
    }

    /// <summary>
    /// Get absolute position of the left handle
    /// </summary>
    public PointF GetHandleInAbsolute() => new(X + HandleIn.X, Y + HandleIn.Y);

    /// <summary>
    /// Get absolute position of the right handle
    /// </summary>
    public PointF GetHandleOutAbsolute() => new(X + HandleOut.X, Y + HandleOut.Y);

    /// <summary>
    /// Make handles symmetric (moving one affects the other)
    /// </summary>
    public void MirrorHandles(bool mirrorToIn)
    {
        if (mirrorToIn)
        {
            HandleIn = new PointF(-HandleOut.X, -HandleOut.Y);
        }
        else
        {
            HandleOut = new PointF(-HandleIn.X, -HandleIn.Y);
        }
    }

    public BezierNode Clone() => new(X, Y, HandleIn, HandleOut);
}

public readonly struct PointF
{
    public float X { get; }
    public float Y { get; }

    public PointF(float x, float y)
    {
        X = x;
        Y = y;
    }
}

