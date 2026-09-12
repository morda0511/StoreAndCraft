using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Nearby chests via Awake registration (no per-tick OverlapSphere).
    /// </summary>
    internal static class NearbyIndex
    {
        private static readonly List<Container> Registered = new List<Container>();
        private static readonly HashSet<int> RegisteredIds = new HashSet<int>();
        private static readonly List<Container> Cached = new List<Container>();
        private static float _nextScan;
        private static float _nextPrune;
        private static Vector3 _lastOrigin;
        private static int _cacheFrame = -1;
        private static readonly Dictionary<string, int> CountCache = new Dictionary<string, int>();
        private const float RescanMove = 1.5f;

        public static IReadOnlyList<Container> Current
        {
            get { return Cached; }
        }

        public static void Register(Container container)
        {
            if (container == null)
                return;
            int id = container.GetInstanceID();
            if (!RegisteredIds.Add(id))
                return;
            Registered.Add(container);
        }

        public static void Unregister(Container container)
        {
            if (container == null)
                return;
            int id = container.GetInstanceID();
            if (!RegisteredIds.Remove(id))
                return;
            Registered.Remove(container);
        }

        public static void BootstrapExisting()
        {
            Container[] all = Resources.FindObjectsOfTypeAll<Container>();
            if (all == null)
                return;
            foreach (Container c in all)
            {
                if (c == null || !c.gameObject.scene.IsValid())
                    continue;
                Register(c);
            }
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

            if (Time.unscaledTime >= _nextPrune)
            {
                PruneDead();
                _nextPrune = Time.unscaledTime + 5f;
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
            CountCache.Clear();
            _cacheFrame = Time.frameCount;
            if (range <= 0f)
                return;

            float rangeSq = range * range;
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                Container container = Registered[i];
                if (!IsAlive(container))
                {
                    RemoveAt(i);
                    continue;
                }

                if (ContainerFilter.SqrDistance(origin, container.transform.position) > rangeSq)
                    continue;

                if (!ContainerFilter.PlayerMayUse(container, origin))
                    continue;

                Cached.Add(container);
            }
        }

        public static List<Container> Within(Vector3 origin, float range)
        {
            var result = new List<Container>();
            float rangeSq = range * range;
            foreach (Container c in Cached)
            {
                if (c == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, c.transform.position) <= rangeSq)
                    result.Add(c);
            }
            return result;
        }

        public static int CountItem(Vector3 origin, float range, string sharedName, bool leaveOne, int quality = -1)
        {
            if (string.IsNullOrEmpty(sharedName))
                return 0;

            if (_cacheFrame != Time.frameCount)
            {
                CountCache.Clear();
                _cacheFrame = Time.frameCount;
            }

            string key = sharedName + "|" + quality + "|" + (leaveOne ? 1 : 0);
            int cached;
            if (CountCache.TryGetValue(key, out cached))
                return cached;

            int total = 0;
            float craftRange = Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : range;
            float craftSq = craftRange * craftRange;
            foreach (Container c in Cached)
            {
                if (c == null)
                    continue;

                if (ContainerFilter.SqrDistance(origin, c.transform.position) > craftSq)
                    continue;

                Inventory inv = c.GetInventory();
                if (inv == null)
                    continue;

                int n = inv.CountItems(sharedName, quality, true);
                if (leaveOne && n > 0)
                    n -= 1;
                if (n > 0)
                    total += n;
            }

            CountCache[key] = total;
            return total;
        }

        private static void PruneDead()
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                if (!IsAlive(Registered[i]))
                    RemoveAt(i);
            }
        }

        private static void RemoveAt(int index)
        {
            Container c = Registered[index];
            Registered.RemoveAt(index);
            if (c != null)
                RegisteredIds.Remove(c.GetInstanceID());
            else if (RegisteredIds.Count != Registered.Count)
            {
                RegisteredIds.Clear();
                foreach (Container x in Registered)
                {
                    if (x != null)
                        RegisteredIds.Add(x.GetInstanceID());
                }
            }
        }

        private static bool IsAlive(Container container)
        {
            if (container == null)
                return false;
            ZNetView nv = Refs.View(container);
            return nv != null && nv.IsValid();
        }
    }
}
