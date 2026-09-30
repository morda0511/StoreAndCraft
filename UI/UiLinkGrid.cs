using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>Shared 3×3 l1–l9 button grid with green selection outline.</summary>
    internal static class UiLinkGrid
    {
        // Sprites are cropped to opaque bounds — keep cells compact (chest rename default).
        public const float Cell = 22f;
        public const float Gap = 2f;
        /// <summary>Tighter cells for Station Settings bottom margin (between inset and panel).</summary>
        public const float CompactCell = 15f;
        public const float CompactGap = 1.5f;

        public static float BlockWidth => BlockSize(Cell, Gap);
        public static float BlockHeight => BlockSize(Cell, Gap);

        public static float BlockSize(float cell, float gap)
        {
            return 3f * cell + 2f * gap;
        }

        public static GameObject Build(
            Transform parent,
            string rootName,
            int selectedId,
            UnityAction<int> onClick)
        {
            return Build(parent, rootName, selectedId, onClick, Cell, Gap);
        }

        public static GameObject Build(
            Transform parent,
            string rootName,
            int selectedId,
            UnityAction<int> onClick,
            float cell,
            float gap)
        {
            var root = new GameObject(rootName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.transform as RectTransform;
            float block = BlockSize(cell, gap);
            rootRt.sizeDelta = new Vector2(block, block);

            for (int i = 1; i <= StationLink.MaxId; i++)
            {
                int gridRow = (i - 1) / 3;
                int gridCol = (i - 1) % 3;
                float x = gridCol * (cell + gap);
                float y = (2 - gridRow) * (cell + gap);
                MakeCell(rootRt, x, y, i, selectedId == i, onClick, cell);
            }

            return root;
        }

        public static void RefreshSelection(GameObject root, int selectedId)
        {
            if (root == null)
                return;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child == null || !child.name.StartsWith("SAC_l"))
                    continue;
                int id = 0;
                int.TryParse(child.name.Substring(5), out id);
                SetOutline(child.gameObject, id > 0 && id == selectedId);
            }
        }

        /// <summary>Dim link buttons (still clickable) when Ignore mode is active.</summary>
        public static void SetDimmed(GameObject root, bool dimmed)
        {
            if (root == null)
                return;
            float a = dimmed ? 0.5f : 1f;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child == null || !child.name.StartsWith("SAC_l"))
                    continue;
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    Color c = img.color;
                    c.a = a;
                    img.color = c;
                }
                Transform outline = child.Find("Outline");
                if (outline != null)
                {
                    Image outImg = outline.GetComponent<Image>();
                    if (outImg != null)
                    {
                        Color c = outImg.color;
                        c.a = a;
                        outImg.color = c;
                    }
                }
                Transform num = child.Find("Num");
                TMPro.TMP_Text numText = num != null ? num.GetComponent<TMPro.TMP_Text>() : null;
                if (numText != null)
                {
                    Color c = numText.color;
                    c.a = a;
                    numText.color = c;
                }
            }
        }

        private static void MakeCell(
            RectTransform parent,
            float x,
            float y,
            int linkId,
            bool selected,
            UnityAction<int> onClick,
            float cell)
        {
            var go = new GameObject(
                "SAC_l" + linkId,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(cell, cell);

            Image img = go.GetComponent<Image>();
            Button vanillaBtn = go.GetComponent<Button>();
            // Vanilla look: Valheim button with the number; selection = highlight frame.
            if (UiStyle.Sprite("button_small") != null)
            {
                ApplyVanillaCell(go, selected, cell);
                var numGo = new GameObject("Num", typeof(RectTransform));
                numGo.transform.SetParent(go.transform, false);
                RectTransform numRt = numGo.transform as RectTransform;
                numRt.anchorMin = Vector2.zero;
                numRt.anchorMax = Vector2.one;
                numRt.offsetMin = Vector2.zero;
                numRt.offsetMax = Vector2.zero;
                TMPro.TextMeshProUGUI num = UiFonts.CreateLabel(numGo, Mathf.Max(9f, cell * 0.62f));
                num.alignment = TMPro.TextAlignmentOptions.Center;
                num.raycastTarget = false;
                num.text = linkId.ToString();
                num.color = selected ? LinkGold : LinkMuted;
                vanillaBtn.transition = Selectable.Transition.None;
                int capturedV = linkId;
                vanillaBtn.onClick.AddListener(() =>
                {
                    if (onClick != null)
                        onClick(capturedV);
                });
                return;
            }

            Sprite sprite = UiAssets.Link(linkId);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);
            }

            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int captured = linkId;
            btn.onClick.AddListener(() =>
            {
                if (onClick != null)
                    onClick(captured);
            });

            // Outline matches the button image rect (same parent cell, slight bleed).
            var outline = new GameObject(
                "Outline",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            outline.transform.SetParent(go.transform, false);
            RectTransform outRt = outline.transform as RectTransform;
            outRt.anchorMin = new Vector2(0.5f, 0.5f);
            outRt.anchorMax = new Vector2(0.5f, 0.5f);
            outRt.pivot = new Vector2(0.5f, 0.5f);
            outRt.anchoredPosition = Vector2.zero;
            outRt.sizeDelta = new Vector2(cell + 2f, cell + 2f);
            Image outImg = outline.GetComponent<Image>();
            outImg.sprite = UiAssets.LinkSelected;
            outImg.preserveAspect = true;
            outImg.raycastTarget = false;
            outImg.color = Color.white;
            outline.SetActive(selected);
        }

        private static void SetOutline(GameObject cell, bool on)
        {
            Transform t = cell.transform.Find("Outline");
            if (t != null)
            {
                t.gameObject.SetActive(on);
                return;
            }
            // Vanilla cell (no outline): swap frame + number colour.
            Transform num = cell.transform.Find("Num");
            if (num == null)
                return;
            RectTransform rt = cell.transform as RectTransform;
            ApplyVanillaCell(cell, on, rt != null ? rt.sizeDelta.x : Cell);
            TMPro.TMP_Text text = num.GetComponent<TMPro.TMP_Text>();
            if (text != null)
            {
                float a = text.color.a;
                Color c = on ? LinkGold : LinkMuted;
                c.a = a;
                text.color = c;
            }
        }

        private static readonly Color LinkGold = new Color(1f, 0.85f, 0.35f, 1f);
        private static readonly Color LinkMuted = new Color(0.85f, 0.8f, 0.7f, 1f);

        private static void ApplyVanillaCell(GameObject cell, bool selected, float side)
        {
            Image img = cell.GetComponent<Image>();
            if (img == null)
                return;
            float a = img.color.a;
            Sprite frame = selected
                ? (UiStyle.Sprite("button_highlight") ?? UiStyle.Sprite("button_small"))
                : UiStyle.Sprite("button_small");
            UiStyle.SetFrame(img, frame, side);
            Color c = img.color;
            c.a = a <= 0f ? 1f : a; // keep SetDimmed alpha
            img.color = c;
        }

        public static void DestroyAll(List<GameObject> owned)
        {
            if (owned == null)
                return;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                    Object.Destroy(owned[i]);
            }
            owned.Clear();
        }
    }
}
