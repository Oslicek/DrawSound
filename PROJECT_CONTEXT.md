# Project Context

> **Last Updated:** 2025-12-14

## Overview

**DrawSound** is a .NET MAUI music synthesizer application targeting Android as the primary platform.

## Technology Stack

| Component | Version/Details |
|-----------|-----------------|
| Framework | .NET 10 LTS (10.0.101) |
| UI Framework | .NET MAUI 10.0.1 |
| Language | C# |
| Primary Platform | Android 6.0+ (API 23+) |
| Audio | 44.1kHz, 32-bit float, wavetable synthesis |
| Testing | xUnit |

## Project Structure

```
DrawSound/
├── src/
│   ├── DrawSound/                    # MAUI application
│   │   ├── Platforms/
│   │   │   └── Android/
│   │   │       └── Services/
│   │   │           └── TonePlayer.cs # AudioTrack player
│   │   ├── Services/
│   │   │   └── ITonePlayer.cs        # Audio interface
│   │   ├── MainPage.xaml             # Synthesizer UI
│   │   └── MauiProgram.cs
│   │
│   └── DrawSound.Core/               # Shared library (platform-independent)
│       └── Audio/
│           ├── WaveTableGenerator.cs # Wavetable generation
│           └── VoiceMixer.cs         # Polyphonic mixing with release
│
├── tests/
│   └── DrawSound.Tests/              # Unit tests
│       ├── WaveTableGeneratorTests.cs
│       ├── BezierNodeTests.cs
│       ├── BezierWaveSamplerTests.cs
│       ├── HarmonicMixerTests.cs
│       ├── UpdateThrottlerTests.cs
│       └── VoiceMixerTests.cs
├── .gitignore
├── global.json
├── PROJECT_RULES.md
├── PROJECT_CONTEXT.md
└── README.md
```

## Architecture

**Pattern:** Service-based with Dependency Injection

**Layers:**
- `DrawSound.Core` - Platform-independent audio logic (wavetable, poly mixer)
- `DrawSound` - MAUI app with platform-specific implementations

**Audio Engine:**
- Sample Rate: 44,100 Hz
- Bit Depth: 32-bit float (PcmFloat)
- Synthesis: Wavetable (single cycle buffer, looped)

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `WaveTableGenerator` | Core | Generates sine wave tables |
| `VoiceMixer` | Core | Polyphonic voice mixing with per-voice release |
| `ITonePlayer` | DrawSound | Audio playback interface |
| `TonePlayer` | Android | AudioTrack player using `VoiceMixer` |
| `MainPage` | DrawSound | Synth UI (Bezier editor, harmonics, preview, keyboard) |

## Tests

| Test Class | Purpose |
|------------|---------|
| `WaveTableGeneratorTests` | Sine wavetable shape/count/amplitude |
| `BezierNodeTests` | Bezier node geometry/handles/mirroring |
| `BezierWaveSamplerTests` | Sampling nodes to wavetable |
| `HarmonicMixerTests` | Harmonic mixing and normalization |
| `UpdateThrottlerTests` | Throttling behavior |
| `VoiceMixerTests` | Polyphony mixing, release, max-voice handling, mix quality, attack/release transitions |

## Test Devices

- Samsung Galaxy S24 Ultra (SM-S928B)
- Samsung Galaxy Tab S9 (SM-X710)
- Android Emulator (Pixel 9 Pro)

## Current State

**Phase:** Polyphony + performance tuning

**Completed:**
- [x] Project setup with .NET 10 LTS and MAUI 10
- [x] Wavetable synthesis (44.1kHz, 32-bit float)
- [x] Shared core audio library with tests
- [x] Harmonic mixer and preview
- [x] Bezier curve editor with touch support
- [x] 6 harmonic sliders (single GraphicsView) with throttled redraws
- [x] Piano keyboard (C3–C5) with stable multi-touch
- [x] Short attack/release envelopes to avoid clicks on note on/off
- [x] 6-voice polyphony via `VoiceMixer` + AudioTrack with headroom scaling
- [x] Mixer regression tests for mix accuracy and attack/release transitions

**Current Features:**
- Playable 25-key keyboard (C3–C5), multi-touch mapped to per-voice playback
- Editable Bezier waveform; sine/reset/delete node buttons
- Harmonic sliders (6) controlling overtones; mixed preview view
- Single-cycle preview rendering; throttled UI/audio updates
- Configurable audio settings (`appsettings.json`): `ReleaseMs`, `MaxPolyphony`

## Notes

- GitHub: https://github.com/Oslicek/DrawSound
- WiFi debugging enabled on physical devices (phone + tablet)
