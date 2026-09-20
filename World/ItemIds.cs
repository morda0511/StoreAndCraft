using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class ItemIds
    {
        private static readonly Dictionary<string, GameObject> PrefabCache =
            new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        private static int _odbCount = -1;

        public static string PrefabName(ItemDrop.ItemData item)
        {
            if (item == null)
                return null;

            if (item.m_dropPrefab != null)
                return StripClone(item.m_dropPrefab.name);

            if (item.m_shared == null || string.IsNullOrEmpty(item.m_shared.m_name) || ObjectDB.instance == null)
                return SharedName(item);

            GameObject prefab = ObjectDB.instance.GetItemPrefab(item.m_shared);
            if (prefab != null)
            {
                item.m_dropPrefab = prefab;
                return StripClone(prefab.name);
            }

            return SharedName(item);
        }

        public static string SharedName(ItemDrop.ItemData item)
        {
            return item?.m_shared != null ? item.m_shared.m_name : null;
        }

        public static string StripClone(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return name.Replace("(Clone)", string.Empty).Trim();
        }

        public static bool Matches(ItemDrop.ItemData item, string token)
        {
            if (item == null || string.IsNullOrEmpty(token))
                return false;

            if (string.Equals(PrefabName(item), token, System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(SharedName(item), token, System.StringComparison.OrdinalIgnoreCase))
                return true;

            string normToken = Normalize(token);
            if (!string.IsNullOrEmpty(normToken))
            {
                if (string.Equals(Normalize(PrefabName(item)), normToken, System.StringComparison.Ordinal))
                    return true;
                if (string.Equals(Normalize(SharedName(item)), normToken, System.StringComparison.Ordinal))
                    return true;
            }

            if (global::Localization.instance != null && item.m_shared != null)
            {
                string localized = global::Localization.instance.Localize(item.m_shared.m_name);
                if (string.Equals(localized, token, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Resolve a stored display token to the inventory shared-name for CountItems.</summary>
        public static string SharedFromToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            if (token.StartsWith("$item_", System.StringComparison.OrdinalIgnoreCase))
                return token;

            GameObject prefab = PrefabFromToken(token);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop?.m_itemData?.m_shared != null && !string.IsNullOrEmpty(drop.m_itemData.m_shared.m_name))
                return drop.m_itemData.m_shared.m_name;

            return token;
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            string s = raw.Trim().ToLowerInvariant();
            if (s.StartsWith("$item_"))
                s = s.Substring(6);
            s = s.Replace(" ", "").Replace("-", "").Replace("_", "");
            return s;
        }

        public static GameObject PrefabFromToken(string token)
        {
            if (string.IsNullOrEmpty(token) || ObjectDB.instance == null)
                return null;

            EnsurePrefabIndex();

            GameObject cached;
            if (PrefabCache.TryGetValue(token, out cached))
                return cached;

            string norm = Normalize(token);
            if (!string.IsNullOrEmpty(norm) && PrefabCache.TryGetValue(norm, out cached))
            {
                PrefabCache[token] = cached;
                return cached;
            }

            GameObject byName = ObjectDB.instance.GetItemPrefab(token);
            if (byName != null)
            {
                PrefabCache[token] = byName;
                return byName;
            }

            // Index miss: one linear pass by shared name only (no Localize per item).
            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                if (go == null)
                    continue;
                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared == null)
                    continue;
                string shared = drop.m_itemData.m_shared.m_name;
                if (string.Equals(shared, token, System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Normalize(shared), norm, System.StringComparison.Ordinal))
                {
                    PrefabCache[token] = go;
                    return go;
                }
            }

            PrefabCache[token] = null;
            return null;
        }

        /// <summary>Build token→prefab map once per ObjectDB load (shared name + prefab name).</summary>
        private static void EnsurePrefabIndex()
        {
            ObjectDB odb = ObjectDB.instance;
            if (odb?.m_items == null)
                return;

            int count = odb.m_items.Count;
            if (_odbCount == count && PrefabCache.Count > 0)
                return;

            PrefabCache.Clear();
            _odbCount = count;
            for (int i = 0; i < count; i++)
            {
                GameObject go = odb.m_items[i];
                if (go == null)
                    continue;
                string pname = StripClone(go.name);
                if (!string.IsNullOrEmpty(pname))
                {
                    PrefabCache[pname] = go;
                    string pn = Normalize(pname);
                    if (!string.IsNullOrEmpty(pn) && !PrefabCache.ContainsKey(pn))
                        PrefabCache[pn] = go;
                }
                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared == null)
                    continue;
                string shared = drop.m_itemData.m_shared.m_name;
                if (string.IsNullOrEmpty(shared))
                    continue;
                PrefabCache[shared] = go;
                string sn = Normalize(shared);
                if (!string.IsNullOrEmpty(sn) && !PrefabCache.ContainsKey(sn))
                    PrefabCache[sn] = go;
            }
        }
    }
}
