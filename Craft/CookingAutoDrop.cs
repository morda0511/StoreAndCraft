using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Per cooking station / beehive / smelter: when on, finished product goes into a nearby chest
    /// (fallback: ground drop for cook/hive; smelter skips Spawn→floor). Hover inventory closed + N.
    /// Independent from Auto-fill (B).
    /// </summary>
    internal static class CookingAutoDrop
    {
        public const string ZdoKey = "SAC_autoDrop";

        private static readonly MethodInfo HaveDoneItem =
            AccessTools.Method(typeof(CookingStation), "HaveDoneItem");
        private static readonly MethodInfo GetSlot =
            AccessTools.Method(typeof(CookingStation), "GetSlot");
        private static readonly MethodInfo SetSlot =
            AccessTools.Method(typeof(CookingStation), "SetSlot");
        private static readonly MethodInfo IsItemDone =
            AccessTools.Method(typeof(CookingStation), "IsItemDone");
        private static readonly Type CookStatusType =
            AccessTools.Inner(typeof(CookingStation), "Status")
            ?? AccessTools.TypeByName("CookingStation+Status");
        private static readonly object StatusNotDone =
            CookStatusType != null ? Enum.ToObject(CookStatusType, 0) : null;
        private static readonly MethodInfo BeeGetHoneyLevel =
            AccessTools.Method(typeof(Beehive), "GetHoneyLevel");
        private static readonly MethodInfo BeeExtract =
            AccessTools.Method(typeof(Beehive), "Extract");
        private static readonly MethodInfo BeeResetLevel =
            AccessTools.Method(typeof(Beehive), "ResetLevel");
        private static readonly MethodInfo SmelterGetConversion =
            AccessTools.Method(typeof(Smelter), "GetItemConversion", new[] { typeof(string) });

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

        public static bool IsOn(Beehive hive)
        {
            return hive != null && IsOn(hive.GetComponent<ZNetView>());
        }

        public static bool IsOn(Smelter smelter)
        {
            return smelter != null && IsOn(smelter.GetComponent<ZNetView>());
        }

        public static void AppendHover(ref string text, CookingStation station)
        {
            if (station == null)
                return;
            AppendHoverLine(ref text, station.GetComponent<ZNetView>());
        }

        public static void AppendHover(ref string text, Beehive hive)
        {
            if (hive == null)
                return;
            AppendHoverLine(ref text, hive.GetComponent<ZNetView>());
        }

        public static void AppendHover(ref string text, Smelter smelter)
        {
            if (smelter == null)
                return;
            AppendHoverLine(ref text, smelter.GetComponent<ZNetView>());
        }

        private static void AppendHoverLine(ref string text, ZNetView nv)
        {
            if (nv == null || !nv.IsValid())
                return;

            string key = KeyUtil.Format(Plugin.Settings.AutoDropKey.Value);
            if (string.IsNullOrEmpty(key))
                key = "N";

            if (string.IsNullOrEmpty(text))
                text = "";

            text += "\n[<color=yellow><b>" + key + "</b></color>] "
                + Loc.T("Auto-store", "Auto-Lagern")
                + " (" + (IsOn(nv) ? Loc.T("on → chest", "an → Kiste") : Loc.T("off", "aus")) + ")";
        }

        public static bool TryToggle()
        {
            if (InventoryGui.IsVisible() || StationFilterMenu.IsOpen || DisplayTypeMenu.IsOpen || DisplayRangeMenu.IsOpen)
                return false;

            Player player = Player.m_localPlayer;
            if (player == null)
                return false;

            CookingStation station = HoveredStation();
            Beehive hive = station == null ? HoveredBeehive() : null;
            Smelter smelter = station == null && hive == null ? StationPullFilter.HoveredSmelter() : null;
            ZNetView nv = station != null
                ? station.GetComponent<ZNetView>()
                : hive != null
                    ? hive.GetComponent<ZNetView>()
                    : smelter != null ? smelter.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;

            Vector3 pos = station != null
                ? station.transform.position
                : hive != null
                    ? hive.transform.position
                    : smelter.transform.position;
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
                    ? Loc.T("Auto-store on (chests)", "Auto-Lagern an (Kisten)")
                    : Loc.T("Auto-store off", "Auto-Lagern aus"),
                0, null, false);
            return true;
        }

        public static void TryPopDone(CookingStation station)
        {
            if (station == null || !IsOn(station))
                return;

            ZNetView nv = station.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                return;

            if (HaveDoneItem == null || !(bool)HaveDoneItem.Invoke(station, null))
                return;

            StationAutoFill.RequestSoon();

            if (TryDepositDoneToChest(station, nv))
                return;

            Vector3 point = DropPoint(station);
            StationOutput.QueueIntakeLinkTag(point, StationLink.Get(station), null);
            nv.InvokeRPC("RPC_RemoveDoneItem", point, 1);
        }

        private static bool TryDepositDoneToChest(CookingStation station, ZNetView nv)
        {
            if (GetSlot == null || SetSlot == null || IsItemDone == null || StatusNotDone == null)
                return false;
            if (station.m_slots == null)
                return false;

            for (int i = 0; i < station.m_slots.Length; i++)
            {
                object[] args = { i, null, 0f, null, false };
                GetSlot.Invoke(station, args);
                string itemName = args[1] as string;
                if (string.IsNullOrEmpty(itemName))
                    continue;
                if (!(bool)IsItemDone.Invoke(station, new object[] { itemName }))
                    continue;

                string label;
                if (!StationOutput.TryDepositNear(station, itemName, 1, out label))
                    return false;

                SetSlot.Invoke(station, new object[] { i, "", 0f, StatusNotDone, false });
                nv.InvokeRPC(ZNetView.Everybody, "RPC_SetSlotVisual", i, "");
                ActivityLog.ToChest(StationOutput.StationLabel(station), 1, label);
                return true;
            }

            return false;
        }

        public static void TryExtractHoney(Beehive hive)
        {
            if (hive == null || !IsOn(hive))
                return;

            ZNetView nv = hive.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                return;

            if (BeeGetHoneyLevel == null)
                return;

            int honey;
            try
            {
                honey = (int)BeeGetHoneyLevel.Invoke(hive, null);
            }
            catch
            {
                return;
            }

            if (honey <= 0)
                return;

            StationAutoFill.RequestSoon();

            string honeyPrefab = hive.m_honeyItem != null
                ? hive.m_honeyItem.gameObject.name
                : "Honey";
            string label;
            if (StationOutput.TryDepositNear(hive, honeyPrefab, honey, out label))
            {
                if (BeeResetLevel != null)
                    BeeResetLevel.Invoke(hive, null);
                else if (nv.GetZDO() != null)
                    nv.GetZDO().Set(ZDOVars.s_level, 0);
                ActivityLog.ToChest(StationOutput.StationLabel(hive), honey, label);
                return;
            }

            if (BeeExtract != null)
            {
                try
                {
                    BeeExtract.Invoke(hive, null);
                }
                catch
                {
                }
            }

            Vector3 hivePos = hive.transform.position + Vector3.up * 0.5f;
            StationOutput.QueueIntakeLinkTag(hivePos, StationLink.Get(hive), honeyPrefab);
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

        private static Beehive HoveredBeehive()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;
            GameObject hover = player.GetHoverObject();
            if (hover == null)
                return null;
            return hover.GetComponentInParent<Beehive>();
        }

        internal static bool TryGetSmelterOutputPrefab(Smelter smelter, string ore, out string prefabName)
        {
            prefabName = null;
            if (smelter == null || string.IsNullOrEmpty(ore) || SmelterGetConversion == null)
                return false;
            object conv = SmelterGetConversion.Invoke(smelter, new object[] { ore });
            if (conv == null)
                return false;
            FieldInfo toField = AccessTools.Field(conv.GetType(), "m_to");
            if (toField == null)
                return false;
            ItemDrop to = toField.GetValue(conv) as ItemDrop;
            if (to == null)
                return false;
            prefabName = to.gameObject.name;
            return true;
        }
    }

    [HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
    internal static class CookingAutoDropUpdatePatch
    {
        private static void Postfix(CookingStation __instance)
        {
            if (__instance == null || !CookingAutoDrop.IsOn(__instance))
                return;
            CookingAutoDrop.TryPopDone(__instance);
        }
    }

    [HarmonyPatch(typeof(Beehive), "UpdateBees")]
    internal static class BeehiveAutoDropUpdatePatch
    {
        private static void Postfix(Beehive __instance)
        {
            if (__instance == null || !CookingAutoDrop.IsOn(__instance))
                return;
            CookingAutoDrop.TryExtractHoney(__instance);
        }
    }

    [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
    internal static class BeehiveAutoDropHoverPatch
    {
        private static void Postfix(Beehive __instance, ref string __result)
        {
            CookingAutoDrop.AppendHover(ref __result, __instance);
        }
    }

    /// <summary>
    /// When Auto-store (N) is on, kiln/smelter finished bars go to a chest instead of the floor.
    /// Independent from Auto-fill (B). Failed deposit → vanilla Spawn, then tag drop so intake
    /// only uses matching / untagged chests (never a different link).
    /// </summary>
    [HarmonyPatch(typeof(Smelter), "Spawn")]
    internal static class SmelterAutoDropDepositPatch
    {
        private static int _tagLink = -1;
        private static Vector3 _tagPos;
        private static string _tagPrefab;

        private static bool Prefix(Smelter __instance, string ore, int stack)
        {
            _tagLink = -1;
            if (__instance == null || stack <= 0 || string.IsNullOrEmpty(ore))
                return true;
            if (!CookingAutoDrop.IsOn(__instance))
                return true;

            ZNetView nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                return true;

            string prefab;
            if (!CookingAutoDrop.TryGetSmelterOutputPrefab(__instance, ore, out prefab))
                return true;

            string label;
            if (!StationOutput.TryDepositNear(__instance, prefab, stack, out label))
            {
                _tagLink = StationLink.Get(__instance);
                _tagPrefab = prefab;
                Transform output = __instance.m_outputPoint;
                _tagPos = output != null
                    ? output.position
                    : __instance.transform.position + Vector3.up * 0.5f;
                return true;
            }

            if (__instance.m_produceEffects != null)
                __instance.m_produceEffects.Create(__instance.transform.position, __instance.transform.rotation);

            ActivityLog.ToChest(StationOutput.StationLabel(__instance), stack, label);
            return false;
        }

        private static void Postfix(Smelter __instance, string ore, int stack)
        {
            if (_tagLink < 0)
                return;
            StationOutput.QueueIntakeLinkTag(_tagPos, _tagLink, _tagPrefab);
            _tagLink = -1;
        }
    }
}
