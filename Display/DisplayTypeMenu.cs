using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    internal static class DisplayTypeMenu
    {
        public static bool IsOpen { get; private set; }

        private static StorageDisplayBoard _board;
        private static SkillsDialog _skills;
        private static bool _skillsWasEnabled;
        private static bool _openedInventory;
        private static float _openedAt;
        private static int _suppressMenuFrame;
        private static readonly List<GameObject> _rows = new List<GameObject>();
        private static readonly List<GameObject> _hiddenVanilla = new List<GameObject>();
        private static readonly List<TMP_Text> _titleTexts = new List<TMP_Text>();
        private static readonly List<string> _titleBackup = new List<string>();
        private static readonly List<bool> _titleLocalize = new List<bool>();
        private static bool _closing;

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

            DisplayFilters.InvalidateSubItemCache();
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
                bool wasOpen = IsOpen;
                bool openedInv = _openedInventory;
                SkillsDialog skills = _skills;
                IsOpen = false;
                _openedInventory = false;
                _board = null;
                Expanded.Clear();
                _suppressMenuFrame = Time.frameCount;

                ClearOurRows();
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

        private static readonly HashSet<int> Expanded = new HashSet<int>();

        private static void Pick(int id)
        {
            StorageDisplayBoard board = _board;
            if (board == null)
                return;
            if (id == 0)
            {
                board.WriteSelection(new List<int>(), new List<string>());
                RebuildRows();
                return;
            }
            if (DisplayFilters.IsExpandable(id))
            {
                if (!Expanded.Add(id))
                    Expanded.Remove(id);
                RebuildRows();
                return;
            }
            board.ToggleFilter(id);
            RebuildRows();
        }

        private static void PickAll(int filterId)
        {
            StorageDisplayBoard board = _board;
            if (board == null)
                return;
            board.ToggleFilter(filterId);
            RebuildRows();
        }

        private static void PickItem(string shared, int parentFilterId)
        {
            StorageDisplayBoard board = _board;
            if (board == null)
                return;
            board.ToggleItemToken(shared, parentFilterId);
            RebuildRows();
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
                GameObject row = _hiddenVanilla[i];
                if (row != null)
                    row.SetActive(true);
            }
            _hiddenVanilla.Clear();
        }

        private static void ClearOurRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    Object.Destroy(_rows[i]);
            }
            _rows.Clear();
        }

        private static void RebuildRows()
        {
            if (_skills == null || _skills.m_elementPrefab == null || _skills.m_listRoot == null)
                return;

            ClearOurRows();
            List<int> selected = _board != null ? _board.FilterIds() : new List<int>();
            List<string> selectedItems = _board != null ? _board.ItemTokens() : new List<string>();

            int rowIndex = 0;
            for (int i = 0; i < DisplayFilters.Choices.Length; i++)
            {
                DisplayFilter choice = DisplayFilters.Choices[i];
                int id = choice.Id;
                bool expandable = DisplayFilters.IsExpandable(id);
                bool expanded = expandable && Expanded.Contains(id);
                bool categoryOn = selected.Contains(id);
                bool anyChildOn = false;
                if (expandable)
                {
                    List<string> subs = DisplayFilters.SubItems(id);
                    for (int s = 0; s < subs.Count; s++)
                    {
                        if (selectedItems.Contains(subs[s]))
                        {
                            anyChildOn = true;
                            break;
                        }
                    }
                }

                bool parentOn = categoryOn || anyChildOn;
                string expandMark = expandable ? (expanded ? " v " : " > ") : "   ";
                string mark = parentOn ? "[+]" : "[-]";
                string label = expandMark + mark + "  " + choice.Label();
                AddToggleRow(rowIndex++, () => Pick(id), label, parentOn);

                if (!expanded)
                    continue;

                string allLabel = "    " + (categoryOn ? "[+]" : "[-]") + "  "
                    + Loc.T("All", "Alle") + " " + choice.Label();
                int capturedId = id;
                AddToggleRow(rowIndex++, () => PickAll(capturedId), allLabel, categoryOn);

                List<string> items = DisplayFilters.SubItems(id);
                for (int s = 0; s < items.Count; s++)
                {
                    string shared = items[s];
                    bool on = selectedItems.Contains(shared);
                    string itemLabel = "    " + (on ? "[+]" : "[-]") + "  " + DisplayFilters.ItemLabel(shared);
                    string capturedShared = shared;
                    int parent = id;
                    AddToggleRow(rowIndex++, () => PickItem(capturedShared, parent), itemLabel, on);
                }
            }

            AddActionRow(rowIndex++, Loc.T("Clear", "Zurücksetzen"), () => Pick(0));
            AddActionRow(rowIndex++, Loc.T("Done", "Fertig"), Close);

            float height = Mathf.Max(_skills.m_listRoot.rect.height, rowIndex * _skills.m_spacing);
            _skills.m_listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (_skills.m_totalSkillText != null)
            {
                _skills.m_totalSkillText.text = Loc.T(
                    "Food / Ingredients: expand for items. All = whole type. Done closes.",
                    "Essen / Zutaten: aufklappen für Items. Alle = ganzer Typ. Fertig schließt.");
            }
        }

        private static void AddToggleRow(int index, UnityEngine.Events.UnityAction action, string label, bool selected)
        {
            GameObject row = Object.Instantiate(
                _skills.m_elementPrefab,
                Vector3.zero,
                Quaternion.identity,
                _skills.m_listRoot);
            row.SetActive(true);
            RectTransform rt = row.transform as RectTransform;
            if (rt != null)
                rt.anchoredPosition = new Vector2(0f, -index * _skills.m_spacing);
            BindActionToggleRow(row, label, selected, action);
            _rows.Add(row);
        }

        private static void AddActionRow(int index, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject row = Object.Instantiate(
                _skills.m_elementPrefab,
                Vector3.zero,
                Quaternion.identity,
                _skills.m_listRoot);
            row.SetActive(true);
            RectTransform rt = row.transform as RectTransform;
            if (rt != null)
                rt.anchoredPosition = new Vector2(0f, -index * _skills.m_spacing);
            BindActionRow(row, label, action);
            _rows.Add(row);
        }

        private static void BindActionToggleRow(
            GameObject row,
            string label,
            bool selected,
            UnityEngine.Events.UnityAction action)
        {
            Transform t = row.transform;
            Color color = selected ? new Color(0.45f, 0.95f, 0.45f, 1f) : new Color(1f, 0.45f, 0.4f, 1f);
            StripSkillChrome(t);
            WidenNameField(t);
            SetChildText(t, "name", label, color);
            SetChildText(t, "leveltext", "", color);

            Button button = EnsureButton(row);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            UIInputHandler input = row.GetComponent<UIInputHandler>() ?? row.GetComponentInChildren<UIInputHandler>(true);
            if (input != null)
                input.m_onLeftClick = go => action();
        }

        private static void BindToggleRow(GameObject row, int id, string label, bool selected)
        {
            string mark = selected ? "[+]" : "[-]";
            BindActionToggleRow(row, mark + "  " + label, selected, () => Pick(id));
        }

        private static void BindActionRow(GameObject row, string label, UnityEngine.Events.UnityAction action)
        {
            Transform t = row.transform;
            Color accent = new Color(1f, 0.85f, 0.4f, 1f);
            StripSkillChrome(t);
            WidenNameField(t);
            SetChildText(t, "name", label, accent);
            SetChildText(t, "leveltext", "", Color.white);

            Button button = EnsureButton(row);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            UIInputHandler input = row.GetComponent<UIInputHandler>() ?? row.GetComponentInChildren<UIInputHandler>(true);
            if (input != null)
                input.m_onLeftClick = go => action();
        }

        private static void StripSkillChrome(Transform root)
        {
            HideChild(root, "bonustext");
            HideChild(root, "levelbar");
            HideChild(root, "levelbar_total");
            HideChild(root, "currentlevel");
            HideChild(root, "icon");

            if (root == null)
                return;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (child == null || child == root)
                    continue;
                string n = child.gameObject.name.ToLowerInvariant();
                if (!(n.Contains("icon") || n.Contains("skillicon") || n.EndsWith("_icon")))
                    continue;
                if (child.GetComponent<TMP_Text>() != null)
                    continue;
                child.gameObject.SetActive(false);
            }
        }

        private static Button EnsureButton(GameObject row)
        {
            Button button = row.GetComponent<Button>() ?? row.GetComponentInChildren<Button>(true);
            if (button != null)
                return button;
            if (row.GetComponent<Image>() == null)
            {
                Image bg = row.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.01f);
            }
            return row.AddComponent<Button>();
        }

        private static void ApplyTitle()
        {
            CacheTitles();
            string title = Loc.T("Select types", "Typen wählen");
            for (int i = 0; i < _titleTexts.Count; i++)
            {
                TMP_Text tmp = _titleTexts[i];
                if (tmp != null)
                    tmp.text = title;
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

        private static void WidenNameField(Transform root)
        {
            // Skills rows reserve space for icon + level — reclaim it for the label.
            HideChild(root, "leveltext");
            Transform name = FindChild(root, "name");
            if (name == null)
                return;
            RectTransform rt = name as RectTransform;
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0.02f, 0.05f);
            rt.anchorMax = new Vector2(0.98f, 0.95f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void SetChildText(Transform root, string name, string text, Color color)
        {
            Transform child = FindChild(root, name);
            if (child == null)
                return;
            TMP_Text tmp = child.GetComponent<TMP_Text>();
            if (tmp == null)
                return;
            Component localize = child.GetComponent("Localize");
            if (localize is MonoBehaviour mb)
                mb.enabled = false;

            // Skills rows auto-size long names smaller and wrap — lock one uniform line.
            tmp.enableAutoSizing = false;
            tmp.fontSize = 18f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.maxVisibleLines = 1;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            tmp.text = text;
            tmp.color = color;
        }

        private static void HideChild(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            Transform direct = root.Find(name);
            if (direct != null)
                return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
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
