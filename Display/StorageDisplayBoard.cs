using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    internal class StorageDisplayBoard : MonoBehaviour
    {
        public const string ZdoItemKey = "sac_item";
        public const string ZdoRangeKey = "sac_display_range";

        private static readonly List<StorageDisplayBoard> All = new List<StorageDisplayBoard>();

        private DisplayKind _kind = DisplayKind.Medium;
        private int _slotCount = 12;
        private int _columns = 4;
        private int _rows = 3;
        private int _headerColumns;
        private int _itemsPerGroup = 1;
        private float _fontFactor = 0.22f;
        private float _titleFactor = 0.12f;
        private bool _columnMajor;
        private bool _columnHeaders;
        private bool _tightSlots;
        private bool _flowSections;

        private readonly List<Container> _watched = new List<Container>();
        private readonly List<int> _watchIds = new List<int>();
        private static readonly List<Container> _watchScratch = new List<Container>(64);
        private SlotUi[] _slots;
        private TextMeshProUGUI[] _headers;
        private TextMeshProUGUI _signText;
        private float _titleFont;
        private float _born;
        private float _nextScan;
        private float _nextPaint;
        private bool _dirty = true;
        private bool _configured;
        private int _page;
        private int _pages = 1;
        private List<StorageDisplayBoard> _cluster;
        private float _clusterUntil;

        private struct SlotUi
        {
            public Image Icon;
            public TextMeshProUGUI Amount;
        }

        public DisplayKind Kind
        {
            get { return _kind; }
        }

        public int SlotCount
        {
            get { return _slotCount; }
        }

        public void Configure(DisplayKind kind)
        {
            _kind = kind;
            _configured = true;
            switch (kind)
            {
                case DisplayKind.Small:
                    _slotCount = 1;
                    _columns = 1;
                    _rows = 1;
                    _headerColumns = 0;
                    _itemsPerGroup = 1;
                    // Original sign scale: keep count readable next to the icon.
                    _fontFactor = 0.28f;
                    _titleFactor = 0.16f;
                    _columnMajor = false;
                    _columnHeaders = false;
                    _tightSlots = false;
                    _flowSections = false;
                    break;
                case DisplayKind.Large:
                    // Dense row-major grid; categories flow as sections (empty cats still shown).
                    _headerColumns = 0;
                    _itemsPerGroup = 1;
                    _columns = 8;
                    _rows = 12;
                    _slotCount = _columns * _rows;
                    // Prefab is 3x medium scale; keep world text size like medium.
                    _fontFactor = 0.16f / 3f;
                    _titleFactor = 0.10f / 3f;
                    _columnMajor = false;
                    _columnHeaders = false;
                    _tightSlots = true;
                    _flowSections = true;
                    break;
                default:
                    // Category title strip + denser item cells; sort grouped by category.
                    _slotCount = 12;
                    _columns = 4;
                    _rows = 3;
                    _headerColumns = 1;
                    _itemsPerGroup = 4;
                    _fontFactor = 0.15f;
                    _titleFactor = 0.10f;
                    _columnMajor = false;
                    _columnHeaders = true;
                    _tightSlots = true;
                    _flowSections = false;
                    _kind = DisplayKind.Medium;
                    break;
            }
        }

        private void Awake()
        {
            if (!_configured)
                DetectKindFromName();
        }

        private void DetectKindFromName()
        {
            string n = gameObject.name.Replace("(Clone)", "").Trim();
            if (n.IndexOf("small", System.StringComparison.OrdinalIgnoreCase) >= 0)
                Configure(DisplayKind.Small);
            else if (n.IndexOf("large", System.StringComparison.OrdinalIgnoreCase) >= 0)
                Configure(DisplayKind.Large);
            else
                Configure(DisplayKind.Medium);
        }

        private string BoardTitle()
        {
            switch (_kind)
            {
                case DisplayKind.Small:
                    return "Small Storage Display";
                case DisplayKind.Large:
                    return "Large Storage Display";
                default:
                    return "Medium Storage Display";
            }
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            _born = Time.time;
            _dirty = true;
            _nextScan = 0f;
            InvalidateClusters();
        }

        private void Start()
        {
            if (!_configured)
                DetectKindFromName();
            if (!All.Contains(this))
                All.Add(this);
            TryBuild();
        }

        private void OnDestroy()
        {
            DisplayTypeMenu.CloseIf(this);
            DisplayRangeMenu.CloseIf(this);
            All.Remove(this);
            Unwatch();
            InvalidateClusters();
        }

        private static void InvalidateClusters()
        {
            for (int i = 0; i < All.Count; i++)
            {
                StorageDisplayBoard board = All[i];
                if (board != null)
                    board._cluster = null;
            }
        }

        public string HoverLabel()
        {
            string rangeKey = KeyUtil.Format(Plugin.Settings != null
                ? Plugin.Settings.DisplayRangeKey.Value
                : new BepInEx.Configuration.KeyboardShortcut(KeyCode.R, KeyCode.LeftAlt));
            if (string.IsNullOrEmpty(rangeKey))
                rangeKey = "Alt+R";
            string rangeLine = "[<color=yellow><b>" + rangeKey + "</b></color>] "
                + Loc.T("Range", "Reichweite")
                + " (" + Mathf.RoundToInt(EffectiveDisplayRange()) + " m)";

            if (_kind == DisplayKind.Small)
            {
                string hotbar = "[<color=yellow><b>1-8</b></color>] "
                    + Loc.T("Set item from hotbar", "Item aus Hotbar setzen");
                string token = ItemToken();
                if (string.IsNullOrEmpty(token))
                    return BoardTitle() + "\n" + hotbar + "\n" + rangeLine;
                return BoardTitle() + " (" + ItemLabel(token) + ")\n" + hotbar + "\n" + rangeLine;
            }

            List<int> filters = FilterIds();
            List<string> items = ItemTokens();
            string name = DisplayFilters.Label(filters, items);
            string use = "[<color=yellow><b>E</b></color>] " + Loc.T("Select type", "Typ wählen");
            if (filters.Count == 0 && items.Count == 0)
                return BoardTitle() + "\n" + use + "\n" + rangeLine;

            RefreshClusterPages();
            string title = BoardTitle() + " (" + name + ")";
            if (_pages > 1)
                title += " (" + (_page + 1) + "/" + _pages + ")";
            return title + "\n" + use + "\n" + rangeLine;
        }

        public string ItemToken()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return "";
            return zdo.GetString(ZdoItemKey, "") ?? "";
        }

        /// <summary>Per-display override in meters (5–50). 0 = use config DisplayRange.</summary>
        public int DisplayRangeMeters()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return 0;
            return Mathf.Clamp(zdo.GetInt(ZdoRangeKey, 0), 0, 50);
        }

        public float EffectiveDisplayRange()
        {
            int custom = DisplayRangeMeters();
            if (custom > 0)
                return custom;
            float cfg = Plugin.Settings != null ? Plugin.Settings.DisplayRange.Value : 10f;
            return Mathf.Max(1f, cfg);
        }

        /// <summary>
        /// Anyone with ward access may set this (no admin). Stored on the piece ZDO.
        /// </summary>
        public void SetDisplayRangeMeters(int meters)
        {
            meters = Mathf.Clamp(meters, 5, 50);
            // Snap to 5 m steps.
            meters = Mathf.RoundToInt(meters / 5f) * 5;
            if (meters < 5)
                meters = 5;

            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();

            nv.GetZDO().Set(ZdoRangeKey, meters);
            _dirty = true;
            _nextScan = 0f;
            _cluster = null;
            InvalidateClusters();
        }

        public void SetItemFromHotbar(ItemDrop.ItemData item)
        {
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();

            ZDO zdo = nv.GetZDO();
            if (item?.m_shared == null)
            {
                zdo.Set(ZdoItemKey, "");
            }
            else
            {
                // Prefer shared name ($item_…) so chest CountItems matches reliably.
                string token = ItemIds.SharedName(item);
                if (string.IsNullOrEmpty(token))
                    token = ItemIds.PrefabName(item) ?? "";
                // Ensure drop prefab is known for later SampleFromToken / Matches.
                if (item.m_dropPrefab == null)
                    ItemIds.PrefabName(item);
                zdo.Set(ZdoItemKey, token ?? "");
            }

            // Small boards do not use category filters.
            zdo.Set(DisplayFilters.ZdoKeyMulti, "");
            zdo.Set(DisplayFilters.ZdoKey, 0);
            _dirty = true;
            _cluster = null;
            InvalidateClusters();
        }

        /// <summary>
        /// While looking at a small display, hotbar 1-8 assigns that item (empty slot clears).
        /// UseHotbarItem passes 1-8 (same as vanilla); inventory columns are 0-7.
        /// </summary>
        public static bool TryAssignFromHotbar(Player player, int hotbarIndex)
        {
            if (player == null || player != Player.m_localPlayer)
                return false;
            if (InventoryGui.IsVisible() || DisplayTypeMenu.IsOpen || StationFilterMenu.IsOpen || DisplayRangeMenu.IsOpen)
                return false;
            // Vanilla Player.UseHotbarItem(index) uses 1..8, then GetItemAt(index - 1, 0).
            if (hotbarIndex < 1 || hotbarIndex > 8)
                return false;

            StorageDisplayBoard board = HoveredSmall();
            if (board == null)
                return false;
            if (!PrivateArea.CheckAccess(board.transform.position, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            Inventory inv = player.GetInventory();
            if (inv == null)
                return true;

            ItemDrop.ItemData item = inv.GetItemAt(hotbarIndex - 1, 0);
            board.SetItemFromHotbar(item);
            if (item?.m_shared != null)
            {
                string shown = ItemLabel(ItemIds.SharedName(item));
                player.Message(MessageHud.MessageType.TopLeft,
                    Loc.T("Display set: ", "Anzeige gesetzt: ") + shown, 0, null, false);
            }
            else
            {
                player.Message(MessageHud.MessageType.TopLeft,
                    Loc.T("Display cleared", "Anzeige geleert"), 0, null, false);
            }
            return true;
        }

        private static StorageDisplayBoard HoveredSmall()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;

            GameObject hover = player.GetHoverObject();
            StorageDisplayBoard fromHover = hover != null
                ? hover.GetComponentInParent<StorageDisplayBoard>()
                : null;
            if (fromHover != null && fromHover.Kind == DisplayKind.Small)
                return fromHover;

            Piece piece = player.GetHoveringPiece();
            StorageDisplayBoard fromPiece = piece != null
                ? piece.GetComponent<StorageDisplayBoard>()
                : null;
            if (fromPiece != null && fromPiece.Kind == DisplayKind.Small)
                return fromPiece;
            return null;
        }

        private static string ItemLabel(string token)
        {
            if (string.IsNullOrEmpty(token))
                return Loc.T("Select item", "Item wählen");
            if (Localization.instance != null)
                return Localization.instance.Localize(token);
            return token;
        }

        public List<int> FilterIds()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            return DisplayFilters.ReadIds(zdo);
        }

        public int FilterId()
        {
            List<int> ids = FilterIds();
            return ids.Count > 0 ? ids[0] : 0;
        }

        public List<string> ItemTokens()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            return DisplayFilters.ReadItemTokens(zdo);
        }

        public void WriteFilters(List<int> ids)
        {
            WriteSelection(ids, ItemTokens());
        }

        public void WriteSelection(List<int> ids, List<string> itemTokens)
        {
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();

            ZDO zdo = nv.GetZDO();
            zdo.Set(DisplayFilters.ZdoKeyMulti, DisplayFilters.EncodeIds(ids));
            zdo.Set(DisplayFilters.ZdoKeyItems, DisplayFilters.EncodeItemTokens(itemTokens));
            int legacy = 0;
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i] > 0)
                    {
                        legacy = ids[i];
                        break;
                    }
                }
            }
            zdo.Set(DisplayFilters.ZdoKey, legacy);
            _dirty = true;
            _cluster = null;
            InvalidateClusters();
        }

        public void ToggleFilter(int id)
        {
            if (id <= 0)
            {
                WriteSelection(new List<int>(), new List<string>());
                return;
            }

            List<int> ids = FilterIds();
            List<string> items = ItemTokens();
            if (ids.Contains(id))
            {
                ids.Remove(id);
            }
            else
            {
                ids.Add(id);
                // Whole category selected: drop fine-grained picks under it.
                if (DisplayFilters.IsExpandable(id))
                    items = RemoveCategoryItems(items, id);
            }
            WriteSelection(ids, items);
        }

        public void ToggleItemToken(string shared, int parentFilterId)
        {
            if (string.IsNullOrEmpty(shared))
                return;

            List<int> ids = FilterIds();
            List<string> items = ItemTokens();
            if (items.Contains(shared))
                items.Remove(shared);
            else
            {
                items.Add(shared);
                if (parentFilterId > 0)
                    ids.Remove(parentFilterId);
            }
            WriteSelection(ids, items);
        }

        private static List<string> RemoveCategoryItems(List<string> items, int filterId)
        {
            if (items == null || items.Count == 0)
                return new List<string>();
            var next = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if (!DisplayFilters.TokenBelongsToCategory(items[i], filterId))
                    next.Add(items[i]);
            }
            return next;
        }

        public void SetFilter(int id)
        {
            var ids = new List<int>();
            if (id > 0)
                ids.Add(id);
            WriteSelection(ids, new List<string>());
        }

        public bool TryOpenMenu(Humanoid user)
        {
            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer)
                return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            // Small: no category menu — assign with hotbar 1-8 while looking at it.
            if (_kind == DisplayKind.Small)
            {
                player.Message(MessageHud.MessageType.Center,
                    Loc.T("Press 1-8 to set item from hotbar", "1-8 drücken: Item aus Hotbar setzen"),
                    0, null, false);
                return true;
            }

            DisplayTypeMenu.Open(this);
            return true;
        }

        private void Update()
        {
            // Force UI rebuild when switching to small layout tweaks / slot count changes.
            if (_slots != null && _slots.Length != _slotCount)
                _slots = null;
            // Medium layout gained a category header strip — rebuild old grids.
            if (_slots != null && _kind == DisplayKind.Medium && _columnHeaders && _headers == null)
                _slots = null;
            // Large switched from fixed 3-column headers to flowing sections.
            if (_slots != null && _kind == DisplayKind.Large && (_headers != null || !_flowSections || _columnMajor))
                _slots = null;
            if (_slots != null && _kind == DisplayKind.Small && _slots.Length == 1
                && _slots[0].Amount != null && _slots[0].Amount.fontSize < 1f)
                _slots = null;
            if (_slots == null)
                TryBuild();
            if (_slots == null)
                return;

            SilenceSignText();
            ApplyTitle();

            float interval = Time.time - _born < 8f ? 0.4f : 2.5f;
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + interval;
                Resubscribe();
            }

            if (!_dirty || Time.time < _nextPaint)
                return;

            _nextPaint = Time.time + 0.25f;
            _dirty = false;
            Paint();
        }

        private void TryBuild()
        {
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return;
            BuildUi();
            if (_slots != null)
                _dirty = true;
        }

        private void SilenceSignText()
        {
            if (_signText == null || (!_signText.enabled && string.IsNullOrEmpty(_signText.text)))
                return;
            _signText.enabled = false;
            _signText.text = "";
        }

        private void ApplyTitle()
        {
            if (_signText == null)
                return;
            if (FilterIds().Count > 0 || ItemTokens().Count > 0)
            {
                SilenceSignText();
                return;
            }

            if (_kind == DisplayKind.Small && !string.IsNullOrEmpty(ItemToken()))
            {
                SilenceSignText();
                return;
            }

            _signText.enabled = true;
            _signText.alignment = TextAlignmentOptions.Center;
            _signText.overflowMode = TextOverflowModes.Overflow;
            _signText.textWrappingMode = TextWrappingModes.Normal;
            _signText.enableAutoSizing = false;
            if (_titleFont > 0f)
                _signText.fontSize = _titleFont;
            string title = _kind == DisplayKind.Small
                ? Loc.T("Press 1-8", "1-8 drücken")
                : Loc.T("Select type", "Typ wählen");
            if (_signText.text != title)
                _signText.text = title;
        }

        private void BuildUi()
        {
            Sign sign = GetComponent<Sign>();
            TextMeshProUGUI template = sign != null ? sign.m_textWidget : null;
            if (template == null)
                return;

            RectTransform board = template.rectTransform;
            if (board == null || template.canvas == null)
                return;

            _signText = template;
            _titleFont = template.fontSize * _titleFactor;
            SilenceSignText();

            Transform existing = board.parent.Find("SacDisplayGrid");
            if (existing != null)
                Destroy(existing.gameObject);

            var root = new GameObject("SacDisplayGrid", typeof(RectTransform));
            root.transform.SetParent(board.parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = board.anchorMin;
            rt.anchorMax = board.anchorMax;
            rt.pivot = board.pivot;
            rt.anchoredPosition = board.anchoredPosition;
            rt.sizeDelta = board.sizeDelta;
            rt.offsetMin = board.offsetMin;
            rt.offsetMax = board.offsetMax;
            rt.localScale = board.localScale;
            rt.localRotation = board.localRotation;

            float font = template.fontSize * _fontFactor;
            float contentTop = 1f;
            if (_columnHeaders && _headerColumns > 0)
            {
                contentTop = 0.88f;
                _headers = new TextMeshProUGUI[_headerColumns];
                for (int col = 0; col < _headerColumns; col++)
                {
                    GameObject headerGo = Object.Instantiate(template.gameObject, root.transform);
                    headerGo.name = "Header" + col;
                    headerGo.SetActive(true);
                    RectTransform headerRt = headerGo.GetComponent<RectTransform>();
                    float h0 = (col * _itemsPerGroup) / (float)_columns;
                    float h1 = ((col + 1) * _itemsPerGroup) / (float)_columns;
                    headerRt.anchorMin = new Vector2(h0 + 0.01f, contentTop);
                    headerRt.anchorMax = new Vector2(h1 - 0.01f, 1f);
                    headerRt.offsetMin = Vector2.zero;
                    headerRt.offsetMax = Vector2.zero;
                    headerRt.localScale = Vector3.one;
                    headerRt.localRotation = Quaternion.identity;

                    TextMeshProUGUI header = headerGo.GetComponent<TextMeshProUGUI>();
                    var localize = headerGo.GetComponent("Localize") as MonoBehaviour;
                    if (localize != null)
                        Object.Destroy(localize);
                    if (template.font != null)
                        header.font = template.font;
                    header.enabled = true;
                    header.alignment = TextAlignmentOptions.Center;
                    header.textWrappingMode = TextWrappingModes.NoWrap;
                    header.overflowMode = TextOverflowModes.Overflow;
                    header.enableAutoSizing = false;
                    header.fontSize = font * 0.85f;
                    header.color = new Color(1f, 0.95f, 0.75f, 1f);
                    header.faceColor = new Color32(255, 242, 191, 255);
                    header.outlineWidth = 0f;
                    header.raycastTarget = false;
                    header.text = "";
                    _headers[col] = header;
                }
            }
            else
            {
                _headers = null;
            }

            float iconMax = _tightSlots ? 0.40f : (_kind == DisplayKind.Small ? 0.42f : 0.46f);
            float textMin = _tightSlots ? 0.36f : (_kind == DisplayKind.Small ? 0.40f : 0.46f);
            float padX = _tightSlots ? 0.006f : 0.012f;
            float padY = _tightSlots ? 0.012f : 0.018f;

            _slots = new SlotUi[_slotCount];
            for (int i = 0; i < _slotCount; i++)
            {
                int col;
                int row;
                SlotCoord(i, out col, out row);

                var cell = new GameObject("Slot" + i, typeof(RectTransform));
                cell.transform.SetParent(root.transform, false);
                RectTransform cellRt = cell.GetComponent<RectTransform>();
                float y0 = contentTop * (1f - (row + 1) / (float)_rows) + padY * contentTop;
                float y1 = contentTop * (1f - row / (float)_rows) - padY * contentTop;
                cellRt.anchorMin = new Vector2(col / (float)_columns + padX, y0);
                cellRt.anchorMax = new Vector2((col + 1) / (float)_columns - padX, y1);
                cellRt.offsetMin = Vector2.zero;
                cellRt.offsetMax = Vector2.zero;
                cellRt.localScale = Vector3.one;

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(cell.transform, false);
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.00f, 0.10f);
                iconRt.anchorMax = new Vector2(iconMax, 0.92f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                Image icon = iconGo.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.color = Color.white;
                icon.raycastTarget = false;
                icon.enabled = false;

                GameObject textGo = Object.Instantiate(template.gameObject, cell.transform);
                textGo.name = "Amount";
                textGo.SetActive(true);
                textGo.transform.SetAsLastSibling();
                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(textMin, 0.08f);
                textRt.anchorMax = new Vector2(1.00f, 0.92f);
                textRt.pivot = new Vector2(0f, 0.5f);
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
                textRt.anchoredPosition = Vector2.zero;
                textRt.sizeDelta = Vector2.zero;
                textRt.localScale = Vector3.one;
                textRt.localRotation = Quaternion.identity;

                TextMeshProUGUI amount = textGo.GetComponent<TextMeshProUGUI>();
                amount.enabled = true;
                var localizeAmt = textGo.GetComponent("Localize") as MonoBehaviour;
                if (localizeAmt != null)
                    Object.Destroy(localizeAmt);
                if (template.font != null)
                    amount.font = template.font;
                amount.alignment = _kind == DisplayKind.Small
                    ? TextAlignmentOptions.MidlineLeft
                    : TextAlignmentOptions.MidlineLeft;
                amount.textWrappingMode = TextWrappingModes.NoWrap;
                amount.overflowMode = TextOverflowModes.Overflow;
                amount.enableAutoSizing = false;
                amount.fontSize = font;
                if (_kind == DisplayKind.Small)
                    amount.fontSize = Mathf.Max(font, template.fontSize * 0.22f);
                amount.color = new Color(1f, 0.95f, 0.75f, 1f);
                amount.faceColor = new Color32(255, 242, 191, 255);
                amount.outlineWidth = 0f;
                amount.raycastTarget = false;
                amount.text = "";

                _slots[i].Icon = icon;
                _slots[i].Amount = amount;
            }
        }

        private void SlotCoord(int index, out int col, out int row)
        {
            if (_columnMajor)
            {
                col = index / _rows;
                row = index % _rows;
            }
            else
            {
                col = index % _columns;
                row = index / _columns;
            }
        }

        private int SlotIndex(int col, int row)
        {
            if (_columnMajor)
                return col * _rows + row;
            return row * _columns + col;
        }

        private void Resubscribe()
        {
            NearbyIndex.Tick();
            float range = EffectiveDisplayRange();
            Vector3 origin = transform.position;
            var next = new List<Container>();
            var ids = new List<int>();

            // Prefer registered chests around the board (not only the player scan cache).
            NearbyIndex.CollectNear(origin, range, _watchScratch);
            for (int i = 0; i < _watchScratch.Count; i++)
            {
                Container container = _watchScratch[i];
                if (container == null)
                    continue;
                if (!ContainerFilter.IsPlayerBuiltStorage(container))
                    continue;
                if (!ContainerFilter.PlayerMayUse(container, origin))
                    continue;
                if (ChestNames.IsIgnored(container))
                    continue;

                // Unopened chests often have null/empty inv until Load — required for displays.
                NearbyIndex.EnsureInventory(container);
                Inventory inv = container.GetInventory();
                if (inv == null)
                    continue;

                next.Add(container);
                ids.Add(container.GetInstanceID());
            }

            ids.Sort();
            bool same = ids.Count == _watchIds.Count;
            if (same)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i] != _watchIds[i])
                    {
                        same = false;
                        break;
                    }
                }
            }

            if (same)
            {
                // Still refresh inventories so counts stay current without reopening chests.
                for (int i = 0; i < _watched.Count; i++)
                    NearbyIndex.EnsureInventory(_watched[i]);
                return;
            }

            Unwatch();
            foreach (Container container in next)
            {
                Inventory inv = container.GetInventory();
                if (inv != null)
                    inv.m_onChanged += MarkDirty;
                _watched.Add(container);
            }
            _watchIds.Clear();
            _watchIds.AddRange(ids);
            _dirty = true;
            _cluster = null;
        }

        private void Unwatch()
        {
            foreach (Container container in _watched)
            {
                Inventory inv = container != null ? container.GetInventory() : null;
                if (inv != null)
                    inv.m_onChanged -= MarkDirty;
            }
            _watched.Clear();
            _watchIds.Clear();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private List<StorageDisplayBoard> Cluster()
        {
            if (_cluster != null && Time.time < _clusterUntil)
                return _cluster;

            var cluster = new List<StorageDisplayBoard>();
            var queue = new Queue<StorageDisplayBoard>();
            var seen = new HashSet<int>();
            float link = NearbyIndex.AccessRange();
            List<int> myFilters = FilterIds();
            List<string> myItems = ItemTokens();
            string myItem = _kind == DisplayKind.Small ? ItemToken() : "";
            float myRange = EffectiveDisplayRange();

            queue.Enqueue(this);
            seen.Add(GetInstanceID());

            while (queue.Count > 0)
            {
                StorageDisplayBoard cur = queue.Dequeue();
                if (cur == null)
                    continue;
                cluster.Add(cur);
                Vector3 pos = cur.transform.position;
                for (int i = All.Count - 1; i >= 0; i--)
                {
                    StorageDisplayBoard other = All[i];
                    if (other == null)
                    {
                        All.RemoveAt(i);
                        continue;
                    }
                    if (!seen.Add(other.GetInstanceID()))
                        continue;
                    if (other._kind != _kind)
                        continue;
                    if (Mathf.Abs(other.EffectiveDisplayRange() - myRange) > 0.1f)
                        continue;
                    if (_kind == DisplayKind.Small)
                    {
                        if (string.IsNullOrEmpty(myItem)
                            || !string.Equals(other.ItemToken(), myItem, System.StringComparison.Ordinal))
                            continue;
                    }
                    else if (!DisplayFilters.SameIds(other.FilterIds(), myFilters)
                        || !DisplayFilters.SameItemTokens(other.ItemTokens(), myItems))
                    {
                        continue;
                    }
                    else if (myFilters.Count == 0 && myItems.Count == 0)
                    {
                        continue;
                    }
                    if (Vector3.Distance(pos, other.transform.position) <= link)
                        queue.Enqueue(other);
                }
            }

            cluster.Sort(CompareBoards);
            _cluster = cluster;
            _clusterUntil = Time.time + 1.5f;
            return cluster;
        }

        private static int CompareBoards(StorageDisplayBoard a, StorageDisplayBoard b)
        {
            ZDOID ia = ZdoId(a);
            ZDOID ib = ZdoId(b);
            int byId = ia.ID.CompareTo(ib.ID);
            if (byId != 0)
                return byId;
            int byUser = ia.UserID.CompareTo(ib.UserID);
            if (byUser != 0)
                return byUser;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private static ZDOID ZdoId(StorageDisplayBoard board)
        {
            if (board == null)
                return ZDOID.None;
            ZNetView nv = board.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return ZDOID.None;
            ZDO zdo = nv.GetZDO();
            return zdo != null ? zdo.m_uid : ZDOID.None;
        }

        private void RefreshClusterPages()
        {
            List<StorageDisplayBoard> cluster = Cluster();
            _pages = Mathf.Max(1, cluster.Count);
            _page = Mathf.Max(0, cluster.IndexOf(this));
        }

        private void Paint()
        {
            if (_slots == null)
                return;

            ApplyTitle();
            ClearHeaders();

            if (_kind == DisplayKind.Small)
            {
                PaintExactItem();
                return;
            }

            List<int> filters = FilterIds();
            List<string> itemTokens = ItemTokens();
            if (filters.Count == 0 && itemTokens.Count == 0)
            {
                for (int i = 0; i < _slotCount; i++)
                    ClearSlot(i);
                return;
            }

            List<StorageDisplayBoard> cluster = Cluster();
            _pages = Mathf.Max(1, cluster.Count);
            _page = Mathf.Max(0, cluster.IndexOf(this));

            if (_kind == DisplayKind.Large && _flowSections)
            {
                PaintLargeSections(cluster, filters, itemTokens);
                return;
            }

            PaintRanked(cluster, filters, itemTokens);
        }

        private void PaintExactItem()
        {
            string token = ItemToken();
            if (string.IsNullOrEmpty(token))
            {
                for (int i = 0; i < _slotCount; i++)
                    ClearSlot(i);
                return;
            }

            string shared = ItemIds.SharedFromToken(token);
            List<StorageDisplayBoard> cluster = Cluster();
            int total = 0;
            var seenChest = new HashSet<int>();
            ItemDrop.ItemData sample = null;
            foreach (StorageDisplayBoard board in cluster)
            {
                if (board == null)
                    continue;
                foreach (Container container in board._watched)
                {
                    if (container == null || !seenChest.Add(container.GetInstanceID()))
                        continue;
                    NearbyIndex.EnsureInventory(container);
                    Inventory inv = container.GetInventory();
                    if (inv == null)
                        continue;

                    int n = 0;
                    if (!string.IsNullOrEmpty(shared))
                        n = inv.CountItems(shared, -1, true);
                    if (n <= 0)
                    {
                        foreach (ItemDrop.ItemData item in inv.GetAllItems())
                        {
                            if (item?.m_shared == null || item.m_stack <= 0)
                                continue;
                            if (!ItemIds.Matches(item, token) && !ItemIds.Matches(item, shared))
                                continue;
                            n += item.m_stack;
                            if (sample == null)
                                sample = item;
                        }
                    }
                    else if (sample == null)
                    {
                        foreach (ItemDrop.ItemData item in inv.GetAllItems())
                        {
                            if (item?.m_shared == null || item.m_stack <= 0)
                                continue;
                            if (ItemIds.Matches(item, token) || ItemIds.Matches(item, shared))
                            {
                                sample = item;
                                break;
                            }
                        }
                    }

                    total += n;
                }
            }

            if (sample == null)
                sample = SampleFromToken(token);

            for (int i = 0; i < _slotCount; i++)
            {
                if (i == 0 && sample != null)
                {
                    _slots[i].Icon.sprite = StackLimits.Icon(sample);
                    _slots[i].Icon.enabled = _slots[i].Icon.sprite != null;
                    _slots[i].Icon.color = Color.white;
                    SetAmount(i, FormatCount(total));
                }
                else
                {
                    ClearSlot(i);
                }
            }
        }

        private static ItemDrop.ItemData SampleFromToken(string token)
        {
            GameObject prefab = ItemIds.PrefabFromToken(token);
            if (prefab == null)
                return null;
            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            return drop != null ? drop.m_itemData : null;
        }

        private void PaintLargeSections(
            List<StorageDisplayBoard> cluster,
            List<int> filters,
            List<string> itemTokens)
        {
            ClearHeaders();
            List<int> sections = BuildSectionOrder(filters, itemTokens);
            if (sections.Count == 0)
            {
                for (int i = 0; i < _slotCount; i++)
                    ClearSlot(i);
                return;
            }

            // Flatten to paint ops so cluster pages share one continuous flow.
            var ops = new List<SectionOp>(64);
            int cursor = 0;
            for (int s = 0; s < sections.Count; s++)
            {
                int catId = sections[s];

                // Start each category on a new row (keeps headers readable).
                if (cursor % _columns != 0)
                {
                    int pad = _columns - (cursor % _columns);
                    for (int p = 0; p < pad; p++)
                    {
                        ops.Add(new SectionOp { Kind = OpKind.Pad });
                        cursor++;
                    }
                }

                ops.Add(new SectionOp { Kind = OpKind.Header, CategoryId = catId });
                cursor++;

                List<int> oneFilter = null;
                List<string> oneTokens = null;
                SplitSelectionForSection(catId, filters, itemTokens, out oneFilter, out oneTokens);
                var ranked = RankItems(cluster, oneFilter, oneTokens, false);
                if (ranked.Count == 0)
                {
                    // Keep empty selected categories visible (e.g. Fish = 0 in storage).
                    ops.Add(new SectionOp { Kind = OpKind.Empty, CategoryId = catId });
                    cursor++;
                }
                else
                {
                    for (int r = 0; r < ranked.Count; r++)
                    {
                        ops.Add(new SectionOp { Kind = OpKind.Item, CategoryId = catId, Item = ranked[r] });
                        cursor++;
                    }
                }
            }

            int start = _page * _slotCount;
            bool lastBoard = _page >= _pages - 1;
            int remaining = Mathf.Max(0, ops.Count - start);
            int extraAfterThis = Mathf.Max(0, remaining - _slotCount);
            int shown = remaining;
            if (lastBoard && extraAfterThis > 0)
                shown = _slotCount - 1;
            else
                shown = Mathf.Min(_slotCount, remaining);

            for (int i = 0; i < _slotCount; i++)
            {
                if (lastBoard && extraAfterThis > 0 && i == _slotCount - 1)
                {
                    ClearSlot(i);
                    SetAmount(i, "+" + extraAfterThis, new Color(1f, 0.85f, 0.45f, 1f));
                    continue;
                }

                int index = start + i;
                if (i >= shown || index >= ops.Count)
                {
                    ClearSlot(i);
                    continue;
                }

                SectionOp op = ops[index];
                if (op.Kind == OpKind.Header)
                {
                    ClearSlot(i);
                    SetAmount(i, DisplayFilters.Label(op.CategoryId).ToUpperInvariant(),
                        new Color(1f, 0.92f, 0.55f, 1f));
                }
                else if (op.Kind == OpKind.Empty)
                {
                    ClearSlot(i);
                    SetAmount(i, "0", new Color(0.75f, 0.7f, 0.55f, 1f));
                }
                else if (op.Kind == OpKind.Item)
                {
                    PaintSlot(i, op.Item);
                }
                else
                {
                    ClearSlot(i);
                }
            }
        }

        private enum OpKind
        {
            Header,
            Item,
            Empty,
            Pad
        }

        private struct SectionOp
        {
            public OpKind Kind;
            public int CategoryId;
            public RankedItem Item;
        }

        /// <summary>
        /// Ordered category sections from selected filters + parents of item tokens.
        /// Empty selected categories stay in the list so the board shows 0 stock.
        /// </summary>
        private static List<int> BuildSectionOrder(List<int> filters, List<string> itemTokens)
        {
            var sections = new List<int>();
            var seen = new HashSet<int>();

            if (filters != null)
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    int id = filters[i];
                    if (id <= 0 || !seen.Add(id))
                        continue;
                    DisplayFilter unused;
                    if (DisplayFilters.TryGet(id, out unused))
                        sections.Add(id);
                }
            }

            if (itemTokens != null)
            {
                for (int i = 0; i < itemTokens.Count; i++)
                {
                    int parent = DisplayFilters.ParentFilterIdFromToken(itemTokens[i]);
                    if (parent <= 0 || !seen.Add(parent))
                        continue;
                    sections.Add(parent);
                }
            }

            sections.Sort((a, b) =>
                DisplayFilters.CategorySortOrder(a).CompareTo(DisplayFilters.CategorySortOrder(b)));
            return sections;
        }

        private static void SplitSelectionForSection(
            int catId,
            List<int> filters,
            List<string> itemTokens,
            out List<int> oneFilter,
            out List<string> oneTokens)
        {
            oneFilter = null;
            oneTokens = null;
            bool whole = filters != null && filters.Contains(catId);
            if (whole)
            {
                oneFilter = new List<int> { catId };
                return;
            }

            // Only specific items under this category.
            oneTokens = new List<string>();
            if (itemTokens == null)
                return;
            for (int i = 0; i < itemTokens.Count; i++)
            {
                string token = itemTokens[i];
                if (DisplayFilters.ParentFilterIdFromToken(token) == catId)
                    oneTokens.Add(token);
            }
        }

        private void PaintLargeByFilter(List<StorageDisplayBoard> cluster, List<int> filters)
        {
            // Legacy path kept unused; flowing sections replaced fixed 3-column layout.
            PaintLargeSections(cluster, filters, null);
        }

        private void PaintRanked(
            List<StorageDisplayBoard> cluster,
            List<int> filters,
            List<string> itemTokens)
        {
            bool groupByCategory = _kind == DisplayKind.Medium;
            var ranked = RankItems(cluster, filters, itemTokens, groupByCategory);
            int start = _page * _slotCount;
            bool lastBoard = _page >= _pages - 1;
            int remaining = Mathf.Max(0, ranked.Count - start);
            int extraAfterThis = Mathf.Max(0, remaining - _slotCount);
            int shown = remaining;
            if (lastBoard && extraAfterThis > 0)
                shown = _slotCount - 1;
            else
                shown = Mathf.Min(_slotCount, remaining);

            if (_columnHeaders)
            {
                string cats = DisplayFilters.Label(filters, itemTokens);
                if (_headerColumns >= 1)
                    SetHeader(0, cats);
                for (int h = 1; h < _headerColumns; h++)
                    SetHeader(h, "");
            }

            for (int i = 0; i < _slotCount; i++)
            {
                if (lastBoard && extraAfterThis > 0 && i == _slotCount - 1)
                {
                    ClearSlot(i);
                    SetAmount(i, "+" + extraAfterThis);
                    continue;
                }

                int index = start + i;
                if (i >= shown || index >= ranked.Count)
                {
                    ClearSlot(i);
                    continue;
                }

                PaintSlot(i, ranked[index]);
            }
        }

        private struct RankedItem
        {
            public ItemDrop.ItemData Sample;
            public int Count;
            public int CategoryId;
            public int CategoryOrder;
        }

        private List<RankedItem> RankItems(
            List<StorageDisplayBoard> cluster,
            List<int> filters,
            List<string> itemTokens,
            bool groupByCategory = false)
        {
            var totals = new Dictionary<string, int>();
            var sample = new Dictionary<string, ItemDrop.ItemData>();
            var seenChest = new HashSet<int>();
            bool hasFilters = filters != null && filters.Count > 0;
            bool hasItems = itemTokens != null && itemTokens.Count > 0;
            if (!hasFilters && !hasItems)
                return new List<RankedItem>();

            foreach (StorageDisplayBoard board in cluster)
            {
                if (board == null)
                    continue;
                foreach (Container container in board._watched)
                {
                    if (container == null || !seenChest.Add(container.GetInstanceID()))
                        continue;
                    NearbyIndex.EnsureInventory(container);
                    Inventory inv = container.GetInventory();
                    if (inv == null)
                        continue;
                    foreach (ItemDrop.ItemData item in inv.GetAllItems())
                    {
                        if (item?.m_shared == null || item.m_stack <= 0)
                            continue;
                        if (!DisplayFilters.MatchesSelection(item, filters, itemTokens))
                            continue;
                        string key = item.m_shared.m_name;
                        int n;
                        totals.TryGetValue(key, out n);
                        totals[key] = n + item.m_stack;
                        if (!sample.ContainsKey(key))
                            sample[key] = item;
                    }
                }
            }

            var ranked = new List<RankedItem>(totals.Count);
            foreach (KeyValuePair<string, int> pair in totals)
            {
                ItemDrop.ItemData item;
                sample.TryGetValue(pair.Key, out item);
                int catId = CategoryIdForItem(item, filters, itemTokens);
                ranked.Add(new RankedItem
                {
                    Sample = item,
                    Count = pair.Value,
                    CategoryId = catId,
                    CategoryOrder = DisplayFilters.CategorySortOrder(catId)
                });
            }

            if (groupByCategory)
            {
                ranked.Sort((a, b) =>
                {
                    int byCat = a.CategoryOrder.CompareTo(b.CategoryOrder);
                    if (byCat != 0)
                        return byCat;
                    int byCount = b.Count.CompareTo(a.Count);
                    if (byCount != 0)
                        return byCount;
                    string na = a.Sample?.m_shared != null ? a.Sample.m_shared.m_name : "";
                    string nb = b.Sample?.m_shared != null ? b.Sample.m_shared.m_name : "";
                    return string.CompareOrdinal(na, nb);
                });
            }
            else
            {
                ranked.Sort((a, b) =>
                {
                    int byCount = b.Count.CompareTo(a.Count);
                    if (byCount != 0)
                        return byCount;
                    string na = a.Sample?.m_shared != null ? a.Sample.m_shared.m_name : "";
                    string nb = b.Sample?.m_shared != null ? b.Sample.m_shared.m_name : "";
                    return string.CompareOrdinal(na, nb);
                });
            }
            return ranked;
        }

        private static int CategoryIdForItem(
            ItemDrop.ItemData item,
            List<int> filters,
            List<string> itemTokens)
        {
            if (item == null)
                return 0;

            // Prefer a selected filter that matches this item (Choices order among selected).
            if (filters != null && filters.Count > 0)
            {
                int best = 0;
                int bestOrder = int.MaxValue;
                for (int i = 0; i < filters.Count; i++)
                {
                    int id = filters[i];
                    if (!DisplayFilters.Matches(item, id))
                        continue;
                    int order = DisplayFilters.CategorySortOrder(id);
                    if (order < bestOrder)
                    {
                        bestOrder = order;
                        best = id;
                    }
                }
                if (best > 0)
                    return best;
            }

            return DisplayFilters.ParentFilterId(item);
        }

        private void PaintSlot(int i, RankedItem entry)
        {
            _slots[i].Icon.sprite = StackLimits.Icon(entry.Sample);
            _slots[i].Icon.enabled = _slots[i].Icon.sprite != null;
            _slots[i].Icon.color = Color.white;
            SetAmount(i, FormatCount(entry.Count));
        }

        private void ClearHeaders()
        {
            if (_headers == null)
                return;
            for (int i = 0; i < _headers.Length; i++)
                SetHeader(i, "");
        }

        private void SetHeader(int col, string text)
        {
            if (_headers == null || col < 0 || col >= _headers.Length)
                return;
            TextMeshProUGUI header = _headers[col];
            if (header == null)
                return;
            string next = text ?? "";
            if (header.text == next)
                return;
            header.enabled = true;
            header.text = next;
        }

        private void ClearSlot(int i)
        {
            _slots[i].Icon.enabled = false;
            _slots[i].Icon.sprite = null;
            SetAmount(i, "");
        }

        private void SetAmount(int i, string text)
        {
            SetAmount(i, text, new Color(1f, 0.95f, 0.75f, 1f));
        }

        private void SetAmount(int i, string text, Color color)
        {
            TextMeshProUGUI amount = _slots[i].Amount;
            if (amount == null)
                return;
            string next = text ?? "";
            amount.enabled = true;
            amount.color = color;
            amount.faceColor = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255));
            if (amount.text != next)
                amount.text = next;
        }

        private static string FormatCount(int count)
        {
            if (count < 10000)
                return count.ToString();
            if (count < 100000)
                return (count / 1000f).ToString("0.#") + "k";
            return (count / 1000).ToString() + "k";
        }
    }
}
