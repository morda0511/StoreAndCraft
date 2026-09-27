using UnityEngine;

namespace StoreAndCraft
{
    internal static class StackLimits
    {
        public static int MaxStack(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return 1;
            return Mathf.Max(1, item.m_shared.m_maxStackSize);
        }

        public static int RoomInStack(ItemDrop.ItemData item)
        {
            if (item == null)
                return 0;
            return Mathf.Max(0, MaxStack(item) - item.m_stack);
        }

        public static int FitByWeight(Player player, ItemDrop.ItemData item, int want)
        {
            if (player == null || item?.m_shared == null || want <= 0)
                return 0;

            float unit = item.m_shared.m_weight;
            if (unit <= 0.0001f)
                return want;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return 0;

            float room = player.GetMaxCarryWeight() - inv.GetTotalWeight();
            if (room <= 0f)
                return 0;

            return Mathf.Min(want, Mathf.FloorToInt((room / unit) + 0.0001f));
        }

        public static Sprite Icon(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return null;
            try
            {
                Sprite icon = item.GetIcon();
                if (icon != null)
                    return icon;
            }
            catch
            {
            }

            if (item.m_shared.m_icons != null && item.m_shared.m_icons.Length > 0)
                return item.m_shared.m_icons[0];
            return null;
        }
    }
}
