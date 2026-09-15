using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Shift + place: pull the hovered piece's build costs from nearby chests into the
    /// backpack without placing the piece.
    /// </summary>
    internal static class BuildGrab
    {
        public static bool TryGrab(Player player, Piece piece)
        {
            if (player == null || piece == null || !StagingPull.Active)
                return false;
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                return false;

            Piece.Requirement[] reqs = piece.m_resources;
            if (reqs == null || reqs.Length == 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    Loc.T("Nothing to grab for this piece", "Nichts zum Holen für dieses Piece"),
                    0, null, false);
                return true;
            }

            Inventory inv = player.GetInventory();
            if (inv == null)
                return true;

            NearbyIndex.Tick();
            bool leaveOne = Plugin.Settings.LeaveOneItem.Value;
            float range = Plugin.Settings.CraftRange.Value;
            Vector3 origin = player.transform.position;
            int pulled = 0;
            int missing = 0;

            foreach (Piece.Requirement req in reqs)
            {
                if (req?.m_resItem?.m_itemData?.m_shared == null)
                    continue;
                int need = req.GetAmount(1);
                if (need <= 0)
                    continue;

                string shared = req.m_resItem.m_itemData.m_shared.m_name;
                int have = inv.CountItems(shared, -1, true);
                int want = need; // one piece worth into the bag
                int deficit = want - have;
                if (deficit <= 0)
                    continue;

                int still = deficit;
                foreach (Container chest in NearbyIndex.Current)
                {
                    if (still <= 0)
                        break;
                    if (chest == null || ChestNames.IsIgnored(chest))
                        continue;
                    if (ContainerFilter.Distance(origin, chest.transform.position) > range)
                        continue;
                    if (!ContainerFilter.PlayerMayUse(chest, origin))
                        continue;

                    int took = TransferService.Withdraw(chest, shared, still, inv, leaveOne);
                    still -= took;
                    pulled += took;
                }

                if (still > 0)
                    missing += still;
            }

            if (pulled <= 0 && missing > 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    Loc.T("No materials in nearby chests", "Keine Materialien in nahen Truhen"),
                    0, null, false);
            }
            else if (missing > 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    Loc.T("Grabbed some materials (still short)", "Material geholt (noch zu wenig)"),
                    0, null, false);
            }
            else if (pulled > 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    Loc.T("Grabbed build materials from chests", "Baumaterial aus Truhen geholt"),
                    0, null, false);
            }
            else
            {
                player.Message(MessageHud.MessageType.Center,
                    Loc.T("You already have the materials", "Material schon im Inventar"),
                    0, null, false);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class PlacePieceGrabPatch
    {
        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (__instance == null || __instance != Player.m_localPlayer)
                return true;
            if (!BuildGrab.TryGrab(__instance, piece))
                return true;

            // Consumed the place attempt as a grab — do not build.
            __result = false;
            return false;
        }
    }
}
