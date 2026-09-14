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
                && Plugin.Settings.StoreEnabled.Value;
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

            try
            {
                for (int i = 0; i < DropScratch.Count; i++)
                {
                    if (moved >= cap)
                        break;

                    ItemDrop drop = DropScratch[i];
                    if (drop == null || drop.m_itemData == null)
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
            Container best = null;
            float bestDist = float.MaxValue;
            bool mustExist = Plugin.Settings.MustHaveExisting.Value;
            Vector3 pos = drop.transform.position;

            for (int i = 0; i < ChestScratch.Count; i++)
            {
                Container candidate = ChestScratch[i];
                if (candidate == null)
                    continue;
                if (!ChestPicker.CanAccept(candidate, drop.m_itemData, pos, mustExist))
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
