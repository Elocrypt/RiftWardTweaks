using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RiftWardTweaks.Config
{
    /// <summary>
    /// Client-local, per-player settings that are never sent to the server.
    /// Persisted to <c>GamePaths.ModConfig/riftwardtweaks_client.json</c> and set
    /// only by the local player (e.g. via <c>/rwt color</c>).
    /// </summary>
    /// <remarks>
    /// Uses Newtonsoft.Json (not System.Text.Json) deliberately: Newtonsoft ships
    /// with the Vintage Story runtime as a hard dependency of the game API and is
    /// already used throughout the rest of this mod, so introducing a second JSON
    /// stack for one file would add inconsistency for no benefit.
    /// </remarks>
    internal static class RiftWardClientConfig
    {
        private const string ClientConfigFileName = "riftwardtweaks_client.json";

        /// <summary>Default highlight colour: translucent green, in #AARRGGBB form.</summary>
        public const string DefaultHighlightColor = "#3C00FF00";

        /// <summary>Default colour-preview swatch lifetime, in milliseconds.</summary>
        public const int DefaultColorPreviewDurationMs = 10000;

        /// <summary>Local block-highlight colour as an 8-digit ARGB hex string (#AARRGGBB).</summary>
        public static string HighlightColor = DefaultHighlightColor;

        /// <summary>How long <c>/rwt preview</c> swatches remain visible, in milliseconds.</summary>
        public static int ColorPreviewDurationMs = DefaultColorPreviewDurationMs;

        /// <summary>Default state of the Rift Ward ambient hum on this client.</summary>
        public const bool DefaultSoundEnabled = true;

        /// <summary>Default ambient-sound volume, as a percentage of the vanilla level.</summary>
        public const int DefaultSoundVolumePercent = 100;

        /// <summary>Default ambient-sound audible range, in blocks (the vanilla value).</summary>
        public const int DefaultSoundRange = 6;

        /// <summary>Lowest accepted <see cref="SoundRange"/>, in blocks.</summary>
        public const int MinSoundRange = 1;

        /// <summary>Highest accepted <see cref="SoundRange"/>, in blocks.</summary>
        public const int MaxSoundRange = 64;

        /// <summary>
        /// Absolute volume (0–1) the vanilla ward fades its hum to. 100% maps to this,
        /// so the mod never plays the sound louder than stock.
        /// </summary>
        private const float SoundVolumeCeiling = 0.5f;

        /// <summary>Whether the Rift Ward ambient hum plays at all on this client.</summary>
        public static bool SoundEnabled = DefaultSoundEnabled;

        /// <summary>Ambient-sound volume as a percentage (0–100) of the vanilla level.</summary>
        public static int SoundVolumePercent = DefaultSoundVolumePercent;

        /// <summary>Ambient-sound audible range, in blocks.</summary>
        public static int SoundRange = DefaultSoundRange;

        /// <summary>
        /// The configured volume resolved to the absolute 0–1 value the engine uses,
        /// i.e. the chosen percentage of the vanilla ceiling. Read by the sound patch.
        /// </summary>
        public static float ResolvedSoundVolume =>
            Math.Clamp(SoundVolumePercent, 0, 100) / 100f * SoundVolumeCeiling;

        // Captured during Load so Save() can report write failures without having
        // to change its (caller-facing) parameterless signature.
        private static ILogger? _logger;

        /// <summary>
        /// Loads client settings from disk, validating each value and falling back
        /// to the default when a stored value is missing or malformed. Safe to call
        /// when the file does not exist (defaults are left in place).
        /// </summary>
        public static void Load(ICoreClientAPI capi)
        {
            _logger = capi.Logger;
            string path = Path.Combine(GamePaths.ModConfig, ClientConfigFileName);
            try
            {
                if (!File.Exists(path)) return;

                JObject data = JObject.Parse(File.ReadAllText(path));

                // Highlight colour: only accept a full #AARRGGBB string; otherwise default.
                string? loadedColor = data["HighlightColor"]?.ToString();
                if (IsValidArgbHex(loadedColor))
                {
                    HighlightColor = loadedColor!.ToUpperInvariant();
                }
                else if (loadedColor is not null)
                {
                    capi.Logger.Warning(
                        "[RiftWardTweaks] Ignoring invalid HighlightColor '{0}' in client config; expected #AARRGGBB. Falling back to {1}.",
                        loadedColor, DefaultHighlightColor);
                    HighlightColor = DefaultHighlightColor;
                }

                // Preview duration: must be a positive integer; otherwise default.
                int? loadedDuration = data.Value<int?>("ColorPreviewDurationMs");
                if (loadedDuration is > 0)
                {
                    ColorPreviewDurationMs = loadedDuration.Value;
                }
                else if (loadedDuration is not null)
                {
                    capi.Logger.Warning(
                        "[RiftWardTweaks] Ignoring invalid ColorPreviewDurationMs '{0}' in client config; must be > 0. Falling back to {1}.",
                        loadedDuration, DefaultColorPreviewDurationMs);
                    ColorPreviewDurationMs = DefaultColorPreviewDurationMs;
                }

                // Sound — toggle (bool), volume as a 0–100 percentage, range in blocks.
                bool? loadedSoundEnabled = data.Value<bool?>("SoundEnabled");
                if (loadedSoundEnabled.HasValue)
                {
                    SoundEnabled = loadedSoundEnabled.Value;
                }

                int? loadedVolume = data.Value<int?>("SoundVolumePercent");
                if (IsValidVolumePercent(loadedVolume))
                {
                    SoundVolumePercent = loadedVolume!.Value;
                }
                else if (loadedVolume is not null)
                {
                    capi.Logger.Warning(
                        "[RiftWardTweaks] Ignoring invalid SoundVolumePercent '{0}' in client config; must be 0–100. Falling back to {1}.",
                        loadedVolume, DefaultSoundVolumePercent);
                    SoundVolumePercent = DefaultSoundVolumePercent;
                }

                int? loadedRange = data.Value<int?>("SoundRange");
                if (IsValidSoundRange(loadedRange))
                {
                    SoundRange = loadedRange!.Value;
                }
                else if (loadedRange is not null)
                {
                    capi.Logger.Warning(
                        "[RiftWardTweaks] Ignoring invalid SoundRange '{0}' in client config; must be {1}–{2}. Falling back to {3}.",
                        loadedRange, MinSoundRange, MaxSoundRange, DefaultSoundRange);
                    SoundRange = DefaultSoundRange;
                }
            }
            catch (Exception e)
            {
                capi.Logger.Warning("[RiftWardTweaks] Could not load client config: " + e);
            }
        }

        /// <summary>
        /// Persists the current client settings to disk. Write failures are logged
        /// rather than thrown, so a failed save cannot crash the calling chat command.
        /// </summary>
        public static void Save()
        {
            string path = Path.Combine(GamePaths.ModConfig, ClientConfigFileName);
            try
            {
                JObject obj = new()
                {
                    ["HighlightColor"] = HighlightColor,
                    ["ColorPreviewDurationMs"] = ColorPreviewDurationMs,
                    ["SoundEnabled"] = SoundEnabled,
                    ["SoundVolumePercent"] = SoundVolumePercent,
                    ["SoundRange"] = SoundRange
                };
                File.WriteAllText(path, obj.ToString());
            }
            catch (Exception e)
            {
                _logger?.Warning("[RiftWardTweaks] Could not save client config: " + e);
            }
        }

        /// <summary>
        /// Returns true when <paramref name="value"/> is a full 8-digit ARGB hex
        /// string in <c>#AARRGGBB</c> form: a leading '#' followed by exactly eight
        /// hex digits. Exposed internally so command handlers can share a single
        /// definition of a "valid colour" rather than re-checking inline.
        /// </summary>
        internal static bool IsValidArgbHex(string? value)
        {
            if (value is null || value.Length != 9 || value[0] != '#') return false;
            for (int i = 1; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i])) return false;
            }
            return true;
        }

        /// <summary>True when <paramref name="value"/> is a volume percentage in the 0–100 range.</summary>
        internal static bool IsValidVolumePercent(int? value) => value is >= 0 and <= 100;

        /// <summary>
        /// True when <paramref name="value"/> is a sound range within the accepted block bounds
        /// (<see cref="MinSoundRange"/>–<see cref="MaxSoundRange"/>).
        /// </summary>
        internal static bool IsValidSoundRange(int? value) => value is >= MinSoundRange and <= MaxSoundRange;
    }
}
