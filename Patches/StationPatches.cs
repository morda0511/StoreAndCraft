using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class StationHover
    {
        public static void Begin()
        {
            if (!StagingPull.Active)
                return;
            // Do not Tick() here — SetupRequirement / HaveRequirements call Begin many
            // times per frame; Plugin.Update already refreshes the index.
            InventoryCountPatches.IncludeChests++;
        }

        public static void End()
        {
            if (InventoryCountPatches.IncludeChests > 0)
                InventoryCountPatches.IncludeChests--;
        }

        /// <summary>
        /// Safety: never leave IncludeChests stuck across frames (FPS death with inventory open).
        /// </summary>
        public static void ResetFrame()
        {
            InventoryCountPatches.IncludeChests = 0;
        }

        public static Switch HoveredSwitch()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;
            GameObject hover = player.GetHoverObject();
            if (hover == null)
                return null;
            Switch sw = hover.GetComponent<Switch>();
            return sw != null ? sw : hover.GetComponentInParent<Switch>();
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class SmelterAddFuelPatch
    {
        private static void Prefix(Smelter __instance, Humanoid user)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;
            StationFeed.EnsureInInventory(player, StationFeed.SharedFrom(__instance.m_fuelItem), 1);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class SmelterAddOrePatch
    {
        private static void Prefix(Smelter __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;
            StationFeed.EnsureForUse(player, ref item, StationPullFilter.AllowedOreNames(__instance));
        }

        internal static List<string> OreNames(Smelter smelter)
        {
            var names = new List<string>();
            if (smelter?.m_conversion == null)
                return names;
            foreach (Smelter.ItemConversion conv in smelter.m_conversion)
            {
                string shared = StationFeed.SharedFrom(conv != null ? conv.m_from : null);
                if (!string.IsNullOrEmpty(shared) && !names.Contains(shared))
                    names.Add(shared);
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(Smelter), nameof(Smelter.CanUseItems))]
    internal static class SmelterCanUseItemsPatch
    {
        private static void Prefix()
        {
            StationHover.Begin();
        }

        private static void Postfix(Smelter __instance, Player player, ref bool __result)
        {
            try
            {
                if (__result || !StationFeed.Ready() || player == null || __instance == null)
                    return;

                Switch hovered = StationHover.HoveredSwitch();
                if (hovered != null && hovered == __instance.m_emptyOreSwitch)
                    return;
                if (hovered != null && hovered == __instance.m_addOreSwitch)
                {
                    if (StationFeed.HasOrChestsAny(player, StationPullFilter.AllowedOreNames(__instance)))
                        __result = true;
                    return;
                }
                if (hovered != null && hovered == __instance.m_addWoodSwitch)
                {
                    if (StationFeed.HasOrChests(player, StationFeed.SharedFrom(__instance.m_fuelItem)))
                        __result = true;
                    return;
                }

                if (StationFeed.HasOrChests(player, StationFeed.SharedFrom(__instance.m_fuelItem))
                    || StationFeed.HasOrChestsAny(player, StationPullFilter.AllowedOreNames(__instance)))
                    __result = true;
            }
            finally
            {
                StationHover.End();
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
    internal static class SmelterHoverAddOrePatch
    {
        private static void Postfix(Smelter __instance, ref string __result)
        {
            if (__instance == null || !StationPullFilter.CanConfigure(__instance) || !StationFeed.Ready())
                return;
            string key = StationPullFilter.PromptLabel();
            __result += "\n[<color=yellow><b>" + key + "</b></color>] "
                + Loc.T("Chest pull filter", "Truhen-Zug Filter");
        }
    }

    /// <summary>
    /// Vanilla picks the first cookable item in conversion order (Wood before Core wood).
    /// Respect the per-station allow/deny filter so OFF types are never auto-selected.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), "FindCookableItem")]
    internal static class SmelterFindCookablePatch
    {
        private static void Postfix(Smelter __instance, Inventory inventory, ref ItemDrop.ItemData __result)
        {
            if (__instance == null || inventory == null || !StationFeed.Ready())
                return;
            if (!StationPullFilter.CanConfigure(__instance))
                return;

            HashSet<string> denied = StationPullFilter.ReadDenied(__instance);
            if (denied.Count == 0)
                return;

            if (__result?.m_shared != null && !denied.Contains(__result.m_shared.m_name))
                return;

            __result = null;
            if (__instance.m_conversion == null)
                return;

            foreach (Smelter.ItemConversion conv in __instance.m_conversion)
            {
                string shared = StationFeed.SharedFrom(conv != null ? conv.m_from : null);
                if (string.IsNullOrEmpty(shared) || denied.Contains(shared))
                    continue;

                ItemDrop.ItemData found = inventory.GetItem(shared, -1, false);
                if (found != null)
                {
                    __result = found;
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    internal static class FireplaceInteractPatch
    {
        private static readonly MethodInfo GetFuel = AccessTools.Method(typeof(Fireplace), "GetFuel");

        private static void Prefix(Fireplace __instance, Humanoid user, bool hold, bool alt)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null || !__instance.m_canRefill)
                return;
            if (__instance.m_canTurnOff && !hold && !alt && FireplaceFuel(__instance) > 0f)
                return;
            StationFeed.EnsureInInventory(player, StationFeed.SharedFrom(__instance.m_fuelItem), 1);
        }

        private static float FireplaceFuel(Fireplace fireplace)
        {
            if (fireplace == null || GetFuel == null)
                return 0f;
            try
            {
                object value = GetFuel.Invoke(fireplace, null);
                if (value is float)
                    return (float)value;
                return 0f;
            }
            catch
            {
                return 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.UseItem))]
    internal static class FireplaceUseItemPatch
    {
        private static void Prefix(Fireplace __instance, Humanoid user)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null || !__instance.m_canRefill)
                return;
            StationFeed.EnsureInInventory(player, StationFeed.SharedFrom(__instance.m_fuelItem), 1);
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.CanUseItems))]
    internal static class FireplaceCanUseItemsPatch
    {
        private static void Prefix()
        {
            StationHover.Begin();
        }

        private static void Postfix(Fireplace __instance, Player player, ref bool __result)
        {
            try
            {
                if (__result || !StationFeed.Ready() || player == null || __instance == null)
                    return;
                if (StationFeed.HasOrChests(player, StationFeed.SharedFrom(__instance.m_fuelItem)))
                    __result = true;
            }
            finally
            {
                StationHover.End();
            }
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
    internal static class CookingAddFuelPatch
    {
        private static void Prefix(CookingStation __instance, Humanoid user)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;
            StationFeed.EnsureInInventory(player, StationFeed.SharedFrom(__instance.m_fuelItem), 1);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnUseItem")]
    internal static class CookingOnUseItemPatch
    {
        private static void Prefix(CookingStation __instance, Humanoid user, ref ItemDrop.ItemData item)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;
            StationFeed.EnsureForUse(player, ref item, CookingOnInteractPatch.FoodNames(__instance));
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnInteract")]
    internal static class CookingOnInteractPatch
    {
        private static readonly System.Reflection.MethodInfo HaveDoneItem =
            AccessTools.Method(typeof(CookingStation), "HaveDoneItem");

        private static void Prefix(CookingStation __instance, Humanoid user)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;

            // OnInteract first collects finished food. Do not pull raw meat from chests
            // in that case (was giving cooked + raw boar on the first E).
            if (HaveDoneItem != null && (bool)HaveDoneItem.Invoke(__instance, null))
                return;

            StationFeed.EnsureAny(player, FoodNames(__instance), 1);
        }

        internal static List<string> FoodNames(CookingStation station)
        {
            var names = new List<string>();
            if (station?.m_conversion == null)
                return names;
            foreach (CookingStation.ItemConversion conv in station.m_conversion)
            {
                string shared = StationFeed.SharedFrom(conv != null ? conv.m_from : null);
                if (!string.IsNullOrEmpty(shared) && !names.Contains(shared))
                    names.Add(shared);
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.CanUseItems))]
    internal static class CookingCanUseItemsPatch
    {
        private static void Prefix()
        {
            StationHover.Begin();
        }

        private static void Postfix(CookingStation __instance, Player player, ref bool __result)
        {
            try
            {
                if (__result || !StationFeed.Ready() || player == null || __instance == null)
                    return;
                Switch hovered = StationHover.HoveredSwitch();
                if (hovered != null && hovered == __instance.m_addFuelSwitch)
                {
                    if (StationFeed.HasOrChests(player, StationFeed.SharedFrom(__instance.m_fuelItem)))
                        __result = true;
                    return;
                }
                if (StationFeed.HasOrChestsAny(player, CookingOnInteractPatch.FoodNames(__instance))
                    || (hovered == null && StationFeed.HasOrChests(player, StationFeed.SharedFrom(__instance.m_fuelItem))))
                    __result = true;
            }
            finally
            {
                StationHover.End();
            }
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
    internal static class FermenterInteractPatch
    {
        private static void Prefix(Fermenter __instance, Humanoid user)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;
            StationFeed.EnsureAny(player, MeadNames(__instance), 1);
        }

        internal static List<string> MeadNames(Fermenter fermenter)
        {
            var names = new List<string>();
            if (fermenter?.m_conversion == null)
                return names;
            foreach (Fermenter.ItemConversion conv in fermenter.m_conversion)
            {
                string shared = StationFeed.SharedFrom(conv != null ? conv.m_from : null);
                if (!string.IsNullOrEmpty(shared) && !names.Contains(shared))
                    names.Add(shared);
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
    internal static class TurretUseItemPatch
    {
        private static void Prefix(Turret __instance, Humanoid user)
        {
            Player player = StationFeed.LocalPlayer(user);
            if (player == null || __instance == null)
                return;
            StationFeed.EnsureAny(player, AmmoNames(__instance), 1);
        }

        internal static List<string> AmmoNames(Turret turret)
        {
            var names = new List<string>();
            if (turret?.m_allowedAmmo == null)
                return names;
            foreach (Turret.AmmoType ammo in turret.m_allowedAmmo)
            {
                string shared = StationFeed.SharedFrom(ammo.m_ammo);
                if (!string.IsNullOrEmpty(shared) && !names.Contains(shared))
                    names.Add(shared);
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.CanUseItems))]
    internal static class TurretCanUseItemsPatch
    {
        private static void Postfix(Turret __instance, Player player, ref bool __result)
        {
            if (__result || !StationFeed.Ready() || player == null || __instance == null)
                return;
            if (StationFeed.HasOrChestsAny(player, TurretUseItemPatch.AmmoNames(__instance)))
                __result = true;
        }
    }
}
