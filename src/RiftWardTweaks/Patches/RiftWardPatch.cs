using HarmonyLib;
using RiftWardTweaks.Config;
using RiftWardTweaks.Core;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RiftWardTweaks.Patches
{
    #region Fuel & Range

    [HarmonyPatch(typeof(BlockEntityRiftWard), "OnServerTick")]
    internal class Patch_RiftWard_FuelReduction
    {
        // BlockEntityRiftWard stores these non-publicly (fuelDays + lastUpdateTotalDays
        // are protected). Verified against VS 1.22.x (BlockEntity/BERiftWard.cs). Reflected
        // once at type load instead of on every server tick - this prefix is a hot path.
        private static readonly FieldInfo? FuelDaysField =
            typeof(BlockEntityRiftWard).GetField("fuelDays", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? LastUpdateField =
            typeof(BlockEntityRiftWard).GetField("lastUpdateTotalDays", BindingFlags.NonPublic | BindingFlags.Instance);

        // Ensures the "could not reflect" warning is logged at most once per session.
        private static bool _warnedMissingFields;

        static void Prefix(BlockEntityRiftWard __instance)
        {
            if (!__instance.On || __instance.Api == null) return;

            if (FuelDaysField == null || LastUpdateField == null)
            {
                if (!_warnedMissingFields)
                {
                    _warnedMissingFields = true;
                    __instance.Api.Logger.Warning(
                        "[RiftWardTweaks] Could not reflect fuelDays/lastUpdateTotalDays on BlockEntityRiftWard. " +
                        "Fuel multiplier disabled; vanilla fuel rate applies. The rift ward internals may have changed.");
                }
                return; // Allow vanilla OnServerTick to run unmodified.
            }

            double totalDays = __instance.Api.World.Calendar.TotalDays;

            double fuelDays = (double)(FuelDaysField.GetValue(__instance) ?? 0d);
            double lastUpdate = (double)(LastUpdateField.GetValue(__instance) ?? totalDays);

            double daysPassed = totalDays - lastUpdate;
            double adjustedDays = daysPassed * ModSystemRiftWardTweaks.Config.FuelConsumptionMultiplier;

            fuelDays -= adjustedDays;

            // Pre-advancing lastUpdateTotalDays to "now" makes vanilla's own subtraction
            // (which still runs after this void prefix) a no-op, so the multiplier is the
            // only thing that drains fuel.
            FuelDaysField.SetValue(__instance, fuelDays);
            LastUpdateField.SetValue(__instance, totalDays);

            __instance.MarkDirty(true);

            if (fuelDays <= 0)
            {
                // Deactivate() is public on BlockEntityRiftWard (verified against VS 1.22.x),
                // so call it directly - no reflection needed in this tick path.
                __instance.Deactivate();
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "BlockEntityRiftWard_OnRiftSpawned")]
    internal class Patch_RiftWard_RangeIncrease
    {
        // Private counter field on BlockEntityRiftWard; verified against VS 1.22.x. Cached once.
        private static readonly FieldInfo? RiftsBlockedField =
            typeof(BlockEntityRiftWard).GetField("riftsBlocked", BindingFlags.NonPublic | BindingFlags.Instance);

        static bool Prefix(BlockEntityRiftWard __instance, Rift rift)
        {
            var sapi = (ICoreServerAPI)__instance.Api;
            if (__instance.On && sapi.World.Rand.NextDouble() <= 0.95 &&
                rift.Position.DistanceTo(__instance.Pos.X + 0.5, __instance.Pos.Y + 1, __instance.Pos.Z + 0.5) < ModSystemRiftWardTweaks.Config.RiftBlockRange)
            {
                rift.Size = 0;

                if (RiftsBlockedField != null)
                {
                    int blocked = (int)(RiftsBlockedField.GetValue(__instance) ?? 0);
                    RiftsBlockedField.SetValue(__instance, blocked + 1);
                }

                __instance.MarkDirty();
            }

            return false;
        }
    }

    #endregion

    #region UI

    [HarmonyPatch(typeof(BlockEntityRiftWard), "GetBlockInfo")]
    internal class Patch_RiftWard_Tooltip
    {
        // Tooltip only reads fuelDays; cached once. Verified against VS 1.22.x.
        private static readonly FieldInfo? FuelDaysField =
            typeof(BlockEntityRiftWard).GetField("fuelDays", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(BlockEntityRiftWard __instance, IPlayer forPlayer, StringBuilder dsc)
        {
            double multiplier = ModSystemRiftWardTweaks.Config.FuelConsumptionMultiplier;
            if (multiplier <= 0 || double.IsInfinity(multiplier) || double.IsNaN(multiplier))
            {
                multiplier = 1.0;
            }

            // No-op at the default multiplier: leave the vanilla tooltip exactly as-is.
            if (multiplier == 1.0) return;

            double fuelDays = (double)(FuelDaysField?.GetValue(__instance) ?? 0d);
            if (fuelDays <= 0) return; // Vanilla already shows the "out of power" line.

            double effectiveRuntime = fuelDays / multiplier;

            // Reconstruct the exact line vanilla appended - same lang key, same fuelDays
            // value, same active locale - and swap it for the multiplier-adjusted version.
            // Resolving through the shared vanilla key makes this work in EVERY language,
            // not just English. If the line isn't found (e.g. another mod rewrote it),
            // Replace is a harmless no-op and we still append the extra lines below.
            string vanillaChargeLine = Lang.Get("Charge for {0:0.#} days", fuelDays);
            dsc.Replace(vanillaChargeLine, Lang.Get("riftward:tooltip-chargefor", effectiveRuntime));

            // Append the mod's extra info directly. Vanilla never emits these lines, so there
            // is nothing to de-duplicate against.
            dsc.AppendLine(Lang.Get("riftward:tooltip-blockrange", ModSystemRiftWardTweaks.Config.RiftBlockRange));

            // Highlight colour is a client-local setting now, so read it from the client config.
            string hex = RiftWardClientConfig.HighlightColor.ToUpperInvariant();
            dsc.AppendLine(Lang.Get("riftward:tooltip-highlightcolor", hex));
        }
    }

    #endregion

    #region Light

    [HarmonyPatch]
    internal static class Patch_RiftWard_GetLight
    {
        [HarmonyTargetMethod]
        public static MethodBase? TargetMethod()
        {
            // CollectibleObject.GetLightHsv(IBlockAccessor, BlockPos, ItemStack stack = null) -> byte[]
            // Signature verified unchanged against VS 1.22.x (vsapi Common/Collectible/Collectible.cs);
            // single overload, so GetMethod-by-name is unambiguous.
            // NOTE: the engine may call this on a background thread, so the prefix must stay
            // side-effect free (it only reads config and the block entity).
            return typeof(CollectibleObject).GetMethod("GetLightHsv", BindingFlags.Instance | BindingFlags.Public);
        }

        public static bool Prefix(CollectibleObject __instance, IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack, ref byte[] __result)
        {
            try
            {
                if (__instance is not BlockRiftWard) return true;
                if (blockAccessor == null || pos == null) return true;

                var be = blockAccessor.GetBlockEntity(pos) as BlockEntityRiftWard;

                if (be?.On == true && ModSystemRiftWardTweaks.Config?.ToggleLight == true)
                {
                    int[] raw = ModSystemRiftWardTweaks.Config?.LightHSV ?? new int[] { 0, 0, 0 };
                    int h = GameMath.Clamp(raw[0], 0, 64);
                    int s = GameMath.Clamp(raw[1], 0, 8);
                    int v = GameMath.Clamp(raw[2], 3, 21);
                    __result = new byte[] { (byte)h, (byte)s, (byte)v };
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RiftWardTweaks] LightHsv patch failed: {ex}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "Deactivate")]
    internal class Patch_RiftWard_Deactivate_LightRemove
    {
        static void Postfix(BlockEntityRiftWard __instance)
        {
            var accessor = __instance?.Api?.World?.BlockAccessor;
            if (accessor == null) return;

            byte[] oldHsv = ModSystemRiftWardTweaks.GetSafeHSV();
            accessor.RemoveBlockLight(oldHsv, __instance?.Pos);
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "OnBlockRemoved")]
    internal class Patch_RiftWard_Remove_LightRemove
    {
        static void Postfix(BlockEntityRiftWard __instance)
        {
            var accessor = __instance?.Api?.World?.BlockAccessor;
            if (accessor == null) return;

            byte[] oldHsv = ModSystemRiftWardTweaks.GetSafeHSV();
            accessor.RemoveBlockLight(oldHsv, __instance?.Pos);
        }
    }
    #endregion
}
