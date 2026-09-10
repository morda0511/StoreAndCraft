using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    [HarmonyPatch]
    internal static class PlayerCraftPatches
    {
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

            bool waiting;
            StagingPull.PullRequirements(__instance, requirements, qualityLevel, itemQuality, multiplier, out waiting);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static bool DoCraftingPrefix(InventoryGui __instance, Player player)
        {
            Recipe recipe = Refs.CraftRecipe(__instance);
            if (!StagingPull.Active || player == null || __instance == null || recipe == null)
                return true;

            int amount = Refs.MultiCrafting(__instance) ? Mathf.Max(1, __instance.m_multiCraftAmount) : 1;
            bool waiting;
            StagingPull.EnsureRecipe(player, recipe, amount, out waiting);
            if (waiting)
            {
                StagingPull.ScheduleCraftRetry();
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool TryPlacePiecePrefix(Player __instance, Piece piece, ref bool __result)
        {
            if (!StagingPull.Active || __instance == null || piece == null)
                return true;

            bool waiting;
            StagingPull.EnsurePiece(__instance, piece, out waiting);
            if (waiting)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
