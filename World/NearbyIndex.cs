using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Nearby chests via Awake registration, with OverlapSphere fallback.
    /// Inventory Load is throttled (never every rescan) to avoid base lag spikes.
    /// </summary>
    internal static class NearbyIndex
    {
        private static readonly List<Container> Registered = new List<Container>();
        private static readonly HashSet<int> RegisteredIds = new HashSet<int>();
        private static readonly List<Container> Cached = new List<Container>();
        private static readonly Dictionary<int, float> LastInventoryLoad = new Dictionary<int, float>();
        private static float _nextScan;
        private static float _nextPrune;
        private static Vector3 _lastOrigin;
        private static int _cacheFrame = -1;
        private static readonly Dictionary<string, int> CountCache = new Dictionary<string, int>();
        private const float RescanMove = 1.5f;
        private const float IdleRescanSeconds = 2f;
        private const float MoveRescanSeconds = 0.75f;
        private const float InventoryLoadCooldown = 4f;

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
            LastInventoryLoad.Remove(id);
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
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            TickAt(player.transform.position);
        }

        /// <summary>
        /// Refresh the nearby-chest cache around an origin. Dedicated auto-store has
        /// no local player, so Tick() must not wipe this cache.
        /// </summary>
        public static void TickAt(Vector3 origin)
        {
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
            {
                Cached.Clear();
                return;
            }

            if (Time.unscaledTime >= _nextPrune)
            {
                PruneDead();
                if (Registered.Count == 0)
                    BootstrapExisting();
                _nextPrune = Time.unscaledTime + 5f;
            }

            float range = ScanRange();
            bool moved = Vector3.Distance(origin, _lastOrigin) > RescanMove;
            if (Time.time < _nextScan && !moved && Cached.Count > 0)
                return;

            Rescan(origin, range);
            _lastOrigin = origin;
            _nextScan = Time.time + (moved ? MoveRescanSeconds : IdleRescanSeconds);
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

        /// <summary>
        /// Load chest inventory from ZDO at most once per cooldown (or force).
        /// Used by craft counts / dump checks — never from the idle rescan loop.
        /// </summary>
        public static void EnsureInventory(Container container, bool force = false)
        {
            if (container == null)
                return;

            int id = container.GetInstanceID();
            float now = Time.unscaledTime;
            float last;
            if (!force && LastInventoryLoad.TryGetValue(id, out last) && now - last < InventoryLoadCooldown)
                return;

            ContainerFilter.RefreshInventory(container);
            LastInventoryLoad[id] = now;
            PendingChestDebit.OnInventoryLoaded(container);
        }

        public static void Rescan(Vector3 origin, float range)
        {
            Cached.Clear();
            CountCache.Clear();
            _cacheFrame = Time.frameCount;
            if (range <= 0f)
                return;

            float rangeSq = range * range;
            var seen = new HashSet<int>();
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                Container container = Registered[i];
                if (IsDestroyed(container))
                {
                    RemoveAt(i);
                    continue;
                }

                if (!IsReady(container))
                    continue;

                if (ContainerFilter.SqrDistance(origin, container.transform.position) > rangeSq)
                    continue;

                if (!ContainerFilter.PlayerMayUse(container, origin))
                    continue;

                int id = container.GetInstanceID();
                if (!seen.Add(id))
                    continue;

                Cached.Add(container);
            }

            if (Cached.Count == 0)
                FillFromOverlap(origin, range, rangeSq, seen);
        }

        private static void FillFromOverlap(Vector3 origin, float range, float rangeSq, HashSet<int> seen)
        {
            Collider[] hits = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide);
            if (hits == null)
                return;

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                Container container = hit.GetComponentInParent<Container>();
                if (container == null || IsDestroyed(container))
                    continue;

                Register(container);

                if (!IsReady(container))
                    continue;

                if (ContainerFilter.SqrDistance(origin, container.transform.position) > rangeSq)
                    continue;

                if (!ContainerFilter.PlayerMayUse(container, origin))
                    continue;

                int id = container.GetInstanceID();
                if (!seen.Add(id))
                    continue;

                Cached.Add(container);
            }
        }

        public static void CollectNear(Vector3 origin, float range, List<Container> dest)
        {
            if (dest == null)
                return;
            dest.Clear();
            if (range <= 0f)
                return;

            if (Time.unscaledTime >= _nextPrune)
            {
                PruneDead();
                if (Registered.Count == 0)
                    BootstrapExisting();
                _nextPrune = Time.unscaledTime + 5f;
            }

            float rangeSq = range * range;
            for (int i = 0; i < Registered.Count; i++)
            {
                Container container = Registered[i];
                if (IsDestroyed(container) || !IsReady(container))
                    continue;
                if (ContainerFilter.SqrDistance(origin, container.transform.position) > rangeSq)
                    continue;
                if (!ContainerFilter.PlayerMayUse(container, origin))
                    continue;
                dest.Add(container);
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

        public static void InvalidateCounts()
        {
            CountCache.Clear();
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

            float useRange = range > 0f ? range : StationFeed.ActivePullRange();
            string key = sharedName + "|" + quality + "|" + (leaveOne ? 1 : 0) + "|" + useRange.ToString("0.##");
            int cached;
            if (CountCache.TryGetValue(key, out cached))
                return cached;

            int total = 0;
            float craftSq = useRange * useRange;
            foreach (Container c in Cached)
            {
                if (c == null)
                    continue;

                if (ContainerFilter.SqrDistance(origin, c.transform.position) > craftSq)
                    continue;

                EnsureInventory(c);
                Inventory inv = c.GetInventory();
                if (inv == null)
                    continue;

                int n = inv.CountItems(sharedName, quality, true);
                n -= PendingChestDebit.Of(c, sharedName);
                if (n < 0)
                    n = 0;
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
                if (IsDestroyed(Registered[i]))
                    RemoveAt(i);
            }
        }

        private static void RemoveAt(int index)
        {
            Container c = Registered[index];
            Registered.RemoveAt(index);
            if (c != null)
            {
                int id = c.GetInstanceID();
                RegisteredIds.Remove(id);
                LastInventoryLoad.Remove(id);
            }
            else if (RegisteredIds.Count != Registered.Count)
            {
                RegisteredIds.Clear();
                LastInventoryLoad.Clear();
                foreach (Container x in Registered)
                {
                    if (x != null)
                        RegisteredIds.Add(x.GetInstanceID());
                }
            }
        }

        private static bool IsDestroyed(Container container)
        {
            return container == null;
        }

        private static bool IsReady(Container container)
        {
            if (container == null)
                return false;
            ZNetView nv = Refs.View(container);
            return nv != null && nv.IsValid();
        }
    }
}
