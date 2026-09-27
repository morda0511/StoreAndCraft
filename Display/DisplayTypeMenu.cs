using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Select Types — standalone overlay matching the 1920×1080 Photoshop mockup.
    /// Left: categories. Right: item grid. Uses Content/UI PS assets + UiToggle.
    /// </summary>
    internal static class DisplayTypeMenu
    {
        public static bool IsOpen { get; private set; }

        // Mockup reference layout (1920×1080) — clean panels + separate insets
        private const float RefW = 1920f;
        private const float RefH = 1080f;
        private const float LeftX = 111f;
        private const float LeftY = 90f;
        private const float LeftW = 552f;
        private const float LeftH = 887f;
        private const float RightX = 669f;
        private const float RightY = 88f;
        private const float RightW = 1053f;
        private const float RightH = 882f;

        // Dark inset SPRITE — stretch to white OUTER so the well is fully dark (no light panel peeking)
        private const float CatInsetL = 40f;
        private const float CatInsetT = 74f;
        private const float CatInsetR = 32f;
        private const float CatInsetB = 93f;

        // Clip + scrollbar = white INNER hole of panel_*_inset_rim (ignore gray)
        private const float CatClipL = 48f;
        private const float CatClipT = 86f;
        private const float CatClipR = 40f;
        private const float CatClipB = 105f;

        private const float CatRowW = 400f;
        private const float CatRowH = 54f;
        private const float CatRowGap = 8f;
        private const float CatContentPadL = 16f;

        private const float ItemInsetL = 50f;
        private const float ItemInsetT = 77f;
        private const float ItemInsetR = 41f;
        private const float ItemInsetB = 32f;

        private const float ItemClipL = 67f;
        private const float ItemClipT = 90f;
        private const float ItemClipR = 58f;
        private const float ItemClipB = 45f;

        private const float ItemCellW = 148f;
        private const float ItemCellH = 108f;
        private const float ItemGapX = 16f;
        private const float ItemGapY = 16f;
        private const float ItemContentPadL = 28f;
        private const float ItemContentPadT = 8f;
        private const int ItemColumns = 5;

        // Small gap so rows/cells don't sit on the white rim line
        private const float ContentPad = 12f;

        private const float ToggleW = 36f;
        private const float ToggleH = 18f;
        private const float ApplyW = 140f;
        private const float ApplyH = 38f;
        private const float LetterSpace = 14f;
        private const float CatFont = 24f;
        private const float ItemFont = 16f;
        private const float ApplyFont = 20f;
        private const float HeaderFont = 22f;

        private static StorageDisplayBoard _board;
        private static float _openedAt;
        private static int _suppressMenuFrame;
        private static bool _closing;
        private static int _focusId;
        private static bool _pendingItems;

        private static GameObject _root;
        private static RectTransform _catContent;
        private static RectTransform _itemContent;
        private static ScrollRect _catScroll;
        private static ScrollRect _itemScroll;

        private static readonly Dictionary<string, Sprite> _iconCache =
            new Dictionary<string, Sprite>(System.StringComparer.Ordinal);

        private static readonly Color Gold = new Color(0.925f, 0.77f, 0.29f, 1f);
        private static readonly Color Muted = new Color(0.70f, 0.64f, 0.52f, 1f);

        public static void Open(StorageDisplayBoard board)
        {
            if (board == null || Player.m_localPlayer == null)
                return;

            if (IsOpen)
                Close();
            if (DisplayRangeMenu.IsOpen)
                DisplayRangeMenu.Close();
            if (DisplaySmallOptions.IsOpen)
                DisplaySmallOptions.Close();
            if (StationFilterMenu.IsOpen)
                StationFilterMenu.Close();

            if (InventoryGui.instance != null && InventoryGui.IsVisible())
                InventoryGui.instance.Hide();

            _board = board;
            _openedAt = Time.unscaledTime;
            _focusId = 0;

            UiFonts.ThinNorse();
            ItemIds.PrefabFromToken("$item_wood");

            EnsureRoot();
            if (_root == null)
                return;
            _root.SetActive(true);
            RebuildUi(buildItems: false);
            _pendingItems = true;
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
                _board = null;
                _focusId = 0;
                _pendingItems = false;
                _suppressMenuFrame = Time.frameCount;
                DestroyRoot();
            }
            finally
            {
                _closing = false;
            }
        }

        public static void CloseIf(StorageDisplayBoard board)
        {
            if (IsOpen && _board == board)
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
            if (_board == null || Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            if (_pendingItems)
            {
                _pendingItems = false;
                RebuildUi(buildItems: true, preserveCatScroll: false, resetItemScroll: true);
            }

            if (Time.unscaledTime < _openedAt + 0.15f)
                return;
            if (ZInput.GetKeyDown(KeyCode.Escape, true) || ZInput.GetButtonDown("JoyButtonB"))
                Close();
        }

        private static void ToggleCategory(int id)
        {
            if (_board == null || id <= 0)
                return;
            _board.ToggleFilter(id);
            bool sameFocus = _focusId == id;
            _focusId = id;
            _pendingItems = false;
            RebuildUi(buildItems: true, preserveCatScroll: true, resetItemScroll: !sameFocus);
        }

        private static void FocusCategory(int id)
        {
            if (id <= 0)
                return;
            bool changed = _focusId != id;
            _focusId = id;
            _pendingItems = false;
            RebuildUi(buildItems: true, preserveCatScroll: true, resetItemScroll: changed);
        }

        private static void PickItem(string shared, int parentFilterId)
        {
            if (_board == null || string.IsNullOrEmpty(shared))
                return;
            _board.ToggleItemToken(shared, parentFilterId);
            _pendingItems = false;
            RebuildUi(buildItems: true, preserveCatScroll: true, resetItemScroll: false);
        }

        private static void PickEpicLootSub(int subFilterId)
        {
            if (_board == null || subFilterId <= 0)
                return;
            _board.ToggleFilter(subFilterId);
            _pendingItems = false;
            RebuildUi(buildItems: true, preserveCatScroll: true, resetItemScroll: false);
        }

        private static void EnsureRoot()
        {
            if (_root != null)
                return;

            _root = new GameObject("SAC_SelectTypes", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;

            // Dim
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            dim.transform.SetParent(_root.transform, false);
            Stretch(dim.transform as RectTransform);
            Image dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;
            dim.GetComponent<Button>().transition = Selectable.Transition.None;
            dim.GetComponent<Button>().onClick.AddListener(Close);

            // 1920×1080 reference host (centered)
            var host = new GameObject("Host", typeof(RectTransform));
            host.transform.SetParent(_root.transform, false);
            RectTransform hostRt = host.transform as RectTransform;
            hostRt.anchorMin = hostRt.anchorMax = new Vector2(0.5f, 0.5f);
            hostRt.pivot = new Vector2(0.5f, 0.5f);
            hostRt.sizeDelta = new Vector2(RefW, RefH);
            hostRt.anchoredPosition = Vector2.zero;

            BuildLeftPanel(hostRt);
            BuildRightPanel(hostRt);
        }

        private static void DestroyRoot()
        {
            _catContent = null;
            _itemContent = null;
            _catScroll = null;
            _itemScroll = null;
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        private static void BuildLeftPanel(RectTransform host)
        {
            var panel = new GameObject("PanelLeft", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(host, false);
            RectTransform rt = panel.transform as RectTransform;
            PlaceTopLeft(rt, LeftX, LeftY, LeftW, LeftH);
            ApplySprite(panel.GetComponent<Image>(), UiAssets.PanelLeft, Color.white);

            // Layer: panel → inset → content (clip to white rim, not gray halo)
            PlaceInsetBg(rt, UiAssets.PanelLeftInset, CatInsetL, CatInsetT, CatInsetR, CatInsetB);
            PlacePanelHeader(rt, Loc.T("Select type", "Select type"), CatInsetT);

            var scrollGo = new GameObject("CatScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(rt, false);
            RectTransform scrollRt = scrollGo.transform as RectTransform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(CatClipL + ContentPad, CatClipB + ContentPad);
            scrollRt.offsetMax = new Vector2(-(CatClipR + ContentPad), -(CatClipT + ContentPad));
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0f);
            scrollBg.raycastTarget = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollRt, false);
            RectTransform vpRt = viewport.transform as RectTransform;
            Stretch(vpRt);
            Image vpImg = viewport.GetComponent<Image>();
            // Must stay fully transparent — even 1% white washes the dark inset lighter.
            vpImg.color = new Color(1f, 1f, 1f, 0f);
            vpImg.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vpRt, false);
            _catContent = content.transform as RectTransform;
            _catContent.anchorMin = new Vector2(0f, 1f);
            _catContent.anchorMax = new Vector2(1f, 1f);
            _catContent.pivot = new Vector2(0.5f, 1f);
            _catContent.anchoredPosition = Vector2.zero;
            _catContent.sizeDelta = Vector2.zero;

            _catScroll = scrollGo.GetComponent<ScrollRect>();
            _catScroll.viewport = vpRt;
            _catScroll.content = _catContent;
            _catScroll.horizontal = false;
            _catScroll.vertical = true;
            _catScroll.movementType = ScrollRect.MovementType.Clamped;
            _catScroll.scrollSensitivity = 360f;
            _catScroll.inertia = true;
            _catScroll.verticalScrollbar = null;

            // Apply button (mockup: below dark inset)
            var apply = new GameObject("Apply", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            apply.transform.SetParent(rt, false);
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
        }

        private static void BuildRightPanel(RectTransform host)
        {
            var panel = new GameObject("PanelRight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(host, false);
            RectTransform rt = panel.transform as RectTransform;
            PlaceTopLeft(rt, RightX, RightY, RightW, RightH);
            ApplySprite(panel.GetComponent<Image>(), UiAssets.PanelRight, Color.white);

            PlaceInsetBg(rt, UiAssets.PanelRightInset, ItemInsetL, ItemInsetT, ItemInsetR, ItemInsetB);
            PlacePanelHeader(rt, Loc.T("Filter", "Filter"), ItemInsetT);

            var scrollGo = new GameObject("ItemScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(rt, false);
            RectTransform scrollRt = scrollGo.transform as RectTransform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(ItemClipL + ContentPad, ItemClipB + ContentPad);
            scrollRt.offsetMax = new Vector2(-(ItemClipR + ContentPad), -(ItemClipT + ContentPad));
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
            _itemContent = content.transform as RectTransform;
            _itemContent.anchorMin = new Vector2(0f, 1f);
            _itemContent.anchorMax = new Vector2(1f, 1f);
            _itemContent.pivot = new Vector2(0.5f, 1f);
            _itemContent.anchoredPosition = Vector2.zero;
            _itemContent.sizeDelta = Vector2.zero;

            _itemScroll = scrollGo.GetComponent<ScrollRect>();
            _itemScroll.viewport = vpRt;
            _itemScroll.content = _itemContent;
            _itemScroll.horizontal = false;
            _itemScroll.vertical = true;
            _itemScroll.movementType = ScrollRect.MovementType.Clamped;
            _itemScroll.scrollSensitivity = 540f;
            _itemScroll.inertia = true;
            _itemScroll.verticalScrollbar = null;
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

        private static void PlacePanelHeader(RectTransform panel, string text, float insetTop)
        {
            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Slightly below band center so it sits closer to the inset.
            float bandMid = insetTop * 0.5f + 10f;
            rt.anchoredPosition = new Vector2(0f, -bandMid);
            rt.sizeDelta = new Vector2(-80f, insetTop - 8f);
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

        private static void ClearContent(RectTransform content)
        {
            if (content == null)
                return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }

        private static void RebuildUi(bool buildItems, bool preserveCatScroll = false, bool resetItemScroll = true)
        {
            EnsureRoot();
            if (_catContent == null || _itemContent == null)
                return;

            float catScrollPos = _catScroll != null ? _catScroll.verticalNormalizedPosition : 1f;
            float itemScrollPos = _itemScroll != null ? _itemScroll.verticalNormalizedPosition : 1f;

            ClearContent(_catContent);
            if (buildItems)
                ClearContent(_itemContent);

            List<int> selected = _board != null ? _board.FilterIds() : new List<int>();
            List<string> selectedItems = _board != null ? _board.ItemTokens() : new List<string>();
            var selectedSet = new HashSet<int>(selected);
            var selectedItemSet = new HashSet<string>(selectedItems, System.StringComparer.Ordinal);
            var partialParents = new HashSet<int>();
            foreach (string token in selectedItemSet)
            {
                int parent = DisplayFilters.ParentFilterIdFromToken(token);
                if (parent > 0)
                    partialParents.Add(parent);
            }
            EnsureFocus(selected, selectedItems);

            int catN = 0;
            for (int i = 0; i < DisplayFilters.Choices.Length; i++)
            {
                DisplayFilter choice = DisplayFilters.Choices[i];
                int id = choice.Id;
                if (DisplayFilters.IsEpicLootSubFilter(id))
                    continue;
                bool on = CategoryIsOn(id, selectedSet, partialParents);
                bool focused = id == _focusId;
                int captured = id;
                AddCategoryRow(catN++, choice.Label(), on, focused,
                    () => FocusCategory(captured),
                    () => ToggleCategory(captured));
            }
            _catContent.sizeDelta = new Vector2(0f, catN * (CatRowH + CatRowGap) + 4f);

            if (_catScroll != null)
                _catScroll.verticalNormalizedPosition = preserveCatScroll ? catScrollPos : 1f;

            if (!buildItems)
                return;

            var cells = new List<ItemCellData>();
            if (_focusId == DisplayFilters.EpicLootGroupId)
            {
                bool allOn = selectedSet.Contains(DisplayFilters.EpicLootGroupId);
                cells.Add(new ItemCellData
                {
                    Label = Loc.T("All", "All") + " Epic Loot",
                    On = allOn,
                    Icon = ItemIcon(DisplayFilters.RepresentativeShared(DisplayFilters.ElDustFilterId)),
                    Click = () => ToggleCategory(DisplayFilters.EpicLootGroupId)
                });
                for (int s = 0; s < DisplayFilters.EpicLootSubFilterIds.Length; s++)
                {
                    int subId = DisplayFilters.EpicLootSubFilterIds[s];
                    DisplayFilter sub;
                    if (!DisplayFilters.TryGet(subId, out sub))
                        continue;
                    int capturedSub = subId;
                    cells.Add(new ItemCellData
                    {
                        Label = sub.Label(),
                        On = selectedSet.Contains(subId),
                        Icon = ItemIcon(DisplayFilters.RepresentativeShared(subId)),
                        Click = () => PickEpicLootSub(capturedSub)
                    });
                }

                // Individual craft mats under the group (with sprites).
                List<string> elItems = DisplayFilters.SubItems(DisplayFilters.EpicLootGroupId);
                if (elItems != null)
                {
                    for (int s = 0; s < elItems.Count; s++)
                    {
                        string shared = elItems[s];
                        string captured = shared;
                        int parent = DisplayFilters.ParentFilterIdFromToken(shared);
                        if (parent <= 0)
                            parent = DisplayFilters.EpicLootGroupId;
                        int capturedParent = parent;
                        cells.Add(new ItemCellData
                        {
                            Label = DisplayFilters.ItemLabel(shared),
                            On = allOn
                                || selectedSet.Contains(parent)
                                || selectedItemSet.Contains(shared),
                            Icon = ItemIcon(shared),
                            Click = () => PickItem(captured, capturedParent)
                        });
                    }
                }
            }
            else
            {
                List<string> items = DisplayFilters.SubItems(_focusId);
                if (items != null)
                {
                    bool categoryOn = selectedSet.Contains(_focusId);
                    for (int s = 0; s < items.Count; s++)
                    {
                        string shared = items[s];
                        string captured = shared;
                        int parent = _focusId;
                        cells.Add(new ItemCellData
                        {
                            Label = DisplayFilters.ItemLabel(shared),
                            On = categoryOn || selectedItemSet.Contains(shared),
                            Icon = ItemIcon(shared),
                            Click = () => PickItem(captured, parent)
                        });
                    }
                }
            }

            BuildItemGrid(cells);
            if (_itemScroll != null)
                _itemScroll.verticalNormalizedPosition = resetItemScroll ? 1f : itemScrollPos;
        }

        private struct ItemCellData
        {
            public string Label;
            public bool On;
            public Sprite Icon;
            public UnityAction Click;
        }

        private static void AddCategoryRow(
            int index,
            string label,
            bool on,
            bool focused,
            UnityAction onFocus,
            UnityAction onToggle)
        {
            var row = new GameObject("Cat" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            row.transform.SetParent(_catContent, false);
            RectTransform rt = row.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(CatRowW, CatRowH);
            rt.anchoredPosition = new Vector2(CatContentPadL, -index * (CatRowH + CatRowGap));

            Sprite rowSp = focused ? UiAssets.RawCategoryFocus : UiAssets.RawCategory;
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

            // Focus click (left part)
            var hit = new GameObject("Focus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            hit.transform.SetParent(rt, false);
            RectTransform hitRt = hit.transform as RectTransform;
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = Vector2.one;
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = new Vector2(-(ToggleW + 12f), 0f);
            Image hitImg = hit.GetComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0.001f);
            hitImg.raycastTarget = true;
            Button hb = hit.GetComponent<Button>();
            hb.transition = Selectable.Transition.None;
            if (onFocus != null)
                hb.onClick.AddListener(onFocus);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            RectTransform labelRt = labelGo.transform as RectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(36f, 2f);
            labelRt.offsetMax = new Vector2(-(ToggleW + 14f), -2f);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(labelGo, CatFont);
            StyleLabel(tmp, CatFont, LetterSpace);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = focused || on ? Gold : Muted;
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

        private static void BuildItemGrid(List<ItemCellData> cells)
        {
            int n = cells.Count;
            int cols = ItemColumns;
            int rows = Mathf.Max(1, (n + cols - 1) / cols);

            for (int i = 0; i < n; i++)
            {
                int col = i % cols;
                int row = i / cols;
                AddItemCell(col, row, cells[i]);
            }

            _itemContent.sizeDelta = new Vector2(0f, ItemContentPadT + rows * (ItemCellH + ItemGapY) + 8f);
            _itemContent.anchoredPosition = Vector2.zero;
        }

        private static void AddItemCell(int col, int row, ItemCellData data)
        {
            var go = new GameObject("Item" + row + "_" + col, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_itemContent, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ItemCellW, ItemCellH);
            rt.anchoredPosition = new Vector2(
                ItemContentPadL + col * (ItemCellW + ItemGapX),
                -(ItemContentPadT + row * (ItemCellH + ItemGapY)));
            ApplySprite(go.GetComponent<Image>(), UiAssets.ItemCell, Color.white);
            Image cellImg = go.GetComponent<Image>();
            if (cellImg != null && cellImg.sprite != null)
            {
                cellImg.type = Image.Type.Simple;
                cellImg.preserveAspect = false;
            }

            // Icon top-center
            if (data.Icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(rt, false);
                RectTransform iconRt = iconGo.transform as RectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 1f);
                iconRt.anchorMax = new Vector2(0.5f, 1f);
                iconRt.pivot = new Vector2(0.5f, 1f);
                iconRt.anchoredPosition = new Vector2(0f, -10f);
                iconRt.sizeDelta = new Vector2(52f, 52f);
                Image iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = data.Icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.color = Color.white;
            }

            // Name under icon
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            RectTransform labelRt = labelGo.transform as RectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(4f, ToggleH + 6f);
            labelRt.offsetMax = new Vector2(-4f, -50f);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(labelGo, ItemFont);
            StyleLabel(tmp, ItemFont, LetterSpace * 0.7f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.maxVisibleLines = 2;
            tmp.color = data.On ? Gold : Muted;
            tmp.text = data.Label;

            // Toggle bottom-right (mockup)
            GameObject toggle = UiToggle.Create(
                rt, "Toggle", data.On, true, data.Click, UiFonts.ThinNorse(),
                ToggleW, ToggleH);
            RectTransform togRt = toggle.transform as RectTransform;
            togRt.anchorMin = new Vector2(1f, 0f);
            togRt.anchorMax = new Vector2(1f, 0f);
            togRt.pivot = new Vector2(1f, 0f);
            togRt.anchoredPosition = new Vector2(-6f, 6f);
            togRt.sizeDelta = new Vector2(ToggleW, ToggleH);
            Image togImg = toggle.GetComponent<Image>();
            if (togImg != null)
                togImg.preserveAspect = true;
            togRt.SetAsLastSibling();
        }

        private static Sprite ItemIcon(string shared)
        {
            if (string.IsNullOrEmpty(shared))
                return null;
            Sprite cached;
            if (_iconCache.TryGetValue(shared, out cached))
                return cached;
            GameObject prefab = ItemIds.PrefabFromToken(shared);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            Sprite icon = drop?.m_itemData != null ? StackLimits.Icon(drop.m_itemData) : null;
            _iconCache[shared] = icon;
            return icon;
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

        private static void PlaceTopLeft(RectTransform rt, float x, float yFromTop, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void EnsureFocus(List<int> selected, List<string> selectedItems)
        {
            if (_focusId > 0 && !DisplayFilters.IsEpicLootSubFilter(_focusId))
            {
                DisplayFilter unused;
                if (DisplayFilters.TryGet(_focusId, out unused))
                    return;
            }
            for (int i = 0; i < selected.Count; i++)
            {
                int id = selected[i];
                if (DisplayFilters.IsEpicLootSubFilter(id))
                {
                    _focusId = DisplayFilters.EpicLootGroupId;
                    return;
                }
                if (id > 0)
                {
                    _focusId = id;
                    return;
                }
            }
            for (int i = 0; i < selectedItems.Count; i++)
            {
                int parent = DisplayFilters.ParentFilterIdFromToken(selectedItems[i]);
                if (parent > 0)
                {
                    _focusId = parent;
                    return;
                }
            }
            for (int i = 0; i < DisplayFilters.Choices.Length; i++)
            {
                int id = DisplayFilters.Choices[i].Id;
                if (DisplayFilters.IsEpicLootSubFilter(id))
                    continue;
                _focusId = id;
                return;
            }
            _focusId = 0;
        }

        private static bool CategoryIsOn(int id, HashSet<int> selected, HashSet<int> partialParents)
        {
            if (selected.Contains(id))
                return true;
            if (id == DisplayFilters.EpicLootGroupId)
            {
                for (int s = 0; s < DisplayFilters.EpicLootSubFilterIds.Length; s++)
                {
                    if (selected.Contains(DisplayFilters.EpicLootSubFilterIds[s]))
                        return true;
                }
                return false;
            }
            return partialParents != null && partialParents.Contains(id);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
    internal static class DisplayTypeMenuInventoryVisiblePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (DisplayTypeMenu.IsOpen)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class DisplayTypeMenuInventoryShowPatch
    {
        private static bool Prefix()
        {
            if (!DisplayTypeMenu.IsOpen)
                return true;
            DisplayTypeMenu.Close();
            return true;
        }
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
    internal static class DisplayTypeMenuPausePatch
    {
        private static bool Prefix()
        {
            if (!DisplayTypeMenu.ShouldBlockPause())
                return true;
            DisplayTypeMenu.Close();
            return false;
        }
    }
}
