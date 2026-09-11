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

            float cfgDump = NearbyIndex.AccessRange();
            Container best = null;
            float bestDist = float.MaxValue;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null)
                    continue;

                string prefab = ContainerFilter.PiecePrefab(chest);
                float range = RulesFile.DumpRange(prefab, cfgDump);
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

            Inventory inv = chest.GetInventory();
            if (inv == null || !inv.CanAddItem(item, item.m_stack))
                return false;

            string prefab = ContainerFilter.PiecePrefab(chest);
            if (!RulesFile.AllowsStore(prefab, item))
                return false;

            if (mustExist)
            {
                string shared = ItemIds.SharedName(item);
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

                ContainerFilter.RefreshInventory(chest);
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
