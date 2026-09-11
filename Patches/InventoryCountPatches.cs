using HarmonyLib;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(Inventory))]
    internal static class InventoryCountPatches
    {
        internal static int Skip;
        internal static int IncludeChests;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Inventory.CountItems))]
        private static void CountItemsPostfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
        {
            if (Skip > 0 || IncludeChests <= 0)
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
            if (__result || Skip > 0 || IncludeChests <= 0 || !StagingPull.Active)
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

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Inventory.CountItemsByName))]
        private static void CountItemsByNamePostfix(
            Inventory __instance,
            string[] names,
            bool stacksOnly,
            ref int __result)
        {
            if (Skip > 0 || IncludeChests <= 0 || !StagingPull.Active)
                return;

            Player player = Player.m_localPlayer;
            if (player == null || player.GetInventory() != __instance || names == null)
                return;

            Skip++;
            try
            {
                foreach (string name in names)
                {
                    if (string.IsNullOrEmpty(name))
                        continue;
                    int n = RequirementBridge.CountNearby(player, name);
                    if (n <= 0)
                        continue;
                    __result += stacksOnly ? 1 : n;
                }
            }
            finally
            {
                Skip--;
            }
        }
    }
}
