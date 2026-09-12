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
        /// </summary>
        public static bool RecipeHasItems(Player player, Recipe recipe, int qualityLevel, int amount)
        {
            if (player == null || recipe?.m_resources == null)
                return false;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return false;

            // Index is refreshed in Plugin.Update; avoid per-requirement Tick spam.
            CraftingStation station = player.GetCurrentCraftingStation();
            bool onlyOne = recipe.m_requireOnlyOneIngredient;
            int mult = Mathf.Max(1, amount);

            foreach (Piece.Requirement req in recipe.m_resources)
            {
                if (req?.m_resItem?.m_itemData?.m_shared == null)
                    continue;

                if (station != null && station.m_upgrader != req.m_upgraderResource)
                    continue;
                if (station == null && req.m_upgraderResource)
                    continue;

                int need = req.GetAmount(qualityLevel) * mult;
                if (need <= 0)
                    continue;

                string shared = req.m_resItem.m_itemData.m_shared.m_name;
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
