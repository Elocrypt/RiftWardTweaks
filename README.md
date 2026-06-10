<div align="center">

# Rift Ward Tweaks

**Take full control of Vintage Story's Rift Wards — fuel burn, blocking range, light emission, and an in-world range visualiser, all tunable live without a restart.**

Blocking range · fuel multiplier · HSV light · range highlight · colour previews · adjustable ward sound · server-authoritative config with client sync.

[![CI](https://github.com/Elocrypt/RiftWardTweaks/actions/workflows/ci.yml/badge.svg)](https://github.com/Elocrypt/RiftWardTweaks/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Elocrypt/RiftWardTweaks?include_prereleases)](https://github.com/Elocrypt/RiftWardTweaks/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![VS 1.22.3](https://img.shields.io/badge/Vintage%20Story-1.22.3-purple)](https://www.vintagestory.at/)

</div>

---

> **Updated for Vintage Story 1.22.3 on .NET 10.** Highlight colour and preview duration are now per-player client settings — set them with `.rwt color` and `.rwt duration` — and have been removed from the server config and `/rwt set`. Existing server configs still load; the old keys are simply ignored.

## Features

<table>
<tr>
<td width="50%" valign="top">

### Gameplay
- **Configurable blocking radius** — change how far an active ward suppresses rifts, live or via config
- **Fuel consumption multiplier** — slow the burn for relaxed play, or speed it up for a challenge
- **Configurable HSV light emission** — active wards cast a light you choose, kept inside the engine-safe [VSLightWheel](http://tyron.at/vs/vslightwheel.html) range and updatable live with `/rwt set hsv`

### Visuals
- **In-world range highlight** — toggle a cube around every nearby active ward with `.rwt show`
- **ARGB colour preview** — spawn sample swatches in front of you with `.rwt preview` to pick a highlight colour
- **Accurate fuel-runtime tooltip** — the ward tooltip reflects your real runtime under the configured multiplier, now localised in every supported language

</td>
<td width="50%" valign="top">

### Config & sync
- **Server-authoritative config** — gameplay values live on the server and sync to each client on join and after any change
- **Per-player local settings** — your highlight colour, preview duration, and ward-sound settings (mute / volume / range) are stored client-side and never forced by the server

### Commands & control
- **Live `/rwt` admin commands** (`get` / `set` / `reload`) and per-player `.rwt` client commands — no restart required
- **Targeted Harmony patches** — coexists quietly with most other mods

### Languages
- Ships in **10 languages** (see below)

</td>
</tr>
</table>

## Install

1. Download the latest `RiftWardTweaks_<version>.zip` from the [Releases](https://github.com/Elocrypt/RiftWardTweaks/releases) page or the [Vintage Story mod portal](https://mods.vintagestory.at/riftwardtweaks).
2. Drop the zip (don't extract it) into your Vintage Story `Mods/` folder:
   - **Windows:** `%AppData%\VintagestoryData\Mods`
   - **Linux:** `~/.config/VintagestoryData/Mods`
   - **macOS:** `~/Library/Application Support/VintagestoryData/Mods`
3. Launch the game. The config file is generated automatically on first load.

Works in singleplayer and on multiplayer servers. **For multiplayer, install it on the server** — it changes server-side gameplay (fuel, range, light) and syncs the values to each client on join.

## Commands

Server commands use `/rwt` and require the `controlserver` privilege. Client commands use `.rwt` and affect only your own game.

### Server (`/rwt`)

| Command | Description |
|---|---|
| `/rwt get` | Show the current server-side config values. |
| `/rwt set <key> <value>` | Change a value live, save it, and sync to all clients. |
| `/rwt reload` | Reload the config from disk and sync to all clients. |

### Client (`.rwt`)

| Command | Description |
|---|---|
| `.rwt show` | Toggle the range highlight for nearby active wards. |
| `.rwt color <hex>` | Set your local highlight colour, e.g. `#3C00FF00`. |
| `.rwt duration <ms>` | Set how long `.rwt preview` swatches stay on screen. |
| `.rwt preview` | Show ARGB colour sample swatches in front of you. |
| `.rwt clear` | Clear any remaining preview swatches. |
| `.rwt sound <on/off>` | Mute or unmute the Rift Ward ambient hum for yourself. |
| `.rwt volume <0-100>` | Set the ward hum volume (100% = vanilla; 0 = silent). |
| `.rwt soundrange <blocks>` | Set how far the hum carries (1–64); lower it to keep wards quiet near your base. |
| `.rwt get` | Show your local client settings. |

### Keys for `/rwt set`

* `fuel`, `f`, `fuelconsumptionmultiplier`
* `range`, `r`, `riftblockrange`
* `scan`, `s`, `scanradius`
* `light`, `hsv`, `lh`, `l`, `h`, `lighthsv` — value format `H,S,V`
* `toggle`, `tl`, `t`, `togglelight` — `true` / `false`

> Highlight colour and preview duration are per-player client settings — set them with `.rwt color` and `.rwt duration`, not `/rwt set`.

### Examples

```bash
/rwt set f 0.02
/rwt set r 120
/rwt set scan 80
/rwt set hsv 7,7,7
/rwt set tl false
```

## Configuration

Both files live in your `VintagestoryData/ModConfig/` folder and are generated automatically on first load.

**Server** — `riftwardtweaksconfig.json`

```json
{
  "RiftBlockRange": 30,
  "FuelConsumptionMultiplier": 0.05,
  "ScanRadius": 10,
  "LightHSV": [ 34, 5, 15 ],
  "ToggleLight": true
}
```

**Client** — `riftwardtweaks_client.json`

```json
{
  "HighlightColor": "#3C00FF00",
  "ColorPreviewDurationMs": 10000,
  "SoundEnabled": true,
  "SoundVolumePercent": 100,
  "SoundRange": 6
}
```

Edit the server file and run `/rwt reload`, or just use `/rwt set` — no restart required. Your client settings are written automatically by `.rwt color`, `.rwt duration`, and the sound commands (`.rwt sound`, `.rwt volume`, `.rwt soundrange`).

## Light &amp; HSV

Light colour is set by `LightHSV` and constrained to the engine-safe [VSLightWheel](http://tyron.at/vs/vslightwheel.html) range:

- `Hue (H)`: 0–64 (wraps at 64, same as 0)
- `Saturation (S)`: 0–8 (use 0–7; 8 wraps back to white)
- `Value / brightness (V)`: 3–21 (also controls light radius)

> Values are clamped for stability — going outside these ranges can produce invisible light, oversaturation, or client crashes. Changing HSV while wards are active updates their light live; faint ghost light may linger briefly when you lower V and clears on relog or chunk reload. You can recolour safely with `/rwt set hsv` without restarting or breaking the world.

A few starting points for `/rwt set hsv`: red `0,7,15` · green `21,7,15` · cyan `32,7,15` · blue `43,7,15` · violet `50,7,15` · the default calm blue `34,5,15` · white `0,0,15`.

### Highlight colour chart (`.rwt color`)

| Colour | ARGB code |
|---|---|
| 🔴 Translucent red | `#3C0000FF` |
| 🟢 Translucent green | `#3C00FF00` |
| 🔵 Translucent blue | `#3CFF0000` |
| ⚪ Translucent white | `#3CFFFFFF` |
| 💜 Translucent purple | `#3CFF00FF` |
| ⚫ Invisible | `#00000000` |

`#AABBGGRR` — Alpha, Blue, Green, Red.

## Compatibility

- Works in both **singleplayer** and **multiplayer/server**.
- Uses targeted Harmony patches and coexists with most other mods.
- **Currently incompatible with [More Lanterns](https://mods.vintagestory.at/apelanterns)** — it breaks the Rift Ward light feature. Everything else still works if you don't mind losing the light.

## Languages

English, German, French, Spanish, Portuguese (Brazil &amp; Portugal), Russian, Ukrainian, Chinese, Japanese, and Korean. The game picks the right language automatically based on your client locale, and the fuel-runtime tooltip is localised in all of them. Contributions welcome.

---

<details>
<summary><b>Building from source</b></summary>

### Requirements

- Vintage Story 1.22.3 or later (for the referenced game DLLs)
- .NET 10 SDK

### Setup

The project references the game assemblies by `HintPath` into your install (no NuGet game packages), resolved from environment variables:

- `VINTAGE_STORY` — the game install directory (contains `VintagestoryAPI.dll`, `Vintagestory.exe`).
- `VINTAGE_STORY_DATA` — the data directory (contains `Mods/`, `ModConfig/`, `Saves/`).

```powershell
# Windows (PowerShell)
[Environment]::SetEnvironmentVariable("VINTAGE_STORY",      "F:\VintageStory\Client_v1.22.3\Vintagestory",   "User")
[Environment]::SetEnvironmentVariable("VINTAGE_STORY_DATA", "F:\VintageStory\Client_v1.22.3\Vintagestory\v", "User")
```

`Directory.Build.props` falls back to a default Windows install path if the variables aren't set, so a standard developer machine may need no configuration. Restart your IDE after setting the variables so it picks them up.

### Build

```powershell
dotnet build RiftWardTweaks.sln -c Release
```

The game DLLs are referenced with `<Private>false</Private>`, so they are **not** copied into `bin/` — only the mod's own `RiftWardTweaks.dll` and assets are produced.

### Test

```powershell
dotnet test RiftWardTweaks.sln -c Release
```

### Package a release

```powershell
./build/package.ps1 -Configuration Release -Version <version>
```

Produces `build/dist/RiftWardTweaks_<version>.zip`, ready to upload to the mod portal. On push of a tag matching `v*.*.*`, GitHub Actions builds, tests, packages, and publishes a release automatically — see `.github/workflows/release.yml`. CI downloads the matching Vintage Story server build for its reference assemblies, so no game DLLs are committed to the repo.

### Project layout

- **`Config/`** — `RiftWardConfig` (server-authoritative data class) and `RiftWardClientConfig` (client-local highlight colour + preview duration).
- **`Core/`** — `ModSystemRiftWardTweaks`, the entry point for both sides; loads config, registers commands, applies side-gated Harmony patches, and runs the sync channel.
- **`Networking/`** — `RiftWardConfigSyncPacket`, the ProtoBuf DTO that carries server config values to clients.
- **`Patches/`** — all Harmony patches: fuel consumption, rift-block range, the tooltip rewrite, light emission, and light cleanup on deactivate/remove.

</details>

## Roadmap

Planned, not guaranteed:

- Configurable highlight shape (e.g. dome).
- An in-range indicator so players know when they're standing inside an active ward's field.
- Ward statistics (fuel consumed, time active) and a low-fuel / deactivation notification.
- Per-player config overrides for multiplayer fairness.
- A config GUI with a colour picker, openable via a configurable hotkey.

## License

MIT — see [LICENSE](LICENSE).

## Credits

Created and maintained by [Elocrypt](https://mods.vintagestory.at/show/user/341F988399701B22FFDA). Thanks to everyone who has contributed translations.

## Support

If you enjoy this mod and want to support future development:

**[Ko-fi](https://ko-fi.com/elo)** · **[Patreon](https://www.patreon.com/c/Elocrypt)** · **[Throne](https://throne.com/elocrypt)**

<a href="https://discord.com/users/111920932842450944" target="_blank"><img src="https://discord.c99.nl/widget/theme-1/111920932842450944.png" alt="Discord" width="395" height="80"></a>
