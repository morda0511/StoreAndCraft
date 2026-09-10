using HarmonyLib;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(Inventory))]
    internal static class InventoryCountPatches
    {
        internal static int Skip;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Inventory.CountItems))]
        private static void CountItemsPostfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
        {
            if (Skip > 0)
                return;
            if (!StagingPull.Active)
                return;

            Player player = Player.m_localPlayer;
            if (player == null || player.GetInventory() != __instance)
                return;
            if (string.IsNullOrEmpty(name))
                return;

            Skip++;
            try
            {
                __result += RequirementBridge.CountNearby(player, name);
            }
            finally
            {
                Skip--;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
        private static void HaveItemPostfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
        {
            if (__result || Skip > 0 || !StagingPull.Active)
                return;

            Player player = Player.m_localPlayer;
            if (player == null || player.GetInventory() != __instance)
                return;

            Skip++;
            try
            {
                __result = RequirementBridge.CountNearby(player, name) > 0;
            }
            finally
            {
                Skip--;
            }
        }
    }
}
