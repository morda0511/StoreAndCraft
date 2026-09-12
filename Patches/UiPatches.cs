using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class CraftUiHintPatch
    {
        private static readonly Color Flash = new Color(1f, 0.92f, 0.2f, 1f);

        private static void Prefix()
        {
            // Count nearby chests while drawing requirement amounts (same path as HaveRequirements).
            StationHover.Begin();
        }

        private static void Postfix(Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier, bool __result)
        {
            try
            {
                if (!__result || !StagingPull.Active || player == null || req == null || req.m_resItem == null || elementRoot == null)
                    return;

                string shared = req.m_resItem.m_itemData != null ? req.m_resItem.m_itemData.m_shared.m_name : null;
                if (string.IsNullOrEmpty(shared))
                    return;

                int nearby = RequirementBridge.CountNearby(player, shared);
                if (nearby <= 0)
                    return;

                TMP_Text[] texts = elementRoot.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text text in texts)
                {
                    if (text == null)
                        continue;
                    text.color = Color.Lerp(text.color, Flash, 0.65f);
                }
            }
            finally
            {
                StationHover.End();
            }
        }
    }

    /// <summary>
    /// Gold icon tint + ★ badge on favorited player-inventory stacks.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class FavoriteGridVisualPatch
    {
        private const string BadgeName = "SAC_FavStar";

        private static void Postfix(InventoryGrid __instance)
        {
            if (__instance == null || Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return;

            InventoryGui gui = InventoryGui.instance;
            if (gui == null || __instance != gui.m_playerGrid)
                return;

            Inventory inv = __instance.GetInventory();
            if (inv == null)
                return;

            var field = AccessTools.Field(typeof(InventoryGrid), "m_elements");
            if (field == null)
                return;

            var list = field.GetValue(__instance) as System.Collections.Generic.List<InventoryElement>;
            if (list == null)
                return;

            foreach (InventoryElement el in list)
            {
                if (el == null)
                    continue;

                if (!el.m_used)
                {
                    ClearBadge(el.transform);
                    continue;
                }

                Vector2i pos = el.Position;
                ItemDrop.ItemData item = inv.GetItemAt(pos.x, pos.y);
                bool fav = item != null && Favorites.IsFavorite(item);
                ApplyVisual(el, fav);
            }
        }

        private static void ClearBadge(Transform root)
        {
            if (root == null)
                return;
            Transform badge = root.Find(BadgeName);
            if (badge != null)
                badge.gameObject.SetActive(false);
        }

        private static void ApplyVisual(InventoryElement el, bool favorite)
        {
            if (el.m_icon != null)
            {
                if (favorite)
                    el.m_icon.color = Color.Lerp(Color.white, Favorites.Tint, 0.55f);
                else
                {
                    Color t = Color.Lerp(Color.white, Favorites.Tint, 0.55f);
                    if (ColorsClose(el.m_icon.color, t))
                        el.m_icon.color = Color.white;
                }
            }

            Transform root = el.transform;
            Transform badge = root.Find(BadgeName);
            if (favorite)
            {
                if (badge != null && IsBrokenTmpBadge(badge))
                {
                    Object.Destroy(badge.gameObject);
                    badge = null;
                }
                if (badge == null)
                    badge = CreateBadge(el);
                if (badge != null)
                    badge.gameObject.SetActive(true);
            }
            else if (badge != null)
            {
                badge.gameObject.SetActive(false);
            }
        }

        private static bool IsBrokenTmpBadge(Transform badge)
        {
            TextMeshProUGUI tmp = badge.GetComponent<TextMeshProUGUI>();
            return tmp != null && tmp.font == null;
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.08f
                && Mathf.Abs(a.g - b.g) < 0.08f
                && Mathf.Abs(a.b - b.b) < 0.08f;
        }

        private static Transform CreateBadge(InventoryElement el)
        {
            Transform parent = el.transform;
            var go = new GameObject(BadgeName);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(2f, -1f);
            rt.sizeDelta = new Vector2(20f, 20f);

            // Prefer TMP ★ with the same font Valheim already uses on the stack count.
            if (el.m_amount != null && el.m_amount.font != null)
            {
                TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.font = el.m_amount.font;
                tmp.fontSharedMaterial = el.m_amount.fontSharedMaterial;
                tmp.text = "★";
                tmp.fontSize = 16f;
                tmp.color = Favorites.Tint;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.raycastTarget = false;
                return go.transform;
            }

            // Fallback: tiny gold square (no font asset required).
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = Favorites.Tint;
            if (el.m_icon != null && el.m_icon.sprite != null)
                img.sprite = el.m_icon.sprite;
            rt.sizeDelta = new Vector2(8f, 8f);
            rt.anchoredPosition = new Vector2(3f, -3f);
            return go.transform;
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(int))]
    internal static class FavoriteTooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref string __result)
        {
            if (__instance == null || string.IsNullOrEmpty(__result) || !Favorites.IsFavorite(__instance))
                return;
            __result += "\n<color=#ffd24d>★ Favorite — protected from dump / hover-store (F to toggle)</color>";
        }
    }
}
