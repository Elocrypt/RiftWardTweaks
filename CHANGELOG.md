# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.7.0] - 2026/06/07

Update for Vintage Story 1.22.3 on .NET 10, with a structural cleanup of the config, commands, and Harmony patches.

### Added

- Client-side `.rwt duration <ms>` command to set how long `.rwt preview` colour swatches stay visible.
- Per-player Rift Ward sound controls: `.rwt sound <on|off>` to mute the ambient hum, `.rwt volume <0-100>` to set its volume, and `.rwt soundrange <blocks>` to change how far it carries. All three are client-local, saved to `riftwardtweaks_client.json`, and applied to nearby wards immediately.
- Active wards are now tracked as they are placed and broken, so `/rwt set hsv` and `/rwt set toggle` update the light on already-placed wards within the session. Wards in chunks that were loaded before the change pick it up on chunk reload or relog.
- `ColorPreviewDurationMs` is now persisted in the client config (`riftwardtweaks_client.json`) and validated on load.
- `.rwt get` now also lists the sound settings (on/off, volume, range) alongside the highlight colour and preview duration.
- Config self-repair: malformed values (non-positive fuel multiplier, wrong-length `LightHSV`, non-positive range/scan) are corrected to safe defaults on load.

### Changed

- **Retargeted to Vintage Story 1.22.3 on .NET 10** (from 1.21.x on .NET 8); language level C# 13.
- The adjusted fuel-runtime tooltip line is now localised in every shipped language. It previously rewrote only the English "Charge for…" line, so other locales showed the unadjusted vanilla value.
- Highlight colour and preview duration are now strictly client-local, stored in `riftwardtweaks_client.json`.
- Reflected field handles in the fuel and rift-spawn patches are cached once at type load instead of on every server tick / spawn call.
- `.rwt show` now tracks the exact highlight slots it uses and clears only those, replacing the previous fixed 0–99 slot sweep.
- Source reorganised into `RiftWardTweaks.Config`, `.Core`, `.Networking`, and `.Patches` namespaces; non-entry-point types are now `internal`.

### Fixed

- **Fuel drained at double speed in singleplayer.** Harmony patches were applied on both sides of a singleplayer game, so the server-side fuel patch ran twice. Server-only and client-only patches are now applied strictly on their own side.
- The tooltip no longer rebuilds its entire text buffer on every render; it rewrites only the charge line.
- An invalid `HighlightColor` in the client config now falls back to the default with a logged warning instead of breaking the highlight.
- Corrected a missing comma in every shipped language file — invalid JSON that could break the tooltip strings for the affected locale.
- Loading the server config when the file is missing no longer risks a null reference (the null check now runs before validation).

### Removed

- Server `/rwt set` keys `color` / `c` / `highlightcolor` and `duration` / `d` / `colorpreviewdurationms` — these settings are per-player and now live client-side (`.rwt color`, `.rwt duration`).
- `HighlightColor` and `ColorPreviewDurationMs` fields from the server config (`RiftWardConfig`).

[Unreleased]: https://github.com/Elocrypt/RiftWardTweaks/compare/v2.7.0...HEAD
[2.7.0]: https://github.com/Elocrypt/RiftWardTweaks/compare/v2.6.0...v2.7.0
