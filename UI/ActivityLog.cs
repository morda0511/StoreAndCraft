using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Optional transfer log (F11). Up to 10 lines under MessageHud TopLeft
    /// ("Dir ist kalt" / item pickup text), same TMP font. Starts off. Local only.
    /// </summary>
    internal static class ActivityLog
    {
        private const int MaxLines = 10;
        private const float LineSeconds = 12f;

        private struct Entry
        {
            public string Text;
            public float Expires;
        }

        private static readonly List<Entry> Lines = new List<Entry>(MaxLines);
        private static readonly StringBuilder Sb = new StringBuilder(256);
        private static bool _visible;
        private static bool _booted;
        private static TextMeshProUGUI _tmp;
        private static RectTransform _rt;

        public static bool Visible
        {
            get { return _visible; }
        }

        public static void Toggle()
        {
            _visible = !_visible;
            EnsureHost();
            RefreshUi();
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    _visible
                        ? Loc.T("Activity log on", "Aktivitäts-Log an")
                        : Loc.T("Activity log off", "Aktivitäts-Log aus"),
                    0, null, false);
            }
        }

        /// <summary>Chest → station, e.g. "Chest 10x Tin → Smelter".</summary>
        public static void FromChest(string stationLabel, int amount, string itemLabel)
        {
            if (amount <= 0 || string.IsNullOrEmpty(itemLabel))
                return;
            Add(Format(ChestWord(), amount, itemLabel, StationWord(stationLabel)));
        }

        /// <summary>Station → chest, e.g. "Smelter 1x Tin → Chest".</summary>
        public static void ToChest(string stationLabel, int amount, string itemLabel)
        {
            if (amount <= 0 || string.IsNullOrEmpty(itemLabel))
                return;
            Add(Format(StationWord(stationLabel), amount, itemLabel, ChestWord()));
        }

        public static void Add(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;
            EnsureHost();
            float exp = Time.unscaledTime + LineSeconds;
            while (Lines.Count >= MaxLines)
                Lines.RemoveAt(0);
            Lines.Add(new Entry { Text = line, Expires = exp });
            RefreshUi();
        }

        public static void Tick()
        {
            if (!_booted)
                return;
            EnsureHost();
            if (Lines.Count == 0)
            {
                RefreshUi();
                return;
            }

            float now = Time.unscaledTime;
            bool changed = false;
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].Expires <= now)
                {
                    Lines.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed)
                RefreshUi();
            else
                LayoutUnderMessage();
        }

        private static string Format(string from, int amount, string item, string to)
        {
            return from + " " + amount + "x " + item + " → " + to;
        }

        private static string ChestWord()
        {
            return Loc.T("Chest", "Kiste");
        }

        private static string StationWord(string stationLabel)
        {
            return string.IsNullOrEmpty(stationLabel) ? "?" : stationLabel;
        }

        private static void EnsureHost()
        {
            if (Plugin.Instance == null)
                return;
            if (!_booted)
            {
                _booted = true;
                Plugin.Instance.gameObject.AddComponent<ActivityLogHud>();
            }

            if (_tmp != null)
                return;

            MessageHud hud = MessageHud.instance;
            if (hud == null || hud.m_messageText == null)
                return;

            TMP_Text template = hud.m_messageText;
            GameObject go = new GameObject("SAC_ActivityLog");
            go.transform.SetParent(template.rectTransform.parent, false);
            go.layer = template.gameObject.layer;

            _tmp = go.AddComponent<TextMeshProUGUI>();
            _tmp.font = template.font;
            _tmp.fontSharedMaterial = template.fontSharedMaterial;
            _tmp.fontSize = template.fontSize;
            _tmp.color = template.color;
            _tmp.alignment = TextAlignmentOptions.TopLeft;
            _tmp.raycastTarget = false;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
            _tmp.overflowMode = TextOverflowModes.Overflow;
            _tmp.richText = false;

            _rt = _tmp.rectTransform;
            LayoutUnderMessage();
            RefreshUi();
        }

        private static void LayoutUnderMessage()
        {
            if (_rt == null || _tmp == null)
                return;
            MessageHud hud = MessageHud.instance;
            if (hud == null || hud.m_messageText == null)
                return;

            RectTransform msg = hud.m_messageText.rectTransform;
            float font = _tmp.fontSize > 0f ? _tmp.fontSize : 18f;
            float gap = font * 1.25f;
            float width = Mathf.Max(360f, msg.rect.width > 10f ? msg.rect.width : 420f);
            float height = MaxLines * (font + 4f);

            _rt.anchorMin = msg.anchorMin;
            _rt.anchorMax = msg.anchorMax;
            _rt.pivot = new Vector2(msg.pivot.x, 1f);
            _rt.sizeDelta = new Vector2(width, height);
            // Grow downward from just under the TopLeft status / pickup line.
            _rt.anchoredPosition = msg.anchoredPosition + new Vector2(0f, -gap);
        }

        private static void RefreshUi()
        {
            if (_tmp == null)
                return;

            bool show = _visible
                && Lines.Count > 0
                && Player.m_localPlayer != null
                && !Hud.IsUserHidden();
            if (Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()))
                show = false;

            _tmp.gameObject.SetActive(show);
            if (!show)
            {
                _tmp.text = string.Empty;
                return;
            }

            LayoutUnderMessage();
            Sb.Length = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                if (i > 0)
                    Sb.Append('\n');
                Sb.Append(Lines[i].Text);
            }
            _tmp.text = Sb.ToString();
        }
    }

    internal sealed class ActivityLogHud : MonoBehaviour
    {
        private void Update()
        {
            ActivityLog.Tick();
        }
    }
}
