using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraftServer
{
    internal static class StoreDrop
    {
        private static readonly Dictionary<int, float> ClaimAt = new Dictionary<int, float>();

        public static bool TryStore(Container chest, ItemDrop drop)
        {
            if (chest == null || drop == null || drop.m_itemData == null)
                return false;

            ZNetView chestView = Refs.View(chest);
            ZNetView dropView = Refs.View(drop);
            if (chestView == null || !chestView.IsValid() || dropView == null || !dropView.IsValid())
                return false;

            if (!EnsureOwner(chestView, chest.GetInstanceID()))
                return false;

            if (!dropView.IsOwner())
            {
                drop.RequestOwn();
                EnsureOwner(dropView, drop.GetInstanceID());
                return false;
            }

            Inventory inv = chest.GetInventory();
            if (inv == null || !inv.CanAddItem(drop.m_itemData, drop.m_itemData.m_stack))
                return false;

            ItemDrop.ItemData clone = drop.m_itemData.Clone();
            if (!inv.AddItem(clone))
                return false;

            if (ZNetScene.instance != null)
                ZNetScene.instance.Destroy(drop.gameObject);
            else
                Object.Destroy(drop.gameObject);

            return true;
        }

        private static bool EnsureOwner(ZNetView view, int id)
        {
            if (view.IsOwner())
                return true;

            float next;
            if (ClaimAt.TryGetValue(id, out next) && Time.time < next)
                return false;

            view.ClaimOwnership();
            ClaimAt[id] = Time.time + 0.35f;
            return view.IsOwner();
        }
    }
}
