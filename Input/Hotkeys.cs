using UnityEngine;

namespace StoreAndCraft
{
    internal static class Hotkeys
    {
        public static void Tick()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return;
            if (Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()) || TextInput.IsVisible()
                || DisplayTypeMenu.IsOpen || StationFilterMenu.IsOpen)
                return;

            if (KeyUtil.Down(Plugin.Settings.DumpKey.Value))
                InventoryDump.DumpNearby();

            if (InventoryGui.IsVisible())
            {
                if (KeyUtil.Down(Plugin.Settings.FavoriteKey.Value))
                    Favorites.TryToggleHovered();
                else if (KeyUtil.Down(Plugin.Settings.SortKey.Value))
                    InventorySort.TrySort();
                else if (KeyUtil.Down(Plugin.Settings.SearchKey.Value))
                    SearchPing.PingItem(HoverStore.GetHoveredPlayerItem());
                else if (KeyUtil.Down(Plugin.Settings.TakeStackKey.Value))
                    TakeStack.TryFillHovered();
                else if (KeyUtil.Down(Plugin.Settings.HoverStoreKey.Value)
                    && !Input.GetKey(KeyCode.LeftControl)
                    && !Input.GetKey(KeyCode.RightControl))
                    HoverStore.TryStoreHovered();
            }

            if (KeyUtil.Down(Plugin.Settings.RenameKey.Value))
            {
                // Same key: kiln/smelter pull-filter when looking at a multi-input station,
                // otherwise chest rename.
                if (!StationPullFilter.TryOpen(warnIfMissing: false))
                    ChestRename.TryOpen(null, true);
            }
        }
    }
}
