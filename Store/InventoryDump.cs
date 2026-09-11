using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class InventoryDump
    {
        public static void DumpNearby()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value || !Plugin.Settings.StoreEnabled.Value)
                return;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return;

            NearbyIndex.Rescan(player.transform.position, NearbyIndex.AccessRange());
            var items = new List<ItemDrop.ItemData>(inv.GetAllItems());
            int stored = 0;

            foreach (ItemDrop.ItemData item in items)
            {
                if (!ShouldDump(item, inv))
                    continue;

                Container chest = ChestPicker.FindStoreTarget(
                    player.transform.position,
                    item,
                    Plugin.Settings.MustHaveExisting.Value);

                if (chest == null)
                    continue;

                if (TransferService.StoreItem(chest, inv, item, item.m_stack))
                    stored++;
            }

            player.Message(
                MessageHud.MessageType.TopLeft,
                stored > 0
                    ? Loc.T("Stored " + stored + " stacks.", stored + " Stapel eingelagert.")
                    : Loc.T("Nothing to store.", "Nichts einzulagern."),
                0, null, false);
        }

        public static bool StoreOne(ItemDrop.ItemData item)
        {
            Player player = Player.m_localPlayer;
            if (player == null || item == null || Plugin.Settings == null)
                return false;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return false;

            NearbyIndex.Rescan(player.transform.position, NearbyIndex.AccessRange());
            Container chest = ChestPicker.FindStoreTarget(
                player.transform.position,
                item,
                Plugin.Settings.MustHaveExisting.Value);

            if (chest == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T("No matching chest.", "Keine passende Truhe."), 0, null, false);
                return false;
            }

            return TransferService.StoreItem(chest, inv, item, item.m_stack);
        }

        public static bool ShouldDump(ItemDrop.ItemData item, Inventory inv)
        {
            if (item == null || item.m_stack <= 0)
                return false;
            if (item.m_equipped)
                return false;
            if (Plugin.Settings.IgnoreHotbar.Value && item.m_gridPos.y == 0)
                return false;
            return true;
        }
    }
}
