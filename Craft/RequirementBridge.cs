using UnityEngine;

namespace StoreAndCraft
{
    internal static class RequirementBridge
    {
        /// <summary>
        /// Count craftable mats in nearby chests for UI / HaveRequirements.
        /// Never applies LeaveOneItem here — that only matters when withdrawing.
        /// </summary>
        public static int CountNearby(Player player, string sharedName, int quality = -1)
        {
            if (player == null || Plugin.Settings == null || string.IsNullOrEmpty(sharedName))
                return 0;

            NearbyIndex.Tick();
            return NearbyIndex.CountItem(
                player.transform.position,
                0f,
                sharedName,
                leaveOne: false,
                quality: quality);
        }

        /// <summary>
        /// Same rules as vanilla HaveRequirementItems (forge / workbench / black forge / …),
        /// but counts inventory + chests the way SetupRequirement does (all qualities).
        /// Vanilla takes the max of each quality tier separately, which can leave the upgrade
        /// button grey while the requirement row already looks OK.
        /// </summary>
        public static bool RecipeHasItems(Player player, Recipe recipe, int qualityLevel, int amount)
        {
            if (player == null || recipe?.m_resources == null)
                return false;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return false;

            NearbyIndex.Tick();
            CraftingStation station = player.GetCurrentCraftingStation();
            bool onlyOne = recipe.m_requireOnlyOneIngredient;
            int mult = Mathf.Max(1, amount);

            foreach (Piece.Requirement req in recipe.m_resources)
            {
                if (req?.m_resItem?.m_itemData?.m_shared == null)
                    continue;

                // Mirror Player.HaveRequirementItems station / upgrader filtering.
                if (station != null && station.m_upgrader != req.m_upgraderResource)
                    continue;
                if (station == null && req.m_upgraderResource)
                    continue;

                int need = req.GetAmount(qualityLevel) * mult;
                if (need <= 0)
                    continue;

                string shared = req.m_resItem.m_itemData.m_shared.m_name;
                // quality -1: match InventoryGui.SetupRequirement (button must agree with UI).
                int have = inv.CountItems(shared);
                if (onlyOne)
                {
                    if (have >= need)
                        return true;
                    continue;
                }

                if (have < need)
                    return false;
            }

            return !onlyOne;
        }

        public static bool SenderInRange(long sender, Vector3 target, float range)
        {
            if (ZNet.instance == null)
                return true;

            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return true;

            float slack = 6f;
            return Vector3.Distance(peer.m_refPos, target) <= range + slack;
        }
    }
}
