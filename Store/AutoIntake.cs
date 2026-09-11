using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class AutoIntake
    {
        private static float _next;

        public static float PauseUntil { get; set; }

        public static bool Paused
        {
            get { return Time.time < PauseUntil; }
        }

        public static void TogglePause()
        {
            if (Plugin.Settings == null)
                return;

            if (Paused)
            {
                PauseUntil = 0f;
                Tell(Loc.T("Auto-store resumed.", "Auto-Einlagern wieder an."));
            }
            else
            {
                PauseUntil = Time.time + Plugin.Settings.PauseSeconds.Value;
                Tell(Loc.T(
                    "Auto-store paused for " + Plugin.Settings.PauseSeconds.Value + "s.",
                    "Auto-Einlagern pausiert für " + Plugin.Settings.PauseSeconds.Value + "s."));
            }
        }

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
            if (players == null)
                return;

            foreach (Player player in players)
                TickFor(player, false);
        }

        private static bool Ready()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value || !Plugin.Settings.StoreEnabled.Value)
                return false;
            if (Paused)
                return false;
            return true;
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
            }

            List<ItemDrop> drops = Refs.Drops();
            if (drops == null)
                return;

            int moved = 0;
            int cap = Plugin.Settings.MaxTransfersPerTick.Value;
            Vector3 origin = player.transform.position;
            NearbyIndex.Rescan(origin, NearbyIndex.ScanRange());

            foreach (ItemDrop drop in drops)
            {
                if (moved >= cap)
                    break;
                if (drop == null || drop.m_itemData == null)
                    continue;
                ZNetView nv = Refs.View(drop);
                if (nv == null || !nv.IsValid())
                    continue;
                if (drop.IsPiece())
                    continue;
                if (!drop.CanPickup(true))
                    continue;

                float storeRange = Plugin.Settings.StoreRange.Value;
                Container chest = null;
                float best = float.MaxValue;

                foreach (Container candidate in NearbyIndex.Current)
                {
                    if (candidate == null)
                        continue;

                    string piece = ContainerFilter.PiecePrefab(candidate);
                    float range = RulesFile.StoreRange(piece, storeRange);
                    float distItem = ContainerFilter.Distance(drop.transform.position, candidate.transform.position);
                    if (distItem > range)
                        continue;
                    if (ContainerFilter.Distance(origin, candidate.transform.position) > NearbyIndex.ScanRange())
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

        private static void Tell(string msg)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
                player.Message(MessageHud.MessageType.Center, msg, 0, null, false);
        }
    }
}
