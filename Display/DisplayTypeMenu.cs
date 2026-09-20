using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Select Types mockup layout: Categories box (scroll) on top, Items box (scroll) below,
    /// Cancel / Apply. Uses real UiToggle sprites. No skills-prefab icon squares.
    /// </summary>
    internal static class DisplayTypeMenu
    {
        public static bool IsOpen { get; private set; }

        private const float RowH = 36f;
        private const float IconSize = 28f;
        private const float Pad = 8f;

        private static StorageDisplayBoard _board;
        private static SkillsDialog _skills;
        private static bool _skillsWasEnabled;
        private static bool _openedInventory;
        private static float _openedAt;
        private static int _suppressMenuFrame;
        private static bool _closing;
        private static int _focusId;
        private static bool _listWasActive = true;

        private static readonly List<GameObject> _hiddenVanilla = new List<GameObject>();
        private static readonly List<TMP_Text> _titleTexts = new List<TMP_Text>();
        private static readonly List<string> _titleBackup = new List<string>();
        private static readonly List<bool> _titleLocalize = new List<bool>();

        private static GameObject _host;
        private static RectTransform _catContent;
        private static RectTransform _itemContent;
        private static TMP_Text _itemHeader;
        private static GameObject _itemBox;
        private static bool _pendingItems;
        private static readonly Dictionary<string, Sprite> _iconCache =
            new Dictionary<string, Sprite>(System.StringComparer.Ordinal);

        private static readonly Color Gold = new Color(1f, 0.85f, 0.4f, 1f);
        private static readonly Color Cream = new Color(0.95f, 0.92f, 0.82f, 1f);
        private static readonly Color Muted = new Color(0.7f, 0.65f, 0.55f, 1f);
        private static readonly Color BoxBg = new Color(0.14f, 0.09f, 0.05f, 0.94f);
        private static readonly Color BoxBorder = new Color(0.42f, 0.30f, 0.16f, 1f);
        private static readonly Color RowBg = new Color(0.18f, 0.12f, 0.07f, 0.45f);
        private static readonly Color RowFocus = new Color(0.34f, 0.24f, 0.12f, 0.95f);

        public static void Open(StorageDisplayBoard board)
        {
            if (board == null || Player.m_localPlayer == null)
                return;

            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_skillsDialog == null)
                return;

            if (IsOpen)
                Close();
            if (DisplayRangeMenu.IsOpen)
                DisplayRangeMenu.Close();
            if (DisplaySmallOptions.IsOpen)
                DisplaySmallOptions.Close();

            // Keep sub-item caches warm — invalidating here caused multi-second Freezes on open.
            _board = board;
            _skills = gui.m_skillsDialog;
            _openedAt = Time.unscaledTime;
            _openedInventory = false;
            _skillsWasEnabled = _skills.enabled;
            _skills.enabled = false;

            GameObject panel = _skills.gameObject;
            panel.SetActive(true);
            if (!panel.activeInHierarchy)
            {
                gui.Show(null, 0);
                _openedInventory = true;
                panel.SetActive(true);
            }

            panel.transform.SetAsLastSibling();
            HideVanillaRows();
            ApplyTitle();
            // Warm caches before building rows (avoids LiberationSans spam + PrefabFromToken scans).
            UiFonts.ThinNorse();
            ItemIds.PrefabFromToken("$item_wood");
            EnsureHost();
            if (_host != null)
                _host.SetActive(true);
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
                bool wasOpen = IsOpen;
                bool openedInv = _openedInventory;
                SkillsDialog skills = _skills;
                IsOpen = false;
                _openedInventory = false;
                _board = null;
                _focusId = 0;
                _suppressMenuFrame = Time.frameCount;

                DestroyHost();
                _pendingItems = false;
                RestoreVanillaRows();
                RestoreTitle();

                if (skills != null)
                {
                    skills.enabled = _skillsWasEnabled;
                    if (skills.gameObject.activeSelf)
                        skills.OnClose();
                }
                _skills = null;

                if (wasOpen && openedInv && InventoryGui.instance != null)
                    InventoryGui.instance.Hide();
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
            if (_board == null || _skills == null || Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            if (_pendingItems)
            {
                _pendingItems = false;
                RebuildUi(buildItems: true);
            }

            if (Time.unscaledTime < _openedAt + 0.2f)
                return;
            if (ZInput.GetKeyDown(KeyCode.Escape, true) || ZInput.GetButtonDown("JoyButtonB"))
                Close();
        }

        internal static void OnSkillsClosed(SkillsDialog dialog)
        {
            if (!IsOpen || _closing || dialog == null || dialog != _skills)
                return;
            Close();
        }

        private static void ToggleCategory(int id)
        {
            if (_board == null || id <= 0)
                return;
            _board.ToggleFilter(id);
            _focusId = id;
            _pendingItems = false;
            RebuildUi(buildItems: true);
        }

        private static void FocusCategory(int id)
        {
            if (id <= 0)
                return;
            _focusId = id;
            _pendingItems = false;
            RebuildUi(buildItems: true);
        }

        private static void PickItem(string shared, int parentFilterId)
        {
            if (_board == null)
                return;
            _board.ToggleItemToken(shared, parentFilterId);
            if (parentFilterId > 0)
                _focusId = parentFilterId;
            _pendingItems = false;
            RebuildUi(buildItems: true);
        }

        private static void PickEpicLootSub(int subFilterId)
        {
            if (_board == null || !DisplayFilters.IsEpicLootSubFilter(subFilterId))
                return;
            _board.ToggleFilter(subFilterId);
            _focusId = DisplayFilters.EpicLootGroupId;
            _pendingItems = false;
            RebuildUi(buildItems: true);
        }

        private static void HideVanillaRows()
        {
            _hiddenVanilla.Clear();
            if (_skills == null || _skills.m_listRoot == null)
                return;
            for (int i = 0; i < _skills.m_listRoot.childCount; i++)
            {
                GameObject child = _skills.m_listRoot.GetChild(i).gameObject;
                if (!child.activeSelf)
                    continue;
                child.SetActive(false);
                _hiddenVanilla.Add(child);
            }
        }

        private static void RestoreVanillaRows()
        {
            for (int i = 0; i < _hiddenVanilla.Count; i++)
            {
                if (_hiddenVanilla[i] != null)
                    _hiddenVanilla[i].SetActive(true);
            }
            _hiddenVanilla.Clear();
            if (_skills != null && _skills.m_listRoot != null)
                _skills.m_listRoot.gameObject.SetActive(_listWasActive);
        }

        private static void EnsureHost()
        {
            if (_host != null || _skills == null)
                return;

            Transform parent = _skills.m_listRoot != null && _skills.m_listRoot.parent != null
                ? _skills.m_listRoot.parent
                : _skills.transform;

            if (_skills.m_listRoot != null)
            {
                _listWasActive = _skills.m_listRoot.gameObject.activeSelf;
                _skills.m_listRoot.gameObject.SetActive(false);
            }

            if (_skills.m_totalSkillText != null)
                _skills.m_totalSkillText.gameObject.SetActive(false);

            _host = new GameObject("SAC_TypeMenuHost", typeof(RectTransform));
            _host.transform.SetParent(parent, false);
            RectTransform hostRt = _host.transform as RectTransform;
            Stretch(hostRt);
            hostRt.offsetMin = new Vector2(8f, 8f);
            hostRt.offsetMax = new Vector2(-8f, -8f);

            // Top: Categories box (~50%)
            MakeScrollBox(hostRt, "CatBox",
                new Vector2(0f, 0.50f), new Vector2(1f, 1f),
                Loc.T("Categories", "Categories"),
                out _catContent, out _);

            // Bottom: Items box
            RectTransform itemBoxRt = MakeScrollBox(hostRt, "ItemBox",
                new Vector2(0f, 0.12f), new Vector2(1f, 0.48f),
                Loc.T("Items", "Items"),
                out _itemContent, out _itemHeader);
            _itemBox = itemBoxRt.gameObject;

            BuildFooter(hostRt);
        }

        private static void DestroyHost()
        {
            _catContent = null;
            _itemContent = null;
            _itemHeader = null;
            _itemBox = null;
            if (_host != null)
            {
                Object.Destroy(_host);
                _host = null;
            }
            if (_skills != null && _skills.m_totalSkillText != null)
                _skills.m_totalSkillText.gameObject.SetActive(true);
        }

        private static RectTransform MakeScrollBox(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string header,
            out RectTransform content,
            out TMP_Text headerLabel)
        {
            var box = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(parent, false);
            RectTransform boxRt = box.transform as RectTransform;
            boxRt.anchorMin = anchorMin;
            boxRt.anchorMax = anchorMax;
            boxRt.offsetMin = new Vector2(0f, 2f);
            boxRt.offsetMax = new Vector2(0f, -2f);
            Image boxImg = box.GetComponent<Image>();
            Sprite panel = UiAssets.PanelWood;
            if (panel != null)
            {
                boxImg.sprite = panel;
                boxImg.type = Image.Type.Sliced;
                boxImg.color = Color.white;
            }
            else
            {
                boxImg.color = BoxBg;
            }
            boxImg.raycastTarget = true;
            if (panel == null)
                AddBoxBorder(boxRt);

            var headGo = new GameObject("Header", typeof(RectTransform));
            headGo.transform.SetParent(boxRt, false);
            RectTransform headRt = headGo.transform as RectTransform;
            headRt.anchorMin = new Vector2(0f, 1f);
            headRt.anchorMax = new Vector2(1f, 1f);
            headRt.pivot = new Vector2(0f, 1f);
            headRt.anchoredPosition = new Vector2(Pad, -6f);
            headRt.sizeDelta = new Vector2(-Pad * 2f, 20f);
            headerLabel = UiFonts.CreateLabel(headGo, 15f);
            StyleLabel(headerLabel, 15f);
            headerLabel.color = Gold;
            headerLabel.alignment = TextAlignmentOptions.MidlineLeft;
            headerLabel.text = header;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(boxRt, false);
            RectTransform scrollRt = scrollGo.transform as RectTransform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(Pad, Pad);
            scrollRt.offsetMax = new Vector2(-Pad, -28f);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.12f);
            scrollBg.raycastTarget = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollRt, false);
            RectTransform vpRt = viewport.transform as RectTransform;
            Stretch(vpRt);
            Image vpImg = viewport.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            vpImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpRt, false);
            content = contentGo.transform as RectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vpRt;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 120f;
            sr.inertia = true;

            return boxRt;
        }

        private static void AddBoxBorder(RectTransform box)
        {
            var top = new GameObject("BordT", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            top.transform.SetParent(box, false);
            RectTransform tRt = top.transform as RectTransform;
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(0f, 2f);
            tRt.anchoredPosition = Vector2.zero;
            top.GetComponent<Image>().color = BoxBorder;
            top.GetComponent<Image>().raycastTarget = false;

            var bot = new GameObject("BordB", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bot.transform.SetParent(box, false);
            RectTransform bRt = bot.transform as RectTransform;
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 0f);
            bRt.pivot = new Vector2(0.5f, 0f);
            bRt.sizeDelta = new Vector2(0f, 2f);
            bot.GetComponent<Image>().color = BoxBorder;
            bot.GetComponent<Image>().raycastTarget = false;

            var left = new GameObject("BordL", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            left.transform.SetParent(box, false);
            RectTransform lRt = left.transform as RectTransform;
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(0f, 1f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.sizeDelta = new Vector2(2f, 0f);
            left.GetComponent<Image>().color = BoxBorder;
            left.GetComponent<Image>().raycastTarget = false;

            var right = new GameObject("BordR", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            right.transform.SetParent(box, false);
            RectTransform rRt = right.transform as RectTransform;
            rRt.anchorMin = new Vector2(1f, 0f);
            rRt.anchorMax = new Vector2(1f, 1f);
            rRt.pivot = new Vector2(1f, 0.5f);
            rRt.sizeDelta = new Vector2(2f, 0f);
            right.GetComponent<Image>().color = BoxBorder;
            right.GetComponent<Image>().raycastTarget = false;
        }

        private static void BuildFooter(RectTransform host)
        {
            float btnW = 118f;
            float btnH = 32f;
            MakeFooterBtn(host, "Cancel", Loc.T("Cancel", "Cancel"),
                true, new Vector2(Pad, Pad), btnW, btnH, false, Close);
            MakeFooterBtn(host, "Apply", Loc.T("Apply", "Apply"),
                false, new Vector2(-Pad, Pad), btnW, btnH, true, Close);
        }

        private static void MakeFooterBtn(
            RectTransform host,
            string name,
            string label,
            bool left,
            Vector2 anchoredPos,
            float w,
            float h,
            bool accent,
            UnityAction click)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(host, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(w, h);
            Image img = go.GetComponent<Image>();
            Sprite sprite = accent ? UiAssets.BtnWoodAccent : UiAssets.BtnWood;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = accent
                    ? new Color(0.40f, 0.28f, 0.12f, 1f)
                    : new Color(0.26f, 0.18f, 0.10f, 1f);
            }
            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(click);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.transform as RectTransform);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(textGo, 15f);
            StyleLabel(tmp, 15f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = accent ? Gold : Cream;
            tmp.text = label;
        }

        private static void ClearContent(RectTransform content)
        {
            if (content == null)
                return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }

        private static void RebuildUi(bool buildItems)
        {
            if (_skills == null)
                return;
            EnsureHost();
            if (_catContent == null || _itemContent == null)
                return;

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
                AddCategoryRow(_catContent, catN++, choice.Label(), on, focused,
                    () => FocusCategory(captured),
                    () => ToggleCategory(captured));
            }
            SetContentHeight(_catContent, catN);

            if (!buildItems)
            {
                if (_itemHeader != null)
                    _itemHeader.text = Loc.T("Items", "Items");
                return;
            }

            string focusName = "?";
            DisplayFilter focusFilter;
            if (DisplayFilters.TryGet(_focusId, out focusFilter))
                focusName = focusFilter.Label();
            if (_itemHeader != null)
                _itemHeader.text = Loc.T("Items in", "Items in") + " " + focusName;

            int itemN = 0;
            bool hasItems = false;

            if (_focusId == DisplayFilters.EpicLootGroupId)
            {
                hasItems = true;
                bool allOn = selectedSet.Contains(DisplayFilters.EpicLootGroupId);
                AddCategoryRow(_itemContent, itemN++,
                    Loc.T("All", "All") + " Epic Loot", allOn, false, null,
                    () => ToggleCategory(DisplayFilters.EpicLootGroupId));
                for (int s = 0; s < DisplayFilters.EpicLootSubFilterIds.Length; s++)
                {
                    int subId = DisplayFilters.EpicLootSubFilterIds[s];
                    DisplayFilter sub;
                    if (!DisplayFilters.TryGet(subId, out sub))
                        continue;
                    bool on = selectedSet.Contains(subId);
                    int capturedSub = subId;
                    AddItemRow(_itemContent, itemN++, sub.Label(), on, null,
                        () => PickEpicLootSub(capturedSub));
                }
            }
            else
            {
                List<string> items = DisplayFilters.SubItems(_focusId);
                if (items != null && items.Count > 0)
                {
                    hasItems = true;
                    bool categoryOn = selectedSet.Contains(_focusId);
                    for (int s = 0; s < items.Count; s++)
                    {
                        string shared = items[s];
                        bool on = categoryOn || selectedItemSet.Contains(shared);
                        string captured = shared;
                        int parent = _focusId;
                        AddItemRow(_itemContent, itemN++, DisplayFilters.ItemLabel(shared), on,
                            ItemIcon(shared),
                            () => PickItem(captured, parent));
                    }
                }
            }

            if (_itemBox != null)
                _itemBox.SetActive(hasItems);
            SetContentHeight(_itemContent, hasItems ? itemN : 0);
        }

        private static void SetContentHeight(RectTransform content, int rows)
        {
            if (content == null)
                return;
            content.sizeDelta = new Vector2(0f, Mathf.Max(rows * RowH + 4f, 4f));
            content.anchoredPosition = Vector2.zero;
        }

        private static void AddCategoryRow(
            RectTransform parent,
            int index,
            string label,
            bool on,
            bool focused,
            UnityAction onFocus,
            UnityAction onToggle)
        {
            GameObject row = MakeRow(parent, index, focused ? RowFocus : RowBg);
            RectTransform rowRt = row.transform as RectTransform;

            if (onFocus != null)
            {
                var hit = new GameObject("Focus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                hit.transform.SetParent(rowRt, false);
                RectTransform hitRt = hit.transform as RectTransform;
                hitRt.anchorMin = Vector2.zero;
                hitRt.anchorMax = new Vector2(0.78f, 1f);
                hitRt.offsetMin = Vector2.zero;
                hitRt.offsetMax = Vector2.zero;
                Image hitImg = hit.GetComponent<Image>();
                hitImg.color = new Color(1f, 1f, 1f, 0.001f);
                hitImg.raycastTarget = true;
                Button b = hit.GetComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(onFocus);
            }

            string text = (focused ? "<color=#FFB84D>▼</color>  " : "") + label;
            AddLabel(rowRt, text, focused ? Gold : (on ? Cream : Muted), 0.03f, 0.76f);
            AttachToggle(rowRt, on, onToggle);
        }

        private static void AddItemRow(
            RectTransform parent,
            int index,
            string label,
            bool on,
            Sprite icon,
            UnityAction onToggle)
        {
            GameObject row = MakeRow(parent, index, RowBg);
            RectTransform rowRt = row.transform as RectTransform;

            float labelLeft = 0.03f;
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(rowRt, false);
                RectTransform iconRt = iconGo.transform as RectTransform;
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(6f, 0f);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                Image img = iconGo.GetComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
                labelLeft = 0.14f;
            }

            AddLabel(rowRt, label, on ? Cream : Muted, labelLeft, 0.76f);
            AttachToggle(rowRt, on, onToggle);
        }

        private static GameObject MakeRow(RectTransform parent, int index, Color bg)
        {
            var row = new GameObject("Row" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -index * RowH);
            rt.sizeDelta = new Vector2(0f, RowH - 2f);
            Image img = row.GetComponent<Image>();
            if (bg == RowFocus && UiAssets.RowFocus != null)
            {
                img.sprite = UiAssets.RowFocus;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = bg;
            }
            img.raycastTarget = true;
            return row;
        }

        private static void AddLabel(RectTransform row, string text, Color color, float a0, float a1)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(row, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(a0, 0f);
            rt.anchorMax = new Vector2(a1, 1f);
            rt.offsetMin = new Vector2(4f, 0f);
            rt.offsetMax = new Vector2(-4f, 0f);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(go, 15f);
            StyleLabel(tmp, 15f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = color;
            tmp.richText = true;
            tmp.text = text;
        }

        private static void AttachToggle(RectTransform row, bool on, UnityAction onClick)
        {
            if (onClick == null)
                return;
            GameObject toggle = UiToggle.Create(
                row, "Toggle", on, true, onClick, UiFonts.ThinNorse(),
                UiToggle.CompactWidth, UiToggle.CompactHeight);
            RectTransform rt = toggle.transform as RectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, 0f);
            rt.sizeDelta = new Vector2(UiToggle.CompactWidth, UiToggle.CompactHeight);
            rt.SetAsLastSibling();
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

        private static void StyleLabel(TMP_Text tmp, float size)
        {
            UiFonts.StyleThinLabel(tmp, size);
            tmp.enableAutoSizing = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.maxVisibleLines = 1;
            tmp.raycastTarget = false;
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

        private static void ApplyTitle()
        {
            CacheTitles();
            string title = Loc.T("Select types", "Select types");
            for (int i = 0; i < _titleTexts.Count; i++)
            {
                if (_titleTexts[i] != null)
                    _titleTexts[i].text = title;
            }
        }

        private static void RestoreTitle()
        {
            for (int i = 0; i < _titleTexts.Count && i < _titleBackup.Count; i++)
            {
                TMP_Text tmp = _titleTexts[i];
                if (tmp != null)
                    tmp.text = _titleBackup[i];
                if (i < _titleLocalize.Count && _titleLocalize[i] && tmp != null)
                {
                    Component localize = tmp.GetComponent("Localize");
                    if (localize is MonoBehaviour mb)
                        mb.enabled = true;
                }
            }
            _titleTexts.Clear();
            _titleBackup.Clear();
            _titleLocalize.Clear();
        }

        private static void CacheTitles()
        {
            _titleTexts.Clear();
            _titleBackup.Clear();
            _titleLocalize.Clear();
            if (_skills == null)
                return;
            Transform listRoot = _skills.m_listRoot;
            TMP_Text[] texts = _skills.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text tmp = texts[i];
                if (tmp == null || tmp == _skills.m_totalSkillText)
                    continue;
                if (listRoot != null && tmp.transform != listRoot && tmp.transform.IsChildOf(listRoot))
                    continue;
                string n = tmp.gameObject.name.ToLowerInvariant();
                if (!(n.Contains("title") || n.Contains("header") || n.Contains("topic") || n.Contains("label")
                    || n == "text" || n.Contains("skill")))
                    continue;
                Component localize = tmp.GetComponent("Localize");
                bool hadLocalize = localize is MonoBehaviour mb && mb.enabled;
                if (hadLocalize)
                    ((MonoBehaviour)localize).enabled = false;
                _titleTexts.Add(tmp);
                _titleBackup.Add(tmp.text ?? "");
                _titleLocalize.Add(hadLocalize);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
    internal static class DisplayMenuInventoryVisiblePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (DisplayTypeMenu.IsOpen)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class DisplayMenuInventoryShowPatch
    {
        private static bool Prefix()
        {
            if (!DisplayTypeMenu.IsOpen)
                return true;
            DisplayTypeMenu.Close();
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    internal static class DisplayMenuInventoryHidePatch
    {
        private static void Postfix()
        {
            if (DisplayTypeMenu.IsOpen)
                DisplayTypeMenu.Close();
        }
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
    internal static class DisplayMenuPausePatch
    {
        private static bool Prefix()
        {
            if (!DisplayTypeMenu.ShouldBlockPause())
                return true;
            DisplayTypeMenu.Close();
            return false;
        }
    }

    [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.OnClose))]
    internal static class DisplayMenuSkillsClosePatch
    {
        private static void Prefix(SkillsDialog __instance)
        {
            DisplayTypeMenu.OnSkillsClosed(__instance);
        }
    }

    [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
    internal static class DisplayMenuSkillsSetupPatch
    {
        private static bool Prefix()
        {
            return !DisplayTypeMenu.IsOpen;
        }
    }
}
