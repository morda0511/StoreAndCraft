using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Player bag helpers. Wider/Deeper Pockets (and other row/width upgrades) expand
    /// Inventory.GetWidth/GetHeight — those slots are normal bag space for dump/sort.
    /// Only out-of-bounds positions (typical equipment/quick-slot mod overflow), equipped
    /// items, and optionally the hotbar stay protected.
    /// </summary>
    internal static class PlayerBag
    {
        public const int VanillaWidth = 8;
        public const int VanillaHeight = 4;

        public static Inventory PlayerInventory()
        {
            Player player = Player.m_localPlayer;
            return player != null ? player.GetInventory() : null;
        }

        public static int BagWidth(Inventory inv)
        {
            if (inv == null)
                return VanillaWidth;
            int w = inv.GetWidth();
            return w > 0 ? w : VanillaWidth;
        }

        public static int BagHeight(Inventory inv)
        {
            if (inv == null)
                return VanillaHeight;
            int h = inv.GetHeight();
            return h > 0 ? h : VanillaHeight;
        }

        /// <summary>
        /// True for slots outside the live bag grid (negative coords, past width/height).
        /// EquipmentAndQuickSlotsPlus-style overflow lives here — not Wider/Deeper Pockets.
        /// </summary>
        public static bool IsOutOfBagSlot(ItemDrop.ItemData item, Inventory inv = null)
        {
            if (item == null)
                return true;
            return IsOutOfBagSlot(item.m_gridPos, inv ?? PlayerInventory());
        }

        public static bool IsOutOfBagSlot(Vector2i pos, Inventory inv)
        {
            if (pos.x < 0 || pos.y < 0)
                return true;
            return pos.x >= BagWidth(inv) || pos.y >= BagHeight(inv);
        }

        public static bool IsDumpProtected(ItemDrop.ItemData item)
        {
            if (item == null || item.m_equipped)
                return true;
            if (IsOutOfBagSlot(item))
                return true;
            return Plugin.Settings != null
                && Plugin.Settings.IgnoreHotbar.Value
                && item.m_gridPos.y == 0;
        }

        public static bool IsSortLocked(
            ItemDrop.ItemData item,
            bool lockFavorites,
            bool lockEquipped,
            bool lockHotbar,
            Inventory inv = null)
        {
            if (item == null)
                return true;
            if (IsOutOfBagSlot(item, inv))
                return true;
            if (lockEquipped && item.m_equipped)
                return true;
            if (lockFavorites && Favorites.IsFavorite(item))
                return true;
            if (lockHotbar && item.m_gridPos.y == 0)
                return true;
            return false;
        }

        public static bool InBag(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null || item.m_stack <= 0)
                return false;
            if (item.m_equipped)
                return false;
            if (IsOutOfBagSlot(item))
                return false;
            if (Plugin.Settings != null && Plugin.Settings.IgnoreHotbar.Value && item.m_gridPos.y == 0)
                return false;
            return true;
        }

        public static int CountInBag(Inventory inv, string shared)
        {
            if (inv == null || string.IsNullOrEmpty(shared))
                return 0;

            int n = 0;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (!InBag(item) || item.m_shared.m_name != shared)
                    continue;
                n += item.m_stack;
            }
            return n;
        }

        public static ItemDrop.ItemData FindInBag(Inventory inv, string shared)
        {
            if (inv == null || string.IsNullOrEmpty(shared))
                return null;

            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (!InBag(item) || item.m_shared.m_name != shared)
                    continue;
                return item;
            }
            return null;
        }

        public static bool RemoveOneFromBag(Inventory inv, string shared)
        {
            ItemDrop.ItemData item = FindInBag(inv, shared);
            if (item == null)
                return false;

            InventoryCountPatches.Skip++;
            try
            {
                inv.RemoveItem(item, 1);
            }
            finally
            {
                InventoryCountPatches.Skip--;
            }
            return true;
        }
    }
}
