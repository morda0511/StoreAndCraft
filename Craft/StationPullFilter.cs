using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Per-station (ZDO) deny-list for chest pulls into Smelter ore slots
    /// (charcoal kiln wood types, blast furnace ores, …). Empty = allow all.
    /// Inventory use is never blocked — only auto-pull from chests.
    /// </summary>
    internal static class StationPullFilter
    {
        public const string ZdoKey = "SAC_stationDeny";

        public static Smelter HoveredSmelter()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;
            GameObject hover = player.GetHoverObject();
            if (hover == null)
                return null;
            Smelter smelter = hover.GetComponentInParent<Smelter>();
            return smelter;
        }

        public static bool CanConfigure(Smelter smelter)
        {
            return OreChoices(smelter).Count >= 2;
        }

        public static List<string> OreChoices(Smelter smelter)
        {
            return SmelterAddOrePatch.OreNames(smelter);
        }

        public static List<string> AllowedOreNames(Smelter smelter)
        {
            List<string> all = OreChoices(smelter);
            if (all.Count == 0)
                return all;

            HashSet<string> denied = ReadDenied(smelter);
            if (denied.Count == 0)
                return all;

            var allowed = new List<string>();
            foreach (string shared in all)
            {
                if (!denied.Contains(shared))
                    allowed.Add(shared);
            }
            return allowed;
        }

        public static bool IsDenied(Smelter smelter, string shared)
        {
            if (smelter == null || string.IsNullOrEmpty(shared))
                return false;
            return ReadDenied(smelter).Contains(shared);
        }

        public static bool IsAllowed(Smelter smelter, string shared)
        {
            return !IsDenied(smelter, shared);
        }

        public static void SetDenied(Smelter smelter, string shared, bool denied)
        {
            if (smelter == null || string.IsNullOrEmpty(shared))
                return;

            ZNetView nv = smelter.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();

            HashSet<string> set = ReadDenied(smelter);
            if (denied)
                set.Add(shared);
            else
                set.Remove(shared);

            nv.GetZDO().Set(ZdoKey, Encode(set));
        }

        public static void Clear(Smelter smelter)
        {
            ZNetView nv = smelter != null ? smelter.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();
            nv.GetZDO().Set(ZdoKey, "");
        }

        public static HashSet<string> ReadDenied(Smelter smelter)
        {
            var set = new HashSet<string>();
            ZNetView nv = smelter != null ? smelter.GetComponent<ZNetView>() : null;
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return set;

            string raw = zdo.GetString(ZdoKey, "");
            if (string.IsNullOrEmpty(raw))
                return set;

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (!string.IsNullOrEmpty(p))
                    set.Add(p);
            }
            return set;
        }

        public static string DisplayName(string shared)
        {
            if (string.IsNullOrEmpty(shared))
                return "?";
            return Localization.instance != null
                ? Localization.instance.Localize(shared)
                : shared;
        }

        public static string PromptLabel()
        {
            return ChestRename.PromptLabel();
        }

        public static bool TryOpen(bool warnIfMissing = true)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !StationFeed.Ready())
                return false;

            Smelter smelter = HoveredSmelter();
            if (smelter == null || !CanConfigure(smelter))
            {
                if (warnIfMissing)
                {
                    player.Message(
                        MessageHud.MessageType.Center,
                        Loc.T(
                            "Look at a kiln / smelter with multiple inputs, then press " + PromptLabel() + ".",
                            "Schau einen Ofen / eine Schmelze mit mehreren Inputs an, dann " + PromptLabel() + "."),
                        0, null, false);
                }
                return false;
            }

            if (!PrivateArea.CheckAccess(smelter.transform.position, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            StationFilterMenu.Open(smelter);
            return true;
        }

        private static string Encode(HashSet<string> set)
        {
            if (set == null || set.Count == 0)
                return "";
            var sb = new StringBuilder();
            foreach (string s in set)
            {
                if (string.IsNullOrEmpty(s))
                    continue;
                if (sb.Length > 0)
                    sb.Append('|');
                sb.Append(s);
            }
            return sb.ToString();
        }
    }
}
