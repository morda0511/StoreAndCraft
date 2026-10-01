using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// On/off toggle from embedded <c>toggle_button_on</c> / <c>toggle_button_off</c> sprites.
    /// </summary>
    internal static class UiToggle
    {
        // Native art is 96×48 — default size (Small Display).
        public const float Width = 96f;
        public const float Height = 48f;
        // Compact size for the rename-chest panel.
        public const float CompactWidth = 56f;
        public const float CompactHeight = 28f;

        public static GameObject Create(
            Transform parent,
            string name,
            bool on,
            bool interactable,
            UnityAction onClick,
            TMP_FontAsset font,
            float width = Width,
            float height = Height)
        {
            _ = font;
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.transform as RectTransform;
            rt.sizeDelta = new Vector2(width, height);

            Image img = root.GetComponent<Image>();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = true;
            ApplyVisual(img, on, interactable);

            Button btn = root.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.interactable = interactable;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            return root;
        }

        public static void ApplyVisual(Image img, bool on, bool interactable)
        {
            if (img == null)
                return;

            // Vanilla look: Valheim checkbox, yellow tick when on (disabled = dimmed, no tick).
            Sprite box = UiStyle.Sprite("checkbox");
            if (box != null)
            {
                img.sprite = box;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                UiStyle.Lit(img);
                img.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                Image mark = EnsureMarker(img);
                if (mark != null)
                {
                    LayoutMarker(img, mark);
                    mark.gameObject.SetActive(on && interactable);
                }
                return;
            }
            Transform oldMark = img.transform.Find("Marker");
            if (oldMark != null)
                oldMark.gameObject.SetActive(false);

            Sprite onSp = UiAssets.ToggleOn;
            Sprite offSp = UiAssets.ToggleOff;
            if (!interactable)
            {
                img.sprite = offSp ?? onSp;
                img.color = new Color(1f, 1f, 1f, 0.45f);
                return;
            }

            img.sprite = on ? (onSp ?? offSp) : (offSp ?? onSp);
            img.color = Color.white;
        }

        /// <summary>Tick image over the checkbox (created on first vanilla use).</summary>
        private static Image EnsureMarker(Image box)
        {
            Transform t = box.transform.Find("Marker");
            if (t != null)
                return t.GetComponent<Image>();
            // vanilla checkbox_marker sits off-centre in its sprite (showed at the right edge), so the
            // checkbox sprite itself is used as a smaller, yellow, centred knob.
            Sprite tick = UiStyle.Sprite("checkbox");
            if (tick == null)
                return null;
            var go = new GameObject("Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(box.transform, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0.25f);
            rt.anchorMax = new Vector2(1f, 0.75f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.sprite = tick;
            img.preserveAspect = true;
            img.color = UiStyle.TickYellow;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Centre the tick on the drawn checkbox. preserveAspect places the square box at the
        /// rect's pivot (left / right edge on a wide rect with pivot 0 / 1), not at its centre.
        /// </summary>
        private static void LayoutMarker(Image box, Image mark)
        {
            RectTransform b = box.rectTransform;
            RectTransform m = mark.rectTransform;
            Vector2 size = b.rect.size;
            float side = Mathf.Min(size.x, size.y);
            if (side <= 0.01f)
                return;
            float cx = (size.x - side) * b.pivot.x + side * 0.5f;
            float cy = (size.y - side) * b.pivot.y + side * 0.5f;
            m.anchorMin = Vector2.zero;
            m.anchorMax = Vector2.zero;
            m.pivot = new Vector2(0.5f, 0.5f);
            m.anchoredPosition = new Vector2(cx, cy);
            m.sizeDelta = new Vector2(side * 0.5f, side * 0.5f);
        }

        /// <summary>Re-centre the tick after the caller changed the toggle's pivot / size.</summary>
        public static void RefreshMarker(GameObject toggle)
        {
            Image img = toggle != null ? toggle.GetComponent<Image>() : null;
            Transform t = img != null ? img.transform.Find("Marker") : null;
            Image mark = t != null ? t.GetComponent<Image>() : null;
            if (mark != null)
                LayoutMarker(img, mark);
        }

        public static void SetState(GameObject toggle, bool on, bool interactable)
        {
            if (toggle == null)
                return;
            Image img = toggle.GetComponent<Image>();
            ApplyVisual(img, on, interactable);
            Button btn = toggle.GetComponent<Button>();
            if (btn != null)
                btn.interactable = interactable;
        }
    }
}
