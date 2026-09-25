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
                t.gameObject.SetActive(on);
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
