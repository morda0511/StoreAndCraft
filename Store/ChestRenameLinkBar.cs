using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Rename-chest extras: Ignore / Show-on-display toggles + l1–l9 link grid.
    /// </summary>
    internal static class ChestRenameLinkBar
    {
        private static readonly List<GameObject> Owned = new List<GameObject>();
        private static readonly FieldInfo InputField =
            AccessTools.Field(typeof(TextInput), "m_inputField");
        private static readonly FieldInfo PanelField =
            AccessTools.Field(typeof(TextInput), "m_panel");

        private static GameObject _ignoreToggle;
        private static GameObject _showToggle;
        private static GameObject _linkGrid;
        private static TMP_FontAsset _font;
        private static Container _chest;

        public static void Show(Container chest = null)
        {
            Hide();
            _chest = chest;
            TextInput ti = TextInput.instance;
            if (ti == null)
                return;

            GameObject panel = PanelField?.GetValue(ti) as GameObject;
            if (panel == null)
                return;

            RectTransform panelRt = panel.transform as RectTransform;
            if (panelRt == null)
                return;

            _font = UiFonts.ThinNorse() ?? ResolveFont(ti);

            string current = ReadFieldText();
            bool ignore = ChestNames.IsIgnoredName(current);
            bool showOnDisplay = ChestNames.IsHiddenName(current);
            int linkId = StationLink.ParseFromName(current);
            bool showOk = ignore;

            // Ignore — always clickable (clears link if one is set)
            var ignoreBlock = new GameObject("SAC_IgnoreBlock", typeof(RectTransform));
            ignoreBlock.transform.SetParent(panel.transform, false);
            RectTransform ignoreRt = ignoreBlock.transform as RectTransform;
            ignoreRt.anchorMin = new Vector2(0f, 1f);
            ignoreRt.anchorMax = new Vector2(0f, 1f);
            ignoreRt.pivot = new Vector2(0f, 1f);
            ignoreRt.anchoredPosition = new Vector2(10f, -6f);
            ignoreRt.sizeDelta = new Vector2(UiToggle.CompactWidth + 8f, UiToggle.CompactHeight + 20f);
            Owned.Add(ignoreBlock);
            AddLabel(ignoreRt, Loc.T("Ignore", "Ignorieren"), new Vector2(0f, -1f));
            _ignoreToggle = UiToggle.Create(
                ignoreRt,
                "SAC_IgnoreToggle",
                ignore,
                true,
                OnIgnoreClicked,
                _font,
                UiToggle.CompactWidth,
                UiToggle.CompactHeight);
            PlaceToggle(_ignoreToggle, new Vector2(0f, -16f));

            // Show on display — only when Ignore is on
            var showBlock = new GameObject("SAC_ShowBlock", typeof(RectTransform));
            showBlock.transform.SetParent(panel.transform, false);
            RectTransform showRt = showBlock.transform as RectTransform;
            showRt.anchorMin = new Vector2(1f, 1f);
            showRt.anchorMax = new Vector2(1f, 1f);
            showRt.pivot = new Vector2(1f, 1f);
            showRt.anchoredPosition = new Vector2(-10f, -6f);
            showRt.sizeDelta = new Vector2(UiToggle.CompactWidth + 8f, UiToggle.CompactHeight + 20f);
            Owned.Add(showBlock);
            AddLabel(showRt, Loc.T("Show on display", "Auf Display zeigen"), new Vector2(0f, -1f), true);
            _showToggle = UiToggle.Create(
                showRt,
                "SAC_ShowToggle",
                showOk && showOnDisplay,
                showOk,
                OnShowClicked,
                _font,
                UiToggle.CompactWidth,
                UiToggle.CompactHeight);
            PlaceToggle(_showToggle, new Vector2(0f, -16f), true);

            // Link grid — bottom-left (dimmed while Ignore is on, still clickable)
            _linkGrid = UiLinkGrid.Build(panel.transform, "SAC_ChestLinks", linkId, OnLinkClicked);
            RectTransform gridRt = _linkGrid.transform as RectTransform;
            gridRt.anchorMin = new Vector2(0f, 0f);
            gridRt.anchorMax = new Vector2(0f, 0f);
            gridRt.pivot = new Vector2(0f, 0f);
            gridRt.anchoredPosition = new Vector2(10f, 10f);
            Owned.Add(_linkGrid);
            UiLinkGrid.SetDimmed(_linkGrid, ignore);
        }

        public static void Hide()
        {
            _ignoreToggle = null;
            _showToggle = null;
            _linkGrid = null;
            _chest = null;
            for (int i = 0; i < Owned.Count; i++)
            {
                if (Owned[i] != null)
                    Object.Destroy(Owned[i]);
            }
            Owned.Clear();
        }

        private static void PlaceToggle(GameObject toggle, Vector2 anchored, bool fromRight = false)
        {
            RectTransform rt = toggle.transform as RectTransform;
            if (fromRight)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            rt.anchoredPosition = anchored;
        }

        private static void AddLabel(RectTransform parent, string text, Vector2 pos, bool fromRight = false)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.transform as RectTransform;
            if (fromRight)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160f, 18f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            UiFonts.StyleThinLabel(label, 13f);
            label.text = text;
            label.color = new Color(1f, 0.85f, 0.4f, 1f);
            label.alignment = fromRight ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        private static void OnIgnoreClicked()
        {
            string current = ReadFieldText();
            int link = StationLink.ParseFromName(current);
            bool ignore = !ChestNames.IsIgnoredName(current);
            // Turning Ignore on while a link is set: drop the link automatically.
            if (ignore && link > 0)
                current = StationLink.ApplyToName(current, link);
            bool show = ignore && ChestNames.IsHiddenName(current);
            if (!ignore)
                show = false;
            WriteFieldText(ChestNames.ApplyIgnoreFlags(current, ignore, show, FallbackName()));
            RefreshToggles();
            RefreshLinks();
        }

        private static void OnShowClicked()
        {
            string current = ReadFieldText();
            if (!ChestNames.IsIgnoredName(current))
                return;
            bool show = !ChestNames.IsHiddenName(current);
            WriteFieldText(ChestNames.ApplyIgnoreFlags(current, true, show, FallbackName()));
            RefreshToggles();
            RefreshLinks();
        }

        private static string FallbackName()
        {
            return ChestNames.LocalizedVanillaName(_chest);
        }

        private static void OnLinkClicked(int linkId)
        {
            string current = ReadFieldText();
            string next = StationLink.ApplyToName(current, linkId);
            // Link and Ignore are mutually exclusive — picking a link clears Ignore / Show.
            if (StationLink.ParseFromName(next) > 0)
                next = ChestNames.ApplyIgnoreFlags(next, false, false);
            WriteFieldText(next);
            RefreshToggles();
            RefreshLinks();
        }

        private static void RefreshToggles()
        {
            string current = ReadFieldText();
            bool ignore = ChestNames.IsIgnoredName(current);
            bool show = ChestNames.IsHiddenName(current);
            UiToggle.SetState(_ignoreToggle, ignore, true);
            UiToggle.SetState(_showToggle, show, ignore);
        }

        private static void RefreshLinks()
        {
            string current = ReadFieldText();
            int active = StationLink.ParseFromName(current);
            bool ignore = ChestNames.IsIgnoredName(current);
            UiLinkGrid.RefreshSelection(_linkGrid, active);
            UiLinkGrid.SetDimmed(_linkGrid, ignore);
        }

        private static string ReadFieldText()
        {
            TextInput ti = TextInput.instance;
            if (ti == null)
                return "";
            object field = InputField?.GetValue(ti);
            return field != null ? GetFieldText(field) : "";
        }

        private static void WriteFieldText(string next)
        {
            TextInput ti = TextInput.instance;
            if (ti == null)
                return;
            object field = InputField?.GetValue(ti);
            if (field == null)
                return;
            if (next != null && next.Length > ChestNames.MaxLength)
                next = next.Substring(0, ChestNames.MaxLength);
            SetFieldText(field, next ?? "");
        }

        private static TMP_FontAsset ResolveFont(TextInput ti)
        {
            return UiFonts.ThinNorse()
                ?? (ti != null ? ti.GetComponentInChildren<TMP_Text>(true)?.font : null);
        }

        private static string GetFieldText(object field)
        {
            PropertyInfo prop = field.GetType().GetProperty("text");
            if (prop != null)
                return prop.GetValue(field, null) as string ?? "";
            FieldInfo f = AccessTools.Field(field.GetType(), "m_text");
            if (f != null)
                return f.GetValue(field) as string ?? "";
            return "";
        }

        private static void SetFieldText(object field, string value)
        {
            PropertyInfo prop = field.GetType().GetProperty("text");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(field, value, null);
                return;
            }

            MethodInfo set = AccessTools.Method(field.GetType(), "SetText", new[] { typeof(string) });
            if (set != null)
                set.Invoke(field, new object[] { value });
        }
    }

    [HarmonyPatch(typeof(TextInput), nameof(TextInput.Hide))]
    internal static class TextInputHideChestLinksPatch
    {
        private static void Prefix()
        {
            ChestRenameLinkBar.Hide();
        }
    }
}
