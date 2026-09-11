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

            return ItemFromGrid(gui.m_playerGrid);
        }

        public static void TryStoreHovered()
        {
            if (Plugin.Settings == null || !Plugin.Settings.StoreEnabled.Value)
                return;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            if (!InventoryGui.IsVisible())
                return;

            ItemDrop.ItemData item = GetHoveredPlayerItem();
            if (item == null)
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T("Hover an inventory item first.", "Zuerst über ein Inventar-Item zeigen."),
                    0, null, false);
                return;
            }

            if (InventoryDump.StoreOne(item))
            {
                player.Message(
                    MessageHud.MessageType.TopLeft,
                    Loc.T("Stored hovered item.", "Gehovertes Item eingelagert."),
                    0, null, false);
            }
        }

        private static ItemDrop.ItemData ItemFromGrid(InventoryGrid grid)
        {
            if (grid == null)
                return null;

            InventoryElement element = GetHoveredElement(grid);
            if (element == null)
                return null;

            Inventory inv = grid.GetInventory();
            if (inv == null)
                return null;

            Vector2i pos = GetElementPos(grid, element);
            if (pos.x < 0 || pos.y < 0)
                return null;

            return inv.GetItemAt(pos.x, pos.y);
        }

        private static InventoryElement GetHoveredElement(InventoryGrid grid)
        {
            var method = AccessTools.Method(typeof(InventoryGrid), "GetHoveredElement");
            if (method == null)
                return null;
            return method.Invoke(grid, null) as InventoryElement;
        }

        private static Vector2i GetElementPos(InventoryGrid grid, InventoryElement element)
        {
            var method = AccessTools.Method(typeof(InventoryGrid), "GetElementPos");
            if (method != null)
            {
                object raw = method.Invoke(grid, new object[] { element });
                if (raw is Vector2i fromGrid)
                    return fromGrid;
            }

            return element.Position;
        }
    }
}
