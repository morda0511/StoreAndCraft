using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Valheim look used by UiToggle, UiLinkGrid and the station filter menu (always on; the
    /// DisplayMenuStyle = Classic option was removed in 1.3.45). SAC artwork = fallback only.
    /// Sprite names verified against Jotunn GUIManager (button, button_small, button_highlight,
    /// checkbox, checkbox_marker, woodpanel_settings). Missing sprite → caller keeps Classic.
    /// </summary>
    internal static class UiStyle
    {
        public static readonly Color TickYellow = new Color(1f, 0.86f, 0.1f, 1f);

        public static bool Vanilla => true;

        private static Material _litPanel;
        private static Material _litHud;

        /// <summary>
        /// Valheim's own UI materials (shader Custom/LitGui): panels / buttons take the day-night
        /// light like the inventory and crafting panels (verified in game: woodpanel_* = litpanel,
        /// Sort/Stack button = lithud). Item icons and text are not lit in vanilla either.
        /// Missing material → image keeps the default UI material (old look).
        /// </summary>
        public static void Lit(Image img)
        {
            if (img == null || img.sprite == null)
                return;
            if (_litPanel == null || _litHud == null)
            {
                Material[] all = Resources.FindObjectsOfTypeAll<Material>();
                for (int i = 0; i < all.Length; i++)
                {
                    Material m = all[i];
                    if (m == null || m.shader == null || m.shader.name != "Custom/LitGui")
                        continue;
                    if (_litPanel == null && m.name == "litpanel")
                        _litPanel = m;
                    else if (_litHud == null && m.name == "lithud")
                        _litHud = m;
                }
            }
            Material mat = img.sprite.name.StartsWith("woodpanel", System.StringComparison.Ordinal) ? _litPanel : _litHud;
            if (mat != null)
                img.material = mat;
        }

        /// <summary>Vanilla sprite when the Vanilla look is on, else null (caller uses Classic art).</summary>
        public static Sprite Sprite(string name)
        {
            return Vanilla ? SettingsPanel.Vanilla(name) : null;
        }

        /// <summary>
        /// Sliced vanilla frame. For small rects the border is scaled down (pixelsPerUnitMultiplier)
        /// so button art still reads at 15–22 px.
        /// </summary>
        public static void SetFrame(Image img, Sprite sprite, float minSide = 0f)
        {
            if (img == null || sprite == null)
                return;
            img.sprite = sprite;
            img.preserveAspect = false;
            img.color = Color.white;
            Lit(img);
            if (sprite.border == Vector4.zero)
            {
                img.type = Image.Type.Simple;
                return;
            }
            img.type = Image.Type.Sliced;
            float border = Mathf.Max(Mathf.Max(sprite.border.x, sprite.border.y), Mathf.Max(sprite.border.z, sprite.border.w));
            img.pixelsPerUnitMultiplier = minSide > 0.01f ? Mathf.Max(1f, border * 4f / minSide) : 1f;
        }

        /// <summary>Dark wash instead of the SAC inset art (like the crafting list).</summary>
        public static void AddDarkInset(RectTransform panel, float padL, float padT, float padR, float padB)
        {
            var go = new GameObject("InsetDark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(panel, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padL, padB);
            rt.offsetMax = new Vector2(-padR, -padT);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.38f);
            img.raycastTarget = false;
        }

        /// <summary>Soft drop shadow under item icons (same as the display filter).</summary>
        public static void AddIconShadow(GameObject icon)
        {
            if (icon == null)
                return;
            Shadow shadow = icon.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -3f);
        }
    }
}
