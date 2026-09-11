using UnityEngine;

namespace StoreAndCraft
{
    internal static class TakeStack
    {
        public static void TryFillHovered()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value || !Plugin.Settings.StoreEnabled.Value)
                return;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            if (!InventoryGui.IsVisible())
                return;

            ItemDrop.ItemData item = HoverStore.GetHoveredPlayerItem();
            if (item == null || item.m_shared == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T("Hover a stack first.", "Zuerst über einen Stapel zeigen."), 0, null, false);
                return;
            }

            int room = StackLimits.RoomInStack(item);
            if (room <= 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, Loc.T("Stack is already full.", "Stapel ist schon voll."), 0, null, false);
                return;
            }

            room = StackLimits.FitByWeight(player, item, room);
            if (room <= 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, Loc.T("Too heavy.", "Zu schwer."), 0, null, false);
                return;
            }

            NearbyIndex.Rescan(player.transform.position, NearbyIndex.AccessRange());
            TransferService.FillsQueued = 0;
            int taken = 0;
            foreach (Container chest in ChestPicker.FindHolding(player.transform.position, NearbyIndex.AccessRange(), item.m_shared.m_name))
            {
                if (room <= 0)
                    break;
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;

                int got = TransferService.TakeIntoExistingStack(chest, item, room);
                taken += got;
                room = StackLimits.RoomInStack(item);
                room = StackLimits.FitByWeight(player, item, room);
            }

            Refs.NotifyChanged(player.GetInventory());
            if (taken > 0)
            {
                player.Message(
                    MessageHud.MessageType.TopLeft,
                    Loc.T("Filled stack +" + taken + ".", "Stapel +" + taken + "."),
                    0, null, false);
            }
            else if (TransferService.FillsQueued > 0)
            {
                player.Message(
                    MessageHud.MessageType.TopLeft,
                    Loc.T("Pulling from nearby chests...", "Hole aus nahen Truhen..."),
                    0, null, false);
            }
            else
            {
                player.Message(
                    MessageHud.MessageType.TopLeft,
                    Loc.T("No matching items in range.", "Keine passenden Items in Reichweite."),
                    0, null, false);
            }
        }
    }
}
