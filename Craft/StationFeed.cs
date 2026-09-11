using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class StationFeed
    {
        public static bool Ready()
        {
            return StagingPull.Active;
        }

        public static Player LocalPlayer(Humanoid user)
        {
            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer)
                return null;
            return player;
        }

        public static string SharedFrom(ItemDrop drop)
        {
            return drop?.m_itemData?.m_shared != null ? drop.m_itemData.m_shared.m_name : null;
        }

        public static int LocalCount(Player player, string shared)
        {
            if (player == null || string.IsNullOrEmpty(shared))
                return 0;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return 0;

            InventoryCountPatches.Skip++;
            try
            {
                return inv.CountItems(shared, -1, true);
            }
            finally
            {
                InventoryCountPatches.Skip--;
            }
        }

        public static bool ChestsHave(Player player, string shared)
        {
            if (player == null || string.IsNullOrEmpty(shared))
                return false;

            NearbyIndex.Tick();
            return RequirementBridge.CountNearby(player, shared) > 0;
        }

        public static bool HasOrChests(Player player, string shared)
        {
            return LocalCount(player, shared) > 0 || ChestsHave(player, shared);
        }

        public static bool HasOrChestsAny(Player player, List<string> sharedNames)
        {
            if (sharedNames == null)
                return false;
            foreach (string shared in sharedNames)
            {
                if (HasOrChests(player, shared))
                    return true;
            }
            return false;
        }

        public static List<string> NamesFromDrops(IEnumerable<ItemDrop> drops)
        {
            var names = new List<string>();
            if (drops == null)
                return names;
            foreach (ItemDrop drop in drops)
            {
                string shared = SharedFrom(drop);
                if (!string.IsNullOrEmpty(shared) && !names.Contains(shared))
                    names.Add(shared);
            }
            return names;
        }

        public static bool EnsureInInventory(Player player, string shared, int amount)
        {
            if (player == null || amount <= 0 || string.IsNullOrEmpty(shared))
                return false;

            int have = LocalCount(player, shared);
            if (have >= amount)
                return true;
            if (!Ready())
                return false;

            PullIntoInventory(player, shared, amount - have);
            return LocalCount(player, shared) >= amount;
        }

        public static bool EnsureAny(Player player, List<string> sharedNames, int amount)
        {
            if (player == null || sharedNames == null || amount <= 0)
                return false;

            foreach (string shared in sharedNames)
            {
                if (string.IsNullOrEmpty(shared))
                    continue;
                if (EnsureInInventory(player, shared, amount))
                    return true;
            }

            return false;
        }

        public static void PullIntoInventory(Player player, string shared, int amount)
        {
            if (player == null || amount <= 0 || string.IsNullOrEmpty(shared))
                return;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return;

            NearbyIndex.Tick();
            bool leaveOne = Plugin.Settings != null && Plugin.Settings.LeaveOneItem.Value;
            float cfgCraft = Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : 20f;
            Vector3 origin = player.transform.position;
            int need = amount;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (need <= 0)
                    break;
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;

                string pieceName = ContainerFilter.PiecePrefab(chest);
                if (!RulesFile.AllowsCraft(pieceName, shared))
                    continue;

                float chestRange = RulesFile.CraftRange(pieceName, cfgCraft);
                if (ContainerFilter.Distance(origin, chest.transform.position) > chestRange)
                    continue;

                int took = TransferService.Withdraw(chest, shared, need, inv, leaveOne);
                need -= took;
            }
        }

        public static void EnsureForUse(Player player, ref ItemDrop.ItemData item, List<string> fallbackNames)
        {
            if (player == null)
                return;

            ItemDrop.ItemData requested = item;
            string want = requested != null && requested.m_shared != null ? requested.m_shared.m_name : null;

            if (!string.IsNullOrEmpty(want))
                EnsureInInventory(player, want, 1);
            else
                EnsureAny(player, fallbackNames, 1);

            if (requested == null)
                return;

            ItemDrop.ItemData found = GetLocal(player, want);
            if (found == null)
            {
                item = null;
                return;
            }

            if (found.m_dropPrefab == null)
            {
                GameObject prefab = ItemIds.PrefabFromToken(want);
                if (prefab == null && requested.m_dropPrefab != null)
                    prefab = requested.m_dropPrefab;
                found.m_dropPrefab = prefab;
            }
            item = found;
        }

        public static ItemDrop.ItemData SampleNearby(Player player, string shared)
        {
            if (player == null || string.IsNullOrEmpty(shared))
                return null;

            NearbyIndex.Tick();
            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;
                Inventory inv = chest.GetInventory();
                if (inv == null)
                    continue;
                foreach (ItemDrop.ItemData item in inv.GetAllItems())
                {
                    if (item?.m_shared == null || item.m_stack <= 0)
                        continue;
                    if (item.m_shared.m_name != shared)
                        continue;
                    return item.Clone();
                }
            }

            GameObject prefab = ItemIds.PrefabFromToken(shared);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop?.m_itemData != null ? drop.m_itemData.Clone() : null;
        }

        private static ItemDrop.ItemData GetLocal(Player player, string shared)
        {
            Inventory inv = player != null ? player.GetInventory() : null;
            if (inv == null || string.IsNullOrEmpty(shared))
                return null;
            return inv.GetItem(shared, -1, false);
        }
    }
}
