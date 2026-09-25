using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Deposit finished station products into nearby player chests (Auto-store).
    /// Uses NearbyIndex + TransferService — not a ground drop + intake round-trip.
    /// </summary>
    internal static class StationOutput
    {
        /// <summary>
        /// On a ground ItemDrop: restrict AutoIntake to <see cref="StationLink.ChestAllowedForOutput"/>.
        /// Missing / -1 = normal unrestricted intake.
        /// </summary>
        public const string IntakeLinkZdoKey = "SAC_intakeLink";

        private static readonly List<Container> NearScratch = new List<Container>(32);
        private static readonly List<ItemDrop> DropScratch = new List<ItemDrop>(32);

        private static Vector3 _pendingTagPos;
        private static int _pendingTagLink = -1;
        private static string _pendingTagPrefab;
        private static float _pendingTagUntil;

        public static float DepositRange()
        {
            if (Plugin.Settings == null)
                return 10f;
            return Mathf.Max(5f, Plugin.Settings.AutoFillRange.Value);
        }

        /// <summary>
        /// Put <paramref name="amount"/> of prefab/token into a chest near <paramref name="station"/>.
        /// Priority: matching [lN] first; if full / no space, untagged. Never a different link.
        /// MustHaveExisting: only chests that already hold that item (empty never).
        /// No match → false (caller ground-drops + tags intake link). Honors [I].
        /// </summary>
        public static bool TryDepositNear(
            Component station,
            string prefabOrToken,
            int amount,
            out string label)
        {
            label = null;
            if (station == null || amount <= 0 || string.IsNullOrEmpty(prefabOrToken))
                return false;

            GameObject prefab = ItemIds.PrefabFromToken(prefabOrToken);
            if (prefab == null && ObjectDB.instance != null)
                prefab = ObjectDB.instance.GetItemPrefab(prefabOrToken);
            if (prefab == null)
                return false;

            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                return false;

            string shared = drop.m_itemData.m_shared.m_name;
            label = DisplayFilters.ItemLabel(shared);
            if (string.IsNullOrEmpty(label))
                label = prefab.name;

            int linkId = StationLink.Get(station);
            float range = DepositRange();
            NearScratch.Clear();
            NearbyIndex.CollectNear(station.transform.position, range, NearScratch);
            if (NearScratch.Count == 0)
                return false;

            bool mustHave = Plugin.Settings == null || Plugin.Settings.MustHaveExisting.Value;

            // Linked station: matching [lN] first, then untagged fallback.
            if (linkId >= 1)
            {
                if (TryDepositPass(NearScratch, prefabOrToken, shared, amount, exactChestLink: linkId, requireExisting: mustHave))
                    return true;
                return TryDepositPass(NearScratch, prefabOrToken, shared, amount, exactChestLink: 0, requireExisting: mustHave);
            }

            // Unlinked station: untagged only.
            return TryDepositPass(NearScratch, prefabOrToken, shared, amount, exactChestLink: 0, requireExisting: mustHave);
        }

        /// <param name="exactChestLink">0 = untagged only; 1–9 = that [lN] only.</param>
        private static bool TryDepositPass(
            List<Container> chests,
            string prefabOrToken,
            string shared,
            int amount,
            int exactChestLink,
            bool requireExisting)
        {
            exactChestLink = StationLink.Clamp(exactChestLink);
            for (int i = 0; i < chests.Count; i++)
            {
                Container chest = chests[i];
                if (chest == null || ChestNames.IsIgnored(chest))
                    continue;
                if (StationLink.ChestLinkId(chest) != exactChestLink)
                    continue;
                if (!ContainerFilter.IsPlayerBuiltStorage(chest))
                    continue;

                NearbyIndex.EnsureInventory(chest, force: true);
                Inventory inv = chest.GetInventory();
                if (inv == null)
                    continue;

                if (requireExisting && !inv.HaveItem(shared, true))
                    continue;

                if (TransferService.TryDepositCreated(chest, prefabOrToken, amount))
                    return true;
            }
            return false;
        }

        /// <summary>-1 = no intake link restriction.</summary>
        public static int GetIntakeLink(ItemDrop drop)
        {
            if (drop == null)
                return -1;
            ZNetView nv = Refs.View(drop);
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return -1;
            return zdo.GetInt(IntakeLinkZdoKey, -1);
        }

        public static void SetIntakeLink(ItemDrop drop, int linkId)
        {
            if (drop == null)
                return;
            ZNetView nv = Refs.View(drop);
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();
            nv.GetZDO().Set(IntakeLinkZdoKey, StationLink.Clamp(linkId));
        }

        /// <summary>
        /// After Auto-store fails and vanilla leaves a ground drop: keep intake from other links.
        /// </summary>
        public static void QueueIntakeLinkTag(Vector3 pos, int stationLinkId, string prefabNameOrNull)
        {
            _pendingTagPos = pos;
            _pendingTagLink = StationLink.Clamp(stationLinkId);
            _pendingTagPrefab = prefabNameOrNull;
            _pendingTagUntil = Time.time + 2.5f;
            TagNearbyDrops(pos, prefabNameOrNull, _pendingTagLink);
        }

        public static void TickPendingIntakeTags()
        {
            if (_pendingTagLink < 0)
                return;
            if (TagNearbyDrops(_pendingTagPos, _pendingTagPrefab, _pendingTagLink)
                || Time.time >= _pendingTagUntil)
            {
                _pendingTagLink = -1;
                _pendingTagPrefab = null;
            }
        }

        public static bool TagNearbyDrops(Vector3 pos, string prefabNameOrNull, int linkId, float radius = 2.5f)
        {
            List<ItemDrop> live = Refs.Drops();
            if (live == null || live.Count == 0)
                return false;

            DropScratch.Clear();
            DropScratch.AddRange(live);
            float r2 = radius * radius;
            bool any = false;
            for (int i = 0; i < DropScratch.Count; i++)
            {
                ItemDrop drop = DropScratch[i];
                if (drop == null || drop.m_itemData == null)
                    continue;
                if ((drop.transform.position - pos).sqrMagnitude > r2)
                    continue;
                if (!string.IsNullOrEmpty(prefabNameOrNull))
                {
                    string n = drop.gameObject != null ? drop.gameObject.name : null;
                    if (string.IsNullOrEmpty(n))
                        continue;
                    if (!n.StartsWith(prefabNameOrNull))
                        continue;
                }

                if (GetIntakeLink(drop) >= 0)
                {
                    any = true;
                    continue;
                }

                SetIntakeLink(drop, linkId);
                any = true;
            }

            return any;
        }

        public static string StationLabel(Component station)
        {
            if (station == null)
                return "?";
            string name = null;
            Smelter sm = station as Smelter;
            if (sm != null)
                name = sm.m_name;
            else
            {
                CookingStation cook = station as CookingStation;
                if (cook != null)
                    name = cook.m_name;
                else
                {
                    Beehive bee = station as Beehive;
                    if (bee != null)
                        name = bee.m_name;
                    else
                    {
                        Fireplace fire = station as Fireplace;
                        if (fire != null)
                            name = fire.m_name;
                    }
                }
            }

            if (string.IsNullOrEmpty(name))
                return station.gameObject.name;
            return Localization.instance != null
                ? Localization.instance.Localize(name)
                : name;
        }
    }
}
