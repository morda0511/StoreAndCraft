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

        public static void Tick()
        {
            TickFor(Player.m_localPlayer, true);
        }

        public static void TickDedicated()
        {
            if (!Ready())
                return;
            if (Time.time < _next)
                return;

            _next = Time.time + Mathf.Max(1f, Plugin.Settings.IntakeInterval.Value);

            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count == 0)
                return;

            PlayerScratch.Clear();
            PlayerScratch.AddRange(players);

            if (!SnapshotDrops())
                return;

            for (int i = 0; i < PlayerScratch.Count; i++)
                TickFor(PlayerScratch[i], false);
        }

        private static bool Ready()
        {
            return Plugin.Settings != null
                && Plugin.Settings.ModEnabled.Value
                && Plugin.Settings.StoreEnabled.Value;
        }

        private static bool SnapshotDrops()
        {
            DropScratch.Clear();
            List<ItemDrop> live = Refs.Drops();
            if (live == null || live.Count == 0)
                return false;

            // StoreDrop destroys the drop and removes it from ItemDrop.s_instances.
            // Foreach on the live list throws InvalidOperationException and aborts
            // Plugin.Update, so auto-store looks "broken" until the next interval.
            DropScratch.AddRange(live);
            return DropScratch.Count > 0;
        }

        private static void TickFor(Player player, bool respectInterval)
        {
            if (!Ready())
                return;
            if (player == null || player.IsDead() || player.IsTeleporting())
                return;

            if (respectInterval)
            {
                if (Time.time < _next)
                    return;
                _next = Time.time + Mathf.Max(1f, Plugin.Settings.IntakeInterval.Value);
                if (!SnapshotDrops())
                    return;
            }

            int moved = 0;
            int cap = Plugin.Settings.MaxTransfersPerTick.Value;
            Vector3 origin = player.transform.position;
            float storeRange = Plugin.Settings.StoreRange.Value;
            NearbyIndex.TickAt(origin);
            IReadOnlyList<Container> chests = NearbyIndex.Current;

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
                    if (nv == null || !nv.IsValid())
                        continue;
                    if (drop.IsPiece())
                        continue;
                    if (!drop.CanPickup(true))
                        continue;

                    Container chest = null;
                    float best = float.MaxValue;

                    for (int c = 0; c < chests.Count; c++)
                    {
                        Container candidate = chests[c];
                        if (candidate == null)
                            continue;

                        float distItem = ContainerFilter.Distance(drop.transform.position, candidate.transform.position);
                        if (distItem > storeRange)
                            continue;
                        if (!ChestPicker.CanAccept(candidate, drop.m_itemData, drop.transform.position, Plugin.Settings.MustHaveExisting.Value))
                            continue;
                        if (distItem < best)
                        {
                            best = distItem;
                            chest = candidate;
                        }
                    }

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
    }
}
