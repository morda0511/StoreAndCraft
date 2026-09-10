using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class SearchPing
    {
        public static void PingItem(ItemDrop.ItemData item)
        {
            if (item == null)
                return;
            Search(ItemIds.SharedName(item), ItemIds.PrefabName(item));
        }

        public static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            string query = args != null && args.Length > 1 ? args[1] : null;
            if (string.IsNullOrEmpty(query))
            {
                ItemDrop.ItemData hovered = HoverStore.GetHoveredPlayerItem();
                if (hovered != null)
                    PingItem(hovered);
                else
                    Tell(Loc.T("Usage: storesearch <item>", "Nutzung: storesearch <item>"));
                return;
            }

            Search(query, query);
        }

        public static void Search(string sharedOrToken, string prefabHint)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            string shared = ItemIds.SharedFromToken(sharedOrToken);
            NearbyIndex.Rescan(player.transform.position, NearbyIndex.ScanRange());
            List<Container> holding = ChestPicker.FindHolding(player.transform.position, NearbyIndex.ScanRange(), shared);

            if (holding.Count == 0 && !string.IsNullOrEmpty(prefabHint))
            {
                string alt = ItemIds.SharedFromToken(prefabHint);
                if (alt != shared)
                    holding = ChestPicker.FindHolding(player.transform.position, NearbyIndex.ScanRange(), alt);
            }

            if (holding.Count == 0)
            {
                Tell(Loc.T("No nearby chest has that item.", "Keine nahe Truhe hat dieses Item."));
                return;
            }

            Container closest = holding[0];
            int total = 0;
            foreach (Container c in holding)
            {
                Inventory inv = c.GetInventory();
                if (inv != null)
                    total += inv.CountItems(shared, -1, true);
            }

            TransferService.Highlight(closest);
            if (Chat.instance != null)
                Chat.instance.SendPing(closest.transform.position);

            string label = prefabHint;
            if (global::Localization.instance != null && !string.IsNullOrEmpty(shared))
                label = global::Localization.instance.Localize(shared);

            Tell(Loc.T(
                label + ": " + total + " in " + holding.Count + " chest(s).",
                label + ": " + total + " in " + holding.Count + " Truhe(n)."));
        }

        private static void Tell(string msg)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
                player.Message(MessageHud.MessageType.Center, msg, 0, null, false);
        }
    }
}
