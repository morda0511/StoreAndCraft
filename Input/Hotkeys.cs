using UnityEngine;

namespace StoreAndCraft
{
    internal static class Hotkeys
    {
        public static void Tick()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return;
            if (Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()) || TextInput.IsVisible() || DisplayTypeMenu.IsOpen)
                return;

            if (KeyUtil.Down(Plugin.Settings.DumpKey.Value))
                InventoryDump.DumpNearby();

            if (InventoryGui.IsVisible())
            {
                if (KeyUtil.Down(Plugin.Settings.TakeStackKey.Value))
                    TakeStack.TryFillHovered();
                else if (KeyUtil.Down(Plugin.Settings.HoverStoreKey.Value)
                    && !Input.GetKey(KeyCode.LeftControl)
                    && !Input.GetKey(KeyCode.RightControl))
                    HoverStore.TryStoreHovered();
            }

            if (KeyUtil.Down(Plugin.Settings.PauseKey.Value))
                AutoIntake.TogglePause();

            if (KeyUtil.Down(Plugin.Settings.RenameKey.Value))
                ChestRename.TryOpen(null, true);

            if (KeyUtil.Down(Plugin.Settings.PreventPullKey.Value))
            {
                StagingPull.PullingEnabled = !StagingPull.PullingEnabled;
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    string state = StagingPull.PullingEnabled
                        ? Loc.T("on", "an")
                        : Loc.T("off", "aus");
                    player.Message(
                        MessageHud.MessageType.Center,
                        Loc.T("Chest pulling " + state + ".", "Truhen-Ziehen " + state + "."),
                        0, null, false);
                }
            }
        }

        public static bool SearchHeld()
        {
            return Plugin.Settings != null && KeyUtil.Held(Plugin.Settings.SearchKey.Value);
        }
    }
}
