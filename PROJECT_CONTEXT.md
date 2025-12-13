# Project Context

> **Last Updated:** 2025-12-13

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
│   │   │           └── TonePlayer.cs # Android AudioTrack player
│   │   ├── Services/
│   │   │   └── ITonePlayer.cs        # Audio interface
│   │   ├── MainPage.xaml             # Synthesizer UI
│   │   └── MauiProgram.cs
│   │
│   └── DrawSound.Core/               # Shared library (platform-independent)
│       └── Audio/
│           └── WaveTableGenerator.cs # Wavetable generation
│
├── tests/
│   └── DrawSound.Tests/              # Unit tests
│       └── WaveTableGeneratorTests.cs
│
├── .gitignore
├── global.json
├── PROJECT_RULES.md
├── PROJECT_CONTEXT.md
└── README.md
```

## Architecture

**Pattern:** Service-based with Dependency Injection

**Layers:**
- `DrawSound.Core` - Platform-independent audio logic (wavetable generation)
- `DrawSound` - MAUI app with platform-specific implementations

**Audio Engine:**
- Sample Rate: 44,100 Hz
- Bit Depth: 32-bit float (PcmFloat)
- Synthesis: Wavetable (single cycle buffer, looped)

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `WaveTableGenerator` | Core | Generates sine wave tables |
| `ITonePlayer` | DrawSound | Audio playback interface |
| `TonePlayer` | Android | Android AudioTrack implementation |
| `MainPage` | DrawSound | Synthesizer UI with tone button |

## Tests

| Test Class | Tests | Purpose |
|------------|-------|---------|
| `WaveTableGeneratorTests` | 7 | Validates sine wave generation |

**Test Coverage:**
- Wave shape verification (start, peak, zero-crossing, trough)
- Sample count accuracy
- Amplitude range validation
- Multiple frequency support

## Test Devices

- Samsung Galaxy S24 Ultra (SM-S928B)
- Samsung Galaxy Tab S9 (SM-X710)
- Android Emulator (Pixel 9 Pro)

## Current State

**Phase:** TDD refactoring complete

**Completed:**
- [x] Project setup with .NET 10 LTS
- [x] Single-tone synthesizer (C4 button)
- [x] Wavetable synthesis (44.1kHz, 32-bit float)
- [x] Extracted shared WaveTableGenerator to Core library
- [x] Unit tests for wave generation

**Current Features:**
- Single button plays middle C (261.63 Hz)
- Press to play, release to stop
- Waveform canvas displays the playing wave (top half of screen)

## Notes

- GitHub: https://github.com/Oslicek/DrawSound
- WiFi debugging enabled on physical devices
