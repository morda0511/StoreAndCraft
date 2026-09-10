using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class CraftUiHintPatch
    {
        private static readonly Color Flash = new Color(1f, 0.92f, 0.2f, 1f);

        private static void Postfix(Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier, bool __result)
        {
            if (!__result || !StagingPull.Active || player == null || req == null || req.m_resItem == null || elementRoot == null)
                return;

            string shared = req.m_resItem.m_itemData != null ? req.m_resItem.m_itemData.m_shared.m_name : null;
            if (string.IsNullOrEmpty(shared))
                return;

            InventoryCountPatches.Skip++;
            int local;
            try
            {
                local = player.GetInventory().CountItems(shared, -1, true);
            }
            finally
            {
                InventoryCountPatches.Skip--;
            }

            int nearby = RequirementBridge.CountNearby(player, shared);

            if (nearby <= 0)
                return;

            TMP_Text[] texts = elementRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text == null)
                    continue;
                text.color = Color.Lerp(text.color, Flash, 0.65f);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class SearchClickPatch
    {
        private static bool Prefix(ItemDrop.ItemData item)
        {
            if (item == null || !Hotkeys.SearchHeld())
                return true;

            SearchPing.PingItem(item);
            return false;
        }
    }
}
