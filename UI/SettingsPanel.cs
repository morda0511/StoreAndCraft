using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreAndCraft
{
    /// <summary>
    /// F10 panel: activity log checkbox (anyone, local) and gameplay settings with Save
    /// (host / adminlist on servers, anyone offline).
    /// Built with uGUI from Valheim's own UI sprites (woodpanel_settings, button, checkbox,
    /// text_field …) and Norse fonts, so it looks like the vanilla settings menu. Any sprite
    /// the game does not provide falls back to the SAC wood art or a plain color.
    /// </summary>
    internal static class SettingsPanel
    {
        private const float RefW = 1920f;
        private const float RefH = 1080f;
        private const float PanelW = 760f;
        private const float PanelH = 960f;
        private const float SliderMax = 200f;

        private static readonly Color Gold = new Color(1f, 0.79f, 0.34f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.84f, 1f);
        private static readonly Color Muted = new Color(0.78f, 0.74f, 0.66f, 1f);
        private static readonly Color Warn = new Color(1f, 0.64f, 0.35f, 1f);
        private static readonly Color CheckYellow = new Color(1f, 0.86f, 0.1f, 1f);

        private sealed class Row
        {
            public string Key;
            public string En;
            public string De;
            public string HelpEn;
            public string HelpDe;
            public float Value;
            public string Unit = "m";
            public float Min;
            public float SliderMax = SettingsPanel.SliderMax;
            public float Max = ModConfig.MaxRange;
            public Slider Slider;
            public TMP_InputField Input;
            public bool Syncing;
        }

        // SAC-CATCHUP
        private static bool _catchUp;
        private static bool _torchAutoFill;

        public static bool IsOpen { get; private set; }

        private static bool _canEdit;
        private static int _closeAtFrame = -1;
        private static GameObject _root;
        private static RectTransform _content;
        private static GameObject _infoBody;
        private static TextMeshProUGUI _infoHeader;
        private static TextMeshProUGUI _status;
        private static readonly List<Row> Rows = new List<Row>();
        private static readonly Dictionary<string, Sprite> VanillaSprites = new Dictionary<string, Sprite>();

        public static void Toggle()
        {
            if (IsOpen)
                RequestClose();
            else
                Open();
        }

        private static void Open()
        {
            if (Plugin.Settings == null || Player.m_localPlayer == null)
                return;
            _canEdit = ConfigCommands.CanEditRanges();
            LoadRows();
            _closeAtFrame = -1;
            UiFonts.ThinNorse();
            try
            {
                Build();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("F10 panel build failed: " + ex);
                DestroyRoot();
                return;
            }
            IsOpen = true;
        }

        private static void RequestClose()
        {
            // Close one frame later so the same Escape press cannot open the pause menu.
            if (_closeAtFrame < 0)
                _closeAtFrame = Time.frameCount + 1;
        }

        private static void Close()
        {
            IsOpen = false;
            _closeAtFrame = -1;
            DestroyRoot();
        }

        private static void DestroyRoot()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].Slider = null;
                Rows[i].Input = null;
            }
            _content = null;
            _infoBody = null;
            _infoHeader = null;
            _status = null;
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        internal static bool BlocksInput
        {
            get { return IsOpen && Player.m_localPlayer != null; }
        }

        internal static void Tick()
        {
            if (!IsOpen)
                return;
            if (Player.m_localPlayer == null || Plugin.Settings == null || _root == null)
            {
                Close();
                return;
            }

            if (_closeAtFrame >= 0)
            {
                if (Time.frameCount >= _closeAtFrame)
                    Close();
                return;
            }

            if (ZInput.GetKeyDown(KeyCode.Escape, true) || KeyUtil.Down(Plugin.Settings.ActivityLogKey.Value))
                RequestClose();
        }

        // ---------------------------------------------------------------- data

        private static void LoadRows()
        {
            Rows.Clear();
            AddRow("dumprange", "Dump / middle-click (player to chest)", "Einlagern / Mittelklick (Spieler zur Kiste)",
                "Dump key or middle-click: how far chests may be from you when your bag is sorted into them.",
                "Einlager-Taste oder Mittelklick: wie weit Kisten von dir weg sein dürfen, in die deine Tasche einsortiert wird.");
            AddRow("storerange", "Auto-store ground items (item to chest)", "Auto-Lagern vom Boden (Item zur Kiste)",
                "Items on the ground (drops, harvest) move into a matching chest within this distance of the item.",
                "Items auf dem Boden (Drops, Ernte) wandern in eine passende Kiste, die so nah am Item steht.");
            AddRow("storagerange", "Take stack / search", "Stack holen / Suche",
                "Ctrl + middle-click fills a stack from chests, Y points to the chest holding an item. Measured from you.",
                "Strg + Mittelklick füllt einen Stack aus Kisten, Y zeigt die Kiste mit dem Item. Gemessen von dir.");
            AddRow("craftrange", "Craft / build / station [E] (player to chest)", "Craften / Bauen / Station [E] (Spieler zur Kiste)",
                "Crafting, building and [E] on stations can use materials from chests this close to you.",
                "Craften, Bauen und [E] an Stationen nutzen Material aus Kisten, die so nah bei dir stehen.");
            AddRow("autofillrange", "Auto-fill (player to station)", "Auto-Fill (Spieler zur Station)",
                "Stations with auto-fill (B) are refilled while you are at most this far away from them.",
                "Stationen mit Auto-Fill (B) werden befüllt, solange du höchstens so weit von ihnen weg bist.");
            AddRow("autofillchestrange", "Auto-fill + auto-store (station to chest, 0 = same)", "Auto-Fill + Auto-Lagern (Station zur Kiste, 0 = wie oben)",
                "Which chests around a station supply it, and where finished items (N) go. 0 uses the auto-fill value.",
                "Aus welchen Kisten rund um eine Station Material kommt und wohin fertige Items (N) gehen. 0 = Wert von Auto-Fill.");
            AddRow("displayrange", "Storage displays (default)", "Storage Displays (Standard)",
                "Default chest search radius around each Storage Display. Each display can use its own value (Alt+R).",
                "Standard-Suchradius für Kisten rund um ein Storage Display. Jedes Display kann einen eigenen Wert haben (Alt+R).");
            AddRow("feedtroughrange", "Feed trough (animals)", "Futtertrog (Tiere)",
                "How far hungry tames notice a feed trough and walk to it.",
                "Wie weit hungrige gezähmte Tiere einen Futtertrog bemerken und hinlaufen.");

            // SAC-CATCHUP
            _catchUp = Plugin.Settings.CatchUpEnabled.Value;
            _torchAutoFill = Plugin.Settings.TorchAutoFillDefault.Value;
            AddRow("catchuphours", "Catch up while away: max hours", "Nachholen: maximale Stunden",
                "Most game time one smelter catches up after its zone was unloaded (only with the checkbox on).",
                "So viel Spielzeit holt eine Schmelze höchstens nach, wenn ihre Zone entladen war (nur mit Häkchen).");
            Row hours = Rows[Rows.Count - 1];
            hours.Unit = "h";
            hours.Min = 0.5f;
            hours.SliderMax = 48f;
            hours.Max = 48f;
        }

        private static void AddRow(string key, string en, string de, string helpEn, string helpDe)
        {
            ConfigEntry<float> entry = ConfigCommands.EntryFor(key);
            if (entry == null)
                return;
            Rows.Add(new Row
            {
                Key = key,
                En = en,
                De = de,
                HelpEn = helpEn,
                HelpDe = helpDe,
                Value = entry.Value
            });
        }

        private static string Format(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static void Save()
        {
            var changes = new List<KeyValuePair<string, float>>();
            for (int i = 0; i < Rows.Count; i++)
            {
                ConfigEntry<float> entry = ConfigCommands.EntryFor(Rows[i].Key);
                if (entry == null || Mathf.Abs(entry.Value - Rows[i].Value) < 0.001f)
                    continue;
                changes.Add(new KeyValuePair<string, float>(Rows[i].Key, Rows[i].Value));
            }
            if (_catchUp != Plugin.Settings.CatchUpEnabled.Value) // SAC-CATCHUP
                changes.Add(new KeyValuePair<string, float>("catchup", _catchUp ? 1f : 0f));
            if (_torchAutoFill != Plugin.Settings.TorchAutoFillDefault.Value)
                changes.Add(new KeyValuePair<string, float>("torchautofill", _torchAutoFill ? 1f : 0f));

            string result = ConfigCommands.ApplyFromPanel(changes);
            // Rebuild so sliders / fields show what is really stored now.
            Close();
            Open();
            if (_status != null)
                _status.text = result ?? "";
        }

        // ---------------------------------------------------------------- UI build

        private static void Build()
        {
            DestroyRoot();

            _root = new GameObject("SAC_SettingsPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Build inactive: TMP_InputField / Slider must be fully wired before OnEnable runs.
            _root.SetActive(false);
            Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;

            // Dim + click blocker only (no click-to-close: unsaved edits would be lost).
            var dim = NewUi("Dim", _root.transform, typeof(Image));
            Stretch(dim);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            // Wood panel like the vanilla settings dialog.
            var panel = NewUi("Panel", _root.transform, typeof(Image));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelW, PanelH);
            panel.anchoredPosition = Vector2.zero;
            SetSprite(panel.GetComponent<Image>(), Vanilla("woodpanel_settings") ?? UiAssets.PanelLeft,
                new Color(0.16f, 0.12f, 0.08f, 0.97f));

            TextMeshProUGUI title = Label(panel, "Title", 36f, true);
            title.text = "StoreAndCraft";
            title.color = Gold;
            title.alignment = TextAlignmentOptions.Center;
            Place(title.rectTransform, 0f, 1f, 1f, 1f, 40f, -86f, -40f, -30f);

            // Scrollable settings list.
            var scroll = NewUi("Scroll", panel, typeof(Image), typeof(ScrollRect));
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(48f, 150f);
            scroll.offsetMax = new Vector2(-48f, -96f);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var viewport = NewUi("Viewport", scroll, typeof(Image), typeof(RectMask2D));
            Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            var content = NewUi("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 18);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _content = content;

            ScrollRect sr = scroll.GetComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 360f; // same as StationFilterMenu; 40 barely moved

            BuildContent();

            // Bottom: Save / Close + status line.
            Button save = MakeButton(panel, "Save", Loc.T("Save", "Speichern"), Save);
            Place(save.transform as RectTransform, 0.5f, 0f, 0.5f, 0f, -230f, 78f, -20f, 124f);
            save.interactable = _canEdit;

            Button close = MakeButton(panel, "Close", Loc.T("Close", "Schließen"), RequestClose);
            Place(close.transform as RectTransform, 0.5f, 0f, 0.5f, 0f, 20f, 78f, 230f, 124f);

            _status = Label(panel, "Status", 17f, false);
            _status.color = Warn;
            _status.alignment = TextAlignmentOptions.Center;
            _status.text = "";
            Place(_status.rectTransform, 0f, 0f, 1f, 0f, 48f, 44f, -48f, 74f);

            _root.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private static void BuildContent()
        {
            AddToggle(Loc.T("Show activity log", "Aktivitäts-Log anzeigen"), ActivityLog.Visible, true,
                on => ActivityLog.SetVisible(on));

            AddHeader(Loc.T("Ranges (meters, 0-1000)", "Reichweiten (Meter, 0-1000)"));
            if (!_canEdit)
            {
                AddText(Loc.T(
                    "Only admins (adminlist) can change these settings on this server.",
                    "Nur Admins (Adminliste) dürfen diese Einstellungen auf diesem Server ändern."), Warn, 17f);
            }

            for (int i = 0; i < Rows.Count; i++)
                AddRangeRow(Rows[i]);

            AddHeader(Loc.T("Stations", "Stationen"));
            AddToggle(Loc.T( // SAC-CATCHUP
                    "Catch up while away (smelters with B + N)",
                    "Nachholen während niemand da ist (Schmelzen mit B + N)"),
                _catchUp, _canEdit, on => _catchUp = on);
            AddToggle(Loc.T(
                    "Torches start with auto-fill on (B still turns one off)",
                    "Fackeln haben Auto-Fill von Anfang an (B schaltet einzeln aus)"),
                _torchAutoFill, _canEdit, on => _torchAutoFill = on);

            // Collapsible help.
            Button infoBtn = MakeButton(_content, "InfoToggle", "", ToggleInfo);
            AddLayout(infoBtn.gameObject, 44f);
            _infoHeader = infoBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            _infoBody = BuildInfoBody();
            _infoBody.SetActive(false);
            RefreshInfoHeader();
        }

        private static void ToggleInfo()
        {
            if (_infoBody == null)
                return;
            _infoBody.SetActive(!_infoBody.activeSelf);
            RefreshInfoHeader();
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private static void RefreshInfoHeader()
        {
            if (_infoHeader == null)
                return;
            bool open = _infoBody != null && _infoBody.activeSelf;
            _infoHeader.text = (open ? "[-]  " : "[+]  ") + Loc.T("What do these settings do?", "Was bewirken die Einstellungen?");
        }

        private static GameObject BuildInfoBody()
        {
            var body = NewUi("Info", _content, typeof(VerticalLayoutGroup));
            VerticalLayoutGroup v = body.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(8, 8, 2, 8);
            v.spacing = 2f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            for (int i = 0; i < Rows.Count; i++)
            {
                AddText(body, "• " + Loc.T(Rows[i].En, Rows[i].De), TextColor, 17f);
                AddText(body, Loc.T(Rows[i].HelpEn, Rows[i].HelpDe), Muted, 16f);
            }

            AddText(body, Loc.T(
                "Valheim only loads about 120 m around you (Simulation distance in the graphics settings). "
                + "Larger values change nothing beyond that. Tip: auto-fill 80-120, station to chest 10-15, all others 10-30.",
                "Valheim lädt nur etwa 120 m um dich herum (Simulationsdistanz in den Grafik-Einstellungen). "
                + "Größere Werte bewirken darüber hinaus nichts. Tipp: Auto-Fill 80-120, Station zur Kiste 10-15, alles andere 10-30."),
                Warn, 16f);

            AddText(body, "• " + Loc.T("Catch up while away", "Nachholen während niemand da ist"), TextColor, 17f); // SAC-CATCHUP
            AddText(body, Loc.T(
                "Off by default. When nobody is near a base, Valheim unloads it and smelters stop once they are empty. "
                + "With this on, a kiln / smelter / blast furnace / windmill / spinning wheel / eitr refinery with auto-fill (B) "
                + "and auto-store (N) remembers that idle time. When someone with access comes back, it takes ore and fuel "
                + "from its chests and puts the finished items into a chest, one stack at a time, as if it had kept working. "
                + "It stops when the chests run out or the output chest is full. Nothing is taken if the result does not fit. "
                + "Only time after switching this on counts, capped by the max hours. Every package is in the activity log.",
                "Standardmäßig aus. Ist niemand an der Base, entlädt Valheim sie, und Schmelzen stehen still, sobald sie leer sind. "
                + "Mit Häkchen merkt sich ein Meiler / eine Schmelze / Hochofen / Windmühle / Spinnrad / Eitr-Raffinerie mit Auto-Fill (B) "
                + "und Auto-Lagern (N) diese Leerlaufzeit. Kommt jemand mit Zugriff zurück, nimmt sie Erz und Brennstoff "
                + "aus ihren Kisten und legt die fertigen Items in eine Kiste, einen Stapel nach dem anderen, als hätte sie weitergearbeitet. "
                + "Sie stoppt, wenn die Kisten leer sind oder die Ausgabe-Kiste voll ist. Passt das Ergebnis nicht, wird nichts entnommen. "
                + "Es zählt nur Zeit nach dem Einschalten, begrenzt durch die maximalen Stunden. Jedes Paket steht im Aktivitäts-Log."),
                Muted, 16f);

            AddText(body, "• " + Loc.T("Torches start with auto-fill on", "Fackeln mit Auto-Fill von Anfang an"), TextColor, 17f);
            AddText(body, Loc.T(
                "Off by default. When on, every torch (standing, wall, green / blue / mist) that nobody has toggled yet "
                + "refills itself with fuel from nearby chests. Press B on a single torch to switch it off; that choice is kept.",
                "Standardmäßig aus. Mit Häkchen füllt sich jede Fackel (Stand-, Wand-, grüne / blaue / Nebel-Fackel), die noch "
                + "niemand umgeschaltet hat, selbst mit Brennstoff aus nahen Kisten nach. B auf einer Fackel schaltet sie einzeln aus; das bleibt so."),
                Muted, 16f);
            return body.gameObject;
        }

        private static void AddHeader(string text)
        {
            var go = NewUi("Header", _content);
            TextMeshProUGUI tmp = UiFonts.CreateBoldLabel(go.gameObject, 22f);
            tmp.text = text;
            tmp.color = Gold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            AddLayout(go.gameObject, 40f);
        }

        private static void AddText(string text, Color color, float size)
        {
            AddText(_content, text, color, size);
        }

        private static void AddText(RectTransform parent, string text, Color color, float size)
        {
            var go = NewUi("Text", parent);
            TextMeshProUGUI tmp = UiFonts.CreateLabel(go.gameObject, size);
            tmp.text = text;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        private static void AddRangeRow(Row row)
        {
            var go = NewUi("Row_" + row.Key, _content);
            AddLayout(go.gameObject, 64f);

            TextMeshProUGUI label = Label(go, "Label", 18f, false);
            label.text = Loc.T(row.En, row.De);
            label.color = TextColor;
            Place(label.rectTransform, 0f, 1f, 1f, 1f, 0f, -28f, 0f, 0f);

            // Slider (vanilla text_field track, amber fill, button_small knob).
            var sliderRt = NewUi("Slider", go, typeof(Slider));
            Place(sliderRt, 0f, 0f, 1f, 0f, 4f, 10f, -126f, 30f);
            var bg = NewUi("Background", sliderRt, typeof(Image));
            Stretch(bg);
            SetSprite(bg.GetComponent<Image>(), Vanilla("text_field"), new Color(0f, 0f, 0f, 0.55f));

            var fillArea = NewUi("Fill Area", sliderRt);
            Place(fillArea, 0f, 0f, 1f, 1f, 4f, 4f, -4f, -4f);
            var fill = NewUi("Fill", fillArea, typeof(Image));
            Stretch(fill);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.85f, 0.55f, 0.18f, 0.85f);
            fillImg.raycastTarget = false;

            var handleArea = NewUi("Handle Slide Area", sliderRt);
            Place(handleArea, 0f, 0f, 1f, 1f, 8f, 0f, -8f, 0f);
            var handle = NewUi("Handle", handleArea, typeof(Image));
            handle.sizeDelta = new Vector2(18f, 0f);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.offsetMin = new Vector2(-9f, -6f);
            handle.offsetMax = new Vector2(9f, 6f);
            SetSprite(handle.GetComponent<Image>(), Vanilla("button_small") ?? Vanilla("button"), Gold);

            Slider slider = sliderRt.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = row.Min;
            slider.maxValue = row.SliderMax;
            slider.wholeNumbers = row.SliderMax > 50f;
            slider.SetValueWithoutNotify(Mathf.Min(row.Value, row.SliderMax));
            slider.interactable = _canEdit;
            row.Slider = slider;

            // Number field (vanilla text_field).
            var fieldRt = NewUi("Field", go, typeof(Image), typeof(TMP_InputField));
            Place(fieldRt, 1f, 0f, 1f, 0f, -110f, 4f, -30f, 36f);
            SetSprite(fieldRt.GetComponent<Image>(), Vanilla("text_field"), new Color(0f, 0f, 0f, 0.6f));

            var area = NewUi("Text Area", fieldRt, typeof(RectMask2D));
            Place(area, 0f, 0f, 1f, 1f, 8f, 2f, -8f, -2f);
            var textGo = NewUi("Text", area);
            Stretch(textGo);
            TextMeshProUGUI fieldText = UiFonts.CreateLabel(textGo.gameObject, 18f);
            fieldText.color = TextColor;
            fieldText.alignment = TextAlignmentOptions.MidlineRight;
            fieldText.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_InputField input = fieldRt.GetComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = fieldText;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 6;
            input.targetGraphic = fieldRt.GetComponent<Image>();
            input.SetTextWithoutNotify(Format(row.Value));
            input.interactable = _canEdit;
            row.Input = input;

            TextMeshProUGUI unit = Label(go, "Unit", 18f, false);
            unit.text = row.Unit;
            unit.color = Muted;
            Place(unit.rectTransform, 1f, 0f, 1f, 0f, -24f, 4f, 0f, 36f);

            slider.onValueChanged.AddListener(v =>
            {
                if (row.Syncing)
                    return;
                row.Syncing = true;
                row.Value = Mathf.Max(row.Min, row.SliderMax > 50f ? Mathf.Round(v) : Mathf.Round(v * 2f) * 0.5f);
                if (row.Input != null)
                    row.Input.SetTextWithoutNotify(Format(row.Value));
                row.Syncing = false;
            });

            input.onEndEdit.AddListener(text =>
            {
                if (row.Syncing)
                    return;
                row.Syncing = true;
                float parsed;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                    || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
                    row.Value = Mathf.Clamp(parsed, row.Min, row.Max);
                if (row.Input != null)
                    row.Input.SetTextWithoutNotify(Format(row.Value));
                if (row.Slider != null)
                    row.Slider.SetValueWithoutNotify(Mathf.Min(row.Value, row.SliderMax));
                row.Syncing = false;
            });
        }

        private static void AddToggle(string text, bool value, bool interactable, System.Action<bool> changed)
        {
            var go = NewUi("Toggle", _content, typeof(Toggle));
            AddLayout(go.gameObject, 38f);

            var box = NewUi("Background", go, typeof(Image));
            box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(28f, 28f);
            box.anchoredPosition = new Vector2(2f, 0f);
            SetSprite(box.GetComponent<Image>(), Vanilla("checkbox") ?? UiAssets.ToggleOff, new Color(0f, 0f, 0f, 0.6f));

            var mark = NewUi("Checkmark", box, typeof(Image));
            Place(mark, 0f, 0f, 1f, 1f, 3f, 3f, -3f, -3f);
            Image markImg = mark.GetComponent<Image>();
            SetSprite(markImg, Vanilla("checkbox_marker"), Gold);
            // SetSprite tints real sprites white — force a bright yellow tick so on/off is obvious.
            markImg.color = CheckYellow;
            markImg.raycastTarget = false;

            TextMeshProUGUI label = Label(go, "Label", 18f, false);
            label.text = text;
            label.color = interactable ? TextColor : Muted;
            Place(label.rectTransform, 0f, 0f, 1f, 1f, 42f, 0f, 0f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            Toggle toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = markImg;
            toggle.SetIsOnWithoutNotify(value);
            toggle.interactable = interactable;
            toggle.onValueChanged.AddListener(on => changed(on));
        }

        private static Button MakeButton(RectTransform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewUi(name, parent, typeof(Image), typeof(Button));
            SetSprite(rt.GetComponent<Image>(), Vanilla("button") ?? UiAssets.BtnApply, new Color(0.35f, 0.24f, 0.13f, 1f));
            Button btn = rt.GetComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            btn.onClick.AddListener(onClick);

            TextMeshProUGUI label = Label(rt, "Text", 20f, false);
            label.text = text;
            label.color = Gold;
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            return btn;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Vanilla UI sprite by name (same names Jotunn's GUIManager uses). Scans once and
        /// again only while something is still missing. Null = not found → caller falls back.
        /// </summary>
        private static Sprite Vanilla(string name)
        {
            Sprite sprite;
            if (VanillaSprites.TryGetValue(name, out sprite) && sprite != null)
                return sprite;

            Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                Sprite s = all[i];
                if (s == null || string.IsNullOrEmpty(s.name))
                    continue;
                if (!VanillaSprites.ContainsKey(s.name))
                    VanillaSprites[s.name] = s;
            }
            if (VanillaSprites.TryGetValue(name, out sprite) && sprite != null)
                return sprite;

            Plugin.Log.LogDebug("F10 panel: vanilla sprite '" + name + "' not found, using fallback.");
            return null;
        }

        private static void SetSprite(Image img, Sprite sprite, Color fallback)
        {
            if (img == null)
                return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
            }
        }

        /// <summary>
        /// Parent first, components after: under the inactive root no Awake / OnEnable runs
        /// before Slider / Toggle / TMP_InputField are wired up.
        /// </summary>
        private static RectTransform NewUi(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (components != null)
            {
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == typeof(Image) && go.GetComponent<CanvasRenderer>() == null)
                        go.AddComponent<CanvasRenderer>();
                    if (go.GetComponent(components[i]) == null)
                        go.AddComponent(components[i]);
                }
            }
            return go.transform as RectTransform;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, float size, bool bold)
        {
            var rt = NewUi(name, parent);
            TextMeshProUGUI tmp = bold
                ? UiFonts.CreateBoldLabel(rt.gameObject, size)
                : UiFonts.CreateLabel(rt.gameObject, size);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }

        private static void AddLayout(GameObject go, float height)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null)
                le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Anchors (min / max) plus offsets (left, bottom, right, top).</summary>
        private static void Place(RectTransform rt, float minX, float minY, float maxX, float maxY,
            float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }
    }

    // Vanilla treats TextInput as open: cursor free, no player input, Escape does not open the menu.
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class SettingsPanelTextInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (SettingsPanel.BlocksInput)
                __result = true;
        }
    }

    // TextInput alone does not stop GameCamera zoom; zero the Valheim scroll axis while the panel is open.
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
    internal static class SettingsPanelScrollPatch
    {
        private static void Postfix(ref float __result)
        {
            if (SettingsPanel.BlocksInput)
                __result = 0f;
        }
    }
}
