# AGENTS.md

This file provides guidance to AI coding agents (Claude Code, Copilot, Cursor, etc.) when working with this repository.

## Project Overview

PlayKit Unity SDK - AI-powered game development toolkit providing NPC conversations, image generation, audio transcription, and more.

### Repository Structure

```
PlayKit-UnitySDK/
  Packages/
    ai.playkit.sdk/           # Core SDK package
    ai.playkit.sdk.steam/     # Steam authentication addon
  Assets/                     # Unity project assets (for development/testing)
  ProjectSettings/            # Unity project settings
```

### Key Source Locations

- **Runtime Core**: `Packages/ai.playkit.sdk/Runtime/Core/` (PlayKitSDK, PlayKit_NPC, VoiceModule, MicrophoneRecorder, etc.)
- **AI Providers**: `Packages/ai.playkit.sdk/Runtime/Provider/AI/`
- **Editor Tools**: `Packages/ai.playkit.sdk/Editor/` (Settings window, Update checker, Dependency checker)
- **Public API Types**: `Packages/ai.playkit.sdk/Runtime/Public/`
- **Steam Addon**: `Packages/ai.playkit.sdk.steam/`

## Versioning Rules

**IMPORTANT: This project uses a 4-segment version scheme `x.y.z.m` internally, but Unity Package Manager requires semver 3-segment format.**

### Version Mapping

| Location | Format | Example |
|---|---|---|
| `PlayKitSDK.VERSION` (source of truth) | `vx.y.z.m` | `v0.2.5.3` |
| `package.json` version | `x.y.zm` | `0.2.53` |

**Rule**: Concatenate the 3rd and 4th segments to form the patch number in package.json.

- `v0.2.5.3` -> `0.2.53`
- `v0.3.1.2` -> `0.3.12`
- `v1.0.0.1` -> `1.0.01` -> `1.0.1` (leading zero drops)
- `v1.2.10.4` -> `1.2.104`

### When Bumping Version

All three files **must** be updated together:

1. `Packages/ai.playkit.sdk/Runtime/Core/PlayKitSDK.cs` - `VERSION` constant (e.g. `"v0.2.5.3"`)
2. `Packages/ai.playkit.sdk/package.json` - `"version"` field (e.g. `"0.2.53"`)
3. `Packages/ai.playkit.sdk.steam/package.json` - `"version"` field (keep in sync)

## Development Notes

- Unity minimum version: 2022.3
- Depends on `com.unity.nuget.newtonsoft-json`
- Uses UniTask (`Cysharp.Threading.Tasks`) for async operations
- Install via Unity Package Manager git URL:
  - SDK: `https://github.com/PlayKit-ai/PlayKit-UnitySDK.git?path=Packages/ai.playkit.sdk`
  - Steam: `https://github.com/PlayKit-ai/PlayKit-UnitySDK.git?path=Packages/ai.playkit.sdk.steam`
