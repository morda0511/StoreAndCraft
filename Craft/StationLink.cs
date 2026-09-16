using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Optional chest↔station channels [link1]…[link9].
    /// Station: pick Link in Alt+E filter menu (ZDO).
    /// Chest: put [linkN] in the rename label (same as [I]/[H]).
    /// Station Link N only pulls chests tagged [linkN].
    /// Station with no link only pulls chests that have no [linkN] tag.
    /// </summary>
    internal static class StationLink
    {
        public const string ZdoKey = "SAC_stationLink";
        public const int MaxId = 9;

        /// <summary>
        /// Pull context: -1 = no link filter (craft / EL / default).
        /// 0 = station with no link (untagged chests only).
        /// 1–9 = only matching [linkN] chests.
        /// </summary>
        public static int ActiveId { get; private set; } = -1;

        public static void Push(int linkId)
        {
            ActiveId = Clamp(linkId);
        }

        /// <summary>Enter station pull context (0–9). Use for autofill / [E].</summary>
        public static void PushStation(Component station)
        {
            ActiveId = Get(station);
        }

        public static void Pop()
        {
            ActiveId = -1;
        }

        public static int Clamp(int id)
        {
            if (id < 0)
                return 0;
            if (id > MaxId)
                return MaxId;
            return id;
        }

        public static int Get(Component station)
        {
            return Get(View(station));
        }

        public static int Get(ZNetView nv)
        {
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return 0;
            return Clamp(zdo.GetInt(ZdoKey, 0));
        }

        public static void Set(Component station, int linkId)
        {
            Set(View(station), linkId);
        }

        public static void Set(ZNetView nv, int linkId)
        {
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();
            nv.GetZDO().Set(ZdoKey, Clamp(linkId));
        }

        /// <summary>
        /// First [link1]…[link9] in the label (case-insensitive). 0 if none.
        /// </summary>
        public static int ParseFromName(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return 0;

            string s = stored;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '[')
                    continue;
                if (i + 6 >= s.Length)
                    break;

                // [linkN]
                if (!StartsWithIgnoreCase(s, i + 1, "link"))
                    continue;
                int digitAt = i + 5;
                if (digitAt >= s.Length)
                    continue;
                char d = s[digitAt];
                if (d < '1' || d > '9')
                    continue;
                if (digitAt + 1 >= s.Length || s[digitAt + 1] != ']')
                    continue;
                // Reject [link10] etc.: digit must be followed by ].
                return d - '0';
            }

            return 0;
        }

        public static int ChestLinkId(Container chest)
        {
            return ParseFromName(ChestNames.Get(chest));
        }

        /// <summary>
        /// Whether this chest may feed the given station link context.
        /// linkId &lt; 0: any non-ignored chest. 0: untagged only. 1–9: matching tag only.
        /// </summary>
        public static bool ChestAllowed(Container chest, int stationLinkId)
        {
            if (chest == null || ChestNames.IsIgnored(chest))
                return false;

            if (stationLinkId < 0)
                return true;

            int chestLink = ChestLinkId(chest);
            stationLinkId = Clamp(stationLinkId);

            if (stationLinkId == 0)
                return chestLink == 0;
            return chestLink == stationLinkId;
        }

        public static bool ChestAllowedForActive(Container chest)
        {
            return ChestAllowed(chest, ActiveId);
        }

        public static string Label(int linkId)
        {
            linkId = Clamp(linkId);
            if (linkId == 0)
                return Loc.T("Link: none", "Link: keiner");
            return Loc.T("Link " + linkId, "Link " + linkId);
        }

        public static string HoverSuffix(int linkId)
        {
            linkId = Clamp(linkId);
            if (linkId <= 0)
                return null;
            return "[link" + linkId + "]";
        }

        private static bool StartsWithIgnoreCase(string s, int index, string token)
        {
            if (index + token.Length > s.Length)
                return false;
            for (int i = 0; i < token.Length; i++)
            {
                char a = s[index + i];
                char b = token[i];
                if (a >= 'A' && a <= 'Z')
                    a = (char)(a + 32);
                if (a != b)
                    return false;
            }
            return true;
        }

        private static ZNetView View(Component c)
        {
            return c != null ? c.GetComponent<ZNetView>() : null;
        }
    }
}
