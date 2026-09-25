using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Chest pull filter — centered panel_left chrome (same assets as Select Types left panel).
    /// Link l1–l9 grid stays bottom-left on the panel.
    /// </summary>
    internal static class StationFilterMenu
    {
        public static bool IsOpen { get; private set; }

        private const float RefW = 1920f;
        private const float RefH = 1080f;
        private const float PanelW = 552f;
        private const float PanelH = 887f;

        private const float InsetL = 40f;
        private const float InsetT = 74f;
        private const float InsetR = 32f;
        private const float InsetB = 93f;
        private const float ClipL = 48f;
        private const float ClipT = 86f;
        private const float ClipR = 40f;
        private const float ClipB = 105f;
        private const float ContentPad = 12f;

        private const float RowW = 400f;
        private const float RowH = 54f;
        private const float RowGap = 8f;
        private const float RowPadL = 16f;
        private const float ToggleW = 36f;
        private const float ToggleH = 18f;
        private const float ApplyW = 140f;
        private const float ApplyH = 38f;
        private const float LetterSpace = 14f;
        private const float RowFont = 22f;
        private const float ApplyFont = 20f;
        private const float HeaderFont = 22f;

        private static readonly Color Gold = new Color(0.925f, 0.77f, 0.29f, 1f);
        private static readonly Color Muted = new Color(0.70f, 0.64f, 0.52f, 1f);
        private static readonly Color OnGreen = new Color(0.45f, 0.95f, 0.45f, 1f);
        private static readonly Color OffRed = new Color(1f, 0.45f, 0.4f, 1f);

        private static Smelter _smelter;
        private static CookingStation _cook;
        private static Fermenter _fermenter;
        private static Fireplace _fire;
        private static float _openedAt;
        private static int _suppressMenuFrame;
        private static bool _closing;

        private static GameObject _root;
        private static RectTransform _listContent;
        private static ScrollRect _listScroll;
        private static GameObject _linkGrid;
        private static RectTransform _panelRt;

        public static bool AnySkillsMenuOpen
        {
            get { return IsOpen || DisplayTypeMenu.IsOpen || DisplayRangeMenu.IsOpen; }
        }

        public static void Open(Smelter smelter)
        {
            OpenInternal(smelter, null, null, null);
        }

        public static void Open(CookingStation cook)
        {
            OpenInternal(null, cook, null, null);
        }

        public static void Open(Fermenter fermenter)
        {
            OpenInternal(null, null, fermenter, null);
        }

        public static void Open(Fireplace fire)
        {
            OpenInternal(null, null, null, fire);
        }

        private static void OpenInternal(
            Smelter smelter,
            CookingStation cook,
            Fermenter fermenter,
            Fireplace fire)
        {
            if ((smelter == null && cook == null && fermenter == null && fire == null)
                || Player.m_localPlayer == null)
                return;

            if (DisplayTypeMenu.IsOpen)
                DisplayTypeMenu.Close();
            if (DisplayRangeMenu.IsOpen)
                DisplayRangeMenu.Close();
            if (DisplaySmallOptions.IsOpen)
                DisplaySmallOptions.Close();
            if (IsOpen)
                Close();

            if (InventoryGui.instance != null && InventoryGui.IsVisible())
                InventoryGui.instance.Hide();

            _smelter = smelter;
            _cook = cook;
            _fermenter = fermenter;
            _fire = fire;
            _openedAt = Time.unscaledTime;

            UiFonts.ThinNorse();
            EnsureRoot();
            if (_root == null)
                return;
            _root.SetActive(true);
            RebuildRows();
            IsOpen = true;
        }

        public static void Close()
        {
            if (_closing)
                return;
            _closing = true;
            try
            {
                IsOpen = false;
                _smelter = null;
                _cook = null;
                _fermenter = null;
                _fire = null;
                _suppressMenuFrame = Time.frameCount;
                DestroyRoot();
            }
            finally
            {
                _closing = false;
            }
        }

        public static void CloseIf(Smelter smelter)
        {
            if (IsOpen && _smelter == smelter)
                Close();
        }

        public static void CloseIf(CookingStation cook)
        {
            if (IsOpen && _cook == cook)
                Close();
        }

        public static void CloseIf(Fermenter fermenter)
        {
            if (IsOpen && _fermenter == fermenter)
                Close();
        }

        public static void CloseIf(Fireplace fire)
        {
            if (IsOpen && _fire == fire)
                Close();
        }

        public static bool ShouldBlockPause()
        {
            return IsOpen || Time.frameCount == _suppressMenuFrame;
        }

        internal static void Tick()
        {
            if (!IsOpen)
                return;
            if ((_smelter == null && _cook == null && _fermenter == null && _fire == null)
                || Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            if (Time.unscaledTime < _openedAt + 0.15f)
                return;
            if (ZInput.GetKeyDown(KeyCode.Escape, true) || ZInput.GetButtonDown("JoyButtonB"))
                Close();
        }

        private static void Toggle(string shared)
        {
            if (string.IsNullOrEmpty(shared))
                return;
            if (_smelter != null)
            {
                bool nowDenied = !StationPullFilter.IsDenied(_smelter, shared);
                StationPullFilter.SetDenied(_smelter, shared, nowDenied);
            }
            else if (_cook != null)
            {
                bool nowDenied = !StationPullFilter.IsDenied(_cook, shared);
                StationPullFilter.SetDenied(_cook, shared, nowDenied);
            }
            else
                return;
            RebuildRows(preserveScroll: true);
        }

        private static void SetLink(int linkId)
        {
            Component station = ActiveStation();
            if (station == null)
                return;
            StationLink.Toggle(station, linkId);
            if (_linkGrid != null)
                UiLinkGrid.RefreshSelection(_linkGrid, StationLink.Get(station));
        }

        private static Component ActiveStation()
        {
            return (Component)_smelter ?? _cook ?? (Component)_fermenter ?? _fire;
        }

        private static void AllowAll()
        {
            if (_smelter != null)
                StationPullFilter.Clear(_smelter);
            else if (_cook != null)
                StationPullFilter.Clear(_cook);
            else
                return;
            RebuildRows(preserveScroll: true);
        }

        private static void EnsureRoot()
        {
            if (_root != null)
                return;

            _root = new GameObject("SAC_StationFilter", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            dim.transform.SetParent(_root.transform, false);
            Stretch(dim.transform as RectTransform);
            Image dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;
            dim.GetComponent<Button>().transition = Selectable.Transition.None;
            dim.GetComponent<Button>().onClick.AddListener(Close);

            var host = new GameObject("Host", typeof(RectTransform));
            host.transform.SetParent(_root.transform, false);
            RectTransform hostRt = host.transform as RectTransform;
            hostRt.anchorMin = hostRt.anchorMax = new Vector2(0.5f, 0.5f);
            hostRt.pivot = new Vector2(0.5f, 0.5f);
            hostRt.sizeDelta = new Vector2(RefW, RefH);
            hostRt.anchoredPosition = Vector2.zero;

            BuildPanel(hostRt);
        }

        private static void DestroyRoot()
        {
            _listContent = null;
            _listScroll = null;
            _linkGrid = null;
            _panelRt = null;
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        private static void BuildPanel(RectTransform host)
        {
            var panel = new GameObject("PanelLeft", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(host, false);
            _panelRt = panel.transform as RectTransform;
            // Center the left panel on the 1920×1080 host.
            _panelRt.anchorMin = _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0.5f, 0.5f);
            _panelRt.sizeDelta = new Vector2(PanelW, PanelH);
            _panelRt.anchoredPosition = Vector2.zero;
            ApplySprite(_panelRt.GetComponent<Image>(), UiAssets.PanelLeft, Color.white);

            PlaceInsetBg(_panelRt, UiAssets.PanelLeftInset, InsetL, InsetT, InsetR, InsetB);
            PlaceHeader(_panelRt, Loc.T("Chest pull filter", "Chest pull filter"));

            var scrollGo = new GameObject("ListScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_panelRt, false);
            RectTransform scrollRt = scrollGo.transform as RectTransform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(ClipL + ContentPad, ClipB + ContentPad);
            scrollRt.offsetMax = new Vector2(-(ClipR + ContentPad), -(ClipT + ContentPad));
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0f);
            scrollBg.raycastTarget = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollRt, false);
            RectTransform vpRt = viewport.transform as RectTransform;
            Stretch(vpRt);
            Image vpImg = viewport.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0f);
            vpImg.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vpRt, false);
            _listContent = content.transform as RectTransform;
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = Vector2.zero;

            _listScroll = scrollGo.GetComponent<ScrollRect>();
            _listScroll.viewport = vpRt;
            _listScroll.content = _listContent;
            _listScroll.horizontal = false;
            _listScroll.vertical = true;
            _listScroll.movementType = ScrollRect.MovementType.Clamped;
            _listScroll.scrollSensitivity = 360f;
            _listScroll.inertia = true;
            _listScroll.verticalScrollbar = null;

            // Apply / Done — same spot as Select Types.
            var apply = new GameObject("Apply", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            apply.transform.SetParent(_panelRt, false);
            RectTransform applyRt = apply.transform as RectTransform;
            applyRt.anchorMin = applyRt.anchorMax = new Vector2(0.5f, 0f);
            applyRt.pivot = new Vector2(0.5f, 0f);
            applyRt.anchoredPosition = new Vector2(0f, 44f);
            applyRt.sizeDelta = new Vector2(ApplyW, ApplyH);
            ApplySprite(apply.GetComponent<Image>(), UiAssets.BtnApply, Color.white);
            Button applyBtn = apply.GetComponent<Button>();
            applyBtn.transition = Selectable.Transition.None;
            applyBtn.onClick.AddListener(Close);

            var applyTextGo = new GameObject("Text", typeof(RectTransform));
            applyTextGo.transform.SetParent(apply.transform, false);
            Stretch(applyTextGo.transform as RectTransform);
            TextMeshProUGUI applyTmp = UiFonts.CreateLabel(applyTextGo, ApplyFont);
            StyleLabel(applyTmp, ApplyFont, LetterSpace);
            applyTmp.alignment = TextAlignmentOptions.Center;
            applyTmp.color = Gold;
            applyTmp.text = Loc.T("Apply", "Apply");

            // Link buttons — same size/art, bottom-left of this panel (same relative spot as before).
            BuildLinkGrid();
        }

        private static void BuildLinkGrid()
        {
            if (_panelRt == null)
                return;
            Component station = ActiveStation();
            int current = StationLink.Get(station);
            _linkGrid = UiLinkGrid.Build(_panelRt, "SAC_StationLinks", current, id => SetLink(id));
            RectTransform rt = _linkGrid.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(14f, 10f);
            rt.SetAsLastSibling();
        }

        private static void RebuildRows(bool preserveScroll = false)
        {
            EnsureRoot();
            if (_listContent == null)
                return;

            float scrollPos = _listScroll != null ? _listScroll.verticalNormalizedPosition : 1f;
            ClearContent(_listContent);

            List<string> choices = _cook != null
                ? StationPullFilter.FoodChoices(_cook)
                : (_smelter != null ? StationPullFilter.OreChoices(_smelter) : new List<string>());

            int rowIndex = 0;
            Component station = ActiveStation();

            for (int i = 0; i < choices.Count; i++)
            {
                string shared = choices[i];
                bool allowed = _cook != null
                    ? StationPullFilter.IsAllowed(_cook, shared)
                    : StationPullFilter.IsAllowed(_smelter, shared);
                string label = StationPullFilter.DisplayName(shared);
                string captured = shared;
                AddToggleRow(rowIndex++, label, allowed, () => Toggle(captured));
            }

            if (_smelter != null || _cook != null)
                AddActionRow(rowIndex++, Loc.T("Allow all inputs", "Allow all inputs"), AllowAll);

            _listContent.sizeDelta = new Vector2(0f, rowIndex * (RowH + RowGap) + 4f);
            if (_listScroll != null)
                _listScroll.verticalNormalizedPosition = preserveScroll ? scrollPos : 1f;

            if (_linkGrid != null)
                UiLinkGrid.RefreshSelection(_linkGrid, StationLink.Get(station));
            else
                BuildLinkGrid();
        }

        private static void AddToggleRow(int index, string label, bool on, UnityAction onToggle)
        {
            var row = new GameObject("Row" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            row.transform.SetParent(_listContent, false);
            RectTransform rt = row.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(RowW, RowH);
            rt.anchoredPosition = new Vector2(RowPadL, -index * (RowH + RowGap));

            Sprite rowSp = on ? UiAssets.RawCategoryFocus : UiAssets.RawCategory;
            Image rowImg = row.GetComponent<Image>();
            if (rowSp != null)
            {
                rowImg.sprite = rowSp;
                rowImg.type = Image.Type.Simple;
                rowImg.preserveAspect = false;
                rowImg.color = Color.white;
            }
            else
            {
                rowImg.color = new Color(0.2f, 0.15f, 0.1f, 0.9f);
            }
            rowImg.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            RectTransform labelRt = labelGo.transform as RectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(36f, 2f);
            labelRt.offsetMax = new Vector2(-(ToggleW + 36f), -2f);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(labelGo, RowFont);
            StyleLabel(tmp, RowFont, LetterSpace);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = on ? OnGreen : OffRed;
            tmp.text = label;

            GameObject toggle = UiToggle.Create(
                rt, "Toggle", on, true, onToggle, UiFonts.ThinNorse(),
                ToggleW, ToggleH);
            RectTransform togRt = toggle.transform as RectTransform;
            togRt.anchorMin = new Vector2(1f, 0.5f);
            togRt.anchorMax = new Vector2(1f, 0.5f);
            togRt.pivot = new Vector2(1f, 0.5f);
            togRt.anchoredPosition = new Vector2(-28f, 0f);
            togRt.sizeDelta = new Vector2(ToggleW, ToggleH);
            Image togImg = toggle.GetComponent<Image>();
            if (togImg != null)
                togImg.preserveAspect = true;
            togRt.SetAsLastSibling();
        }

        private static void AddActionRow(int index, string label, UnityAction action)
        {
            var row = new GameObject("Action" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            row.transform.SetParent(_listContent, false);
            RectTransform rt = row.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(RowW, RowH);
            rt.anchoredPosition = new Vector2(RowPadL, -index * (RowH + RowGap));

            Image rowImg = row.GetComponent<Image>();
            if (UiAssets.RawCategory != null)
            {
                rowImg.sprite = UiAssets.RawCategory;
                rowImg.type = Image.Type.Simple;
                rowImg.color = Color.white;
            }
            else
            {
                rowImg.color = new Color(0.2f, 0.15f, 0.1f, 0.9f);
            }
            rowImg.raycastTarget = true;

            Button btn = row.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(action);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            Stretch(labelGo.transform as RectTransform);
            RectTransform labelRt = labelGo.transform as RectTransform;
            labelRt.offsetMin = new Vector2(36f, 2f);
            labelRt.offsetMax = new Vector2(-16f, -2f);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(labelGo, RowFont);
            StyleLabel(tmp, RowFont, LetterSpace);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Gold;
            tmp.text = label;
        }

        private static void PlaceInsetBg(RectTransform panel, Sprite sprite, float padL, float padT, float padR, float padB)
        {
            if (sprite == null)
                return;
            var go = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(panel, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padL, padB);
            rt.offsetMax = new Vector2(-padR, -padT);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private static void PlaceHeader(RectTransform panel, string text)
        {
            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float bandMid = InsetT * 0.5f + 10f;
            rt.anchoredPosition = new Vector2(0f, -bandMid);
            rt.sizeDelta = new Vector2(-80f, InsetT - 8f);
            TextMeshProUGUI tmp = UiFonts.CreateBoldLabel(go, HeaderFont);
            tmp.enableAutoSizing = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.maxVisibleLines = 1;
            tmp.raycastTarget = false;
            tmp.characterSpacing = LetterSpace;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Gold;
            tmp.text = text;
        }

        private static void StyleLabel(TMP_Text tmp, float size, float letterSpace = 0f)
        {
            UiFonts.StyleThinLabel(tmp, size);
            tmp.enableAutoSizing = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.maxVisibleLines = 1;
            tmp.raycastTarget = false;
            tmp.characterSpacing = letterSpace;
        }

        private static void ApplySprite(Image img, Sprite sprite, Color fallback)
        {
            if (img == null)
                return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
            }
            img.raycastTarget = true;
        }

        private static void ClearContent(RectTransform content)
        {
            if (content == null)
                return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
    internal static class StationFilterInventoryVisiblePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (StationFilterMenu.IsOpen)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class StationFilterInventoryShowPatch
    {
        private static bool Prefix()
        {
            if (!StationFilterMenu.IsOpen)
                return true;
            StationFilterMenu.Close();
            return true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    internal static class StationFilterInventoryHidePatch
    {
        private static void Postfix()
        {
            if (StationFilterMenu.IsOpen)
                StationFilterMenu.Close();
        }
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
    internal static class StationFilterPausePatch
    {
        private static bool Prefix()
        {
            if (!StationFilterMenu.ShouldBlockPause())
                return true;
            StationFilterMenu.Close();
            return false;
        }
    }
}
