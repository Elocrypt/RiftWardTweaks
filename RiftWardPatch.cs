using HarmonyLib;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RiftWardTweaks
{
    [HarmonyPatch(typeof(BlockEntityRiftWard), "OnServerTick")]
    public class Patch_RiftWard_FuelReduction
    {
        static void Prefix(BlockEntityRiftWard __instance)
        {
            if (!__instance.On || __instance.Api == null) return;

            var type = typeof(BlockEntityRiftWard);

            double totalDays = __instance.Api.World.Calendar.TotalDays;

            var fuelDaysField = type.GetField("fuelDays", BindingFlags.NonPublic | BindingFlags.Instance);
            var lastUpdateField = type.GetField("lastUpdateTotalDays", BindingFlags.NonPublic | BindingFlags.Instance);

            double fuelDays = (double)(fuelDaysField?.GetValue(__instance) ?? 0);
            double lastUpdate = (double)(lastUpdateField?.GetValue(__instance) ?? totalDays);

            double daysPassed = totalDays - lastUpdate;
            double adjustedDays = daysPassed * ModSystemRiftWardTweaks.Config.FuelConsumptionMultiplier;

            fuelDays -= adjustedDays;

            fuelDaysField?.SetValue(__instance, fuelDays);
            lastUpdateField?.SetValue(__instance, totalDays);

            __instance.MarkDirty(false);

            if (fuelDays <= 0)
            {
                MethodInfo? deactivate = type.GetMethod("Deactivate", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                deactivate?.Invoke(__instance, null);
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "BlockEntityRiftWard_OnRiftSpawned")]
    class Patch_RiftWard_RangeIncrease
    {
        static bool Prefix(BlockEntityRiftWard __instance, Rift rift)
        {
            var sapi = (ICoreServerAPI)__instance.Api;
            if (__instance.On && sapi.World.Rand.NextDouble() <= 0.95 &&
                rift.Position.DistanceTo(__instance.Pos.X + 0.5, __instance.Pos.Y + 1, __instance.Pos.Z + 0.5) < ModSystemRiftWardTweaks.Config.RiftBlockRange)
            {
                rift.Size = 0;
                var field = typeof(BlockEntityRiftWard).GetField("riftsBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    int? blocked = (int?)field.GetValue(__instance);
                    field.SetValue(__instance, blocked + 1);
                }
                __instance.MarkDirty();
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "GetBlockInfo")]
    public class Patch_RiftWard_Tooltip
    {
        static void Postfix(BlockEntityRiftWard __instance, IPlayer forPlayer, StringBuilder dsc)
        {
            var type = typeof(BlockEntityRiftWard);
            double fuelDays = (double)(type.GetField("fuelDays", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) ?? 0);
            double multiplier = ModSystemRiftWardTweaks.Config.FuelConsumptionMultiplier;
            double effectiveRuntime = fuelDays / multiplier;

            if (fuelDays > 0 && multiplier != 1.0)
            {
                string[] lines = dsc.ToString().Split('\n');
                var newLines = new List<string>();
                bool hasBlockingInfo = false;
                bool hasHighlightInfo = false;

                foreach (string rawLine in lines)
                {
                    string trimmed = rawLine.TrimStart();

                    if (trimmed.StartsWith("Charge for", StringComparison.OrdinalIgnoreCase))
                    {
                        newLines.Add(Lang.Get("riftward:tooltip-chargefor", effectiveRuntime));
                    }
                    else if (trimmed.StartsWith("Blocking range", StringComparison.OrdinalIgnoreCase))
                    {
                        hasBlockingInfo = true;
                        newLines.Add(rawLine);
                    }
                    else if (trimmed.StartsWith("Highlight color", StringComparison.OrdinalIgnoreCase))
                    {
                        hasHighlightInfo = true;
                        newLines.Add(rawLine);
                    }
                    else if (!trimmed.StartsWith("Effective runtime", StringComparison.OrdinalIgnoreCase))
                    {
                        newLines.Add(rawLine);
                    }
                }

                dsc.Clear();
                dsc.Append(string.Join("\n", newLines));

                if (!hasBlockingInfo)
                {
                    dsc.AppendLine(Lang.Get("riftward:tooltip-blockrange", ModSystemRiftWardTweaks.Config.RiftBlockRange));
                }
                if (!hasHighlightInfo)
                {
                    string hex = ModSystemRiftWardTweaks.Config.HighlightColor.ToUpperInvariant();
                    dsc.AppendLine(Lang.Get("riftward:tooltip-highlightcolor", hex));
                }
            }
        }
    }

    [HarmonyPatch]
    public static class Patch_RiftWard_GetLight
    {
        [HarmonyTargetMethod]
        public static MethodBase? TargetMethod()
        {
            return typeof(CollectibleObject).GetMethod("GetLightHsv", BindingFlags.Instance | BindingFlags.Public);
        }

        public static bool Prefix(CollectibleObject __instance, IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack, ref byte[] __result)
        {
            try
            {
                if (__instance is not BlockRiftWard || !ModSystemRiftWardTweaks.Config.ToggleLight) return true;

                if (blockAccessor == null || pos == null) return true;

                var be = blockAccessor.GetBlockEntity(pos) as BlockEntityRiftWard;

                if (be?.On == true)
                {
                    int[] raw = ModSystemRiftWardTweaks.Config.LightHSV ?? new int[] { 0, 0, 0 };
                    int h = GameMath.Clamp(raw[0], 0, 64);
                    int s = GameMath.Clamp(raw[1], 0, 8);
                    int v = GameMath.Clamp(raw[2], 3, 21);
                    __result = new byte[] { (byte)h, (byte)s, (byte)v };
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RWT] LightHsv patch failed: {ex}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "Deactivate")]
    public class Patch_RiftWard_Deactivate_LightRemove
    {
        static void Postfix(BlockEntityRiftWard __instance)
        {
            var accessor = __instance?.Api?.World?.BlockAccessor;
            if (accessor == null) return;

            byte[] oldHsv = ModSystemRiftWardTweaks.Config.LightHSV != null
                ? new byte[] {
                (byte)GameMath.Clamp(ModSystemRiftWardTweaks.Config.LightHSV[0], 0, 64),
                (byte)GameMath.Clamp(ModSystemRiftWardTweaks.Config.LightHSV[1], 0, 8),
                (byte)GameMath.Clamp(ModSystemRiftWardTweaks.Config.LightHSV[2], 3, 21)
                }
                : new byte[] { 0, 0, 0 };

            accessor.RemoveBlockLight(oldHsv, __instance?.Pos);
        }
    }

    [HarmonyPatch(typeof(BlockEntityRiftWard), "OnBlockRemoved")]
    public class Patch_RiftWard_Remove_LightRemove
    {
        static void Postfix(BlockEntityRiftWard __instance)
        {
            var accessor = __instance?.Api?.World?.BlockAccessor;
            if (accessor == null) return;

            byte[] oldHsv = ModSystemRiftWardTweaks.Config.LightHSV != null
                ? new byte[] {
                (byte)GameMath.Clamp(ModSystemRiftWardTweaks.Config.LightHSV[0], 0, 64),
                (byte)GameMath.Clamp(ModSystemRiftWardTweaks.Config.LightHSV[1], 0, 8),
                (byte)GameMath.Clamp(ModSystemRiftWardTweaks.Config.LightHSV[2], 3, 21)
                }
                : new byte[] { 0, 0, 0 };

            accessor.RemoveBlockLight(oldHsv, __instance?.Pos);
        }
    }
}