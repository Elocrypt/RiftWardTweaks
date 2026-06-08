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
                    ["ColorPreviewDurationMs"] = ColorPreviewDurationMs
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
    }
}
