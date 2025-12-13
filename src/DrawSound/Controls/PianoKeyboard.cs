namespace DrawSound.Controls;

/// <summary>
/// Piano keyboard with 25 keys (C3 to C5)
/// </summary>
public class PianoKeyboard : IDrawable
{
    // Note frequencies (A4 = 440Hz)
    private static readonly double[] Frequencies = new double[25];
    private static readonly bool[] IsBlackKey = new bool[25];
    private static readonly string[] NoteNames = new string[25];
    
    private int _activeKey = -1;
    private float _viewWidth;
    private float _viewHeight;
    
    // White key positions (0-14 for 15 white keys)
    private readonly float[] _whiteKeyPositions = new float[15];
    
    public event EventHandler<double>? KeyPressed;
    public event EventHandler? KeyReleased;
    
    public IDrawable Drawable => this;

    static PianoKeyboard()
    {
        // C3 = MIDI 48, frequency = 130.81 Hz
        double c3Freq = 130.81;
        string[] notePattern = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        bool[] blackPattern = { false, true, false, true, false, false, true, false, true, false, true, false };
        
        for (int i = 0; i < 25; i++)
        {
            // Frequency doubles every 12 semitones
            Frequencies[i] = c3Freq * Math.Pow(2, i / 12.0);
            IsBlackKey[i] = blackPattern[i % 12];
            int octave = 3 + (i / 12);
            NoteNames[i] = notePattern[i % 12] + octave;
        }
    }

    public void OnTouch(float x, float y, float width, float height, bool isStart)
    {
        _viewWidth = width;
        _viewHeight = height;
        
        int key = GetKeyAtPosition(x, y);
        if (key != _activeKey)
        {
            _activeKey = key;
            if (key >= 0)
            {
                KeyPressed?.Invoke(this, Frequencies[key]);
            }
        }
    }

    public void OnTouchEnd()
    {
        if (_activeKey >= 0)
        {
            _activeKey = -1;
            KeyReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private int GetKeyAtPosition(float x, float y)
    {
        if (_viewWidth <= 0 || _viewHeight <= 0) return -1;
        
        float whiteKeyWidth = _viewWidth / 15f; // 15 white keys
        float blackKeyWidth = whiteKeyWidth * 0.6f;
        float blackKeyHeight = _viewHeight * 0.6f;
        
        // Check black keys first (they're on top)
        if (y < blackKeyHeight)
        {
            int whiteIndex = (int)(x / whiteKeyWidth);
            float xInKey = x - (whiteIndex * whiteKeyWidth);
            
            // Check if we're in a black key zone
            // Black keys are between: C-D, D-E, F-G, G-A, A-B
            for (int i = 0; i < 25; i++)
            {
                if (!IsBlackKey[i]) continue;
                
                // Calculate black key position
                int whitesBefore = CountWhiteKeysBefore(i);
                float blackX = (whitesBefore * whiteKeyWidth) - (blackKeyWidth / 2);
                
                if (x >= blackX && x <= blackX + blackKeyWidth)
                {
                    return i;
                }
            }
        }
        
        // Check white keys
        int whiteKeyIndex = (int)(x / whiteKeyWidth);
        whiteKeyIndex = Math.Clamp(whiteKeyIndex, 0, 14);
        
        // Convert white key index to note index
        return WhiteKeyIndexToNoteIndex(whiteKeyIndex);
    }

    private int CountWhiteKeysBefore(int noteIndex)
    {
        int count = 0;
        for (int i = 0; i < noteIndex; i++)
        {
            if (!IsBlackKey[i]) count++;
        }
        return count;
    }

    private int WhiteKeyIndexToNoteIndex(int whiteIndex)
    {
        int count = 0;
        for (int i = 0; i < 25; i++)
        {
            if (!IsBlackKey[i])
            {
                if (count == whiteIndex) return i;
                count++;
            }
        }
        return 0;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float width = dirtyRect.Width;
        float height = dirtyRect.Height;
        float whiteKeyWidth = width / 15f;
        float blackKeyWidth = whiteKeyWidth * 0.6f;
        float blackKeyHeight = height * 0.6f;

        // Draw white keys first
        int whiteIndex = 0;
        for (int i = 0; i < 25; i++)
        {
            if (IsBlackKey[i]) continue;
            
            float x = whiteIndex * whiteKeyWidth;
            bool isActive = (i == _activeKey);
            
            // Key background
            canvas.FillColor = isActive ? Color.FromArgb("#aaaaff") : Colors.White;
            canvas.FillRectangle(x + 1, 0, whiteKeyWidth - 2, height - 2);
            
            // Key border
            canvas.StrokeColor = Color.FromArgb("#333");
            canvas.StrokeSize = 1;
            canvas.DrawRectangle(x + 1, 0, whiteKeyWidth - 2, height - 2);
            
            // Draw C labels
            if (NoteNames[i].StartsWith("C"))
            {
                canvas.FontColor = Color.FromArgb("#666");
                canvas.FontSize = 10;
                canvas.DrawString(NoteNames[i], x + 2, height - 18, whiteKeyWidth - 4, 16,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            
            whiteIndex++;
        }

        // Draw black keys on top
        for (int i = 0; i < 25; i++)
        {
            if (!IsBlackKey[i]) continue;
            
            int whitesBefore = CountWhiteKeysBefore(i);
            float x = (whitesBefore * whiteKeyWidth) - (blackKeyWidth / 2);
            bool isActive = (i == _activeKey);
            
            // Black key
            canvas.FillColor = isActive ? Color.FromArgb("#4444aa") : Color.FromArgb("#222");
            canvas.FillRoundedRectangle(x, 0, blackKeyWidth, blackKeyHeight, 0, 0, 3, 3);
        }
    }
}

