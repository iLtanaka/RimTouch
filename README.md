# RimTouch

Experimental direct touch input layer for RimWorld 1.6.

RimTouch targets Windows touch devices first, especially Microsoft Surface-style tablets and laptops. Linux support is actively being developed; the mod will likely start and work there when Unity reports touch input through legacy `UnityEngine.Input`, but Linux support is still experimental.

## Features

- Touch tap repair for RimWorld UI buttons.
- Double tap support for UI and map selection.
- One-finger map pan.
- Hold-to-select with RimWorld-style drag selection.
- Long press for right-click/context actions.
- Two-finger map pan and pinch zoom.
- World map touch pan and pinch zoom.
- Two-finger tap to cancel active designator/targeter tools.
- UI scale list unlock up to `3.0x`.
- SimpleCameraSetting-compatible local map zoom range.

## Requirements

- RimWorld `1.6`.
- Harmony.
- SimpleCameraSetting.

## Installation

Copy this folder into RimWorld's `Mods` directory and enable it after Harmony and SimpleCameraSetting.

For the current local development setup, the built assembly is:

`Assemblies/RimTouch.dll`

## Build

The build script defaults to the local GOG RimWorld install path used during development, but it also supports environment overrides:

```powershell
powershell -ExecutionPolicy Bypass -File Source\build.ps1
```

If your paths differ, set:

- `RIMWORLD_ROOT` to the RimWorld install directory.
- `HARMONY_DLL` to the full path of `0Harmony.dll`.

## Status

This is an alpha-quality touch mod. Core map, world map, UI tap repair, and zoom gestures are implemented, but touch behavior varies across devices and operating systems. Please test on real hardware before relying on it in a long colony session.
