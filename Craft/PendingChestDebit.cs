using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Client-side reservation after a consume RPC. Remote chest inventories do not
    /// drop until the owner applies the RPC, so HaveRequirements would stay true and
    /// listen-server clients could craft many items for one pull.
    /// </summary>
    internal static class PendingChestDebit
    {
        private const float Lifetime = 8f;
        private static readonly Dictionary<string, int> Amounts = new Dictionary<string, int>();
        private static readonly Dictionary<string, float> ExpireAt = new Dictionary<string, float>();

        public static void Add(Container chest, string shared, int amount)
        {
            if (chest == null || amount <= 0 || string.IsNullOrEmpty(shared))
                return;

            string key = Key(chest, shared);
            int have;
            Amounts.TryGetValue(key, out have);
            Amounts[key] = have + amount;
            ExpireAt[key] = Time.unscaledTime + Lifetime;
            NearbyIndex.InvalidateCounts();
        }

        public static int Of(Container chest, string shared)
        {
            if (chest == null || string.IsNullOrEmpty(shared) || Amounts.Count == 0)
                return 0;

            Prune();
            int n;
            return Amounts.TryGetValue(Key(chest, shared), out n) ? n : 0;
        }

        public static void Tick()
        {
            if (Amounts.Count == 0)
                return;
            Prune();
        }

        private static void Prune()
        {
            if (ExpireAt.Count == 0)
                return;

            float now = Time.unscaledTime;
            var dead = new List<string>();
            foreach (KeyValuePair<string, float> pair in ExpireAt)
            {
                if (now >= pair.Value)
                    dead.Add(pair.Key);
            }

            if (dead.Count == 0)
                return;

            for (int i = 0; i < dead.Count; i++)
            {
                Amounts.Remove(dead[i]);
                ExpireAt.Remove(dead[i]);
            }

            NearbyIndex.InvalidateCounts();
        }

        private static string Key(Container chest, string shared)
        {
            return chest.GetInstanceID() + "|" + shared;
        }
    }
}
