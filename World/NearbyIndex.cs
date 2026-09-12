using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class NearbyIndex
    {
        private static readonly List<Container> Cached = new List<Container>();
        private static float _nextScan;
        private static Vector3 _lastOrigin;
        private const float RescanMove = 1.5f;

        public static IReadOnlyList<Container> Current
        {
            get { return Cached; }
        }

        public static void Tick()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
            {
                Cached.Clear();
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Cached.Clear();
                return;
            }

            float range = ScanRange();
            Vector3 origin = player.transform.position;
            bool moved = Vector3.Distance(origin, _lastOrigin) > RescanMove;
            if (Time.time < _nextScan && !moved && Cached.Count > 0)
                return;

            Rescan(origin, range);
            _lastOrigin = origin;
            _nextScan = Time.time + 0.6f;
        }

        public static float AccessRange()
        {
            return ScanRange();
        }

        public static float ScanRange()
        {
            if (Plugin.Settings == null)
                return 20f;
            return Plugin.Settings.MaxGameplayRange();
        }

        public static void Rescan(Vector3 origin, float range)
        {
            Cached.Clear();
            if (range <= 0f)
                return;

            Collider[] hits = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<int>();
            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                Container container = hit.GetComponentInParent<Container>();
                if (container == null)
                    continue;

                int id = container.GetInstanceID();
                if (!seen.Add(id))
                    continue;

                if (!ContainerFilter.PlayerMayUse(container, origin))
                    continue;

                Cached.Add(container);
            }
        }

        public static List<Container> Within(Vector3 origin, float range)
        {
            var result = new List<Container>();
            foreach (Container c in Cached)
            {
                if (c == null)
                    continue;
                if (ContainerFilter.Distance(origin, c.transform.position) <= range)
                    result.Add(c);
            }
            return result;
        }

        public static int CountItem(Vector3 origin, float range, string sharedName, bool leaveOne)
        {
            if (string.IsNullOrEmpty(sharedName))
                return 0;

            int total = 0;
            float craftRange = Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : range;
            foreach (Container c in Cached)
            {
                if (c == null)
                    continue;

                if (ContainerFilter.Distance(origin, c.transform.position) > craftRange)
                    continue;

                Inventory inv = c.GetInventory();
                if (inv == null)
                    continue;

                int n = inv.CountItems(sharedName, -1, true);
                if (leaveOne && n > 0)
                    n -= 1;
                if (n > 0)
                    total += n;
            }

            return total;
        }
    }
}
