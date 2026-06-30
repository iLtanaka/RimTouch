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
- Runtime camera zoom range support, including ranges changed by other camera mods.

## Requirements

- RimWorld `1.6`.
- Harmony.

## Optional Compatibility

- SimpleCameraSetting (`ray1203.SimpleCameraSetting`) can be used for local map zoom range and zoom speed settings. When it is active, RimTouch leaves those zoom settings in control and disables only SimpleCameraSetting's `CameraMapConfig.ConfigFixedUpdate_60` prefix, which otherwise replaces vanilla camera fixed-update logic and can make touch camera control unstable.

## Installation

Copy this folder into RimWorld's `Mods` directory and enable it after Harmony.
If you use SimpleCameraSetting, load RimTouch after it.

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

## Reporting touch issues

RimTouch depends heavily on your hardware, operating system, Unity/RimWorld input behavior, and active mod list. If something does not work correctly, please open a GitHub issue and include as much detail as possible.

Please include:

* Device model, for example: Surface Pro 8, Steam Deck OLED, Lenovo Yoga, etc.
* Operating system and version, for example: Windows 11 24H2, Ubuntu 24.04, SteamOS.
* RimWorld version.
* RimTouch version or commit.
* Whether you use Steam, GOG, or another RimWorld build.
* Full active mod list, especially UI, camera, input, or performance mods.
* What gesture failed: tap, double tap, one-finger pan, long press, two-finger pan, pinch zoom, world map gesture, UI scale, etc.
* What you expected to happen.
* What actually happened.
* Whether the issue happens with only Harmony and RimTouch enabled.
* Player.log or HugsLib log, if available.

Short reports like “touch does not work” are hard to fix. A good report makes it much easier to reproduce the problem and improve the mod.


## License

GPL-3.0-or-later.
