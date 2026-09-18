using TMPro;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>Valheim TMP fonts — prefer thin Norse (not Norsebold / title weight).</summary>
    internal static class UiFonts
    {
        private static TMP_FontAsset _thin;
        private static bool _tried;

        /// <summary>Thin Valheim body font (Valheim-Norse). Falls back to any non-Liberation TMP font.</summary>
        public static TMP_FontAsset ThinNorse()
        {
            if (_tried)
                return _thin;
            _tried = true;

            TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (all == null || all.Length == 0)
                return null;

            // Exact thin Norse first.
            for (int i = 0; i < all.Length; i++)
            {
                TMP_FontAsset f = all[i];
                if (f == null || string.IsNullOrEmpty(f.name))
                    continue;
                string n = f.name;
                if (n.Equals("Valheim-Norse", System.StringComparison.OrdinalIgnoreCase)
                    || n.Equals("Norse", System.StringComparison.OrdinalIgnoreCase))
                {
                    _thin = f;
                    return _thin;
                }
            }

            // Any Norse that is not bold.
            for (int i = 0; i < all.Length; i++)
            {
                TMP_FontAsset f = all[i];
                if (f == null || string.IsNullOrEmpty(f.name))
                    continue;
                string n = f.name;
                if (n.IndexOf("Liberation", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n.IndexOf("Norse", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (n.IndexOf("bold", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                _thin = f;
                return _thin;
            }

            // AveriaSans Regular / Light as secondary thin look.
            for (int i = 0; i < all.Length; i++)
            {
                TMP_FontAsset f = all[i];
                if (f == null || string.IsNullOrEmpty(f.name))
                    continue;
                string n = f.name;
                if (n.IndexOf("Liberation", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n.IndexOf("AveriaSans", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("Bold", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    _thin = f;
                    return _thin;
                }
            }

            for (int i = 0; i < all.Length; i++)
            {
                TMP_FontAsset f = all[i];
                if (f == null || string.IsNullOrEmpty(f.name))
                    continue;
                if (f.name.IndexOf("Liberation", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (f.name.IndexOf("bold", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                _thin = f;
                return _thin;
            }

            _thin = all[0];
            return _thin;
        }

        public static void StyleThinLabel(TMP_Text tmp, float size = 16f)
        {
            if (tmp == null)
                return;
            TMP_FontAsset font = ThinNorse();
            if (font != null)
                tmp.font = font;
            tmp.fontStyle = FontStyles.Normal;
            tmp.fontSize = size;
            tmp.enableAutoSizing = false;
            tmp.richText = false;
            tmp.raycastTarget = false;
        }
    }
}
