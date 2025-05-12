using HarmonyLib;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

            // If fuel ran out, turn it off
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
                    int blocked = (int)field.GetValue(__instance);
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

            string runtimePrefix = Lang.Get("Effective runtime:");
            if (fuelDays > 0 && multiplier != 1.0 && !dsc.ToString().Contains(runtimePrefix))
            {
                double effectiveRuntime = fuelDays / multiplier;
                dsc.AppendLine($"{runtimePrefix} {effectiveRuntime:0.#} days");
            }
        }
    }
}