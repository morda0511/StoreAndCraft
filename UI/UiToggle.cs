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
