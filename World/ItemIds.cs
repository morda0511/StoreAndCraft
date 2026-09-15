using UnityEngine;

namespace StoreAndCraft
{
    internal static class ItemIds
    {
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

            GameObject byName = ObjectDB.instance.GetItemPrefab(token);
            if (byName != null)
                return byName;

            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                if (go == null)
                    continue;
                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared == null)
                    continue;
                if (string.Equals(drop.m_itemData.m_shared.m_name, token, System.StringComparison.OrdinalIgnoreCase))
                    return go;
                if (global::Localization.instance != null)
                {
                    string localized = global::Localization.instance.Localize(drop.m_itemData.m_shared.m_name);
                    if (string.Equals(localized, token, System.StringComparison.OrdinalIgnoreCase))
                        return go;
                }
            }

            return null;
        }
    }
}
