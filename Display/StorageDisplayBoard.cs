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
        public const string ZdoScaleKey = "sac_ui_scale";
        public const string ZdoShowNameKey = "sac_show_name";
        public const string ZdoShowAmountKey = "sac_show_amount";
        public const int ScaleMin = -10;
        public const int ScaleMax = 10;

        private static readonly List<StorageDisplayBoard> All = new List<StorageDisplayBoard>();

        private DisplayKind _kind = DisplayKind.Medium;
        private int _slotCount = 12;
        private int _columns = 4;
        private int _rows = 3;
        private int _baseColumns = 4;
        private int _baseRows = 3;
        private int _headerColumns;
        private int _itemsPerGroup = 1;
        private float _fontFactor = 0.22f;
        private float _titleFactor = 0.12f;
        private bool _columnMajor;
        private bool _columnHeaders;
        private bool _tightSlots;
        private bool _flowSections;
        private float _labelWidth;
        private int _builtBandCount = -1;
        private int _builtScaleStep = int.MinValue;
        private int _builtShowName = int.MinValue;
        private int _builtShowAmount = int.MinValue;
        private TextMeshProUGUI[] _bandLabels;
        private RectTransform _gridRoot;

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
        /// <summary>Chest discovery / subscribe list — only when selection, range, or rare discovery.</summary>
        private bool _watchDirty = true;
        private bool _configured;
        private int _page;
        private int _pages = 1;
        private List<StorageDisplayBoard> _cluster;
        private float _clusterUntil;

        private struct SlotUi
        {
            public Image Icon;
            public TextMeshProUGUI Amount;
            public TextMeshProUGUI Name;
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
                    // Slightly larger count than before; icon size is handled in CreateSlotCell.
                    _fontFactor = 0.34f;
                    _titleFactor = 0.16f;
                    _columnMajor = false;
                    _columnHeaders = false;
                    _tightSlots = false;
                    _flowSections = false;
                    break;
                case DisplayKind.Large:
                    // Fixed 12-row table (label | items). Do not rebuild when filter count changes.
                    _headerColumns = 0;
                    _itemsPerGroup = 1;
                    // Dense row at scale 0; higher scale → fewer, bigger cells (down to 4).
                    _columns = 13;
                    _rows = DisplayFilters.MaxCategories; // 12
                    _slotCount = _columns * _rows;
                    _fontFactor = 0.18f / 3f;
                    _titleFactor = 0.10f / 3f;
                    _columnMajor = false;
                    _columnHeaders = false;
                    _tightSlots = true;
                    _flowSections = true;
                    // Room for WEAPONS; label X nudge is BuildLargeUi offsetMin (-0.3).
                    _labelWidth = 0.10f;
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

            _baseColumns = _columns;
            _baseRows = _rows;
            ApplyScaleToLayout();
        }

        public int ContentScaleStep()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return 0;
            return Mathf.Clamp(zdo.GetInt(ZdoScaleKey, 0), ScaleMin, ScaleMax);
        }

        public float ContentScaleMul()
        {
            // −10…+10 → 0.2…1.8 (readable at the extremes).
            return 1f + ContentScaleStep() * 0.08f;
        }

        public void SetContentScaleStep(int step)
        {
            step = Mathf.Clamp(step, ScaleMin, ScaleMax);
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                nv?.ClaimOwnership();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return;
            if (zdo.GetInt(ZdoScaleKey, 0) == step)
                return;
            zdo.Set(ZdoScaleKey, step);
            InvalidateUi();
            // Drop the live grid immediately so Update/TryBuild rebuilds with the new columns.
            if (_gridRoot != null)
            {
                Destroy(_gridRoot.gameObject);
                _gridRoot = null;
            }
            _slots = null;
            MarkDirty();
        }

        /// <summary>
        /// Cycle 0 → +1…+10 → −10…−1 → 0 → … (this board only).
        /// </summary>
        public void CycleContentScaleStep()
        {
            if (_kind != DisplayKind.Medium && _kind != DisplayKind.Large)
                return;
            int step = ContentScaleStep();
            int next = step >= ScaleMax ? ScaleMin : step + 1;
            SetContentScaleStep(next);
        }

        /// <summary>Hover: only the current step, e.g. <c>Display Scale : +3</c>.</summary>
        public string FormatHoverScaleLine()
        {
            return Loc.T("Display Scale", "Display Scale") + " : "
                + "<color=yellow><b>" + FormatScaleStep(ContentScaleStep()) + "</b></color>";
        }

        /// <summary>
        /// Small: All → no name → icon only → All.
        /// </summary>
        public void CycleSmallDisplayMode()
        {
            if (_kind != DisplayKind.Small)
                return;

            bool name = ShowName();
            bool amount = ShowAmount();
            if (name && amount)
                WriteShowFlags(false, true);   // name off
            else if (!name && amount)
                WriteShowFlags(false, false);  // icon only
            else
                WriteShowFlags(true, true);    // all on
        }

        public string FormatHoverSmallModeLine()
        {
            string mode;
            if (ShowName() && ShowAmount())
                mode = Loc.T("Name + Amount", "Name + Anzahl");
            else if (!ShowName() && ShowAmount())
                mode = Loc.T("No name", "Name weg");
            else
                mode = Loc.T("Icon only", "Nur Icon");
            return Loc.T("Display", "Display") + " : "
                + "<color=yellow><b>" + mode + "</b></color>";
        }

        /// <summary>
        /// Shift+LMB: exactly one cycle on mouse/attack button <b>down</b> (not while held, not on release).
        /// StartAttack only blocks the swing — it must not cycle (fires many times per click).
        /// </summary>
        private static int _cycleFrame = -1;

        public static void TickCycleInput()
        {
            if (!IsScaleChordHeld())
                return;

            bool pressed = Input.GetMouseButtonDown(0);
            try
            {
                if (ZInput.GetButtonDown("Attack"))
                    pressed = true;
            }
            catch
            {
            }

            if (!pressed)
                return;

            // Same frame can see both mouse and Attack down — only once.
            if (_cycleFrame == Time.frameCount)
                return;
            _cycleFrame = Time.frameCount;

            TryCycleHovered();
        }

        public static bool TryCycleHovered()
        {
            if (!IsScaleChordHeld())
                return false;

            Player player = Player.m_localPlayer;
            if (player == null)
                return false;

            StorageDisplayBoard board = HoveredBoard();
            if (board == null)
                return false;

            if (!PrivateArea.CheckAccess(board.transform.position, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            // Scale / mode is always per hovered board ZDO only (never the cluster).
            if (board.Kind == DisplayKind.Small)
                board.CycleSmallDisplayMode();
            else if (board.Kind == DisplayKind.Medium || board.Kind == DisplayKind.Large)
                board.CycleContentScaleStep();
            else
                return false;

            return true;
        }

        public static bool IsScaleChordHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>True when Shift is held over a display that uses the cycle chord.</summary>
        public static bool ShouldBlockAttackForCycle()
        {
            if (!IsScaleChordHeld())
                return false;
            StorageDisplayBoard board = HoveredBoard();
            if (board == null)
                return false;
            return board.Kind == DisplayKind.Small
                || board.Kind == DisplayKind.Medium
                || board.Kind == DisplayKind.Large;
        }

        private static StorageDisplayBoard HoveredBoard()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;

            GameObject hover = player.GetHoverObject();
            StorageDisplayBoard fromHover = hover != null
                ? hover.GetComponentInParent<StorageDisplayBoard>()
                : null;
            if (fromHover != null)
                return fromHover;

            Piece piece = player.GetHoveringPiece();
            return piece != null ? piece.GetComponent<StorageDisplayBoard>() : null;
        }

        public bool ShowName()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return true;
            return zdo.GetInt(ZdoShowNameKey, 1) != 0;
        }

        public bool ShowAmount()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return true;
            return zdo.GetInt(ZdoShowAmountKey, 1) != 0;
        }

        public void SetShowName(bool on)
        {
            WriteShowFlags(on, ShowAmount());
        }

        public void SetShowAmount(bool on)
        {
            WriteShowFlags(ShowName(), on);
        }

        private void WriteShowFlags(bool name, bool amount)
        {
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                nv?.ClaimOwnership();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return;

            int nameV = name ? 1 : 0;
            int amountV = amount ? 1 : 0;
            bool changed = false;
            if (zdo.GetInt(ZdoShowNameKey, 1) != nameV)
            {
                zdo.Set(ZdoShowNameKey, nameV);
                changed = true;
            }
            if (zdo.GetInt(ZdoShowAmountKey, 1) != amountV)
            {
                zdo.Set(ZdoShowAmountKey, amountV);
                changed = true;
            }
            if (!changed)
                return;

            // Layout (icon position) depends on flags — rebuild, don't only hide labels.
            InvalidateUi();
            if (_gridRoot != null)
            {
                Destroy(_gridRoot.gameObject);
                _gridRoot = null;
            }
            _slots = null;
            MarkDirty();
        }

        public void InvalidateUi()
        {
            _slots = null;
            _headers = null;
            _bandLabels = null;
            _builtScaleStep = int.MinValue;
            _builtShowName = int.MinValue;
            _builtShowAmount = int.MinValue;
            _builtBandCount = -1;
        }

        private void ApplyScaleToLayout()
        {
            float mul = ContentScaleMul();
            if (_kind == DisplayKind.Medium)
            {
                // Keep changing density across the full −10…+10 range (old −step hit the floor at +2/+5).
                _columns = Mathf.Clamp(Mathf.RoundToInt(_baseColumns / mul), 2, 8);
                _rows = _baseRows;
                _slotCount = _columns * _rows;
                _itemsPerGroup = _columns;
            }
            else if (_kind == DisplayKind.Large)
            {
                // Min 4 so +6…+10 still grows cells (was floored at 8 → icons froze, only fonts grew).
                _columns = Mathf.Clamp(Mathf.RoundToInt(_baseColumns / mul), 4, 18);
                _rows = DisplayFilters.MaxCategories;
                _slotCount = _columns * _rows;
            }
            // Small: no content scale.
        }

        /// <summary>
        /// Font / icon sizing vs default — tracks the scale step across the full −10…+10 range.
        /// </summary>
        private float CellSizeScale()
        {
            if (_kind != DisplayKind.Medium && _kind != DisplayKind.Large)
                return 1f;
            return Mathf.Clamp(ContentScaleMul(), 0.35f, 1.85f);
        }

        /// <summary>How much of each cell the icon may own (rest is the count).</summary>
        private void SlotIconShare(out float iconMax, out float textMin)
        {
            float cell = CellSizeScale();
            if (_kind == DisplayKind.Small)
            {
                iconMax = 0.40f;
                textMin = 0.40f;
                return;
            }

            float baseIcon = _tightSlots ? 0.40f : 0.46f;
            // Grow icon share with scale so sprites keep up with amount font past +5.
            float t = Mathf.InverseLerp(1f, 1.85f, Mathf.Max(1f, cell));
            iconMax = Mathf.Lerp(baseIcon, 0.72f, t);
            textMin = Mathf.Min(0.78f, iconMax + 0.03f);
        }

        public static string FormatScaleStep(int step)
        {
            step = Mathf.Clamp(step, ScaleMin, ScaleMax);
            if (step > 0)
                return "+" + step;
            return step.ToString();
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
            _watchDirty = true;
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
            DisplaySmallOptions.CloseIf(this);
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
                string modeLine = "[<color=yellow><b>Shift+LMB</b></color>] " + FormatHoverSmallModeLine();
                string token = ItemToken();
                if (string.IsNullOrEmpty(token))
                    return BoardTitle() + "\n" + hotbar + "\n" + modeLine + "\n" + rangeLine;
                return BoardTitle() + " (" + ItemLabel(token) + ")\n" + hotbar + "\n" + modeLine + "\n" + rangeLine;
            }

            List<int> filters = FilterIds();
            List<string> items = ItemTokens();
            string name = DisplayFilters.Label(filters, items);
            string use = "[<color=yellow><b>E</b></color>] " + Loc.T("Select type", "Typ wählen");
            string scaleKey = "[<color=yellow><b>Shift+LMB</b></color>] ";
            string scaleLine = scaleKey + FormatHoverScaleLine();
            if (filters.Count == 0 && items.Count == 0)
                return BoardTitle() + "\n" + use + "\n" + scaleLine + "\n" + rangeLine;

            RefreshClusterPages();
            string title = BoardTitle() + " (" + name + ")";
            if (_pages > 1)
                title += " (" + (_page + 1) + "/" + _pages + ")";
            return title + "\n" + use + "\n" + scaleLine + "\n" + rangeLine;
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
            _watchDirty = true;
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
            if (InventoryGui.IsVisible() || DisplayTypeMenu.IsOpen || StationFilterMenu.IsOpen
                || DisplayRangeMenu.IsOpen || DisplaySmallOptions.IsOpen)
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
            _watchDirty = true;
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
                if (!CanAddCategory(ids, items, id))
                    return;
                ids.Add(id);
                // Whole category selected: drop fine-grained picks under it.
                if (DisplayFilters.IsExpandable(id))
                    items = RemoveCategoryItems(items, id);
                // Epic Loot "All" replaces Dust/Essence/Reagent/Shard bands.
                if (id == DisplayFilters.EpicLootGroupId)
                    RemoveEpicLootSubFilters(ids);
                // A subtype band replaces the Epic Loot "All" parent label.
                if (DisplayFilters.IsEpicLootSubFilter(id))
                    ids.Remove(DisplayFilters.EpicLootGroupId);
            }
            WriteSelection(ids, items);
        }

        private static void RemoveEpicLootSubFilters(List<int> ids)
        {
            if (ids == null)
                return;
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (DisplayFilters.IsEpicLootSubFilter(ids[i]))
                    ids.RemoveAt(i);
            }
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
                // Adding a token whose parent is not already represented counts as a new category.
                int parent = parentFilterId > 0 ? parentFilterId : DisplayFilters.ParentFilterIdFromToken(shared);
                if (parent > 0 && !ids.Contains(parent) && !CategoryRepresentedByItems(items, parent))
                {
                    if (!CanAddCategory(ids, items, parent))
                        return;
                }
                items.Add(shared);
                if (parentFilterId > 0)
                    ids.Remove(parentFilterId);
            }
            WriteSelection(ids, items);
        }

        private static bool CategoryRepresentedByItems(List<string> items, int parentId)
        {
            if (items == null || parentId <= 0)
                return false;
            for (int i = 0; i < items.Count; i++)
            {
                if (DisplayFilters.ParentFilterIdFromToken(items[i]) == parentId)
                    return true;
            }
            return false;
        }

        private static bool CanAddCategory(List<int> ids, List<string> items, int newId)
        {
            var probeIds = new List<int>(ids ?? new List<int>());
            if (newId > 0 && !probeIds.Contains(newId))
                probeIds.Add(newId);
            int count = BuildSectionOrder(probeIds, items).Count;
            if (count <= DisplayFilters.MaxCategories)
                return true;

            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T(
                        "You reached the maximum of 12 categories",
                        "Maximal 12 Kategorien erreicht"),
                    0, null, false);
            }
            return false;
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

        public bool TryOpenMenu(Humanoid user, bool alt = false)
        {
            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer)
                return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            // Small: Alt+E (alt use) opens Name / Amount toggles. Plain E does nothing special
            // (hotbar 1-8 while looking at it still assigns — see hover text).
            if (_kind == DisplayKind.Small)
            {
                if (alt)
                    DisplaySmallOptions.Open(this);
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
            // Content scale changed (Medium / Large columns + font).
            if (_slots != null && (_kind == DisplayKind.Medium || _kind == DisplayKind.Large)
                && ContentScaleStep() != _builtScaleStep)
                _slots = null;
            // Small name/amount layout changed — rebuild so icon can recenter.
            if (_slots != null && _kind == DisplayKind.Small
                && ((ShowName() ? 1 : 0) != _builtShowName || (ShowAmount() ? 1 : 0) != _builtShowAmount))
                _slots = null;
            // Medium layout gained a category header strip — rebuild old grids.
            if (_slots != null && _kind == DisplayKind.Medium && _columnHeaders && _headers == null)
                _slots = null;
            // Large switched from fixed 3-column headers to flowing sections.
            if (_slots != null && _kind == DisplayKind.Large && (_headers != null || !_flowSections || _columnMajor))
                _slots = null;
            // Large must keep a full 12-row slot grid (never shrink with filter count).
            if (_slots != null && _kind == DisplayKind.Large && _flowSections
                && (_bandLabels == null || _bandLabels.Length < DisplayFilters.MaxCategories
                    || _slots.Length != _columns * DisplayFilters.MaxCategories))
                _slots = null;
            if (_slots != null && _kind == DisplayKind.Small && _slots.Length == 1
                && (_slots[0].Name == null
                    || (_slots[0].Amount != null && _slots[0].Amount.fontSize < 1f)))
                _slots = null;
            if (_slots == null)
                TryBuild();
            if (_slots == null)
                return;

            // Cheap: keep vanilla sign text off. Avoid ApplyTitle/FilterIds every frame.
            SilenceSignText();

            // Chest discovery only on demand (enable / selection / range) or rare pickup of new chests.
            // Inventory count changes use m_onChanged → MarkDirty → Paint, not a full rescan.
            if (_watchDirty || _watched.Count == 0)
            {
                if (Time.time >= _nextScan)
                {
                    _nextScan = Time.time + 0.35f;
                    Resubscribe();
                    _watchDirty = false;
                }
            }
            else if (Time.time >= _nextScan)
            {
                // Very rare discovery pass for newly placed chests (no ZDO reload when list unchanged).
                _nextScan = Time.time + 30f;
                Resubscribe();
            }

            if (_dirty)
                ApplyTitle();

            if (!_dirty || Time.time < _nextPaint)
                return;

            _nextPaint = Time.time + (_kind == DisplayKind.Large ? 0.5f : 0.3f);
            _dirty = false;
            Paint();
        }

        private void TryBuild()
        {
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return;
            ApplyScaleToLayout();
            BuildUi();
            if (_slots != null)
            {
                _builtScaleStep = ContentScaleStep();
                _builtShowName = ShowName() ? 1 : 0;
                _builtShowAmount = ShowAmount() ? 1 : 0;
                _dirty = true;
            }
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
            _titleFont = template.fontSize * _titleFactor * CellSizeScale();
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

            // Font follows cell size + scale step so icons and amounts grow together.
            float font = template.fontSize * _fontFactor * CellSizeScale();
            float bandFont = template.fontSize * _fontFactor; // category labels stay stable

            if (_kind == DisplayKind.Large && _flowSections)
            {
                BuildLargeUi(root, template, font, bandFont);
                return;
            }

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

            float iconMax;
            float textMin;
            SlotIconShare(out iconMax, out textMin);

            float padX = _tightSlots ? 0.006f : (_kind == DisplayKind.Small ? 0.04f : 0.012f);
            float padY = _tightSlots ? 0.012f : (_kind == DisplayKind.Small ? 0.06f : 0.018f);

            _slots = new SlotUi[_slotCount];
            for (int i = 0; i < _slotCount; i++)
            {
                int col;
                int row;
                SlotCoord(i, out col, out row);
                CreateSlotCell(root.transform, template, font, iconMax, textMin, padX, padY, 1f,
                    col / (float)_columns, (col + 1) / (float)_columns,
                    contentTop * (1f - (row + 1) / (float)_rows),
                    contentTop * (1f - row / (float)_rows),
                    i);
            }
            _bandLabels = null;
            _builtBandCount = -1;
            _gridRoot = rt;
        }

        private void BuildLargeUi(GameObject root, TextMeshProUGUI template, float font, float bandFont)
        {
            _headers = null;
            _gridRoot = root.GetComponent<RectTransform>();
            // Always build the full 12-row chassis once. Paint decides how many rows are used.
            int rows = DisplayFilters.MaxCategories;
            int itemCols = _columns;
            _rows = rows;
            _slotCount = rows * itemCols;
            _builtBandCount = rows;

            float labelW = Mathf.Clamp(_labelWidth, 0.08f, 0.16f);
            float padX = 0.0015f;
            float padY = 0.006f;
            float iconMax;
            float textMin;
            SlotIconShare(out iconMax, out textMin);

            _bandLabels = new TextMeshProUGUI[rows];
            for (int r = 0; r < rows; r++)
            {
                GameObject labelGo = Object.Instantiate(template.gameObject, root.transform);
                labelGo.name = "BandLabel" + r;
                labelGo.SetActive(false);
                RectTransform labelRt = labelGo.GetComponent<RectTransform>();
                float y0 = 1f - (r + 1) / (float)rows;
                float y1 = 1f - r / (float)rows;
                labelRt.anchorMin = new Vector2(0f, y0 + padY);
                labelRt.anchorMax = new Vector2(labelW, y1 - padY);
                // -0.3 canvas units left of the grid edge (soft nudge).
                labelRt.offsetMin = new Vector2(-0.3f, 0f);
                labelRt.offsetMax = Vector2.zero;
                labelRt.localScale = Vector3.one;
                labelRt.localRotation = Quaternion.identity;

                TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
                var localize = labelGo.GetComponent("Localize") as MonoBehaviour;
                if (localize != null)
                    Object.Destroy(localize);
                if (template.font != null)
                    label.font = template.font;
                label.enabled = true;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.margin = Vector4.zero;
                label.enableAutoSizing = false;
                label.fontSize = bandFont;
                label.color = new Color(1f, 0.92f, 0.55f, 1f);
                label.faceColor = new Color32(255, 235, 140, 255);
                label.outlineWidth = 0f;
                label.raycastTarget = false;
                label.text = "";
                _bandLabels[r] = label;
            }

            _slots = new SlotUi[_slotCount];
            int slot = 0;
            for (int r = 0; r < rows; r++)
            {
                float y0 = 1f - (r + 1) / (float)rows;
                float y1 = 1f - r / (float)rows;
                for (int col = 0; col < itemCols; col++)
                {
                    float x0 = labelW + (1f - labelW) * (col / (float)itemCols);
                    float x1 = labelW + (1f - labelW) * ((col + 1) / (float)itemCols);
                    CreateSlotCell(root.transform, template, font, iconMax, textMin, padX, padY, 1f,
                        x0, x1, y0, y1, slot);
                    slot++;
                }
            }
        }

        private void CreateSlotCell(
            Transform parent,
            TextMeshProUGUI template,
            float font,
            float iconMax,
            float textMin,
            float padX,
            float padY,
            float contentTop,
            float x0,
            float x1,
            float y0,
            float y1,
            int index)
        {
            var cell = new GameObject("Slot" + index, typeof(RectTransform));
            cell.transform.SetParent(parent, false);
            RectTransform cellRt = cell.GetComponent<RectTransform>();
            cellRt.anchorMin = new Vector2(x0 + padX, y0 + padY);
            cellRt.anchorMax = new Vector2(x1 - padX, y1 - padY);
            cellRt.offsetMin = Vector2.zero;
            cellRt.offsetMax = Vector2.zero;
            cellRt.localScale = Vector3.one;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(cell.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            if (_kind != DisplayKind.Small)
            {
                iconRt.anchorMin = new Vector2(0.00f, 0.10f);
                iconRt.anchorMax = new Vector2(iconMax, 0.92f);
            }
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
            if (_kind == DisplayKind.Small)
            {
                // Filled in by LayoutSmallIconAndLabels after name exists.
            }
            else
            {
                textRt.anchorMin = new Vector2(textMin, 0.08f);
                textRt.anchorMax = new Vector2(1.00f, 0.92f);
                textRt.pivot = new Vector2(0f, 0.5f);
            }
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
            amount.alignment = TextAlignmentOptions.MidlineLeft;
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.overflowMode = TextOverflowModes.Overflow;
            amount.enableAutoSizing = false;
            amount.fontSize = font;
            if (_kind == DisplayKind.Small)
                amount.fontSize = Mathf.Max(font, template.fontSize * 0.30f);
            amount.color = new Color(1f, 0.95f, 0.75f, 1f);
            amount.faceColor = new Color32(255, 242, 191, 255);
            amount.outlineWidth = 0f;
            amount.raycastTarget = false;
            amount.text = "";

            TextMeshProUGUI nameLabel = null;
            RectTransform nameRt = null;
            if (_kind == DisplayKind.Small)
            {
                GameObject nameGo = Object.Instantiate(template.gameObject, cell.transform);
                nameGo.name = "ItemName";
                nameGo.SetActive(true);
                nameGo.transform.SetAsLastSibling();
                nameRt = nameGo.GetComponent<RectTransform>();
                nameRt.pivot = new Vector2(0.5f, 0.5f);
                nameRt.offsetMin = Vector2.zero;
                nameRt.offsetMax = Vector2.zero;
                nameRt.anchoredPosition = Vector2.zero;
                nameRt.sizeDelta = Vector2.zero;
                nameRt.localScale = Vector3.one;
                nameRt.localRotation = Quaternion.identity;

                nameLabel = nameGo.GetComponent<TextMeshProUGUI>();
                nameLabel.enabled = true;
                var localizeName = nameGo.GetComponent("Localize") as MonoBehaviour;
                if (localizeName != null)
                    Object.Destroy(localizeName);
                if (template.font != null)
                    nameLabel.font = template.font;
                nameLabel.alignment = TextAlignmentOptions.Center;
                nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
                nameLabel.overflowMode = TextOverflowModes.Ellipsis;
                nameLabel.margin = Vector4.zero;
                nameLabel.enableAutoSizing = false;
                nameLabel.fontSize = Mathf.Max(font * 0.55f, template.fontSize * 0.16f);
                nameLabel.color = new Color(1f, 0.92f, 0.55f, 1f);
                nameLabel.faceColor = new Color32(255, 235, 140, 255);
                nameLabel.outlineWidth = 0f;
                nameLabel.raycastTarget = false;
                nameLabel.text = "";

                LayoutSmallIconAndLabels(iconRt, textRt, nameRt);
            }

            _slots[index].Icon = icon;
            _slots[index].Amount = amount;
            _slots[index].Name = nameLabel;
        }

        /// <summary>
        /// Small layouts: All (icon+amount+name) | No name (icon+amount) | Icon only (centered).
        /// </summary>
        private void LayoutSmallIconAndLabels(RectTransform iconRt, RectTransform amountRt, RectTransform nameRt)
        {
            if (iconRt == null)
                return;

            bool showName = ShowName();
            bool showAmount = ShowAmount();

            if (!showName && !showAmount)
            {
                // Icon only — true center (no ghost slot for the count).
                iconRt.anchorMin = new Vector2(0.22f, 0.18f);
                iconRt.anchorMax = new Vector2(0.78f, 0.82f);
                if (amountRt != null)
                {
                    amountRt.anchorMin = Vector2.zero;
                    amountRt.anchorMax = Vector2.zero;
                    amountRt.gameObject.SetActive(false);
                }
                if (nameRt != null)
                {
                    nameRt.anchorMin = Vector2.zero;
                    nameRt.anchorMax = Vector2.zero;
                    nameRt.gameObject.SetActive(false);
                }
                return;
            }

            if (!showName && showAmount)
            {
                // Name off: icon + count, full height, slightly more centered pair.
                iconRt.anchorMin = new Vector2(0.26f, 0.18f);
                iconRt.anchorMax = new Vector2(0.50f, 0.88f);
                if (amountRt != null)
                {
                    amountRt.gameObject.SetActive(true);
                    amountRt.anchorMin = new Vector2(0.52f, 0.18f);
                    amountRt.anchorMax = new Vector2(0.80f, 0.88f);
                    amountRt.pivot = new Vector2(0f, 0.5f);
                }
                if (nameRt != null)
                {
                    nameRt.anchorMin = Vector2.zero;
                    nameRt.anchorMax = Vector2.zero;
                    nameRt.gameObject.SetActive(false);
                }
                return;
            }

            // All on (or name-only from Alt+E): icon + count top, name bottom.
            iconRt.anchorMin = new Vector2(0.28f, 0.38f);
            iconRt.anchorMax = new Vector2(0.50f, 0.92f);
            if (amountRt != null)
            {
                amountRt.gameObject.SetActive(showAmount);
                if (showAmount)
                {
                    amountRt.anchorMin = new Vector2(0.52f, 0.38f);
                    amountRt.anchorMax = new Vector2(0.78f, 0.92f);
                    amountRt.pivot = new Vector2(0f, 0.5f);
                }
                else
                {
                    // Name on, amount off: center icon above the name.
                    iconRt.anchorMin = new Vector2(0.30f, 0.38f);
                    iconRt.anchorMax = new Vector2(0.70f, 0.92f);
                    amountRt.anchorMin = Vector2.zero;
                    amountRt.anchorMax = Vector2.zero;
                }
            }
            if (nameRt != null)
            {
                nameRt.gameObject.SetActive(showName);
                if (showName)
                {
                    nameRt.anchorMin = new Vector2(0.08f, 0.04f);
                    nameRt.anchorMax = new Vector2(0.92f, 0.34f);
                }
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
                if (ChestNames.IsFullyIgnored(container))
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
                // List unchanged: do not reload inventories or force a paint.
                // Counts stay live via Inventory.m_onChanged → MarkDirty.
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
                    SetItemName(i, ItemLabel(sample.m_shared != null ? sample.m_shared.m_name : token));
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

            for (int i = 0; i < _slotCount; i++)
            {
                if (_slots != null && i < _slots.Length && _slots[i].Icon != null)
                    ClearSlot(i);
            }

            if (_bandLabels != null)
            {
                for (int i = 0; i < _bandLabels.Length; i++)
                {
                    if (_bandLabels[i] == null)
                        continue;
                    _bandLabels[i].text = "";
                    _bandLabels[i].gameObject.SetActive(false);
                }
            }

            if (sections.Count == 0)
                return;

            int cats = Mathf.Clamp(sections.Count, 1, DisplayFilters.MaxCategories);
            int rows = DisplayFilters.MaxCategories;
            int itemCols = _columns;
            // Few categories → more rows each; 12 cats → one row each.
            int linesPerCat = Mathf.Max(1, rows / cats);

            // One chest scan for the whole board — not once per category.
            var rankedAll = RankItems(cluster, filters, itemTokens, groupByCategory: true);
            var byCat = new Dictionary<int, List<RankedItem>>();
            for (int i = 0; i < rankedAll.Count; i++)
            {
                RankedItem entry = rankedAll[i];
                int id = entry.CategoryId;
                List<RankedItem> list;
                if (!byCat.TryGetValue(id, out list))
                {
                    list = new List<RankedItem>();
                    byCat[id] = list;
                }
                list.Add(entry);
            }

            for (int c = 0; c < cats; c++)
            {
                int catId = sections[c];
                int row0 = c * linesPerCat;
                if (row0 >= rows)
                    break;

                if (_bandLabels != null && row0 < _bandLabels.Length && _bandLabels[row0] != null)
                {
                    _bandLabels[row0].gameObject.SetActive(true);
                    _bandLabels[row0].text = ShortCategoryLabel(catId);
                }

                List<RankedItem> ranked;
                if (!byCat.TryGetValue(catId, out ranked) || ranked == null)
                    ranked = new List<RankedItem>();

                int capacity = linesPerCat * itemCols;
                int baseSlot = row0 * itemCols;

                if (ranked.Count == 0)
                {
                    if (baseSlot < _slotCount)
                        SetAmount(baseSlot, "0", new Color(0.75f, 0.7f, 0.55f, 1f));
                    continue;
                }

                int extra = Mathf.Max(0, ranked.Count - capacity);
                int show = extra > 0 ? capacity - 1 : Mathf.Min(capacity, ranked.Count);
                for (int i = 0; i < show; i++)
                {
                    int slot = baseSlot + i;
                    if (slot >= _slotCount || _slots[slot].Icon == null)
                        break;
                    PaintSlot(slot, ranked[i]);
                }

                if (extra > 0)
                {
                    int overflowSlot = baseSlot + capacity - 1;
                    if (overflowSlot < _slotCount)
                    {
                        ClearSlot(overflowSlot);
                        // Only "+" — same color/font as before, no leftover count.
                        SetAmount(overflowSlot, "+",
                            new Color(1f, 0.85f, 0.45f, 1f));
                    }
                }
            }
        }

        private static string ShortCategoryLabel(int catId)
        {
            string label = DisplayFilters.Label(catId);
            if (string.IsNullOrEmpty(label))
                return "?";
            // Keep readable but avoid eating item space (CROPS & SEEDS → CROPS).
            int amp = label.IndexOf('&');
            if (amp > 0)
                label = label.Substring(0, amp).Trim();
            int space = label.IndexOf(' ');
            if (space > 0 && label.Length > 10)
                label = label.Substring(0, space);
            // Allow full single words like WEAPONS / UTILITY / INGREDIENTS.
            if (label.Length > 14)
                label = label.Substring(0, 14);
            return label.ToUpperInvariant();
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
                    // Prefer already-loaded inventories; only ZDO-load empty/unopened chests.
                    Inventory inv = container.GetInventory();
                    if (inv == null || inv.NrOfItems() <= 0)
                    {
                        NearbyIndex.EnsureInventory(container);
                        inv = container.GetInventory();
                    }
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
            if (_slots == null || i < 0 || i >= _slots.Length)
                return;
            if (_slots[i].Icon != null)
            {
                _slots[i].Icon.enabled = false;
                _slots[i].Icon.sprite = null;
            }
            SetAmount(i, "");
            SetItemName(i, "");
        }

        private void SetItemName(int i, string text)
        {
            if (_slots == null || i < 0 || i >= _slots.Length)
                return;
            TextMeshProUGUI label = _slots[i].Name;
            if (label == null)
                return;
            if (!ShowName())
            {
                if (label.enabled || !string.IsNullOrEmpty(label.text))
                {
                    label.enabled = false;
                    label.text = "";
                }
                return;
            }
            string next = text ?? "";
            if (label.text == next && label.enabled)
                return;
            label.enabled = true;
            label.text = next;
        }

        private void SetAmount(int i, string text)
        {
            SetAmount(i, text, new Color(1f, 0.95f, 0.75f, 1f));
        }

        private void SetAmount(int i, string text, Color color)
        {
            if (_slots == null || i < 0 || i >= _slots.Length)
                return;
            TextMeshProUGUI amount = _slots[i].Amount;
            if (amount == null)
                return;
            if (!ShowAmount())
            {
                if (amount.enabled || !string.IsNullOrEmpty(amount.text))
                {
                    amount.enabled = false;
                    amount.text = "";
                }
                return;
            }
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
