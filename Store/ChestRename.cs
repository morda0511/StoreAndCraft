using BepInEx.Configuration;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class ChestRename
    {
        public static bool WantsRename(bool alt)
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return false;

            if (alt)
                return true;

            KeyboardShortcut shortcut = Plugin.Settings.RenameKey.Value;
            if (shortcut.MainKey == KeyCode.None)
                return false;

            if (!KeyUtil.ModifiersHeld(shortcut))
                return false;

            return shortcut.MainKey == KeyCode.E || KeyUtil.Held(shortcut);
        }

        public static bool TryOpen(Container container = null, bool warnIfMissing = true)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return false;

            if (container == null)
                container = ChestNames.Hovered();

            if (container == null || !ChestNames.CanRename(container))
            {
                if (warnIfMissing)
                {
                    player.Message(
                        MessageHud.MessageType.Center,
                        "Look at a chest, then press the rename key.",
                        0, null, false);
                }
                return false;
            }

            if (!PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    "No access to this chest.",
                    0, null, false);
                return false;
            }

            ChestRenameReceiver receiver = container.GetComponent<ChestRenameReceiver>();
            if (receiver == null)
                receiver = container.gameObject.AddComponent<ChestRenameReceiver>();

            if (TextInput.instance == null)
                return false;

            TextInput.instance.RequestText(receiver, "Rename chest", ChestNames.MaxLength);
            ChestRenameLinkBar.Show(container);
            return true;
        }

        public static string PromptLabel()
        {
            if (Plugin.Settings == null)
                return "Alt+E";
            string label = KeyUtil.Format(Plugin.Settings.RenameKey.Value);
            return string.IsNullOrEmpty(label) ? "Shift+E" : label;
        }

        /// <summary>
        /// True while RenameKey modifiers are held (e.g. Alt for Alt+E).
        /// Used to block kiln/smelter [E] add so the filter chord does not also insert wood.
        /// </summary>
        public static bool BlocksStationUse()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return false;

            KeyboardShortcut shortcut = Plugin.Settings.RenameKey.Value;
            if (shortcut.MainKey == KeyCode.None)
                return false;

            bool hasModifier = false;
            foreach (KeyCode unused in shortcut.Modifiers)
            {
                hasModifier = true;
                break;
            }

            // Alone-E as rename key must not suppress every station interact.
            if (!hasModifier)
                return false;

            return KeyUtil.ModifiersHeld(shortcut);
        }
    }
}
