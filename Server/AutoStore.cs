using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraftServer
{
    internal static class AutoStore
    {
        private static float _next;

        public static void Tick()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return;
            if (ZNet.instance == null || !ZNet.instance.IsServer())
                return;
            if (Time.time < _next)
                return;

            _next = Time.time + Mathf.Max(1f, Plugin.Settings.IntakeInterval.Value);

            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count == 0)
                return;

            List<ItemDrop> drops = Refs.Drops();
            if (drops == null || drops.Count == 0)
                return;

            float storeRange = Plugin.Settings.StoreRange.Value;
            float scan = RulesFile.MaxScanRange(storeRange, storeRange, storeRange);
            int cap = Plugin.Settings.MaxTransfersPerTick.Value;
            int moved = 0;

            foreach (Player player in players)
            {
                if (moved >= cap)
                    break;
                if (player == null || player.IsDead() || player.IsTeleporting())
                    continue;

                Vector3 origin = player.transform.position;
                List<Container> chests = FindChests(origin, scan);

                foreach (ItemDrop drop in drops)
                {
                    if (moved >= cap)
                        break;
                    if (drop == null || drop.m_itemData == null)
                        continue;
                    if (drop.IsPiece())
                        continue;

                    ZNetView nv = Refs.View(drop);
                    if (nv == null || !nv.IsValid())
                        continue;

                    Container chest = PickChest(chests, drop, storeRange);
                    if (chest == null)
                        continue;

                    if (StoreDrop.TryStore(chest, drop))
                        moved++;
                }
            }
        }

        private static Container PickChest(List<Container> chests, ItemDrop drop, float cfgRange)
        {
            Container best = null;
            float bestDist = float.MaxValue;

            foreach (Container chest in chests)
            {
                if (chest == null)
                    continue;

                string piece = ContainerFilter.PiecePrefab(chest);
                float range = RulesFile.StoreRange(piece, cfgRange);
                float dist = Vector3.Distance(drop.transform.position, chest.transform.position);
                if (dist > range)
                    continue;
                if (!CanAccept(chest, drop.m_itemData))
                    continue;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = chest;
                }
            }

            return best;
        }

        private static bool CanAccept(Container chest, ItemDrop.ItemData item)
        {
            if (chest == null || item == null)
                return false;
            if (!WardAccess.PlayersMayUse(chest))
                return false;
            if (!ContainerFilter.IsPlayerBuiltStorage(chest))
                return false;

            Inventory inv = chest.GetInventory();
            if (inv == null || !inv.CanAddItem(item, item.m_stack))
                return false;

            string prefab = ContainerFilter.PiecePrefab(chest);
            if (!RulesFile.AllowsStore(prefab, item))
                return false;

            if (Plugin.Settings.MustHaveExisting.Value)
            {
                string shared = ItemIds.SharedName(item);
                if (string.IsNullOrEmpty(shared) || inv.CountItems(shared, -1, true) <= 0)
                    return false;
            }

            return true;
        }

        private static List<Container> FindChests(Vector3 origin, float range)
        {
            var result = new List<Container>();
            if (range <= 0f)
                return result;

            Collider[] hits = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<int>();
            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;
                Container container = hit.GetComponentInParent<Container>();
                if (container == null)
                    continue;
                if (!seen.Add(container.GetInstanceID()))
                    continue;
                if (!ContainerFilter.IsUsable(container))
                    continue;
                result.Add(container);
            }

            return result;
        }
    }
}
