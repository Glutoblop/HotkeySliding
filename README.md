# Hotkey Sliding

![version](https://img.shields.io/badge/version-0.1.0-blue) ![valheim](https://img.shields.io/badge/Valheim-1.0.21-orange) ![client](https://img.shields.io/badge/client--side-only-green)

A BepInEx plugin for Valheim.

Valheim's 1-8 hotbar is locked to the top inventory row. Hotkey Sliding lets you slide it down through every row, so one key press turns row 2 (or 3, or 4...) into your active hotbar: swap between a combat loadout, a building loadout and a food row without dragging items around.

## Features
- **Next hotbar row** / **Previous hotbar row** keys, rebindable in the game's own *Settings -> Controls* menu like any vanilla key (default: double-tap Left Ctrl for next row; previous row unbound).
- Press / Tap / DoubleTap trigger modes, so you can share a key with Crouch without conflicts.
- A small `Row X/Y` label next to the hotbar, and the 1-8 labels in the inventory grid move to the active row.
- Respects inventory-expanding mods (row count is read live).
- Ignored while typing in chat or any text field. Only ever affects your own inventory.

## Configuration (`BepInEx/config/jordan.valheim.hotkeysliding.cfg`)
| Section | Key | Default | What it does |
|---|---|---|---|
| Keys | NextRowKeyOverride / PreviousRowKeyOverride | None | Fallback key if you don't want to use the Controls menu. |
| Keys | NextRowTrigger / PreviousRowTrigger | DoubleTap / Press | How the key must be pressed. |
| Keys | ReverseDirection | false | Swap next/previous. |
| Keys | TapMaxSeconds / DoubleTapWindowSeconds | 0.25 / 0.35 | Tap timing. |
| Keys | AllowWhileInventoryOpen | true | Allow sliding with the inventory open. |
| General | ResetOnDeathOrLogout | false | Go back to row 1 on death / logout. |
| HUD | ShowRowIndicator / HideIndicatorOnFirstRow | true / false | `Row X/Y` label. |
| HUD | IndicatorFormat | `Row {0}/{1}` | Label text. |
| HUD | IndicatorOffsetX / IndicatorOffsetY | 8 / 0 | Label position. |
| HUD | HighlightRowInInventory | true | Move the 1-8 labels in the inventory grid. |

## Install

### Easiest: Valheim Mod Installer
Run the **Valheim Mod Installer** (`Start-Installer.bat` on Windows, `start-installer.sh` on Linux/macOS), point it at your Valheim folder, tick **Hotkey Sliding** and click **Install / Update**. It installs BepInEx for you if needed.

### Manual
1. Install [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (5.4.2360 or newer) into your Valheim folder.
2. Download [`dist/HotkeySliding.dll`](https://github.com/Glutoblop/HotkeySliding/raw/main/dist/HotkeySliding.dll).
3. Copy it to `<Valheim>/BepInEx/plugins/HotkeySliding/HotkeySliding.dll`.
4. Launch the game once; settings are written to `BepInEx/config/jordan.valheim.hotkeysliding.cfg`.

## Compatibility

| | |
|---|---|
| Plugin version | **0.1.0** |
| Last tested on Valheim | **1.0.21** (network version 41, Steam build 26249040) on 2026-11-03 |
| BepInExPack_Valheim | 5.4.2360 |
| Multiplayer | Client-side only: works on vanilla servers, other players don't need it |

Machine-readable versions live in [`plugin.json`](plugin.json). The installer reads it straight from `main` to tell users when an update is available, or when the mod hasn't been tested against their game version.

## Building

Requires the .NET SDK (8+) and a Valheim install with BepInEx.

```
dotnet build -c Release -p:GameDir="C:\Path\To\Valheim"
```

The build copies the DLL into `<GameDir>/BepInEx/plugins/HotkeySliding` for testing, and into `dist/` (which is committed, and is what the installer downloads).

## Releasing an update

1. Bump `Version` in `src/Plugin.cs` **and** `version` in `plugin.json` (keep them identical).
2. If you tested on a newer Valheim, update the `valheim` block in `plugin.json` (version and network version are printed in `BepInEx/LogOutput.log`; the Steam build id is in `steamapps/appmanifest_892970.acf`).
3. Add a line to `changelog` in `plugin.json`.
4. `dotnet build -c Release`, then commit `dist/HotkeySliding.dll` + `plugin.json` and push to `main`. Installer users see the update next time they open it.
