using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class ChestPicker
    {
        public static Container FindStoreTarget(Vector3 origin, ItemDrop.ItemData item, bool mustExist)
        {
            if (item == null)
                return null;

            float range = Plugin.Settings != null
                ? Plugin.Settings.PlayerDumpRange.Value
                : 8f;
            Container best = null;
            float bestDist = float.MaxValue;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null)
                    continue;

                float d = ContainerFilter.Distance(origin, chest.transform.position);
                if (d > range)
                    continue;

                if (!CanAccept(chest, item, origin, mustExist))
                    continue;

                if (d < bestDist)
                {
                    bestDist = d;
                    best = chest;
                }
            }

            return best;
        }

        public static bool CanAccept(Container chest, ItemDrop.ItemData item, Vector3 from, bool mustExist)
        {
            if (chest == null || item == null)
                return false;

            // Never dump / auto-store into crypt / house-spawn / other world chests.
            if (!ContainerFilter.IsPlayerBuiltStorage(chest))
                return false;

            Inventory inv = chest.GetInventory();
            if (inv == null || !inv.CanAddItem(item, item.m_stack))
                return false;

            if (mustExist)
            {
                string shared = ItemIds.SharedName(item);
                NearbyIndex.EnsureInventory(chest);
                if (string.IsNullOrEmpty(shared) || inv.CountItems(shared, -1, true) <= 0)
                    return false;
            }

            return true;
        }

        public static List<Container> FindHolding(Vector3 origin, float range, string sharedName)
        {
            var list = new List<Container>();
            if (string.IsNullOrEmpty(sharedName))
                return list;

            foreach (Container chest in NearbyIndex.Within(origin, range))
            {
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;

                NearbyIndex.EnsureInventory(chest);
                Inventory inv = chest.GetInventory();
                if (inv == null)
                    continue;
                if (CountShared(inv, sharedName) > 0)
                    list.Add(chest);
            }

            list.Sort((a, b) =>
                ContainerFilter.Distance(origin, a.transform.position)
                    .CompareTo(ContainerFilter.Distance(origin, b.transform.position)));
            return list;
        }

        public static int CountShared(Inventory inv, string sharedName)
        {
            if (inv == null || string.IsNullOrEmpty(sharedName))
                return 0;

            int n = 0;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null || item.m_stack <= 0)
                    continue;
                if (item.m_shared.m_name == sharedName)
                    n += item.m_stack;
            }
            return n;
        }
    }
}
