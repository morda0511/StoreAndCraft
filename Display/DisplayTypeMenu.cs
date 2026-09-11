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

        private static void Pick(int id)
        {
            StorageDisplayBoard board = _board;
            if (board != null)
                board.SetFilter(id);
            Close();
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
            int count = DisplayFilters.Choices.Length + 1;
            int current = _board != null ? _board.FilterId() : 0;

            for (int i = 0; i < count; i++)
            {
                int id;
                string label;
                if (i < DisplayFilters.Choices.Length)
                {
                    id = DisplayFilters.Choices[i].Id;
                    label = DisplayFilters.Choices[i].Label();
                }
                else
                {
                    id = 0;
                    label = Loc.T("Clear", "Zurücksetzen");
                }

                GameObject row = Object.Instantiate(
                    _skills.m_elementPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    _skills.m_listRoot);
                row.SetActive(true);
                RectTransform rt = row.transform as RectTransform;
                if (rt != null)
                    rt.anchoredPosition = new Vector2(0f, -i * _skills.m_spacing);
                BindRow(row, id, label, id == current && id != 0);
                _rows.Add(row);
            }

            float height = Mathf.Max(_skills.m_listRoot.rect.height, count * _skills.m_spacing);
            _skills.m_listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (_skills.m_totalSkillText != null)
                _skills.m_totalSkillText.text = "";
        }

        private static void BindRow(GameObject row, int id, string label, bool selected)
        {
            Transform t = row.transform;
            SetChildText(t, "name", label, selected);
            SetChildText(t, "leveltext", selected ? "•" : "", false);
            HideChild(t, "bonustext");
            HideChild(t, "levelbar");
            HideChild(t, "levelbar_total");
            HideChild(t, "currentlevel");
            HideChild(t, "icon");

            Button button = row.GetComponent<Button>() ?? row.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                if (row.GetComponent<Image>() == null)
                {
                    Image bg = row.AddComponent<Image>();
                    bg.color = new Color(0f, 0f, 0f, 0.01f);
                }
                button = row.AddComponent<Button>();
            }

            int captured = id;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Pick(captured));

            UIInputHandler input = row.GetComponent<UIInputHandler>() ?? row.GetComponentInChildren<UIInputHandler>(true);
            if (input != null)
                input.m_onLeftClick = go => Pick(captured);
        }

        private static void ApplyTitle()
        {
            CacheTitles();
            string title = Loc.T("Select type", "Typ wählen");
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

        private static void SetChildText(Transform root, string name, string text, bool selected)
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
            tmp.color = selected ? new Color(1f, 0.72f, 0.22f, 1f) : Color.white;
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
