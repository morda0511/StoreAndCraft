using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Per cooking station (spit / stone oven): when on, finished food pops off as a
    /// ground drop so auto-store can pick it up. Hover with inventory closed and press N.
    /// Uses the same RPC path as vanilla [E] pickup.
    /// Off = only a cheap ZDO flag check; never scans chests.
    /// </summary>
    internal static class CookingAutoDrop
    {
        public const string ZdoKey = "SAC_autoDrop";

        private static readonly MethodInfo HaveDoneItem =
            AccessTools.Method(typeof(CookingStation), "HaveDoneItem");

        public static bool IsOn(ZNetView nv)
        {
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;
            return nv.GetZDO().GetInt(ZdoKey, 0) != 0;
        }

        public static bool IsOn(CookingStation station)
        {
            return station != null && IsOn(station.GetComponent<ZNetView>());
        }

        public static void AppendHover(ref string text, CookingStation station)
        {
            if (station == null)
                return;
            ZNetView nv = station.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return;

            string key = KeyUtil.Format(Plugin.Settings.AutoDropKey.Value);
            if (string.IsNullOrEmpty(key))
                key = "N";

            if (string.IsNullOrEmpty(text))
                text = "";

            text += "\n[<color=yellow><b>" + key + "</b></color>] "
                + Loc.T("Auto-drop", "Auto-Drop")
                + " (" + (IsOn(nv) ? Loc.T("on", "an") : Loc.T("off", "aus")) + ")";
        }

        public static bool TryToggle()
        {
            if (InventoryGui.IsVisible() || StationFilterMenu.IsOpen || DisplayTypeMenu.IsOpen || DisplayRangeMenu.IsOpen)
                return false;

            Player player = Player.m_localPlayer;
            if (player == null)
                return false;

            CookingStation station = HoveredStation();
            ZNetView nv = station != null ? station.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;

            Vector3 pos = station.transform.position;
            if (!PrivateArea.CheckAccess(pos, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            if (!nv.IsOwner())
                nv.ClaimOwnership();

            bool next = !IsOn(nv);
            nv.GetZDO().Set(ZdoKey, next ? 1 : 0);

            player.Message(
                MessageHud.MessageType.Center,
                next
                    ? Loc.T("Auto-drop on", "Auto-Drop an")
                    : Loc.T("Auto-drop off", "Auto-Drop aus"),
                0, null, false);
            return true;
        }

        public static void TryPopDone(CookingStation station)
        {
            // Off = stop here. Auto-drop never scans chests (on or off).
            if (station == null || !IsOn(station))
                return;

            ZNetView nv = station.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                return;

            if (HaveDoneItem == null || !(bool)HaveDoneItem.Invoke(station, null))
                return;

            Vector3 point = DropPoint(station);
            // Amount 1: no cooking-skill bonus cheese from automation.
            nv.InvokeRPC("RPC_RemoveDoneItem", point, 1);
        }

        private static Vector3 DropPoint(CookingStation station)
        {
            Transform spawn = station.m_spawnPoint;
            if (spawn != null)
                return spawn.position;
            return station.transform.position + Vector3.up * 0.5f;
        }

        private static CookingStation HoveredStation()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;
            GameObject hover = player.GetHoverObject();
            if (hover == null)
                return null;
            return hover.GetComponentInParent<CookingStation>();
        }
    }

    [HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
    internal static class CookingAutoDropUpdatePatch
    {
        private static void Postfix(CookingStation __instance)
        {
            // Fast path when off: IsOn check only, no HaveDoneItem / RPC.
            if (__instance == null || !CookingAutoDrop.IsOn(__instance))
                return;
            CookingAutoDrop.TryPopDone(__instance);
        }
    }
}
