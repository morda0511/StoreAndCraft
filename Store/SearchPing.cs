using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class SearchPing
    {
        private static Container _blinkChest;
        private static int _blinkLeft;
        private static float _nextBlinkAt;

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

        public static void Tick()
        {
            if (_blinkLeft <= 0 || _blinkChest == null)
                return;
            if (Time.time < _nextBlinkAt)
                return;

            TransferService.Flash(_blinkChest);
            _blinkLeft--;
            _nextBlinkAt = Time.time + 0.35f;
            if (_blinkLeft <= 0)
                _blinkChest = null;
        }

        public static void Search(string sharedOrToken, string prefabHint)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            string shared = ItemIds.SharedFromToken(sharedOrToken);
            float range = Plugin.Settings != null ? Plugin.Settings.StorageRange.Value : NearbyIndex.ScanRange();
            NearbyIndex.Tick();
            List<Container> holding = ChestPicker.FindHolding(player.transform.position, range, shared);

            if (holding.Count == 0 && !string.IsNullOrEmpty(prefabHint))
            {
                string alt = ItemIds.SharedFromToken(prefabHint);
                if (alt != shared)
                    holding = ChestPicker.FindHolding(player.transform.position, range, alt);
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

            StartBlink(closest, 3);
            if (Chat.instance != null)
                Chat.instance.SendPing(closest.transform.position);

            string label = prefabHint;
            if (global::Localization.instance != null && !string.IsNullOrEmpty(shared))
                label = global::Localization.instance.Localize(shared);

            Tell(Loc.T(
                label + ": " + total + " in " + holding.Count + " chest(s).",
                label + ": " + total + " in " + holding.Count + " Truhe(n)."));
        }

        private static void StartBlink(Container chest, int times)
        {
            _blinkChest = chest;
            _blinkLeft = Mathf.Max(1, times);
            _nextBlinkAt = 0f;
        }

        private static void Tell(string msg)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
                player.Message(MessageHud.MessageType.Center, msg, 0, null, false);
        }
    }
}
