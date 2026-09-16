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
