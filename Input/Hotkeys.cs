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
                || DisplayTypeMenu.IsOpen || StationFilterMenu.IsOpen || DisplayRangeMenu.IsOpen
                || DisplaySmallOptions.IsOpen)
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
            else if (KeyUtil.Down(Plugin.Settings.AutoFillKey.Value))
            {
                StationAutoFill.TryToggle();
            }
            else if (KeyUtil.Down(Plugin.Settings.AutoDropKey.Value))
            {
                CookingAutoDrop.TryToggle();
            }
            else if (KeyUtil.Down(Plugin.Settings.ActivityLogKey.Value))
            {
                ActivityLog.Toggle();
            }
            else if (KeyUtil.Down(Plugin.Settings.DisplayRangeKey.Value))
            {
                // Per-display range: any player with ward access (not admin-gated).
                DisplayRangeMenu.TryOpenHovered();
            }

            if (KeyUtil.Down(Plugin.Settings.RenameKey.Value))
            {
                // Small storage display: Name / Amount toggles.
                if (DisplaySmallOptions.TryOpenHovered())
                { }
                // Same key: kiln/smelter pull-filter when looking at a multi-input station,
                // otherwise chest rename.
                else if (!StationPullFilter.TryOpen(warnIfMissing: false))
                    ChestRename.TryOpen(null, true);
            }
        }
    }
}
