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

            NearbyIndex.Tick();
            // One-shot user action: force-Load every chest in dump range so we do not
            // trust a stale/empty view after returning from a raid / unloaded area.
            float range = Plugin.Settings.PlayerDumpRange.Value;
            foreach (Container nearby in NearbyIndex.Within(player.transform.position, range))
                NearbyIndex.EnsureInventory(nearby, force: true);

            var items = new List<ItemDrop.ItemData>(inv.GetAllItems());
            int stored = 0;
            var leftovers = new List<ItemDrop.ItemData>();

            foreach (ItemDrop.ItemData item in items)
            {
                if (!ShouldDump(item, inv))
                    continue;

                Container chest = ChestPicker.FindStoreTarget(
                    player.transform.position,
                    item,
                    Plugin.Settings.MustHaveExisting.Value);

                if (chest == null)
                {
                    leftovers.Add(item);
                    continue;
                }

                int fit = ChestPicker.AmountThatFits(chest.GetInventory(), item);
                if (fit <= 0)
                {
                    leftovers.Add(item);
                    continue;
                }

                int stackBefore = item.m_stack;
                if (TransferService.StoreItem(chest, inv, item, fit)
                    && (!inv.ContainsItem(item) || item.m_stack < stackBefore))
                    stored++;
                else
                    leftovers.Add(item);
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

            if (Favorites.IsFavorite(item))
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T("Favorite — protected from store. Press F to unfavorite.", "Favorit — vor Einlagern geschützt. F zum Entfernen."),
                    0, null, false);
                return false;
            }

            NearbyIndex.Tick();
            float range = Plugin.Settings.PlayerDumpRange.Value;
            foreach (Container nearby in NearbyIndex.Within(player.transform.position, range))
                NearbyIndex.EnsureInventory(nearby, force: true);

            Container chest = ChestPicker.FindStoreTarget(
                player.transform.position,
                item,
                Plugin.Settings.MustHaveExisting.Value);

            if (chest == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T("No matching chest.", "Keine passende Truhe."), 0, null, false);
                return false;
            }

            int fit = ChestPicker.AmountThatFits(chest.GetInventory(), item);
            if (fit <= 0)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T("No matching chest.", "Keine passende Truhe."), 0, null, false);
                return false;
            }

            return TransferService.StoreItem(chest, inv, item, fit);
        }

        public static bool ShouldDump(ItemDrop.ItemData item, Inventory inv)
        {
            if (item == null || item.m_stack <= 0)
                return false;
            if (item.m_equipped)
                return false;
            if (Favorites.IsFavorite(item))
                return false;
            if (Plugin.Settings.IgnoreHotbar.Value && item.m_gridPos.y == 0)
                return false;
            return true;
        }
    }
}
