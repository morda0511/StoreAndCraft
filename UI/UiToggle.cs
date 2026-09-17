using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>Track + knob toggle built from embedded UI sprites.</summary>
    internal static class UiToggle
    {
        public const float Width = 72f;
        public const float Height = 36f;

        public static GameObject Create(
            Transform parent,
            string name,
            bool on,
            bool interactable,
            UnityAction onClick,
            TMP_FontAsset font)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.transform as RectTransform;
            rt.sizeDelta = new Vector2(Width, Height);

            Image track = root.GetComponent<Image>();
            track.sprite = UiAssets.Track;
            track.type = Image.Type.Simple;
            track.preserveAspect = true;
            track.color = Color.white;
            track.raycastTarget = true;

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            knobGo.transform.SetParent(root.transform, false);
            RectTransform knobRt = knobGo.transform as RectTransform;
            knobRt.anchorMin = Vector2.zero;
            knobRt.anchorMax = Vector2.one;
            knobRt.offsetMin = Vector2.zero;
            knobRt.offsetMax = Vector2.zero;
            Image knob = knobGo.GetComponent<Image>();
            knob.type = Image.Type.Simple;
            knob.preserveAspect = true;
            knob.raycastTarget = false;
            ApplyVisual(track, knob, on, interactable);

            Button btn = root.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.interactable = interactable;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            // font unused here; labels are created by caller
            _ = font;
            return root;
        }

        public static void ApplyVisual(Image track, Image knob, bool on, bool interactable)
        {
            if (!interactable)
            {
                if (track != null)
                {
                    track.sprite = UiAssets.ShowDisplayDisabled ?? UiAssets.Track;
                    track.color = Color.white;
                }
                if (knob != null)
                    knob.enabled = false;
                return;
            }

            if (track != null)
            {
                track.sprite = UiAssets.Track;
                track.color = Color.white;
            }
            if (knob != null)
            {
                knob.enabled = true;
                knob.sprite = on ? UiAssets.KnobOn : UiAssets.KnobOff;
                knob.color = Color.white;
            }
        }

        public static void SetState(GameObject toggle, bool on, bool interactable)
        {
            if (toggle == null)
                return;
            Image track = toggle.GetComponent<Image>();
            Transform knobT = toggle.transform.Find("Knob");
            Image knob = knobT != null ? knobT.GetComponent<Image>() : null;
            ApplyVisual(track, knob, on, interactable);
            Button btn = toggle.GetComponent<Button>();
            if (btn != null)
                btn.interactable = interactable;
        }
    }
}
