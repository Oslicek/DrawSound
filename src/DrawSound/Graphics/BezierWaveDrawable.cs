using DrawSound.Core.Audio;
using BezierNodePoint = DrawSound.Core.Audio.PointF;

namespace DrawSound.Graphics;

public enum DragTarget
{
    None,
    Node,
    HandleIn,
    HandleOut
}

public class BezierWaveDrawable : IDrawable
{
    private List<BezierNode> _nodes = new();
    private readonly object _lock = new();
    private bool _isPlaying;
    
    // Editing state
    private int _selectedNodeIndex = -1;
    private DragTarget _dragTarget = DragTarget.None;
    
    // Visual settings
    public Color WaveColor { get; set; } = Colors.Cyan;
    public Color WaveColorPlaying { get; set; } = Colors.Lime;
    public Color BackgroundColor { get; set; } = Color.FromArgb("#0f0f23");
    public Color GridColor { get; set; } = Color.FromArgb("#2a2a4a");
    public Color NodeColor { get; set; } = Colors.White;
    public Color HandleColor { get; set; } = Colors.Orange;
    public Color SelectedNodeColor { get; set; } = Colors.Yellow;
    public float StrokeWidth { get; set; } = 3f;
    public float NodeRadius { get; set; } = 12f;
    public float HandleRadius { get; set; } = 8f;

    public event EventHandler<float[]>? WaveTableChanged;

    public BezierWaveDrawable()
    {
        GenerateSineWave();
    }

    public void GenerateSineWave()
    {
        lock (_lock)
        {
            _nodes = BezierWaveSampler.CreateSineWaveNodes();
            _selectedNodeIndex = -1;
        }
        NotifyChanged();
    }

