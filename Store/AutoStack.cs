using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class AutoStack
    {
        private static float _next;

        public static void Tick()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value || !Plugin.Settings.StoreEnabled.Value)
                return;
            if (!Plugin.Settings.AutoStackEnabled.Value)
                return;

            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting())
                return;

            if (Time.time < _next)
                return;
            _next = Time.time + Mathf.Max(1f, Plugin.Settings.IntakeInterval.Value);

            float range = Mathf.Max(Plugin.Settings.StoreRange.Value, Plugin.Settings.StorageRange.Value);
            NearbyIndex.Tick();
            foreach (Container chest in NearbyIndex.Within(player.transform.position, range))
            {
                if (chest == null)
                    continue;
                ZNetView nv = Refs.View(chest);
                if (nv == null || !nv.IsOwner())
                    continue;
                Inventory chestInv = chest.GetInventory();
                if (chestInv != null)
                    CompactInventory(chestInv, false);
            }
        }

        private static void CompactInventory(Inventory inv, bool skipHotbarAndEquipped)
        {
            if (inv == null)
                return;

            var items = new List<ItemDrop.ItemData>(inv.GetAllItems());
            items.Sort((a, b) => b.m_stack.CompareTo(a.m_stack));

            foreach (ItemDrop.ItemData dest in items)
            {
                if (dest == null || dest.m_shared == null || dest.m_stack <= 0)
                    continue;
                if (StackLimits.MaxStack(dest) <= 1)
                    continue;
                if (skipHotbarAndEquipped && SkipPlayerItem(dest))
                    continue;

                int room = StackLimits.RoomInStack(dest);
                if (room <= 0)
                    continue;

                foreach (ItemDrop.ItemData src in items)
                {
                    if (room <= 0)
                        break;
                    if (src == null || src == dest || src.m_stack <= 0)
                        continue;
                    if (!SameStackType(dest, src))
                        continue;
                    if (skipHotbarAndEquipped && SkipPlayerItem(src))
                        continue;

                    int move = Mathf.Min(room, src.m_stack);
                    if (move <= 0)
                        continue;
                    dest.m_stack += move;
                    src.m_stack -= move;
                    room -= move;
                }
            }

            var leftover = new List<ItemDrop.ItemData>(inv.GetAllItems());
            foreach (ItemDrop.ItemData item in leftover)
            {
                if (item != null && item.m_stack <= 0)
                    inv.RemoveItem(item);
            }

            Refs.NotifyChanged(inv);
        }

        private static bool SkipPlayerItem(ItemDrop.ItemData item)
        {
            if (item == null || item.m_equipped)
                return true;
            if (Plugin.Settings.IgnoreHotbar.Value && item.m_gridPos.y == 0)
                return true;
            return false;
        }

        private static bool SameStackType(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            if (a?.m_shared == null || b?.m_shared == null)
                return false;
            return a.m_shared.m_name == b.m_shared.m_name
                && a.m_quality == b.m_quality
                && a.m_worldLevel == b.m_worldLevel;
        }
    }
}
