using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Multi-select SkillsDialog UI: toggle which Smelter/kiln inputs may be
    /// auto-pulled from chests. Same visual language as Storage Display type menu.
    /// </summary>
    internal static class StationFilterMenu
    {
        public static bool IsOpen { get; private set; }

        private static Smelter _smelter;
        private static CookingStation _cook;
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

        public static bool AnySkillsMenuOpen
        {
            get { return IsOpen || DisplayTypeMenu.IsOpen || DisplayRangeMenu.IsOpen; }
        }

        public static void Open(Smelter smelter)
        {
            OpenInternal(smelter, null);
        }

        public static void Open(CookingStation cook)
        {
            OpenInternal(null, cook);
        }

        private static void OpenInternal(Smelter smelter, CookingStation cook)
        {
            if ((smelter == null && cook == null) || Player.m_localPlayer == null)
                return;

            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_skillsDialog == null)
                return;

            if (DisplayTypeMenu.IsOpen)
                DisplayTypeMenu.Close();
            if (DisplayRangeMenu.IsOpen)
                DisplayRangeMenu.Close();
            if (IsOpen)
                Close();

            _smelter = smelter;
            _cook = cook;
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
                _smelter = null;
                _cook = null;
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

        public static bool ShouldBlockPause()
        {
            return IsOpen || Time.frameCount == _suppressMenuFrame;
        }

        internal static void Tick()
        {
            if (!IsOpen)
                return;
            if ((_smelter == null && _cook == null) || _skills == null || Player.m_localPlayer == null)
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
            {
                return;
            }
            RebuildRows();
        }

        private static void SetLink(int linkId)
        {
            Component station = (Component)_smelter ?? _cook;
            if (station == null)
                return;
            StationLink.Set(station, linkId);
            RebuildRows();
        }

        private static void AllowAll()
        {
            if (_smelter != null)
                StationPullFilter.Clear(_smelter);
            else if (_cook != null)
                StationPullFilter.Clear(_cook);
            else
                return;
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
            List<string> choices = _cook != null
                ? StationPullFilter.FoodChoices(_cook)
                : (_smelter != null ? StationPullFilter.OreChoices(_smelter) : new List<string>());

            Component station = (Component)_smelter ?? _cook;
            int currentLink = StationLink.Get(station);
            // Link none + link1..9 + ore/food rows + Allow all + Done
            int linkRows = StationLink.MaxId + 1;
            int count = linkRows + choices.Count + 2;
            int rowIndex = 0;

            for (int link = 0; link <= StationLink.MaxId; link++)
            {
                GameObject row = SpawnRow(rowIndex++);
                bool on = currentLink == link;
                string mark = on ? "[+]" : "[-]";
                string label = mark + "  " + StationLink.Label(link);
                int captured = link;
                BindLinkRow(row, label, on, () => SetLink(captured));
                _rows.Add(row);
            }

            for (int i = 0; i < choices.Count; i++)
            {
                GameObject row = SpawnRow(rowIndex++);
                string shared = choices[i];
                bool allowed = _cook != null
                    ? StationPullFilter.IsAllowed(_cook, shared)
                    : StationPullFilter.IsAllowed(_smelter, shared);
                string mark = allowed ? "[+]" : "[-]";
                string label = StationPullFilter.DisplayName(shared);
                BindToggleRow(row, shared, mark + "  " + label, allowed);
                _rows.Add(row);
            }

            {
                GameObject row = SpawnRow(rowIndex++);
                BindActionRow(row, Loc.T("Allow all inputs", "Alle Inputs erlauben"), AllowAll);
                _rows.Add(row);
            }
            {
                GameObject row = SpawnRow(rowIndex++);
                BindActionRow(row, Loc.T("Done", "Fertig"), Close);
                _rows.Add(row);
            }

            float height = Mathf.Max(_skills.m_listRoot.rect.height, count * _skills.m_spacing);
            _skills.m_listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (_skills.m_totalSkillText != null)
            {
                _skills.m_totalSkillText.text = Loc.T(
                    "Link 1-9 = only chests named [linkN]. Link none = untagged chests. Then input toggles.",
                    "Link 1-9 = nur Kisten mit [linkN]. Link keiner = Kisten ohne Tag. Dann Input-Toggles.");
            }
        }

        private static GameObject SpawnRow(int index)
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
            return row;
        }

        private static void BindLinkRow(GameObject row, string label, bool on, UnityEngine.Events.UnityAction action)
        {
            Transform t = row.transform;
            Color color = on ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(0.75f, 0.75f, 0.8f, 1f);
            SetChildText(t, "name", label, color);
            SetChildText(t, "leveltext", on ? "ON" : "", color);
            StripSkillChrome(t);

            Button button = EnsureButton(row);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            UIInputHandler input = row.GetComponent<UIInputHandler>() ?? row.GetComponentInChildren<UIInputHandler>(true);
            if (input != null)
                input.m_onLeftClick = go => action();
        }

        private static void BindToggleRow(GameObject row, string shared, string label, bool allowed)
        {
            Transform t = row.transform;
            SetChildText(t, "name", label, allowed ? new Color(0.45f, 0.95f, 0.45f, 1f) : new Color(1f, 0.45f, 0.4f, 1f));
            SetChildText(t, "leveltext", allowed ? "ON" : "OFF", allowed ? new Color(0.45f, 0.95f, 0.45f, 1f) : new Color(1f, 0.45f, 0.4f, 1f));
            StripSkillChrome(t);

            Button button = EnsureButton(row);
            string captured = shared;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Toggle(captured));

            UIInputHandler input = row.GetComponent<UIInputHandler>() ?? row.GetComponentInChildren<UIInputHandler>(true);
            if (input != null)
                input.m_onLeftClick = go => Toggle(captured);
        }

        private static void BindActionRow(GameObject row, string label, UnityEngine.Events.UnityAction action)
        {
            Transform t = row.transform;
            Color accent = new Color(1f, 0.85f, 0.4f, 1f);
            SetChildText(t, "name", label, accent);
            SetChildText(t, "leveltext", "", Color.white);
            StripSkillChrome(t);

            Button button = EnsureButton(row);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            UIInputHandler input = row.GetComponent<UIInputHandler>() ?? row.GetComponentInChildren<UIInputHandler>(true);
            if (input != null)
                input.m_onLeftClick = go => action();
        }

        /// <summary>
        /// Skills rows ship with an empty icon slot (square). Hide bars/icons so only the name remains.
        /// </summary>
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
                // Empty skill icon / icon frame shows as a square slot beside the name.
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
            string title = Loc.T("Chest pull filter", "Truhen-Zug Filter");
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
            return false;
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

    [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.OnClose))]
    internal static class StationFilterSkillsClosePatch
    {
        private static void Prefix(SkillsDialog __instance)
        {
            StationFilterMenu.OnSkillsClosed(__instance);
        }
    }

    [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
    internal static class StationFilterSkillsSetupPatch
    {
        private static bool Prefix()
        {
            return !StationFilterMenu.IsOpen;
        }
    }
}
