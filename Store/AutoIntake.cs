using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class AutoIntake
    {
        private static float _next;
        private static float _nextErrorLog;
        private static readonly List<ItemDrop> DropScratch = new List<ItemDrop>(64);
        private static readonly List<Player> PlayerScratch = new List<Player>(8);
        private static readonly List<Container> ChestScratch = new List<Container>(32);
        private static readonly List<Container> ProbeOrder = new List<Container>(32);

        public static void Tick()
        {
            Run();
        }

        public static void TickDedicated()
        {
            Run();
        }

        private static bool Ready()
        {
            return Plugin.Settings != null
                && Plugin.Settings.ModEnabled.Value
                && Plugin.Settings.StoreEnabled.Value
                && ConfigCommands.IsAutoIntakeActive();
        }

        private static void Run()
        {
            if (!Ready())
                return;
            if (Time.time < _next)
                return;

            _next = Time.time + Mathf.Max(1f, Plugin.Settings.IntakeInterval.Value);
            StationOutput.TickPendingIntakeTags();
            if (!SnapshotDrops())
                return;

            PlayerScratch.Clear();
            List<Player> live = Player.GetAllPlayers();
            if (live != null && live.Count > 0)
                PlayerScratch.AddRange(live);
            if (PlayerScratch.Count == 0)
                return;

            float storeRange = Plugin.Settings.StoreRange.Value;
            int cap = Plugin.Settings.MaxTransfersPerTick.Value;
            int moved = 0;
            NearbyIndex.BeginStorePass();

            try
            {
                for (int i = 0; i < DropScratch.Count; i++)
                {
                    if (moved >= cap)
                        break;

                    ItemDrop drop = DropScratch[i];
                    if (drop == null || drop.m_itemData == null)
                        continue;
                    if (FeedTroughBait.IsBait(drop))
                        continue;
                    ZNetView nv = Refs.View(drop);
                    if (nv == null || !nv.IsValid() || drop.IsPiece())
                        continue;

                    // Whoever owns the drop stores it. One player in StoreRange of
                    // the pile is enough (you can walk off, a buddy can stay).
                    if (!nv.IsOwner())
                        continue;
                    if (!AnyPlayerInRange(drop.transform.position, storeRange))
                        continue;

                    Container chest = BestChest(drop, storeRange);
                    if (chest == null)
                        continue;
                    if (TransferService.StoreDrop(chest, drop))
                        moved++;
                }
            }
            catch (System.Exception ex)
            {
                if (Time.unscaledTime >= _nextErrorLog)
                {
                    _nextErrorLog = Time.unscaledTime + 30f;
                    if (Plugin.Log != null)
                        Plugin.Log.LogWarning("AutoIntake: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private static bool SnapshotDrops()
        {
            DropScratch.Clear();
            List<ItemDrop> live = Refs.Drops();
            if (live == null || live.Count == 0)
                return false;

            DropScratch.AddRange(live);
            return DropScratch.Count > 0;
        }

        private static bool AnyPlayerInRange(Vector3 pos, float range)
        {
            for (int i = 0; i < PlayerScratch.Count; i++)
            {
                Player player = PlayerScratch[i];
                if (player == null || player.IsDead() || player.IsTeleporting())
                    continue;
                if (ContainerFilter.Distance(pos, player.transform.position) <= range)
                    return true;
            }
            return false;
        }

        private static Container BestChest(ItemDrop drop, float storeRange)
        {
            NearbyIndex.CollectNear(drop.transform.position, storeRange, ChestScratch);
            bool mustExist = Plugin.Settings.MustHaveExisting.Value;
            Vector3 pos = drop.transform.position;
            int intakeLink = StationOutput.GetIntakeLink(drop);

            // Station-tagged drops: matching [lN] first, then untagged (same as Auto-store deposit).
            if (intakeLink >= 1)
            {
                Container linked = PickAccepting(ChestScratch, drop.m_itemData, pos, mustExist, exactChestLink: intakeLink);
                if (linked != null)
                    return linked;
                Container untagged = PickAccepting(ChestScratch, drop.m_itemData, pos, mustExist, exactChestLink: 0);
                if (untagged != null || !mustExist)
                    return untagged;
                return ProbePreferLink(drop, pos, mustExist, intakeLink);
            }

            if (intakeLink == 0)
            {
                Container untagged = PickAccepting(ChestScratch, drop.m_itemData, pos, mustExist, exactChestLink: 0);
                if (untagged != null || !mustExist)
                    return untagged;
                return ProbePreferLink(drop, pos, mustExist, exactFirst: 0, exactSecond: -1);
            }

            Container best = PickAccepting(ChestScratch, drop.m_itemData, pos, mustExist, exactChestLink: -1);
            if (best != null || !mustExist)
                return best;

            return ProbePreferLink(drop, pos, mustExist, exactFirst: -1, exactSecond: -1);
        }

        /// <param name="exactChestLink">-1 = any allowed; 0 = untagged; 1–9 = that link only.</param>
        private static Container PickAccepting(
            List<Container> chests,
            ItemDrop.ItemData item,
            Vector3 pos,
            bool mustExist,
            int exactChestLink)
        {
            Container best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < chests.Count; i++)
            {
                Container candidate = chests[i];
                if (candidate == null)
                    continue;
                if (exactChestLink >= 0 && StationLink.ChestLinkId(candidate) != exactChestLink)
                    continue;
                if (!ChestPicker.CanAccept(candidate, item, pos, mustExist))
                    continue;
                float dist = ContainerFilter.Distance(pos, candidate.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            return best;
        }

        private static Container ProbePreferLink(ItemDrop drop, Vector3 pos, bool mustExist, int intakeLink)
        {
            return ProbePreferLink(drop, pos, mustExist, exactFirst: intakeLink, exactSecond: 0);
        }

        private static Container ProbePreferLink(
            ItemDrop drop,
            Vector3 pos,
            bool mustExist,
            int exactFirst,
            int exactSecond)
        {
            Container hit = ProbePass(drop, pos, mustExist, exactFirst);
            if (hit != null)
                return hit;
            if (exactSecond >= 0 && exactSecond != exactFirst)
                return ProbePass(drop, pos, mustExist, exactSecond);
            return null;
        }

        private static Container ProbePass(ItemDrop drop, Vector3 pos, bool mustExist, int exactChestLink)
        {
            ProbeOrder.Clear();
            ProbeOrder.AddRange(ChestScratch);
            ProbeOrder.Sort((a, b) =>
            {
                float da = a == null ? float.MaxValue : ContainerFilter.Distance(pos, a.transform.position);
                float db = b == null ? float.MaxValue : ContainerFilter.Distance(pos, b.transform.position);
                return da.CompareTo(db);
            });

            for (int i = 0; i < ProbeOrder.Count; i++)
            {
                Container candidate = ProbeOrder[i];
                if (candidate == null)
                    continue;
                if (exactChestLink >= 0 && StationLink.ChestLinkId(candidate) != exactChestLink)
                    continue;
                if (!NearbyIndex.TryForceProbeForStore(candidate))
                    continue;

                Inventory inv = candidate.GetInventory();
                bool empty = inv == null || inv.NrOfItems() <= 0;
                NearbyIndex.NoteStoreProbeResult(candidate, empty);
                if (empty)
                    continue;

                if (ChestPicker.CanAccept(candidate, drop.m_itemData, pos, mustExist))
                    return candidate;
            }

            return null;
        }
    }
}
