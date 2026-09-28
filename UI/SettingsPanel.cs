using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// F10 panel: activity log checkbox (anyone, local) and gameplay ranges with Save
    /// (host / adminlist on servers, anyone offline). IMGUI because the SkillsDialog-based
    /// menus only offer click rows, no sliders or number fields.
    /// </summary>
    internal static class SettingsPanel
    {
        private const int WindowId = 0x5AC0F11;
        private const float WindowWidth = 620f;
        private const float SliderMax = 200f;

        private sealed class Row
        {
            public string Key;
            public string En;
            public string De;
            public string HelpEn;
            public string HelpDe;
            public float Value;
            public string Text;
        }

        public static bool IsOpen { get; private set; }

        private static bool _booted;
        private static bool _canEdit;
        private static bool _showInfo;
        private static Vector2 _infoScroll;
        private static int _closeAtFrame = -1;
        private static string _status;
        private static Rect _window;
        private static GUIStyle _windowStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _hintStyle;
        private static GUIStyle _infoTitleStyle;
        private static GUIStyle _infoStyle;
        private static Texture2D _background;
        private static readonly List<Row> Rows = new List<Row>();

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
            EnsureHost();
            _canEdit = ConfigCommands.CanEditRanges();
            LoadRows();
            _status = null;
            _closeAtFrame = -1;
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
            GUIUtility.keyboardControl = 0;
        }

        internal static bool BlocksInput
        {
            get { return IsOpen && Player.m_localPlayer != null; }
        }

        internal static void Tick()
        {
            if (!IsOpen)
                return;
            if (Player.m_localPlayer == null || Plugin.Settings == null)
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

        private static void EnsureHost()
        {
            if (_booted || Plugin.Instance == null)
                return;
            _booted = true;
            Plugin.Instance.gameObject.AddComponent<SettingsPanelGui>();
        }

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
                Value = entry.Value,
                Text = Format(entry.Value)
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

            _status = ConfigCommands.ApplyFromPanel(changes);
            LoadRows();
        }

        internal static void Draw()
        {
            if (!IsOpen)
                return;
            if (Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (_window.width <= 0f)
            {
                _window = new Rect(
                    (Screen.width / scale - WindowWidth) * 0.5f,
                    Screen.height / scale * 0.15f,
                    WindowWidth,
                    10f);
            }

            _window = GUILayout.Window(WindowId, _window, DrawWindow, "StoreAndCraft", _windowStyle,
                GUILayout.Width(WindowWidth));
            GUI.matrix = oldMatrix;
        }

        private static void DrawWindow(int id)
        {
            GUILayout.Space(6f);

            bool logOn = GUILayout.Toggle(ActivityLog.Visible,
                "  " + Loc.T("Show activity log", "Aktivitäts-Log anzeigen"));
            if (logOn != ActivityLog.Visible)
                ActivityLog.SetVisible(logOn);

            GUILayout.Space(10f);
            GUILayout.Label(Loc.T("Ranges (meters, 0-1000)", "Reichweiten (Meter, 0-1000)"), _labelStyle);

            if (!_canEdit)
            {
                GUILayout.Label(Loc.T(
                    "Only admins (adminlist) can change ranges on this server.",
                    "Nur Admins (Adminliste) dürfen auf diesem Server Reichweiten ändern."), _hintStyle);
            }

            bool oldEnabled = GUI.enabled;
            GUI.enabled = _canEdit;
            for (int i = 0; i < Rows.Count; i++)
                DrawRow(Rows[i]);
            GUI.enabled = oldEnabled;

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUI.enabled = _canEdit;
            if (GUILayout.Button(Loc.T("Save", "Speichern"), GUILayout.Height(30f)))
                Save();
            GUI.enabled = oldEnabled;
            if (GUILayout.Button(Loc.T("Close", "Schließen"), GUILayout.Height(30f)))
                RequestClose();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
                GUILayout.Label(_status, _hintStyle);

            GUILayout.Space(8f);
            string infoTitle = Loc.T("What do the ranges do?", "Was bewirken die Reichweiten?");
            if (GUILayout.Button((_showInfo ? "[-]  " : "[+]  ") + infoTitle, _labelStyle))
                _showInfo = !_showInfo;
            if (_showInfo)
                DrawInfo();

            GUI.DragWindow();
        }

        private static void DrawInfo()
        {
            _infoScroll = GUILayout.BeginScrollView(_infoScroll, GUILayout.Height(260f));
            for (int i = 0; i < Rows.Count; i++)
            {
                GUILayout.Label("• " + Loc.T(Rows[i].En, Rows[i].De), _infoTitleStyle);
                GUILayout.Label(Loc.T(Rows[i].HelpEn, Rows[i].HelpDe), _infoStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label(Loc.T(
                "Valheim only loads about 120 m around you (Simulation distance in the graphics settings). "
                + "Larger values change nothing beyond that. Tip: auto-fill 80-120, station to chest 10-15, all others 10-30.",
                "Valheim lädt nur etwa 120 m um dich herum (Simulationsdistanz in den Grafik-Einstellungen). "
                + "Größere Werte bewirken darüber hinaus nichts. Tipp: Auto-Fill 80-120, Station zur Kiste 10-15, alles andere 10-30."),
                _hintStyle);
            GUILayout.EndScrollView();
        }

        private static void DrawRow(Row row)
        {
            GUILayout.Label(Loc.T(row.En, row.De));
            GUILayout.BeginHorizontal();

            float shown = Mathf.Min(row.Value, SliderMax);
            float slid = GUILayout.HorizontalSlider(shown, 0f, SliderMax, GUILayout.ExpandWidth(true));
            if (Mathf.Abs(slid - shown) > 0.001f)
            {
                row.Value = Mathf.Round(slid);
                row.Text = Format(row.Value);
            }

            GUILayout.Space(8f);
            string text = GUILayout.TextField(row.Text ?? "", 6, GUILayout.Width(70f));
            if (text != row.Text)
            {
                row.Text = text;
                float parsed;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                    || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
                    row.Value = Mathf.Clamp(parsed, 0f, ModConfig.MaxRange);
            }
            GUILayout.Label("m", GUILayout.Width(18f));

            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private static void EnsureStyles()
        {
            if (_windowStyle != null)
                return;

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0.08f, 0.07f, 0.06f, 0.96f));
            _background.Apply();

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _background;
            _windowStyle.onNormal.background = _background;
            _windowStyle.fontSize = 18;
            _windowStyle.padding = new RectOffset(14, 14, 28, 12);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
            _labelStyle.normal.textColor = new Color(1f, 0.85f, 0.4f, 1f);

            _hintStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _hintStyle.normal.textColor = new Color(1f, 0.75f, 0.45f, 1f);

            _infoTitleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _infoTitleStyle.normal.textColor = Color.white;

            _infoStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(16, 4, 0, 4) };
            _infoStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        }
    }

    internal sealed class SettingsPanelGui : MonoBehaviour
    {
        private void OnGUI()
        {
            SettingsPanel.Draw();
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
}
