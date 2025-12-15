namespace DrawSound.Core.Audio;

/// <summary>
/// Settings for AHDSHR envelope (Attack, Hold1, Decay, Sustain, Hold2, Release).
/// Times are in milliseconds; Sustain is 0-1 (percentage of max level).
/// Hold2Ms: if -1, sustain is held indefinitely until note-off; otherwise finite hold.
/// </summary>
public class AHDSHRSettings
{
    public float AttackMs { get; set; }
    public float Hold1Ms { get; set; }
    public float DecayMs { get; set; }
    public float SustainLevel { get; set; } // 0..1
    public float Hold2Ms { get; set; } // if -1 => sustain until release
    public float ReleaseMs { get; set; }
}

