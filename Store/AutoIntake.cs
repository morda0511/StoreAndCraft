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

            Container best = PickAccepting(ChestScratch, drop.m_itemData, pos, mustExist);
            if (best != null || !mustExist)
                return best;

            // Store miss with MustHaveExisting: ZDO Load may have been empty/stale.
            // Force-probe nearest chests (budgeted) then retry CanAccept.
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

        private static Container PickAccepting(
            List<Container> chests,
            ItemDrop.ItemData item,
            Vector3 pos,
            bool mustExist)
        {
            Container best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < chests.Count; i++)
            {
                Container candidate = chests[i];
                if (candidate == null)
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
    }
}
