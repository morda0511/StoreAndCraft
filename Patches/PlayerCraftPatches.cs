using HarmonyLib;

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

            // Pay chest deficit here. Do not stage mats into the backpack first — that
            // fills free slots and Valheim then fails to add the crafted item.
            StagingPull.ConsumeRequirements(__instance, requirements, qualityLevel, itemQuality, multiplier);
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
