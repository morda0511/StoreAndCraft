using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    internal class StorageDisplayBoard : MonoBehaviour
    {
        public const int Slots = 12;
        public const int Columns = 4;
        public const int Rows = 3;

        private static readonly List<StorageDisplayBoard> All = new List<StorageDisplayBoard>();

        private readonly List<Container> _watched = new List<Container>();
        private readonly List<int> _watchIds = new List<int>();
        private SlotUi[] _slots;
        private TextMeshProUGUI _signText;
        private float _titleFont;
        private float _born;
        private float _nextScan;
        private float _nextPaint;
        private bool _dirty = true;
        private int _page;
        private int _pages = 1;
        private List<StorageDisplayBoard> _cluster;
        private float _clusterUntil;

        private struct SlotUi
        {
            public Image Icon;
            public TextMeshProUGUI Amount;
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
            if (!All.Contains(this))
                All.Add(this);
            TryBuild();
        }

        private void OnDestroy()
        {
            DisplayTypeMenu.CloseIf(this);
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
            List<int> filters = FilterIds();
            string name = DisplayFilters.Label(filters);
            string use = "[<color=yellow><b>E</b></color>] " + Loc.T("Select type", "Typ wählen");
            if (filters.Count == 0)
                return "Storage Display\n" + use;

            RefreshClusterPages();
            string title = "Storage Display (" + name + ")";
            if (_pages > 1)
                title += " (" + (_page + 1) + "/" + _pages + ")";
            return title + "\n" + use;
        }

        public List<int> FilterIds()
        {
            ZNetView nv = GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            return DisplayFilters.ReadIds(zdo);
        }

        /// <summary>Legacy single-id helper (first selected, or 0).</summary>
        public int FilterId()
        {
            List<int> ids = FilterIds();
            return ids.Count > 0 ? ids[0] : 0;
        }

        public void SetFilter(int id)
        {
            // Replaced by multi-select ToggleFilter; keep for callers that set one type.
            var ids = new List<int>();
            if (id > 0)
                ids.Add(id);
            WriteFilters(ids);
        }

        public void ToggleFilter(int id)
        {
            if (id <= 0)
            {
                WriteFilters(new List<int>());
                return;
            }

            List<int> ids = FilterIds();
            if (ids.Contains(id))
                ids.Remove(id);
            else
                ids.Add(id);
            WriteFilters(ids);
        }

        public void WriteFilters(List<int> ids)
        {
            ZNetView nv = GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();

            string encoded = DisplayFilters.EncodeIds(ids);
            ZDO zdo = nv.GetZDO();
            zdo.Set(DisplayFilters.ZdoKeyMulti, encoded);
            // Keep legacy key in sync for older clients (first id or 0).
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
            DisplayTypeMenu.Open(this);
            return true;
        }

        private void Update()
        {
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
            if (FilterIds().Count > 0)
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
            string title = Loc.T("Select type", "Typ wählen");
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
            _titleFont = template.fontSize * 0.12f;
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

            float font = template.fontSize * 0.22f;

            _slots = new SlotUi[Slots];
            for (int i = 0; i < Slots; i++)
            {
                int col = i % Columns;
                int row = i / Columns;

                var cell = new GameObject("Slot" + i, typeof(RectTransform));
                cell.transform.SetParent(root.transform, false);
                RectTransform cellRt = cell.GetComponent<RectTransform>();
                const float padX = 0.012f;
                const float padY = 0.018f;
                cellRt.anchorMin = new Vector2(col / (float)Columns + padX, 1f - (row + 1) / (float)Rows + padY);
                cellRt.anchorMax = new Vector2((col + 1) / (float)Columns - padX, 1f - row / (float)Rows - padY);
                cellRt.offsetMin = Vector2.zero;
                cellRt.offsetMax = Vector2.zero;
                cellRt.localScale = Vector3.one;

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(cell.transform, false);
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.00f, 0.10f);
                iconRt.anchorMax = new Vector2(0.46f, 0.92f);
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
                textRt.anchorMin = new Vector2(0.46f, 0.08f);
                textRt.anchorMax = new Vector2(1.00f, 0.92f);
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
                textRt.anchoredPosition = Vector2.zero;
                textRt.sizeDelta = Vector2.zero;
                textRt.localScale = Vector3.one;
                textRt.localRotation = Quaternion.identity;

                TextMeshProUGUI amount = textGo.GetComponent<TextMeshProUGUI>();
                amount.enabled = true;
                var localize = textGo.GetComponent("Localize") as MonoBehaviour;
                if (localize != null)
                    Object.Destroy(localize);
                if (template.font != null)
                    amount.font = template.font;
                amount.alignment = TextAlignmentOptions.MidlineLeft;
                amount.textWrappingMode = TextWrappingModes.NoWrap;
                amount.overflowMode = TextOverflowModes.Overflow;
                amount.enableAutoSizing = false;
                amount.fontSize = font;
                amount.color = new Color(1f, 0.95f, 0.75f, 1f);
                amount.faceColor = new Color32(255, 242, 191, 255);
                amount.outlineWidth = 0f;
                amount.raycastTarget = false;
                amount.text = "";

                _slots[i].Icon = icon;
                _slots[i].Amount = amount;
            }
        }

        private void Resubscribe()
        {
            float range = NearbyIndex.AccessRange();
            Collider[] hits = Physics.OverlapSphere(transform.position, range, ~0, QueryTriggerInteraction.Collide);
            var next = new List<Container>();
            var ids = new List<int>();
            var seen = new HashSet<int>();
            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;
                Container container = hit.GetComponentInParent<Container>();
                if (container == null || !seen.Add(container.GetInstanceID()))
                    continue;
                if (!ContainerFilter.PlayerMayUse(container, transform.position))
                    continue;
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
                return;

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
                    if (myFilters.Count == 0 || !DisplayFilters.SameIds(other.FilterIds(), myFilters))
                        continue;
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
            List<int> filters = FilterIds();
            if (filters.Count == 0)
            {
                for (int i = 0; i < Slots; i++)
                    ClearSlot(i);
                return;
            }

            List<StorageDisplayBoard> cluster = Cluster();
            _pages = Mathf.Max(1, cluster.Count);
            _page = Mathf.Max(0, cluster.IndexOf(this));

            var totals = new Dictionary<string, int>();
            var sample = new Dictionary<string, ItemDrop.ItemData>();
            var seenChest = new HashSet<int>();
            foreach (StorageDisplayBoard board in cluster)
            {
                if (board == null)
                    continue;
                foreach (Container container in board._watched)
                {
                    if (container == null || !seenChest.Add(container.GetInstanceID()))
                        continue;
                    Inventory inv = container.GetInventory();
                    if (inv == null)
                        continue;
                    foreach (ItemDrop.ItemData item in inv.GetAllItems())
                    {
                        if (item?.m_shared == null || item.m_stack <= 0)
                            continue;
                        if (!DisplayFilters.MatchesAny(item, filters))
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

            var ranked = new List<KeyValuePair<string, int>>(totals);
            ranked.Sort((a, b) =>
            {
                int byCount = b.Value.CompareTo(a.Value);
                if (byCount != 0)
                    return byCount;
                return string.CompareOrdinal(a.Key, b.Key);
            });

            int start = _page * Slots;
            bool lastBoard = _page >= _pages - 1;
            int remaining = Mathf.Max(0, ranked.Count - start);
            int extraAfterThis = Mathf.Max(0, remaining - Slots);
            int shown = remaining;
            if (lastBoard && extraAfterThis > 0)
                shown = Slots - 1;
            else
                shown = Mathf.Min(Slots, remaining);

            for (int i = 0; i < Slots; i++)
            {
                if (lastBoard && extraAfterThis > 0 && i == Slots - 1)
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

                string key = ranked[index].Key;
                ItemDrop.ItemData item;
                sample.TryGetValue(key, out item);
                int count = ranked[index].Value;

                _slots[i].Icon.sprite = StackLimits.Icon(item);
                _slots[i].Icon.enabled = _slots[i].Icon.sprite != null;
                _slots[i].Icon.color = Color.white;
                SetAmount(i, FormatCount(count));
            }
        }

        private void ClearSlot(int i)
        {
            _slots[i].Icon.enabled = false;
            _slots[i].Icon.sprite = null;
            SetAmount(i, "");
        }

        private void SetAmount(int i, string text)
        {
            TextMeshProUGUI amount = _slots[i].Amount;
            if (amount == null)
                return;
            string next = text ?? "";
            if (amount.text == next)
                return;
            amount.enabled = true;
            amount.color = new Color(1f, 0.95f, 0.75f, 1f);
            amount.faceColor = new Color32(255, 242, 191, 255);
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
