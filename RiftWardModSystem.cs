using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RiftWardTweaks
{
    public class ModSystemRiftWardTweaks : ModSystem
    {
        public static RiftWardConfig Config { get; private set; } = null!;
        private ICoreServerAPI? _sapi;
        private ICoreClientAPI? _capi;

        private const string ConfigFileName = "riftwardtweaksconfig.json";
        private const string HarmonyServerId = "elo.riftwardtweaks.server";
        private const string HarmonyClientId = "elo.riftwardtweaks.client";

        private bool rwHighlight = false;

        public static class RiftWardKeys
        {
            public const string Fuel = "fuelconsumptionmultiplier";
            public const string Range = "riftblockrange";
            public const string ScanRadius = "scanradius";
            public const string HighlightColor = "highlightcolor";
        }

        #region Entry Points

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
            _capi = capi;

            ApplyPatches(HarmonyClientId);

            RegisterClientCommands(capi);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);
            _sapi = sapi;

            LoadConfig(sapi);
            ApplyPatches(HarmonyServerId);

            sapi.Logger.Notification(
                "[RiftWardTweaks]  Mod loaded with range={0}, fuelMultiplier={1}",
                Config.RiftBlockRange, Config.FuelConsumptionMultiplier
            );

            RegisterServerCommands(sapi);
        }

        public override void Dispose()
        {
            new Harmony(HarmonyServerId).UnpatchAll(HarmonyServerId);
            new Harmony(HarmonyClientId).UnpatchAll(HarmonyClientId);
            _sapi?.Logger.Notification("[RiftWardTweaks] Harmony patches removed.");
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
                .EndSubCommand();
        }

        private TextCommandResult ToggleHighlightCommand(TextCommandCallingArgs args)
        {
            rwHighlight = !rwHighlight;

            var player = _capi?.World.Player as IClientPlayer;
            if (player == null || _capi == null)
                return TextCommandResult.Success("[RiftWardTweaks] Player or client API unavailable.");

            int scanRadius = Config.ScanRadius;
            int wardRange = Config.RiftBlockRange;
            int color = ParseHexColor(Config.HighlightColor);

            if (!rwHighlight)
            {
                for (int i = 0; i < 100; i++)  // Clear up to 100 highlight slots
                {
                    _capi.World.HighlightBlocks(
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

            IBlockAccessor accessor = _capi.World.BlockAccessor;

            int slotId = 0;
            int count = 0;

            for (int x = minScan.X; x <= maxScan.X; x++)
                for (int y = minScan.Y; y <= maxScan.Y; y++)
                    for (int z = minScan.Z; z <= maxScan.Z; z++)
                    {
                        BlockPos pos = new(x, y, z);
                        Block block = accessor.GetBlock(pos);

                        if (block?.Code?.Path == "riftward")
                        {
                            BlockEntity be = accessor.GetBlockEntity(pos);
                            if (be is not BlockEntityRiftWard ward || !ward.On) continue;

                            BlockPos min = new(pos.X - wardRange, pos.Y - wardRange, pos.Z - wardRange);
                            BlockPos max = new(pos.X + wardRange, pos.Y + wardRange, pos.Z + wardRange);

                            var corners = new List<BlockPos> { min, max };
                            var colors = new List<int> { color, color };

                            _capi.World.HighlightBlocks(
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

        #endregion

        #region Server Commands

        private void RegisterServerCommands(ICoreServerAPI sapi)
        {
            sapi.ChatCommands
                .Create("rwt")
                .WithDescription("Modify Rift Ward Tweaks settings.")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("get")
                    .WithDescription("Show current Rift Ward Tweaks config values.")
                    .HandleWith(HandleGetCommand)
                .EndSubCommand()
                .BeginSubCommand("set")
                    .WithDescription("Set a config value.\n" + "Keys:\n" + "- fuel / f / fuelconsumptionmultiplier\n" + "- range / r / riftblockrange\n" + "- scan / s / scanradius\n" + "- color / c / highlightcolor (ARGB hex, e.g. #3C00FF00)\n" + "Example: /rwt set f 0.02")
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
            var player = args.Caller.Player as IServerPlayer;
            if (player == null)
                return TextCommandResult.Success();

            try
            {
                if (_sapi == null)
                {
                    msg(player, "Server API is not initialized.");
                    return TextCommandResult.Success();
                }

                LoadConfig(_sapi);
                msg(player, "Config reloaded.");
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
            var player = args.Caller.Player as IServerPlayer;
            if (player == null)
                return TextCommandResult.Success();

            if (_sapi == null)
            {
                msg(player, "Server API not initialized.");
                return TextCommandResult.Success();
            }

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

                    case "color":
                    case "c":
                    case RiftWardKeys.HighlightColor:
                        if (string.IsNullOrEmpty(val) || !val.StartsWith("#") || val.Length != 9)
                        {
                            msg(player, "Invalid color. Must be a hex string in ARGB format like #3C00FF00.");
                            return TextCommandResult.Success();
                        }
                        Config.HighlightColor = val.ToUpperInvariant();
                        break;

                    default:
                        msg(player, $"Unknown config key: '{key}'");
                        return TextCommandResult.Success();
                }

                _sapi.StoreModConfig(new JsonObject(JToken.FromObject(Config)), ConfigFileName);
                msg(player, $"Set '{key}' to '{val}'. Config saved.");
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
            var player = args.Caller.Player as IServerPlayer;
            if (player == null)
                return TextCommandResult.Success();

            if (Config == null)
            {
                msg(player, "Config is not loaded.");
                return TextCommandResult.Success();
            }

            msg(player, "Current Config:");
            msg(player, $"- FuelConsumptionMultiplier: {Config.FuelConsumptionMultiplier}");
            msg(player, $"- RiftBlockRange: {Config.RiftBlockRange}");

            return TextCommandResult.Success();
        }

        #endregion

        #region Utility

        private void ApplyPatches(string harmonyId)
        {
            var harmony = new Harmony(harmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void LoadConfig(ICoreServerAPI sapi)
        {
            try
            {
                Config = sapi.LoadModConfig<RiftWardConfig>(ConfigFileName);

                if (Config == null)
                {
                    Config = new RiftWardConfig();
                    sapi.StoreModConfig(new JsonObject(JToken.FromObject(Config)), ConfigFileName);
                    sapi.Logger.Notification("[RiftWardTweaks] Created default config.");
                }
                else
                {
                    sapi.Logger.Notification("[RiftWardTweaks] Loaded config from file.");
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Error("[RiftWardTweaks] Failed to load config: " + e);
                Config = new RiftWardConfig();
            }
        }

        private void msg(IServerPlayer? player, string text)
        {
            player?.SendMessage(GlobalConstants.GeneralChatGroup, "[RiftWardTweaks] " + text, EnumChatType.Notification);
        }

        private int ParseHexColor(string hex)
        {
            if (hex.StartsWith("#")) hex = hex[1..];
            return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int color)
                ? color
                : ColorUtil.ToRgba(60, 0, 255, 0);
        }

        #endregion
    }
}