using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class HoverStore
    {
        public static ItemDrop.ItemData GetHoveredPlayerItem()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || !InventoryGui.IsVisible() || gui.m_playerGrid == null)
                return null;

            ItemDrop.ItemData item = FromGrid(gui.m_playerGrid);
            if (item != null)
                return item;

            return FromGridByMouse(gui.m_playerGrid);
        }

        public static void TryStoreHovered()
        {
            if (Plugin.Settings == null || !Plugin.Settings.StoreEnabled.Value)
                return;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            if (!InventoryGui.IsVisible())
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T("Open your inventory and middle-click an item.", "Inventar öffnen und mit mittlerer Maustaste auf ein Item klicken."),
                    0, null, false);
                return;
            }

            ItemDrop.ItemData item = GetHoveredPlayerItem();
            if (item != null)
            {
                if (InventoryDump.StoreOne(item))
                {
                    player.Message(
                        MessageHud.MessageType.TopLeft,
                        Loc.T("Stored hovered item.", "Gehovertes Item eingelagert."),
                        0, null, false);
                }
                return;
            }

            InventoryDump.DumpNearby();
        }

        private static ItemDrop.ItemData FromGrid(InventoryGrid grid)
        {
            if (grid == null)
                return null;

            var method = AccessTools.Method(typeof(InventoryGrid), "GetHoveredElement");
            if (method == null)
                return null;

            var element = method.Invoke(grid, null) as InventoryElement;
            if (element == null)
                return null;

            return grid.GetItem(element.Position);
        }

        private static ItemDrop.ItemData FromGridByMouse(InventoryGrid grid)
        {
            if (grid == null)
                return null;

            var field = AccessTools.Field(typeof(InventoryGrid), "m_elements");
            if (field == null)
                return null;

            IList elements = field.GetValue(grid) as IList;
            if (elements == null)
                return null;

            Vector3 pointer = Input.mousePosition;
            foreach (object raw in elements)
            {
                InventoryElement element = raw as InventoryElement;
                if (element == null)
                    continue;

                RectTransform rect = element.GetElementRectTransform();
                if (rect == null)
                    continue;

                Vector2 local = rect.InverseTransformPoint(pointer);
                if (!rect.rect.Contains(local))
                    continue;

                ItemDrop.ItemData item = grid.GetItem(element.Position);
                if (item != null)
                    return item;
            }

            return null;
        }
    }
}
