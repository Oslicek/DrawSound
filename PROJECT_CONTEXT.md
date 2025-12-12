# Project Context

> **Last Updated:** 2025-12-12

## Overview

**DrawSound** is a .NET MAUI application targeting Android as the primary platform.

## Technology Stack

| Component | Version/Details |
|-----------|-----------------|
| Framework | .NET 10 Preview (10.0.100-preview.5) |
| UI Framework | .NET MAUI |
| Language | C# |
| Primary Platform | Android |
| IDE | Cursor / Visual Studio |

## Project Structure

```
DrawSound/
├── src/
│   └── DrawSound/              # Main MAUI application
│       ├── Platforms/          # Platform-specific code
│       │   └── Android/        # Android-specific (MainActivity, etc.)
│       ├── Resources/          # App resources (images, fonts, styles)
│       ├── App.xaml            # Application definition
│       ├── AppShell.xaml       # Shell navigation
│       ├── MainPage.xaml       # Main page (Hello World)
│       ├── MauiProgram.cs      # App builder & DI setup
│       └── DrawSound.csproj    # Project file
├── .gitignore
├── global.json                 # Pins .NET SDK to 10.0 preview
├── PROJECT_RULES.md            # Development guidelines (static)
├── PROJECT_CONTEXT.md          # This file (updated each increment)
└── README.md
```

## Environment Setup

- **Android SDK:** Located at `%LOCALAPPDATA%\Android\Sdk`
- **ANDROID_HOME:** Environment variable configured
- **Available Emulators:** Pixel_9_Pro, Medium_Phone_API_36.0
- **MAUI Workloads:** Installed for .NET 10

## Current State

**Phase:** Hello World implementation complete

**Completed:**
- [x] Git repository initialized
- [x] GitHub remote configured (https://github.com/Oslicek/DrawSound)
- [x] .NET 10 SDK pinned via global.json
- [x] MAUI workloads installed
- [x] Android SDK configured
- [x] Project rules established
- [x] MAUI project scaffold created
- [x] Hello World page implemented
- [x] Android build verified

**Next Steps:**
- [ ] Define application purpose and features
- [ ] Design application architecture
- [ ] Implement core functionality

## Architecture

**Pattern:** MVVM (Model-View-ViewModel) - to be implemented

**Current Pages:**
- `MainPage` - Displays "Hello World!" centered on screen

## Key Components

| Component | Purpose |
|-----------|---------|
| `MainPage.xaml` | Single page showing Hello World |
| `App.xaml` | Application resources and startup |
| `AppShell.xaml` | Shell navigation container |
| `MauiProgram.cs` | Application builder and DI configuration |

## Dependencies

Standard .NET MAUI dependencies (from template):
- Microsoft.Maui.Controls
- Microsoft.Maui.Controls.Compatibility
- Microsoft.Extensions.Logging.Debug

## Notes

- Build target: `net10.0-android`
- Build output: `bin/Debug/net10.0-android/DrawSound.dll`
- GitHub username: Oslicek
- Git username: NetDonkey
