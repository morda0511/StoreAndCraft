using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// Small display: Alt+E opens Name / Amount toggles (same SkillsDialog chrome as type/range menus).
    /// </summary>
    internal static class DisplaySmallOptions
    {
        public static bool IsOpen { get; private set; }

        private static StorageDisplayBoard _board;
        private static SkillsDialog _skills;
        private static bool _skillsWasEnabled;
        private static bool _openedInventory;
        private static float _openedAt;
        private static int _suppressMenuFrame;
        private static readonly List<GameObject> _owned = new List<GameObject>();
        private static readonly List<GameObject> _hiddenVanilla = new List<GameObject>();
        private static readonly List<TMP_Text> _titleTexts = new List<TMP_Text>();
        private static readonly List<string> _titleBackup = new List<string>();
        private static readonly List<bool> _titleLocalize = new List<bool>();
        private static bool _closing;
        private static float _listHeightBackup = -1f;
        private static float _chromeHeightBackup = -1f;
        private static RectTransform _chromeRt;
        private static GameObject _nameToggle;
        private static GameObject _amountToggle;

        public static bool TryOpenHovered()
        {
            StorageDisplayBoard board = HoveredSmall();
            if (board == null)
                return false;
            Open(board);
            return true;
        }

        public static void Open(StorageDisplayBoard board)
        {
            if (board == null || board.Kind != DisplayKind.Small || Player.m_localPlayer == null)
                return;

            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_skillsDialog == null)
                return;

            if (!PrivateArea.CheckAccess(board.transform.position, 0f, false, true))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return;
            }

            if (DisplayTypeMenu.IsOpen)
                DisplayTypeMenu.Close();
            if (DisplayRangeMenu.IsOpen)
                DisplayRangeMenu.Close();
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
            BuildToggles();
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

                ClearOwned();
                RestorePanelSize();
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

        private static void BuildToggles()
        {
            ClearOwned();
            if (_skills == null || _board == null)
                return;

            // Host on the wood panel (list parent), not the tall list body.
            Transform host = _skills.m_listRoot != null && _skills.m_listRoot.parent != null
                ? _skills.m_listRoot.parent
                : _skills.transform;

            if (_skills.m_listRoot != null)
                ShrinkPanelForTwoButtons(_skills.m_listRoot);

            TMP_FontAsset font = UiFonts.ThinNorse();
            float midY = -72f;

            // Name — left
            var nameBlock = new GameObject("SAC_SmallNameBlock", typeof(RectTransform));
            nameBlock.transform.SetParent(host, false);
            RectTransform nameRt = nameBlock.transform as RectTransform;
            nameRt.anchorMin = new Vector2(0.5f, 1f);
            nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.pivot = new Vector2(1f, 1f);
            nameRt.anchoredPosition = new Vector2(-12f, midY);
            nameRt.sizeDelta = new Vector2(UiToggle.Width + 8f, UiToggle.Height + 24f);
            _owned.Add(nameBlock);
            AddToggleLabel(nameRt, Loc.T("Name", "Name"));
            _nameToggle = UiToggle.Create(
                nameRt,
                "SAC_SmallNameToggle",
                _board.ShowName(),
                true,
                OnNameClicked,
                font);
            PlaceToggle(_nameToggle);

            // Amount — right
            var amountBlock = new GameObject("SAC_SmallAmountBlock", typeof(RectTransform));
            amountBlock.transform.SetParent(host, false);
            RectTransform amountRt = amountBlock.transform as RectTransform;
            amountRt.anchorMin = new Vector2(0.5f, 1f);
            amountRt.anchorMax = new Vector2(0.5f, 1f);
            amountRt.pivot = new Vector2(0f, 1f);
            amountRt.anchoredPosition = new Vector2(12f, midY);
            amountRt.sizeDelta = new Vector2(UiToggle.Width + 8f, UiToggle.Height + 24f);
            _owned.Add(amountBlock);
            AddToggleLabel(amountRt, Loc.T("Amount", "Anzahl"));
            _amountToggle = UiToggle.Create(
                amountRt,
                "SAC_SmallAmountToggle",
                _board.ShowAmount(),
                true,
                OnAmountClicked,
                font);
            PlaceToggle(_amountToggle);

            if (_skills.m_totalSkillText != null)
                _skills.m_totalSkillText.text = "";
        }

        private static void AddToggleLabel(RectTransform parent, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -1f);
            rt.sizeDelta = new Vector2(0f, 18f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            UiFonts.StyleThinLabel(label, 16f);
            label.text = text;
            label.color = new Color(1f, 0.85f, 0.4f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        private static void PlaceToggle(GameObject toggle)
        {
            if (toggle == null)
                return;
            RectTransform rt = toggle.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -20f);
        }

        private static void ShrinkPanelForTwoButtons(RectTransform list)
        {
            float rowH = UiToggle.Height + 36f;
            if (_listHeightBackup < 0f)
                _listHeightBackup = list.rect.height;
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(8f, rowH * 0.25f));

            _chromeRt = list.parent as RectTransform;
            if (_chromeRt != null)
            {
                if (_chromeHeightBackup < 0f)
                    _chromeHeightBackup = _chromeRt.rect.height;
                float compact = Mathf.Clamp(140f, 120f, 180f);
                _chromeRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, compact);
            }
        }

        private static void RestorePanelSize()
        {
            if (_skills != null && _skills.m_listRoot != null && _listHeightBackup > 0f)
                _skills.m_listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _listHeightBackup);
            if (_chromeRt != null && _chromeHeightBackup > 0f)
                _chromeRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _chromeHeightBackup);
            _listHeightBackup = -1f;
            _chromeHeightBackup = -1f;
            _chromeRt = null;
        }

        private static void RebuildToggleRows()
        {
            if (!IsOpen || _board == null)
                return;
            if (_nameToggle != null)
                UiToggle.SetState(_nameToggle, _board.ShowName(), true);
            if (_amountToggle != null)
                UiToggle.SetState(_amountToggle, _board.ShowAmount(), true);
        }

        private static void OnNameClicked()
        {
            if (_board == null)
                return;
            _board.SetShowName(!_board.ShowName());
            RebuildToggleRows();
        }

        private static void OnAmountClicked()
        {
            if (_board == null)
                return;
            _board.SetShowAmount(!_board.ShowAmount());
            RebuildToggleRows();
        }

        private static void ClearOwned()
        {
            _nameToggle = null;
            _amountToggle = null;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                    Object.Destroy(_owned[i]);
            }
            _owned.Clear();
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

        private static void StripSkillChrome(Transform root)
        {
            HideChild(root, "bonustext");
            HideChild(root, "levelbar");
            HideChild(root, "levelbar_total");
            HideChild(root, "currentlevel");
            HideChild(root, "icon");
            HideChild(root, "leveltext");
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
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            }
            return null;
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
            // Keep prefab Norse/Valheim TMP font; only restyle for the compact side buttons.
            TMP_FontAsset norse = null;
            for (int i = 0; i < _titleTexts.Count; i++)
            {
                if (_titleTexts[i] != null && _titleTexts[i].font != null)
                {
                    norse = _titleTexts[i].font;
                    break;
                }
            }
            if (norse != null)
                tmp.font = norse;
            tmp.enableAutoSizing = false;
            tmp.fontSize = 22f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.maxVisibleLines = 1;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = text;
            tmp.color = color;
        }
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
    internal static class DisplaySmallOptionsPausePatch
    {
        private static bool Prefix()
        {
            if (!DisplaySmallOptions.ShouldBlockPause())
                return true;
            DisplaySmallOptions.Close();
            return false;
        }
    }

    [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.OnClose))]
    internal static class DisplaySmallOptionsSkillsClosePatch
    {
        private static void Prefix(SkillsDialog __instance)
        {
            DisplaySmallOptions.OnSkillsClosed(__instance);
        }
    }

    [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
    internal static class DisplaySmallOptionsSkillsSetupPatch
    {
        private static bool Prefix()
        {
            return !DisplaySmallOptions.IsOpen;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    internal static class DisplaySmallOptionsInventoryHidePatch
    {
        private static void Postfix()
        {
            if (DisplaySmallOptions.IsOpen)
                DisplaySmallOptions.Close();
        }
    }
}
