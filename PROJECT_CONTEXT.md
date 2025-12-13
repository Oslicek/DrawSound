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

## Project Structure

```
DrawSound/
├── src/
│   └── DrawSound/
│       ├── Platforms/
│       │   └── Android/
│       │       └── Services/
│       │           └── TonePlayer.cs    # Wavetable audio player
│       ├── Services/
│       │   └── ITonePlayer.cs           # Audio interface
│       ├── Resources/
│       ├── App.xaml
│       ├── AppShell.xaml
│       ├── MainPage.xaml                # Synthesizer UI
│       ├── MauiProgram.cs
│       └── DrawSound.csproj
├── .gitignore
├── global.json
├── PROJECT_RULES.md
├── PROJECT_CONTEXT.md
└── README.md
```

## Current State

**Phase:** Single-tone synthesizer with wavetable synthesis

**Completed:**
- [x] Git repository with GitHub remote
- [x] .NET 10 LTS environment configured
- [x] MAUI project scaffold
- [x] Basic synthesizer UI (single C4 button)
- [x] Audio service with wavetable approach (44.1kHz, 32-bit float)

**Current Features:**
- Single button plays middle C (261.63 Hz)
- Press to play, release to stop
- Wavetable-based tone generation

## Architecture

**Pattern:** Service-based with Dependency Injection

**Audio Engine:**
- Sample Rate: 44,100 Hz
- Bit Depth: 32-bit float (PcmFloat)
- Synthesis: Wavetable (single cycle buffer, looped)

## Key Components

| Component | Purpose |
|-----------|---------|
| `ITonePlayer` | Audio playback interface |
| `TonePlayer` | Android AudioTrack implementation |
| `MainPage` | Synthesizer UI with tone button |

## Test Devices

- Samsung Galaxy S24 Ultra (SM-S928B)
- Samsung Galaxy Tab S9 (SM-X710)
- Android Emulator (Pixel 9 Pro)

## Notes

- GitHub: https://github.com/Oslicek/DrawSound
- WiFi debugging enabled on physical devices

