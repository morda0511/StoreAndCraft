using BepInEx.Configuration;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class KeyUtil
    {
        public static bool Down(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return false;

            if (!MainDown(shortcut.MainKey))
                return false;

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Held(modifier))
                    return false;
            }

            return true;
        }

        public static bool Held(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return false;

            if (!MainHeld(shortcut.MainKey))
                return false;

            return ModifiersHeld(shortcut);
        }

        public static bool ModifiersHeld(KeyboardShortcut shortcut)
        {
            bool any = false;
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                any = true;
                if (!Held(modifier))
                    return false;
            }

            return any;
        }

        public static string Format(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return string.Empty;

            var parts = new System.Collections.Generic.List<string>();
            foreach (KeyCode modifier in shortcut.Modifiers)
                parts.Add(Nice(modifier));
            parts.Add(Nice(shortcut.MainKey));
            return string.Join("+", parts.ToArray());
        }

        private static string Nice(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                default:
                    return key.ToString();
            }
        }

        private static bool MainDown(KeyCode key)
        {
            int mouse = MouseIndex(key);
            if (mouse >= 0)
                return Input.GetMouseButtonDown(mouse);

            return Input.GetKeyDown(key);
        }

        private static bool MainHeld(KeyCode key)
        {
            int mouse = MouseIndex(key);
            if (mouse >= 0)
                return Input.GetMouseButton(mouse);

            return Input.GetKey(key);
        }

        private static bool Held(KeyCode key)
        {
            int mouse = MouseIndex(key);
            if (mouse >= 0)
                return Input.GetMouseButton(mouse);

            return Input.GetKey(key);
        }

        private static int MouseIndex(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return 0;
                case KeyCode.Mouse1: return 1;
                case KeyCode.Mouse2: return 2;
                case KeyCode.Mouse3: return 3;
                case KeyCode.Mouse4: return 4;
                case KeyCode.Mouse5: return 5;
                case KeyCode.Mouse6: return 6;
                default: return -1;
            }
        }
    }
}
