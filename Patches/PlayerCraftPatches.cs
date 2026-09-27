using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    [HarmonyPatch]
    internal static class PlayerCraftPatches
    {
        /// <summary>
        /// onlyOne recipes (Raw Fish): vanilla GetFirstRequiredItem only scans the bag.
        /// When null, Recipe.GetAmount NREs on m_quality. Supply a quality clone from chests
        /// without withdrawing into the bag (avoids the one-frame fish flash).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "GetFirstRequiredItem")]
        private static void GetFirstRequiredItemPostfix(
            Player __instance,
            Inventory inventory,
            Recipe recipe,
            int qualityLevel,
            ref int amount,
            ref int extraAmount,
            int craftMultiplier,
            ref ItemDrop.ItemData __result)
        {
            if (__result != null || !StagingPull.Active)
                return;
            if (__instance == null || __instance != Player.m_localPlayer)
                return;
            if (recipe == null || !recipe.m_requireOnlyOneIngredient)
                return;

            ItemDrop.ItemData fromChest = StagingPull.FindFirstRequiredInChests(
                __instance, recipe, qualityLevel, craftMultiplier, out int need, out int extra);
            if (fromChest == null)
                return;

            __result = fromChest;
            amount = need;
            extraAmount = extra;
        }

        /// <summary>
        /// onlyOne payment happens inside DoCrafting via Inventory.RemoveItem(string),
        /// not via ConsumeResources. Scope chest pay to that window.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void DoCraftingPrefix()
        {
            if (StagingPull.Active)
                StagingPull.BeginDoCrafting();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void DoCraftingPostfix()
        {
            StagingPull.EndDoCrafting();
        }

        /// <summary>
        /// DoCrafting for onlyOne: RemoveItem(name, amount, quality) on the bag.
        /// Pay any missing amount from chests first so craft is not free.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(Inventory),
            nameof(Inventory.RemoveItem),
            typeof(string),
            typeof(int),
            typeof(int),
            typeof(bool))]
        private static void RemoveItemStringPrefix(
            Inventory __instance,
            string name,
            int amount,
            int itemQuality)
        {
            StagingPull.PayOnlyOneRemoveFromChests(__instance, name, amount, itemQuality);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static void ConsumeResourcesPrefix(
            Player __instance,
            Piece.Requirement[] requirements,
            int qualityLevel,
            int itemQuality,
            int multiplier)
        {
            if (__instance == null || !__instance.IsOwner() || !StagingPull.Active)
                return;

            // C+place grab (hammer): never destroy chest stacks; place is cancelled separately.
            if (BuildGrab.ShouldGrab(__instance))
                return;

            // Pay chest deficit here. Do not stage mats into the backpack first — that
            // fills free slots and Valheim then fails to add the crafted item.
            StagingPull.ConsumeRequirements(__instance, requirements, qualityLevel, itemQuality, multiplier);
        }
    }

    /// <summary>
    /// Workbench, forge, black forge, galdr table, artisan table, … all gate the craft /
    /// upgrade button through HaveRequirementItems. Re-check with chest counts using the
    /// same total as the requirement UI when vanilla left the button disabled.
    /// </summary>
    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    internal static class HaveRequirementItemsPatch
    {
        private static void Postfix(
            Player __instance,
            Recipe piece,
            bool discover,
            int qualityLevel,
            int amount,
            ref bool __result)
        {
            if (__result || discover || !StagingPull.Active)
                return;
            if (__instance == null || __instance != Player.m_localPlayer || piece == null)
                return;

            StationHover.Begin();
            try
            {
                if (RequirementBridge.RecipeHasItems(__instance, piece, qualityLevel, amount))
                    __result = true;
            }
            finally
            {
                StationHover.End();
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    internal static class HavePieceRequirementsPatch
    {
        private static void Prefix()
        {
            StationHover.Begin();
        }

        private static void Postfix()
        {
            StationHover.End();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
    internal static class HaveRecipeRequirementsPatch
    {
        private static void Prefix()
        {
            StationHover.Begin();
        }

        private static void Postfix()
        {
            StationHover.End();
        }
    }
}
