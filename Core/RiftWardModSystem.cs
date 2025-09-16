using HarmonyLib;
using Newtonsoft.Json.Linq;
using RiftWardTweaks.Config;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RiftWardTweaks.Core
{
    public class ModSystemRiftWardTweaks : ModSystem
    {
        public static RiftWardConfig Config { get; private set; } = null!;
        private ICoreServerAPI? _api;
        private ICoreServerAPI? _sapi;
        private ICoreClientAPI? _capi;
        private IServerNetworkChannel? serverChannel;
        private IClientNetworkChannel? clientChannel;

        private const string ConfigFileName = "riftwardtweaksconfig.json";
        private const string HarmonyServerId = "elo.riftwardtweaks.server";
        private const string HarmonyClientId = "elo.riftwardtweaks.client";

        private readonly List<int> activeColorPreviewIds = new();

        public static List<BlockEntityRiftWard> ActiveWards = new();

        private bool rwHighlight = false;

        public static class RiftWardKeys
        {
            public const string Fuel = "fuelconsumptionmultiplier";
            public const string Range = "riftblockrange";
            public const string ScanRadius = "scanradius";
            public const string HighlightColor = "highlightcolor";
            public const string Duration = "colorpreviewdurationms";
            public const string LightHSV = "lighthsv";
            public const string ToggleLight = "togglelight";
        }

        #region Entry Points

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            Harmony.DEBUG = false;
            api.World.Logger.Event("Rift Ward Tweaks v2.6.0 loading");
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
            _capi = capi;

            ApplyPatches(HarmonyClientId);
            RiftWardClientConfig.Load(capi);
            LoadConfig(capi);
            clientChannel = capi.Network.RegisterChannel("riftwardsync")
                .RegisterMessageType<RiftWardConfigSyncPacket>()
                .SetMessageHandler<RiftWardConfigSyncPacket>(OnReceiveConfigSync);
            RegisterClientCommands(capi);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);
            _sapi = sapi;
            LoadConfig(sapi);
            ApplyPatches(HarmonyServerId);
            serverChannel = sapi.Network.RegisterChannel("riftwardsync")
                .RegisterMessageType<RiftWardConfigSyncPacket>();
            sapi.Logger.Notification(
                "[RiftWardTweaks]  Mod loaded with range={0}, fuelMultiplier={1}",
                Config.RiftBlockRange, Config.FuelConsumptionMultiplier
            );

            RegisterServerCommands(sapi);
            sapi.Event.PlayerJoin += (player) =>
            {
                serverChannel?.SendPacket(new RiftWardConfigSyncPacket
                {
                    LightHSV = Config.LightHSV,
                    ToggleLight = Config.ToggleLight,
                    RiftBlockRange = Config.RiftBlockRange,
                    FuelConsumptionMultiplier = Config.FuelConsumptionMultiplier,
                    ScanRadius = Config.ScanRadius
                }, player);
            };
        }

        public override void Dispose()
        {
            new Harmony(HarmonyServerId).UnpatchAll(HarmonyServerId);
            new Harmony(HarmonyClientId).UnpatchAll(HarmonyClientId);
            _sapi?.Logger.Notification("[RiftWardTweaks] Harmony patches removed.");

            ActiveWards.Clear();
        }

        #endregion

        #region Client Commands

        private void RegisterClientCommands(ICoreClientAPI capi)
        {
            capi.ChatCommands
                .Create("rwt")
                .BeginSubCommand("show")
                    .WithDescription("Toggle Rift Ward range visual highlight")
                    .HandleWith(ToggleHighlightCommand)
                .EndSubCommand()
                .BeginSubCommand("color")
                    .WithDescription("Set your local Rift Ward highlight color (ARGB hex)")
                    .WithArgs(capi.ChatCommands.Parsers.Word("hex"))
                    .HandleWith(args =>
                    {
                        string hex = args[0]?.ToString() ?? "";
                        if (!hex.StartsWith("#") || hex.Length != 9)
                            return TextCommandResult.Success("Invalid format. Use #AARRGGBB.");

                        RiftWardClientConfig.HighlightColor = hex.ToUpperInvariant();
                        RiftWardClientConfig.Save();
                        return TextCommandResult.Success($"Highlight color set to {hex}");
                    })
                .EndSubCommand()
                .BeginSubCommand("preview")
                    .WithDescription("Display preview highlight colors.")
                    .HandleWith(PreviewHighlightColors)
                .EndSubCommand()
                .BeginSubCommand("clear")
                    .WithDescription("Manually clear the color highlight cubes from .rwt colors.")
                    .HandleWith(ClearColorPreviewCommand)
                .EndSubCommand()
                .BeginSubCommand("get")
                    .WithDescription("Show local client-side Rift Ward config values")
                    .HandleWith(args =>
                    {
                        string color = RiftWardClientConfig.HighlightColor ?? "(none)";
                        int duration = Config?.ColorPreviewDurationMs ?? 0;

                        _capi?.ShowChatMessage("[RiftWardTweaks] Local Client Config:");
                        _capi?.ShowChatMessage($"- HighlightColor: {color}");
                        _capi?.ShowChatMessage($"- PreviewDurationMs: {duration} ms");

                        return TextCommandResult.Success();
                    })
                .EndSubCommand();
        }

        private TextCommandResult ToggleHighlightCommand(TextCommandCallingArgs args)
        {
            rwHighlight = !rwHighlight;

            if (_capi?.World == null)
                return TextCommandResult.Success("[RiftWardTweaks] Client API is not available.");
            var player = _capi?.World.Player as IClientPlayer;
            if (player?.Entity?.Pos == null)
                return TextCommandResult.Success("[RiftWardTweaks] Player or client API unavailable.");
            if (Config == null)
                return TextCommandResult.Success("[RiftWardTweaks] Configuration not loaded.");

            int scanRadius = Config.ScanRadius;
            int wardRange = Config.RiftBlockRange;
            int color = ParseHexColor(RiftWardClientConfig.HighlightColor);

            if (!rwHighlight)
            {
                for (int i = 0; i < 100; i++)  // Clear up to 100 highlight slots
                {
                    _capi?.World.HighlightBlocks(
                        player,
                        i,
                        new List<BlockPos>(),
                        new List<int>(),
                        EnumHighlightBlocksMode.Absolute,
                        EnumHighlightShape.Cube,
                        1f
                    );
                }

                return TextCommandResult.Success("[RiftWardTweaks] Rift Ward highlights disabled.");
            }

            BlockPos center = player.Entity.Pos.AsBlockPos;
            BlockPos minScan = center.AddCopy(-scanRadius, -scanRadius, -scanRadius);
            BlockPos maxScan = center.AddCopy(scanRadius, scanRadius, scanRadius);

            IBlockAccessor? accessor = _capi?.World?.BlockAccessor;

            int slotId = 0;
            int count = 0;

            for (int x = minScan.X; x <= maxScan.X; x++)
                for (int y = minScan.Y; y <= maxScan.Y; y++)
                    for (int z = minScan.Z; z <= maxScan.Z; z++)
                    {
                        BlockPos pos = new(x, y, z);
                        Block? block = accessor?.GetBlock(pos);

                        if (block?.Code?.Path == "riftward")
                        {
                            BlockEntity? be = accessor?.GetBlockEntity(pos);
                            if (be is not BlockEntityRiftWard ward || !ward.On) continue;

                            BlockPos min = new(pos.X - wardRange, pos.Y - wardRange, pos.Z - wardRange);
                            BlockPos max = new(pos.X + wardRange, pos.Y + wardRange, pos.Z + wardRange);

                            var corners = new List<BlockPos> { min, max };
                            var colors = new List<int> { color, color };

                            _capi?.World?.HighlightBlocks(
                                player,
                                slotId,
                                corners,
                                colors,
                                EnumHighlightBlocksMode.Absolute,
                                EnumHighlightShape.Cube,
                                1f
                            );

                            slotId++;
                            count++;
                        }
                    }

            if (count == 0)
                return TextCommandResult.Success("[RiftWardTweaks] No Rift Wards found nearby.");

            return TextCommandResult.Success($"[RiftWardTweaks] Showing {count} Rift Ward highlight range{(count == 1 ? "" : "s")}.");
        }
        private TextCommandResult PreviewHighlightColors(TextCommandCallingArgs args)
        {
            if (_capi?.World?.Player is not IClientPlayer player) return TextCommandResult.Success("Client unavailable.");

            var examples = new Dictionary<int, string>
            {
                { ColorUtil.Hex2Int("#3C0000FF"), "Translucent Red (#3C0000FF)" },
                { ColorUtil.Hex2Int("#3C00FF00"), "Translucent Green (#3C00FF00)" },
                { ColorUtil.Hex2Int("#3CFF0000"), "Translucent Blue (#3CFF0000)" },
                { ColorUtil.Hex2Int("#3CFF00FF"), "Translucent Purple (#3CFF00FF)" },
                { ColorUtil.Hex2Int("#3CFFFFFF"), "Translucent White (#3CFFFFFF)" },
            };
            _capi.ShowChatMessage("[RiftWardTweaks] ARGB Color Samples:");

            foreach (var kvp in examples)
            {
                _capi.ShowChatMessage($"{kvp.Value.PadRight(24)} {ColorUtil.Int2Hex(kvp.Key)}");
            }
            Vec3f look = player.Entity.Pos.GetViewVector();
            Vec3d forward = new Vec3d(look.X, 0, look.Z).Normalize();
            Vec3d right = forward.Cross(new Vec3d(0, 1, 0)).Normalize();

            Vec3d basePos = player.Entity.Pos.XYZ.AddCopy(0, 1.5, 0).AddCopy(forward * 4);
            BlockPos anchor = new((int)basePos.X, (int)basePos.Y, (int)basePos.Z);

            int i = 900;
            int offset = 0;

            activeColorPreviewIds.Clear();
            foreach (var pair in examples)
            {
                Vec3i rightOffset = right.X > right.Z
                    ? new Vec3i(Math.Sign(right.X), 0, 0)
                    : new Vec3i(0, 0, Math.Sign(right.Z));

                BlockPos center = anchor.AddCopy(rightOffset.X * offset, 0, rightOffset.Z * offset);
                BlockPos min = center.AddCopy(-1, 0, -1);
                BlockPos max = center.AddCopy(1, 2, 1);

                _capi.World.HighlightBlocks(
                    player,
                    i,
                    new List<BlockPos> { min, max },
                    new List<int> { pair.Key, pair.Key },
                    EnumHighlightBlocksMode.Absolute,
                    EnumHighlightShape.Cube,
                    1f
                );

                activeColorPreviewIds.Add(i);
                int removeId = i;
                _capi.World.RegisterCallback(_ =>
                {
                    _capi.World.HighlightBlocks(
                        player,
                        removeId,
                        new List<BlockPos>(),
                        new List<int>(),
                        EnumHighlightBlocksMode.Absolute,
                        EnumHighlightShape.Cube,
                        0
                    );
                }, Math.Max(100, Config.ColorPreviewDurationMs));

                offset += 3;
                i++;
            }

            return TextCommandResult.Success($"[RiftWardTweaks] Showing color samples for {Config.ColorPreviewDurationMs} milliseconds.");
        }

        private TextCommandResult ClearColorPreviewCommand(TextCommandCallingArgs args)
        {
            if (_capi?.World?.Player is not IClientPlayer player)
                return TextCommandResult.Success("Client unavailable.");

            foreach (var id in activeColorPreviewIds)
            {
                _capi.World.HighlightBlocks(
                    player,
                    id,
                    new List<BlockPos>(),
                    new List<int>(),
                    EnumHighlightBlocksMode.Absolute,
                    EnumHighlightShape.Cube,
                    0
                );
            }

            activeColorPreviewIds.Clear();
            return TextCommandResult.Success("[RiftWardTweaks] Color previews cleared.");
        }

        #endregion

        #region Server Commands

        private void RegisterServerCommands(ICoreServerAPI sapi)
        {
            sapi.ChatCommands
                .Create("rwt")
                .WithDescription("Modify Rift Ward Tweaks settings.")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("get")
                    .WithDescription("Show current server-side Rift Ward config values.")
                    .HandleWith(HandleGetCommand)
                .EndSubCommand()
                .BeginSubCommand("set")
                    .WithDescription("Set a config value.\n" + "Keys:\n" + "- fuel / f / fuelconsumptionmultiplier\n" + "- range / r / riftblockrange\n" + "- scan / s / scanradius\n" + "- duration / d / previewdurationms (Milliseconds)\n" + "- light / hsv / lh / l / h / lighthsv (7,7,7)\n" + "- toggle / tl / t / togglelight (true/false)\n" + "Example: /rwt set f 0.02")
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Word("value")
                    )
                    .HandleWith(HandleSetCommand)
                .EndSubCommand()
                .BeginSubCommand("reload")
                    .WithDescription("Reload Rift Ward Tweaks config from disk.")
                    .HandleWith(HandleReloadCommand)
                .EndSubCommand();
        }

        private TextCommandResult HandleReloadCommand(TextCommandCallingArgs args)
        {
            if (_sapi == null)
            {
                _sapi?.Logger.Warning("[RiftWardTweaks] Server API not initialized.");
                return TextCommandResult.Success();
            }
            if (!IsAuthorized(args, out var player))
                return TextCommandResult.Success();
            try
            {
                LoadConfig(_sapi);
                SyncConfigToClients();
                msg(player, "Config reloaded and synced to clients.");
                return TextCommandResult.Success();
            }
            catch (Exception ex)
            {
                msg(player, "Failed to reload config: " + ex.Message);
                return TextCommandResult.Success();
            }
        }

        private TextCommandResult HandleSetCommand(TextCommandCallingArgs args)
        {
            if (_sapi == null)
            {
                _sapi?.Logger.Warning("[RiftWardTweaks] Server API not initialized.");
                return TextCommandResult.Success();
            }
            if (!IsAuthorized(args, out var player))
                return TextCommandResult.Success();
            if (args.Parsers.Count < 2)
            {
                msg(player, "Usage: /rwt set <key> <value>");
                return TextCommandResult.Success();
            }

            string? key = args[0]?.ToString()?.ToLowerInvariant();
            string? val = args[1].ToString();

            try
            {
                switch (key)
                {
                    case "fuel":
                    case "f":
                    case RiftWardKeys.Fuel:
                        if (!float.TryParse(val, out float fuelMult) || fuelMult <= 0f)
                        {
                            msg(player, "Invalid float. Must be a positive number.");
                            return TextCommandResult.Success();
                        }
                        Config.FuelConsumptionMultiplier = fuelMult;
                        break;

                    case "range":
                    case "r":
                    case RiftWardKeys.Range:
                        if (!int.TryParse(val, out int range) || range <= 0)
                        {
                            msg(player, "Invalid integer. Must be a positive number.");
                            return TextCommandResult.Success();
                        }
                        Config.RiftBlockRange = range;
                        break;

                    case "scan":
                    case "s":
                    case RiftWardKeys.ScanRadius:
                        if (!int.TryParse(val, out int radius) || radius <= 0)
                        {
                            msg(player, "Invalid scan radius. Must be a positive number.");
                            return TextCommandResult.Success();
                        }
                        Config.ScanRadius = radius;
                        break;

                    case "duration":
                    case "d":
                    case RiftWardKeys.Duration:
                        if (!int.TryParse(val, out int ms) || ms <= 0)
                        {
                            msg(player, "Invalid duration. Must be a positive number (in milliseconds).");
                            return TextCommandResult.Success();
                        }
                        Config.ColorPreviewDurationMs = ms;
                        break;

                    case "light":
                    case "hsv":
                    case "lh":
                    case "l":
                    case "h":
                    case RiftWardKeys.LightHSV:
                        {
                            string?[]? parts = val?.Split(',');
                            if (parts?.Length != 3 ||
                                !int.TryParse(parts[0], out int h) ||
                                !int.TryParse(parts[1], out int s) ||
                                !int.TryParse(parts[2], out int v))
                            {
                                msg(player, "Invalid HSV format. Use: /rwt set hsv 7,7,7");
                                return TextCommandResult.Success();
                            }

                            h = GameMath.Clamp(h, 0, 64);
                            s = GameMath.Clamp(s, 0, 8);
                            v = GameMath.Clamp(v, 3, 21);

                            int[] oldHsvRaw = Config.LightHSV;
                            int oldV = GameMath.Clamp(oldHsvRaw?[2] ?? 0, 3, 21);
                            byte[] oldHsv = new byte[] {
                                (byte)GameMath.Clamp(oldHsvRaw?[0] ?? 0, 0, 64),
                                (byte)GameMath.Clamp(oldHsvRaw?[1] ?? 0, 0, 8),
                                (byte)oldV
                            };

                            foreach (var be in ActiveWards)
                            {
                                if (be is BlockEntityRiftWard ward && ward?.On == true)
                                {
                                    BlockPos center = ward.Pos;

                                    int oldRadius = oldV;
                                    ForceRemoveLight(_sapi, center, oldRadius, oldHsv);
                                }
                            }

                            Config.LightHSV = new int[] { h, s, v };
                            _sapi.StoreModConfig(new JsonObject(JToken.FromObject(Config)), ConfigFileName);
                            msg(player, $"Updated LightHSV to: {h},{s},{v}");

                            foreach (var be in ActiveWards)
                            {
                                if (be is BlockEntityRiftWard ward && ward?.On == true)
                                {
                                    _sapi.World.BlockAccessor.MarkBlockDirty(ward.Pos);
                                    _sapi.World.BlockAccessor.MarkBlockModified(ward.Pos);
                                    _sapi.World.BlockAccessor.TriggerNeighbourBlockUpdate(ward.Pos);
                                }
                            }

                            break;
                        }

                    case "toggle":
                    case "tl":
                    case "t":
                    case RiftWardKeys.ToggleLight:
                        {
                            if (!bool.TryParse(val, out bool toggleLight))
                            {
                                msg(player, "Invalid value. Use: true or false");
                                return TextCommandResult.Success();
                            }

                            Config.ToggleLight = toggleLight;
                            _sapi.StoreModConfig(new JsonObject(JToken.FromObject(Config)), ConfigFileName);
                            msg(player, $"Light emission is now {(toggleLight ? "enabled" : "disabled")}.");

                            foreach (var be in ActiveWards)
                            {
                                if (be is BlockEntityRiftWard ward && ward?.On == true)
                                {
                                    _sapi.World.BlockAccessor.MarkBlockDirty(ward.Pos);
                                    _sapi.World.BlockAccessor.MarkBlockModified(ward.Pos);
                                }
                            }

                            return TextCommandResult.Success();
                        }

                    default:
                        msg(player, $"Unknown config key: '{key}'");
                        return TextCommandResult.Success();
                }

                _sapi.StoreModConfig(new JsonObject(JToken.FromObject(Config)), ConfigFileName);
                msg(player, $"Set '{key}' to '{val}'. Config saved.");
                SyncConfigToClients();
                return TextCommandResult.Success();
            }
            catch (Exception ex)
            {
                msg(player, "Failed to update config: " + ex.Message);
                return TextCommandResult.Success();
            }
        }

        private TextCommandResult HandleGetCommand(TextCommandCallingArgs args)
        {
            if (_sapi == null)
            {
                _sapi?.Logger.Warning("[RiftWardTweaks] Server API not initialized.");
                return TextCommandResult.Success();
            }
            if (!IsAuthorized(args, out var player))
                return TextCommandResult.Success();
            if (Config == null)
            {
                msg(player, "Config is not loaded.");
                return TextCommandResult.Success();
            }

            msg(player, "Current Server-Side Config:");

            float fuelMult = Config.FuelConsumptionMultiplier;
            string fuelNote = fuelMult <= 0 ? "INVALID (<= 0!)" : "";
            msg(player, $"- FuelConsumptionMultiplier: {fuelMult} {fuelNote}");
            msg(player, $"- RiftBlockRange: {Config.RiftBlockRange}");
            msg(player, $"- LightHSV: {string.Join(",", Config.LightHSV ?? new int[] { 0, 0, 0 })}");
            msg(player, $"- ToggleLight: {Config.ToggleLight}");
            msg(player, $"- ScanRadius: {Config.ScanRadius}");

            return TextCommandResult.Success();
        }

        #endregion

        #region Utility

        private void ApplyPatches(string harmonyId)
        {
            var harmony = new Harmony(harmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void LoadConfig(ICoreAPI api)
        {
            try
            {
                Config = api.LoadModConfig<RiftWardConfig>(ConfigFileName);

                if (Config.FuelConsumptionMultiplier <= 0)
                {
                    api.Logger.Warning("[RWT] Invalid FuelConsumptionMultiplier in config, using default of 1.0");
                    Config.FuelConsumptionMultiplier = 1.0f;
                }

                if (Config == null)
                {
                    Config = new RiftWardConfig();
                    api.StoreModConfig(new JsonObject(JToken.FromObject(Config)), ConfigFileName);
                    api.Logger.Notification("[RiftWardTweaks] Created default config.");
                }
                else
                {
                    api.Logger.Notification("[RiftWardTweaks] Loaded config from file.");
                }
            }
            catch (Exception e)
            {
                api.Logger.Error("[RiftWardTweaks] Failed to load config: " + e);
                Config = new RiftWardConfig();
            }
        }

        private bool IsAuthorized(TextCommandCallingArgs args, out IServerPlayer? player)
        {
            player = args.Caller.Player as IServerPlayer;
            if (player == null) return true;

            if (!args.Caller.HasPrivilege("controlserver"))
            {
                msg(player, "You don't have permission to use this command.");
                return false;
            }

            return true;
        }

        private void msg(IServerPlayer? player, string text)
        {
            if (player != null)
            {
                player?.SendMessage(GlobalConstants.GeneralChatGroup, "[RiftWardTweaks] " + text, EnumChatType.Notification);
            }
            else
            {
                _sapi?.Logger.Notification("[RiftWardTweaks] " + text);
            }
        }

        private int ParseHexColor(string hex)
        {
            if (hex.StartsWith("#")) hex = hex[1..];
            return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int color)
                ? color
                : ColorUtil.ToRgba(60, 0, 255, 0);
        }

        public static void ForceRemoveLight(ICoreServerAPI sapi, BlockPos center, int radius, byte[] oldHsv)
        {
            IBlockAccessor accessor = sapi.World.BlockAccessor;

            BlockPos min = new(center.X - radius, center.Y - radius, center.Z - radius);
            BlockPos max = new(center.X + radius, center.Y + radius, center.Z + radius);

            for (int x = min.X; x <= max.X; x++)
                for (int y = min.Y; y <= max.Y; y++)
                    for (int z = min.Z; z <= max.Z; z++)
                    {
                        BlockPos pos = new(x, y, z);
                        accessor.RemoveBlockLight(oldHsv, pos);
                        Block block = accessor.GetBlock(pos);
                        accessor.SetBlock(block.BlockId, pos);
                        accessor.MarkBlockDirty(pos);
                    }
        }

        public static byte[] GetSafeHSV()
        {
            int[] raw = Config?.LightHSV ?? new int[] { 0, 0, 0 };
            return new byte[] {
                (byte)GameMath.Clamp(raw[0], 0, 64),
                (byte)GameMath.Clamp(raw[1], 0, 8),
                (byte)GameMath.Clamp(raw[2], 3, 21)
            };
        }

        private void OnReceiveConfigSync(RiftWardConfigSyncPacket packet)
        {
            Config = new RiftWardConfig
            {
                LightHSV = packet.LightHSV ?? new int[] { 0, 0, 0 },
                ToggleLight = packet.ToggleLight,
                RiftBlockRange = packet.RiftBlockRange,
                FuelConsumptionMultiplier = packet.FuelConsumptionMultiplier,
                ScanRadius = packet.ScanRadius
            };

            _capi?.Logger.Notification("[RiftWardTweaks] Synced config from server.");
        }

        private void SyncConfigToClients()
        {
            var packet = new RiftWardConfigSyncPacket
            {
                LightHSV = Config.LightHSV,
                ToggleLight = Config.ToggleLight,
                RiftBlockRange = Config.RiftBlockRange,
                FuelConsumptionMultiplier = Config.FuelConsumptionMultiplier,
                ScanRadius = Config.ScanRadius
            };

            foreach (IServerPlayer sp in _sapi?.World?.AllOnlinePlayers.OfType<IServerPlayer>() ?? Enumerable.Empty<IServerPlayer>())
            {
                serverChannel?.SendPacket(packet, sp);
            }
        }

        #endregion
    }
}