# Rift Ward Tweaks

## Overview

**Rift Ward Tweaks** is a flexible server/client-side mod that enhances Rift Wards by giving full control over their fuel consumption, blocking range and visual feedback - now with color previews, translations, and advanced customization!

## Features

- **Accurate Fuel Runtime Tooltip**  
  Tooltip reflects effective runtime with your configured multiplier.

- **Client/Server Config Sync**  
  Server config values (e.g., HSV, FuelMultiplier) are synced to clients on join.

- **Client-Side Customization**  
  Use `.rwt color`, `.rwt preview`, and `.rwt get` to manage your local highlight preferences.

- **Configurable Rift Blocking Radius**  
  Change how far a Rift Ward suppresses rifts with a simple command or config tweak.

- **Fuel Consumption Multiplier**  
  Control how quickly temporal gear energy is drained.

- **Visual Highlight of Effective Range**  
  Toggle a highlight cube to see each active Rift Ward's protective area with `.rwt show`.

- **Color Preview System**  
  Preview ARGB highlight colors in-world with `.rwt preview`.

- **Tooltip Debug Info**  
  Includes Blocking Range, effective runtime, and highlight color.

- **Configurable Scan Radius & Highlight Color**  
  Adjust how far around the player to search for Rift Wards and customize the cube color.

- **Configurable Light Emission (HSV)**
  Toggle Rift Ward light emission dynamically when active. Customize the light’s hue, saturation, and brightness with safe values based on the official [VSLightWheel](http://tyron.at/vs/vslightwheel.html). Supports runtime updates with `/rwt set hsv`.

- **Live Runtime Commands**
  Adjust, reload, or visualize without restarting the game or server.

- **Multi-language Support**
  Now includes: 🇯🇵 Japanese, 🇨🇳 Chinese, 🇰🇷 Korean, 🇺🇦 Ukrainian, 🇷🇺 Russian, 🇫🇷 French, 🇩🇪 German, 🇪🇸 Spanish, 🇧🇷 Portuguese

- **Config File**: _%AppData%\VintagestoryData\ModConfig\_ `riftwardtweaksconfig.json`  
  Example:
  ```json
  {
    "RiftBlockRange": 30,
    "FuelConsumptionMultiplier": 0.05,
    "ScanRadius": 10,
    "HighlightColor": "#3C00FF00",
    "ColorPreviewDurationMs": 10000,
    "LightHSV": [ 
      34, 
      5, 
      15
    ],
    "ToggleLight": true
  }
  ```
>**_This config file is automatically generated on first load. 
You can edit and reload the config dynamically using in-game chat commands—no restart required!_**

## Admin Commands

| Command                  | Description                                                        |
|--------------------------|--------------------------------------------------------------------|
| `/rwt reload`            | Reload the config from disk and sync it to all clients.            |
| `/rwt get`               | Display current server-side config values.                         |
| `/rwt set <key> <value>` | Change a config setting live (see keys below).                    |
| `.rwt show`              | Toggle visual highlight of active Rift Wards.                      |
| `.rwt color <hex>`       | Set local highlight color in ARGB (e.g., #3C00FF00).               |
| `.rwt preview`           | Show ARGB color cube preview in front of player.                   |
| `.rwt clear`             | Clear remaining color preview cubes.                              |
| `.rwt get`               | Display your local client-side config values.                     |

**Available Keys for `/rwt set`:**

* `fuel`, `f`, `fuelconsumptionmultiplier`
* `range`, `r`, `riftblockrange`
* `scan`, `s`, `scanradius`
* `color`, `c`, `highlightcolor` (ARGB format, e.g. `#3C00FF00`)
* `duration`, `d`, `colorpreviewdurationms` (in milliseconds)
* `light`, `hsv`, `lh`, `l`, `h`, `lighthsv` (HSV format using VSLightWheel)
* `toggle`, `tl`, `t`, `togglelight` (true/false)

**Examples**:

```bash
/rwt set f 0.02
/rwt set r 120
/rwt set scan 80
/rwt set color #3CFF0000
/rwt set duration 5000
/rwt set hsv 7,7,7
/rwt set tl false
```

**Color Chart**

| What You Want          | Use This ARGB Code |
| ---------------------- | ------------------ |
| `🔴 Translucent Red`  |	#3C0000FF         |
| `🟢 Translucent Green`|	#3C00FF00         |
| `🔵 Translucent Blue` |	#3CFF0000         |
| `⚪ Translucent White`|	#3CFFFFFF          |
| `⚫ Invisible`        |	#0000000            |
| `💜 Translucent Purple` |	#3CFF00FF       |
```# Alpha - Blue - Green - Red```

## Ideal For

* Hardcore worlds with heavy rift activity
* Long-form or passive playthroughs where fuel micromanagement is tedious
* Builders or tech players who want visual clarity on area effects

## Compatibility

* Works in both **singleplayer** and **multiplayer/server**.
* Compatible with most other mods. Uses targeted Harmony patches.

## Installation

1. Drop the `.zip` into your `Mods/` folder.
2. Launch the game. The config file will be auto-generated.
3. Use `/rwt set`, `/rwt reload`, or edit the config file directly.

## Notes

* Rift Wards must be **active** to be highlighted and emit light.
* Rift Ward lighting is controlled by 'LightHSV' in the config.
* HSV values are limited by the **[VSLightWheel](http://tyron.at/vs/vslightwheel.html)** — the engine supports a safe range of: 
  - `Hue (H)`: 0-64,
  - `Saturation (S)`: 0-8
  - `Value (V)`: 3-21

>Going outside this range may result in invisible light, oversaturation, or client crashes. 
> - HSV values are clamped to `H=0-64`, `S=0–8` and `V=3–21` for stability.
>   - Changing HSV values while Rift Wards are active will force lighting to update live.
>   - [WARNING] Ghost light may linger briefly when reducing light radius (V). This clears on relog or chunk reload.
>   - You can safely update light color using `/rwt set hsv` without restarting or breaking the world

* Highlight color must be in full 8-digit ARGB hex (e.g. `#3C00FF00`).
* Highlight range respects your configured `RiftBlockRange`.
* Use `.rwt preview` to test highlight apperance in-game.
* Full config syncing occurs on join and after any server-side change or reload.

## Ideas / Planned Features

### Not Guarenteed

>- Add configurable light to Rift Wards, so they can emit light. ✅

>- Add configurable shape to the Rift Ward range highlighter (e.g. Dome).

>- Add a UI icon or some way to display to the player they are within range of an active Rift Ward.

>- Add togglable statistics to the UI, including: Amount of fuel a ward has consumed (gears), Total time active (in-game days).

>- Add a notification when a Rift Ward becomes inactive (ran out of fuel).

>- Add a debug mode and seperate logic for regular use and debugging.

>- Add a configurable option to have the total charge (effective duration) dynamically change the effective blocking range of rifts.\
  _*Note: Would need to seperate blocking range per rift ward._

>- Add a GUI that can be opened via configurable Hotkey, where you can adjust everything in the config. \
  _*Note: Add color picker._

## Support

If you enjoy this mod and want to support future development:  
<strong><a href="https://ko-fi.com/elo">ko-fi.com/elo</a></strong><br>
<a href="https://discord.com/users/111920932842450944" target="_blank"><img src="https://discord.c99.nl/widget/theme-1/111920932842450944.png" alt="Discord Banner" width="395" height="80"></a>
