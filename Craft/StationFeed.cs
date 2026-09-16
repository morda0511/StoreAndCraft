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

            if (_pulseActive && _pulseSpendable != null)
            {
                int n;
                return _pulseSpendable.TryGetValue(shared, out n) && n > 0;
            }

            return RequirementBridge.CountNearby(player, shared) > 0;
        }

        /// <summary>
        /// While &gt; 0, chest count / pull uses this range instead of CraftRange (auto-fill).
        /// </summary>
        public static float PullRangeOverride;

        /// <summary>Auto-fill pulse: one chest snapshot, then O(1) ChestsHave.</summary>
        private static Dictionary<string, int> _pulseSpendable;
        private static bool _pulseActive;

        public static void BeginAutoFillPulse(Player player, float range)
        {
            _pulseActive = false;
            _pulseSpendable = null;
            if (player == null || Plugin.Settings == null)
                return;

            NearbyIndex.Tick();
            bool leaveOne = Plugin.Settings.LeaveOneItem.Value;
            _pulseSpendable = NearbyIndex.SnapshotSpendable(
                player.transform.position,
                range,
                leaveOne);
            _pulseActive = true;
        }

        public static void EndAutoFillPulse()
        {
            _pulseActive = false;
            _pulseSpendable = null;
        }

        public static void NotePulseConsumed(string shared, int amount)
        {
            if (!_pulseActive || _pulseSpendable == null || amount <= 0 || string.IsNullOrEmpty(shared))
                return;
            int n;
            if (!_pulseSpendable.TryGetValue(shared, out n))
                return;
            n -= amount;
            if (n <= 0)
                _pulseSpendable.Remove(shared);
            else
                _pulseSpendable[shared] = n;
        }

        public static float ActivePullRange()
        {
            if (PullRangeOverride > 0f)
                return PullRangeOverride;
            return Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : 20f;
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

            // Prefer anything already in the inventory before touching chests
            // (e.g. deer meat on you vs boar meat in a nearby chest).
            foreach (string shared in sharedNames)
            {
                if (string.IsNullOrEmpty(shared))
                    continue;
                if (LocalCount(player, shared) >= amount)
                    return true;
            }

            foreach (string shared in sharedNames)
            {
                if (string.IsNullOrEmpty(shared))
                    continue;
                if (EnsureInInventory(player, shared, amount))
                    return true;
            }

            return false;
        }

        public static int ConsumeFromChests(Player player, string shared, int amount)
        {
            if (player == null || amount <= 0 || string.IsNullOrEmpty(shared))
                return 0;
            if (!Ready())
                return 0;

            NearbyIndex.Tick();
            bool leaveOne = Plugin.Settings != null && Plugin.Settings.LeaveOneItem.Value;
            float range = ActivePullRange();
            Vector3 origin = player.transform.position;
            int need = amount;
            int took = 0;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (need <= 0)
                    break;
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;
                if (ContainerFilter.Distance(origin, chest.transform.position) > range)
                    continue;

                int n = TransferService.Consume(chest, shared, need, leaveOne);
                if (n <= 0)
                    continue;
                took += n;
                need -= n;
                NotePulseConsumed(shared, n);
            }

            return took;
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
            float cfgCraft = ActivePullRange();
            Vector3 origin = player.transform.position;
            int need = amount;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (need <= 0)
                    break;
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;

                if (ContainerFilter.Distance(origin, chest.transform.position) > cfgCraft)
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
            {
                // Filtered allow-list: do not chest-pull denied types.
                // Manual use (item already chosen) still works from inventory only.
                bool allowedPull = fallbackNames == null
                    || fallbackNames.Count == 0
                    || fallbackNames.Contains(want);
                if (allowedPull)
                    EnsureInInventory(player, want, 1);
            }
            else
            {
                EnsureAny(player, fallbackNames, 1);
                // Vanilla FindCookableItem runs after us with item still null — pick an allowed stack now.
                if (fallbackNames != null)
                {
                    foreach (string shared in fallbackNames)
                    {
                        ItemDrop.ItemData local = GetLocal(player, shared);
                        if (local == null)
                            continue;
                        if (local.m_dropPrefab == null)
                            local.m_dropPrefab = ItemIds.PrefabFromToken(ItemIds.PrefabName(local) ?? shared);
                        item = local;
                        return;
                    }
                }
                return;
            }

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