    public void ClearWave()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _selectedNodeIndex = -1;
        }
        NotifyChanged();
    }

    public float[] GetWaveTable(int samples = 256)
    {
        lock (_lock)
        {
            return BezierWaveSampler.SampleToWaveTable(_nodes, samples);
        }
    }

    public void SetPlaying(bool isPlaying)
    {
        _isPlaying = isPlaying;
    }

    public void StartTouch(float x, float y, float canvasWidth, float canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var (normX, normY) = ScreenToNormalized(x, y, canvasWidth, canvasHeight);
        
        lock (_lock)
        {
            // Check if we're clicking on a handle first
            if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _nodes.Count)
            {
                var node = _nodes[_selectedNodeIndex];
                var handleInPos = node.GetHandleInAbsolute();
                var handleOutPos = node.GetHandleOutAbsolute();
                
                float handleInDist = Distance(normX, normY, handleInPos.X, handleInPos.Y);
                float handleOutDist = Distance(normX, normY, handleOutPos.X, handleOutPos.Y);
                
                float handleThreshold = 0.05f;
                
                if (handleInDist < handleThreshold)
                {
                    _dragTarget = DragTarget.HandleIn;
                    return;
                }
                if (handleOutDist < handleThreshold)
                {
                    _dragTarget = DragTarget.HandleOut;
                    return;
                }
            }

            // Check if clicking on a node
            float nodeThreshold = 0.04f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float dist = Distance(normX, normY, _nodes[i].X, _nodes[i].Y);
                if (dist < nodeThreshold)
                {
                    _selectedNodeIndex = i;
                    _dragTarget = DragTarget.Node;
                    return;
                }
            }

            // Clicking on empty space - add new node
            var newNode = new BezierNode(normX, normY);
            _nodes.Add(newNode);
            _nodes = _nodes.OrderBy(n => n.X).ToList();
            _selectedNodeIndex = _nodes.FindIndex(n => Math.Abs(n.X - normX) < 0.001f);
            _dragTarget = DragTarget.Node;
        }
        
        NotifyChanged();
    }

    public void DragTouch(float x, float y, float canvasWidth, float canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0) return;
        if (_dragTarget == DragTarget.None) return;

        var (normX, normY) = ScreenToNormalized(x, y, canvasWidth, canvasHeight);
        
        lock (_lock)
        {
            if (_selectedNodeIndex < 0 || _selectedNodeIndex >= _nodes.Count)
                return;

            var node = _nodes[_selectedNodeIndex];

            switch (_dragTarget)
            {
                case DragTarget.Node:
                    // Don't allow moving past adjacent nodes
                    float minX = _selectedNodeIndex > 0 ? _nodes[_selectedNodeIndex - 1].X + 0.01f : 0f;
                    float maxX = _selectedNodeIndex < _nodes.Count - 1 ? _nodes[_selectedNodeIndex + 1].X - 0.01f : 1f;
                    
                    node.X = Math.Clamp(normX, minX, maxX);
                    node.Y = Math.Clamp(normY, -0.5f, 0.5f);
                    break;

                case DragTarget.HandleIn:
                    node.HandleIn = new BezierNodePoint(normX - node.X, normY - node.Y);
                    // Mirror the other handle for smooth curve
                    node.MirrorHandles(mirrorToIn: false);
                    break;

                case DragTarget.HandleOut:
                    node.HandleOut = new BezierNodePoint(normX - node.X, normY - node.Y);
                    // Mirror the other handle for smooth curve
                    node.MirrorHandles(mirrorToIn: true);
                    break;
            }
        }
        
        NotifyChanged();
    }

    public void EndTouch()
    {
        _dragTarget = DragTarget.None;
    }

    public void DeleteSelectedNode()
    {
        lock (_lock)
        {
            if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _nodes.Count)
            {
                _nodes.RemoveAt(_selectedNodeIndex);
                _selectedNodeIndex = -1;
            }
        }
        NotifyChanged();
    }

    private (float x, float y) ScreenToNormalized(float screenX, float screenY, float width, float height)
    {
        float x = Math.Clamp(screenX / width, 0f, 1f);
        float centerY = height / 2;
        float amplitude = height * 0.4f;
        float y = (centerY - screenY) / (amplitude * 2);
        y = Math.Clamp(y, -0.5f, 0.5f);
        return (x, y);
    }

    private (float x, float y) NormalizedToScreen(float normX, float normY, float width, float height)
    {
        float x = normX * width;
        float centerY = height / 2;
        float amplitude = height * 0.4f;
        float y = centerY - (normY * amplitude * 2);
        return (x, y);
    }

    private static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private void NotifyChanged()
    {
        WaveTableChanged?.Invoke(this, GetWaveTable());
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = BackgroundColor;
        canvas.FillRectangle(dirtyRect);

        DrawGrid(canvas, dirtyRect);

        List<BezierNode> nodes;
        int selectedIndex;
        lock (_lock)
        {
            nodes = _nodes.Select(n => n.Clone()).ToList();
            selectedIndex = _selectedNodeIndex;
        }

        if (nodes.Count > 0)
        {
            DrawBezierCurve(canvas, dirtyRect, nodes);
            DrawHandles(canvas, dirtyRect, nodes, selectedIndex);
            DrawNodes(canvas, dirtyRect, nodes, selectedIndex);
        }
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

    private void DrawBezierCurve(ICanvas canvas, RectF rect, List<BezierNode> nodes)
    {
        if (nodes.Count < 2)
        {
            if (nodes.Count == 1)
            {
                // Draw a single point
                var (px, py) = NormalizedToScreen(nodes[0].X, nodes[0].Y, rect.Width, rect.Height);
                canvas.FillColor = _isPlaying ? WaveColorPlaying : WaveColor;
                canvas.FillCircle(px, py, 4);
            }
            return;
        }

        canvas.StrokeColor = _isPlaying ? WaveColorPlaying : WaveColor;
        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        var path = new PathF();

        var (startX, startY) = NormalizedToScreen(nodes[0].X, nodes[0].Y, rect.Width, rect.Height);
        path.MoveTo(startX, startY);

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var n0 = nodes[i];
            var n1 = nodes[i + 1];

            var h0 = n0.GetHandleOutAbsolute();
            var h1 = n1.GetHandleInAbsolute();

            var (cp1x, cp1y) = NormalizedToScreen(h0.X, h0.Y, rect.Width, rect.Height);
            var (cp2x, cp2y) = NormalizedToScreen(h1.X, h1.Y, rect.Width, rect.Height);
            var (endX, endY) = NormalizedToScreen(n1.X, n1.Y, rect.Width, rect.Height);

            path.CurveTo(cp1x, cp1y, cp2x, cp2y, endX, endY);
        }

        canvas.DrawPath(path);
    }

    private void DrawHandles(ICanvas canvas, RectF rect, List<BezierNode> nodes, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= nodes.Count)
            return;

        var node = nodes[selectedIndex];
        var (nodeX, nodeY) = NormalizedToScreen(node.X, node.Y, rect.Width, rect.Height);
        
        var handleIn = node.GetHandleInAbsolute();
        var handleOut = node.GetHandleOutAbsolute();
        
        var (hInX, hInY) = NormalizedToScreen(handleIn.X, handleIn.Y, rect.Width, rect.Height);
        var (hOutX, hOutY) = NormalizedToScreen(handleOut.X, handleOut.Y, rect.Width, rect.Height);

        // Draw handle lines
        canvas.StrokeColor = HandleColor;
        canvas.StrokeSize = 2;
        canvas.DrawLine(nodeX, nodeY, hInX, hInY);
        canvas.DrawLine(nodeX, nodeY, hOutX, hOutY);

        // Draw handle circles
        canvas.FillColor = HandleColor;
        canvas.FillCircle(hInX, hInY, HandleRadius);
        canvas.FillCircle(hOutX, hOutY, HandleRadius);
    }

    private void DrawNodes(ICanvas canvas, RectF rect, List<BezierNode> nodes, int selectedIndex)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var (x, y) = NormalizedToScreen(nodes[i].X, nodes[i].Y, rect.Width, rect.Height);
            
            bool isSelected = i == selectedIndex;
            
            // Draw node circle
            canvas.FillColor = isSelected ? SelectedNodeColor : NodeColor;
            canvas.FillCircle(x, y, NodeRadius);
            
            // Draw node border
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(x, y, NodeRadius);
        }
    }
}

