using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Station Settings — centered panel_left chrome. Item cells (sprite + name + toggle),
    /// 2 per row; link l1–l9 in the bottom parchment margin.
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

        // Item cells (same idea as Display Settings right panel) — 2 per row.
        private const int ItemColumns = 2;
        private const float ItemCellW = 190f;
        private const float ItemCellH = 100f;
        private const float ItemGapX = 12f;
        private const float ItemGapY = 10f;
        private const float ItemPadL = 18f;
        private const float ItemPadT = 4f;
        private const float ItemFont = 16f;
        private const float IconSize = 48f;

        private const float ActionRowW = 400f;
        private const float ActionRowH = 44f;
        private const float ToggleW = 36f;
        private const float ToggleH = 18f;
        private const float ApplyW = 140f;
        private const float ApplyH = 38f;
        private const float LetterSpace = 14f;
        private const float ApplyFont = 20f;
        private const float HeaderFont = 22f;

        private static readonly Color Gold = new Color(0.925f, 0.77f, 0.29f, 1f);
        private static readonly Color Muted = new Color(0.70f, 0.64f, 0.52f, 0.85f);
        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        private static Smelter _smelter;
        private static CookingStation _cook;
        private static Fermenter _fermenter;
        private static Fireplace _fire;
        // SAC feed trough (Container): animal-food filter + link + auto-fill.
        private static Container _trough;
        // Scarecrow: compact panel (Farm/ScarecrowPanel.cs), this menu only hosts it (open / close / Esc).
        private static Scarecrow _scarecrow;
        // Armor stand: presets panel (Craft/ArmorStandPanel.cs), hosted the same way.
        private static ArmorStand _armorStand;

        /// <summary>Menu open for this scarecrow (it shows its grid preview meanwhile).</summary>
        internal static bool IsEditing(Scarecrow s)
        {
            return IsOpen && s != null && _scarecrow == s;
        }
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
            OpenInternal(smelter, null, null, null, null);
        }

        public static void Open(CookingStation cook)
        {
            OpenInternal(null, cook, null, null, null);
        }

        public static void Open(Fermenter fermenter)
        {
            OpenInternal(null, null, fermenter, null, null);
        }

        public static void Open(Fireplace fire)
        {
            OpenInternal(null, null, null, fire, null);
        }

        public static void Open(Container trough)
        {
            OpenInternal(null, null, null, null, trough);
        }

        public static void Open(Scarecrow scarecrow)
        {
            OpenInternal(null, null, null, null, null, scarecrow);
        }

        public static void Open(ArmorStand armorStand)
        {
            OpenInternal(null, null, null, null, null, null, armorStand);
        }

        private static void OpenInternal(
            Smelter smelter,
            CookingStation cook,
            Fermenter fermenter,
            Fireplace fire,
            Container trough,
            Scarecrow scarecrow = null,
            ArmorStand armorStand = null)
        {
            if ((smelter == null && cook == null && fermenter == null && fire == null && trough == null && scarecrow == null
                && armorStand == null)
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
            _trough = trough;
            _scarecrow = scarecrow;
            _armorStand = armorStand;
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
                _trough = null;
                _scarecrow = null;
                _armorStand = null;
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
            if ((_smelter == null && _cook == null && _fermenter == null && _fire == null && _trough == null && _scarecrow == null
                && _armorStand == null)
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
            bool denied;
            if (_smelter != null)
            {
                denied = !StationPullFilter.IsDenied(_smelter, shared);
                StationPullFilter.SetDenied(_smelter, shared, denied);
            }
            else if (_cook != null)
            {
                denied = !StationPullFilter.IsDenied(_cook, shared);
                StationPullFilter.SetDenied(_cook, shared, denied);
            }
            else if (_trough != null)
            {
                denied = StationPullFilter.IsAllowed(_trough, shared);
                StationPullFilter.SetDenied(_trough, shared, denied);
            }
            else if (_fermenter != null)
            {
                denied = StationPullFilter.IsAllowed(_fermenter, shared);
                StationPullFilter.SetDenied(_fermenter, shared, denied);
            }
            else
                return;
            ActivityLog.Note(StationOutput.StationLabel(ActiveStation()),
                Loc.T("Filter", "Filter") + " " + DisplayFilters.ItemLabel(shared) + " "
                + (denied ? Loc.T("blocked", "gesperrt") : Loc.T("allowed", "erlaubt")));
            StationAutoFill.WakeStation(ActiveStation());
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
            ActivityLog.Note(StationOutput.StationLabel(station), Loc.T("Link", "Link") + " " + StationLink.ShortToken(StationLink.Get(station)));
            StationAutoFill.WakeStation(station);
        }

        private static Component ActiveStation()
        {
            return (Component)_smelter ?? _cook ?? (Component)_fermenter ?? (Component)_fire ?? (Component)_trough;
        }

        private static void ToggleAutoFill()
        {
            Component host = _trough != null ? (Component)_trough : (Component)_fermenter;
            if (host == null)
                return;
            StationAutoFill.SetOn(host, !StationAutoFill.IsOn(host.GetComponent<ZNetView>()));
            RebuildRows(preserveScroll: true);
        }

        private static void AllowAll()
        {
            if (_smelter != null)
                StationPullFilter.Clear(_smelter);
            else if (_cook != null)
                StationPullFilter.Clear(_cook);
            else if (_trough != null)
                StationPullFilter.Clear(_trough);
            else if (_fermenter != null)
                StationPullFilter.Clear(_fermenter);
            else
                return;
            ActivityLog.Note(StationOutput.StationLabel(ActiveStation()), Loc.T("Filter: allow all", "Filter: alles erlaubt"));
            StationAutoFill.WakeStation(ActiveStation());
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

            // Scarecrow: small panel under the hotbar, no dimming (the field stays visible).
            if (_scarecrow != null)
            {
                ScarecrowPanel.Create(_root.transform as RectTransform, _scarecrow, Close);
                return;
            }
            if (_armorStand != null)
            {
                ArmorStandPanel.Create(_root.transform as RectTransform, _armorStand, Close);
                return;
            }

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
            ScarecrowPanel.Dispose();
            ArmorStandPanel.Dispose();
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
            ApplySprite(_panelRt.GetComponent<Image>(), UiStyle.Sprite("woodpanel_settings") ?? UiAssets.PanelLeft, Color.white);

            // Vanilla look: dark wash instead of the SAC inset art (same as the display filter).
            if (UiStyle.Vanilla)
                UiStyle.AddDarkInset(_panelRt, InsetL, InsetT, InsetR, InsetB);
            else
                PlaceInsetBg(_panelRt, UiAssets.PanelLeftInset, InsetL, InsetT, InsetR, InsetB);
            PlaceHeader(_panelRt, Loc.T("Settings", "Einstellungen"));

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
            ApplySprite(apply.GetComponent<Image>(), UiStyle.Sprite("button") ?? UiAssets.BtnApply, Color.white);
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
            // Same link buttons (size + look) as Chest Settings, in the bottom margin.
            _linkGrid = UiLinkGrid.Build(
                _panelRt,
                "SAC_StationLinks",
                current,
                id => SetLink(id),
                UiLinkGrid.Cell,
                UiLinkGrid.Gap);
            RectTransform rt = _linkGrid.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            float block = UiLinkGrid.BlockSize(UiLinkGrid.Cell, UiLinkGrid.Gap);
            // Bottom parchment margin: inset from outer corner (not glued to the edge).
            float x = 28f;
            float y = Mathf.Max(28f, (InsetB - block) * 0.42f);
            if (y + block > InsetB - 6f)
                y = Mathf.Max(22f, InsetB - 6f - block);
            rt.anchoredPosition = new Vector2(x, y);
            rt.SetAsLastSibling();
        }

        private static void RebuildRows(bool preserveScroll = false)
        {
            EnsureRoot();
            if (_scarecrow != null)
            {
                ScarecrowPanel.Rebuild();
                return;
            }
            if (_armorStand != null)
            {
                ArmorStandPanel.Rebuild();
                return;
            }
            if (_listContent == null)
                return;

            float scrollPos = _listScroll != null ? _listScroll.verticalNormalizedPosition : 1f;
            ClearContent(_listContent);

            List<string> choices = _cook != null
                ? StationPullFilter.FoodChoices(_cook)
                : _smelter != null ? StationPullFilter.OreChoices(_smelter)
                : _trough != null ? StationPullFilter.TroughFoodChoices()
                : _fermenter != null ? StationPullFilter.MeadChoices(_fermenter)
                : new List<string>();
            // Only discovered items (a blocked undiscovered one stays visible so it can be allowed again).
            var shown = new List<string>(choices.Count);
            for (int i = 0; i < choices.Count; i++)
            {
                string c = choices[i];
                bool allowedNow = _cook != null ? StationPullFilter.IsAllowed(_cook, c)
                    : _trough != null ? StationPullFilter.IsAllowed(_trough, c)
                    : _fermenter != null ? StationPullFilter.IsAllowed(_fermenter, c)
                    : StationPullFilter.IsAllowed(_smelter, c);
                // A blocked one stays visible so it can be allowed again.
                if (DisplayFilters.IsKnownToPlayer(c) || !allowedNow)
                    shown.Add(c);
            }
            choices = shown;

            Component station = ActiveStation();
            int n = choices.Count;
            for (int i = 0; i < n; i++)
            {
                string shared = choices[i];
                bool allowed = _cook != null ? StationPullFilter.IsAllowed(_cook, shared)
                    : _trough != null ? StationPullFilter.IsAllowed(_trough, shared)
                    : _fermenter != null ? StationPullFilter.IsAllowed(_fermenter, shared)
                    : StationPullFilter.IsAllowed(_smelter, shared);
                string label = StationPullFilter.DisplayName(shared);
                string captured = shared;
                int col = i % ItemColumns;
                int row = i / ItemColumns;
                AddItemCell(col, row, label, allowed, ItemIcon(shared), () => Toggle(captured));
            }

            int gridRows = n > 0 ? (n + ItemColumns - 1) / ItemColumns : 0;
            float gridH = gridRows > 0
                ? ItemPadT + gridRows * (ItemCellH + ItemGapY)
                : 0f;

            bool filterStation = _smelter != null || _cook != null || _trough != null || _fermenter != null;
            if (filterStation)
                AddActionRow(gridH, Loc.T("Allow all inputs", "Allow all inputs"), AllowAll);

            float actionH = filterStation ? ActionRowH + 8f : 0f;
            // Trough / fermenter: auto-fill switch as an action row (same as [B]).
            Component autoFillHost = _trough != null ? (Component)_trough : (Component)_fermenter;
            if (autoFillHost != null)
            {
                bool on = StationAutoFill.IsOn(autoFillHost.GetComponent<ZNetView>());
                AddActionRow(gridH + actionH,
                    Loc.T("Auto-fill: ", "Auto-Fill: ") + (on ? Loc.T("on", "an") : Loc.T("off", "aus")),
                    ToggleAutoFill);
                actionH += ActionRowH + 8f;
            }
            _listContent.sizeDelta = new Vector2(0f, gridH + actionH + 4f);
            if (_listScroll != null)
                _listScroll.verticalNormalizedPosition = preserveScroll ? scrollPos : 1f;

            if (_linkGrid != null)
                UiLinkGrid.RefreshSelection(_linkGrid, StationLink.Get(station));
            else
                BuildLinkGrid();
        }

        private static void AddItemCell(
            int col,
            int row,
            string label,
            bool on,
            Sprite icon,
            UnityAction onToggle)
        {
            var go = new GameObject(
                "Item" + row + "_" + col,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            go.transform.SetParent(_listContent, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ItemCellW, ItemCellH);
            rt.anchoredPosition = new Vector2(
                ItemPadL + col * (ItemCellW + ItemGapX),
                -(ItemPadT + row * (ItemCellH + ItemGapY)));
            Image cellImg = go.GetComponent<Image>();
            // Vanilla look (same as the display filter): lighter tile, whole tile is the switch.
            Sprite vCell = UiStyle.Sprite(on ? "button_highlight" : "button_small");
            bool wholeCellClick = vCell != null;
            if (wholeCellClick)
            {
                UiStyle.SetFrame(cellImg, vCell);
                cellImg.color = new Color(1f, 1f, 1f, on ? 0.75f : 0.45f);
                cellImg.raycastTarget = true;
                Button cellBtn = go.AddComponent<Button>();
                cellBtn.targetGraphic = cellImg;
                cellBtn.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = cellBtn.colors;
                colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
                colors.selectedColor = colors.normalColor;
                colors.fadeDuration = 0.08f;
                cellBtn.colors = colors;
                if (onToggle != null)
                    cellBtn.onClick.AddListener(onToggle);
            }
            else
            {
                ApplySprite(cellImg, UiAssets.ItemCell, new Color(0.14f, 0.1f, 0.08f, 0.95f));
                if (cellImg != null && cellImg.sprite != null)
                {
                    cellImg.type = Image.Type.Simple;
                    cellImg.preserveAspect = false;
                }
            }

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(rt, false);
                RectTransform iconRt = iconGo.transform as RectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 1f);
                iconRt.anchorMax = new Vector2(0.5f, 1f);
                iconRt.pivot = new Vector2(0.5f, 1f);
                iconRt.anchoredPosition = new Vector2(0f, -8f);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                Image iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.color = Color.white;
                if (wholeCellClick)
                    UiStyle.AddIconShadow(iconGo);
            }

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            RectTransform labelRt = labelGo.transform as RectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            // No edge toggle in the Vanilla look → the name may use the bottom strip too.
            labelRt.offsetMin = new Vector2(4f, wholeCellClick ? 6f : ToggleH + 6f);
            labelRt.offsetMax = new Vector2(-4f, -(IconSize + 10f));
            TextMeshProUGUI tmp = UiFonts.CreateLabel(labelGo, ItemFont);
            StyleLabel(tmp, ItemFont, LetterSpace * 0.55f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.maxVisibleLines = 2;
            tmp.color = on ? Gold : Muted;
            tmp.text = label;

            if (wholeCellClick)
                return;

            GameObject toggle = UiToggle.Create(
                rt, "Toggle", on, true, onToggle, UiFonts.ThinNorse(),
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

        internal static Sprite ItemIcon(string shared)
        {
            if (string.IsNullOrEmpty(shared))
                return null;
            Sprite cached;
            if (IconCache.TryGetValue(shared, out cached))
                return cached;
            GameObject prefab = ItemIds.PrefabFromToken(shared);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            Sprite icon = drop != null && drop.m_itemData != null
                ? StackLimits.Icon(drop.m_itemData)
                : null;
            IconCache[shared] = icon;
            return icon;
        }

        private static void AddActionRow(float yFromTop, string label, UnityAction action)
        {
            AddActionRow(yFromTop, label, action, ItemPadL, ActionRowW);
        }

        private static void AddActionRow(float yFromTop, string label, UnityAction action, float x, float width)
        {
            var row = new GameObject("Action", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            row.transform.SetParent(_listContent, false);
            RectTransform rt = row.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, ActionRowH);
            rt.anchoredPosition = new Vector2(x, -(yFromTop + 4f));

            Image rowImg = row.GetComponent<Image>();
            Sprite vRow = UiStyle.Sprite("button");
            if (vRow != null)
            {
                UiStyle.SetFrame(rowImg, vRow);
            }
            else if (UiAssets.BtnApply != null)
            {
                rowImg.sprite = UiAssets.BtnApply;
                rowImg.type = Image.Type.Sliced;
                rowImg.color = Color.white;
            }
            else if (UiAssets.RawCategory != null)
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
            labelRt.offsetMin = new Vector2(12f, 2f);
            labelRt.offsetMax = new Vector2(-12f, -2f);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(labelGo, ApplyFont);
            StyleLabel(tmp, ApplyFont, LetterSpace);
            tmp.alignment = TextAlignmentOptions.Center;
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
                UiStyle.Lit(img);
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
