using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace StoreAndCraft
{
    /// <summary>
    /// Armor stand settings (Alt+E), same look as the scarecrow panel: one row per weapon / shield / tool slot
    /// of the stand with a "preset" - the piece of your bag that is swapped with the stand's piece on [E].
    /// Choosing a preset only remembers the item, nothing leaves the bag. Hosted by StationFilterMenu
    /// (open / close / Esc); this class only builds the UI. Helpers come from ScarecrowPanel.
    /// </summary>
    internal static class ArmorStandPanel
    {
        private const float PanelW = 330f;
        private const float Pad = 24f;
        private const float RowH = 40f;
        private const float RowGap = 8f;
        private const float CellW = 92f;
        private const float CellH = 92f;
        private static readonly Color Gold = new Color(0.925f, 0.77f, 0.29f, 1f);
        private static readonly Color Muted = new Color(0.78f, 0.72f, 0.60f, 0.9f);

        private static RectTransform _root;
        private static ArmorStand _stand;
        private static UnityAction _close;
        private static GameObject _panel;
        private static GameObject _list;
        private static int _openSlot = -1;

        internal static void Create(RectTransform root, ArmorStand stand, UnityAction close)
        {
            _root = root;
            _stand = stand;
            _close = close;
            _openSlot = -1;
            Build();
        }

        internal static void Rebuild()
        {
            if (_root == null || _stand == null)
                return;
            Build();
        }

        internal static void Dispose()
        {
            _root = null;
            _stand = null;
            _close = null;
            _panel = null;
            _list = null;
            _openSlot = -1;
        }

        private static void Build()
        {
            if (_panel != null)
                Object.Destroy(_panel);
            if (_list != null)
                Object.Destroy(_list);
            _panel = null;
            _list = null;

            ArmorStand stand = _stand;
            ZNetView nv = ArmorStandSwap.ViewOf(stand);
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;

            var slots = new List<int>();
            for (int i = 0; i < stand.m_slots.Count; i++)
            {
                if (ArmorStandSwap.IsPresetSlot(stand, i) && stand.HaveAttachment(i)) // only pieces that are on the stand now
                    slots.Add(i);
            }

            const float top = 56f;
            const float hintH = 44f;
            float rowBlock = 22f + RowH + RowGap;
            float panelH = top + hintH + RowGap + Mathf.Max(1, slots.Count) * rowBlock + Pad - RowGap;

            _panel = ScarecrowPanel.NewFrame(_root, "SAC_ArmorStandPanel", new Vector2(30f, -150f), new Vector2(PanelW, panelH));
            RectTransform p = (RectTransform)_panel.transform;

            ScarecrowPanel.Label(p, Loc.T("Armor stand", "Rüstungsständer"), new Vector2(Pad, -Pad + 2f),
                new Vector2(PanelW - 2f * Pad - 36f, 26f), 20f, Gold, TextAlignmentOptions.TopLeft);
            ScarecrowPanel.Button(p, "X", new Vector2(PanelW - Pad - 28f, -Pad + 4f), new Vector2(28f, 28f), "X",
                () => _close?.Invoke(), false);

            ScarecrowPanel.Label(p,
                Loc.T("Choose which item from your bag [E] swaps with the piece on the stand.",
                      "Wähle, welches Teil aus deinem Inventar [E] mit dem Teil auf dem Ständer tauscht."),
                new Vector2(Pad, -top), new Vector2(PanelW - 2f * Pad, hintH), 14f, Muted, TextAlignmentOptions.TopLeft);

            if (slots.Count == 0)
            {
                ScarecrowPanel.Label(p, Loc.T("Put a weapon, shield or tool on the stand first.", "Lege zuerst eine Waffe, einen Schild oder ein Werkzeug auf den Ständer."),
                    new Vector2(Pad, -(top + hintH + RowGap)), new Vector2(PanelW - 2f * Pad, 24f), 15f, Muted,
                    TextAlignmentOptions.TopLeft);
                return;
            }

            float y = top + hintH + RowGap;
            for (int n = 0; n < slots.Count; n++)
            {
                int slot = slots[n];
                ItemDrop.ItemData onStand = zdo != null && stand.HaveAttachment(slot) ? ArmorStandSwap.StandItem(stand, zdo, slot) : null;
                ItemDrop.ItemData preset = zdo != null ? ArmorStandSwap.PresetAsItem(zdo, slot) : null;

                string onStandText = onStand != null
                    ? StationPullFilter.DisplayName(onStand.m_shared.m_name)
                    : Loc.T("empty", "leer");
                ScarecrowPanel.Label(p, Loc.T("On the stand: ", "Auf dem Ständer: ") + onStandText, new Vector2(Pad, -y),
                    new Vector2(PanelW - 2f * Pad, 22f), 14f, Muted, TextAlignmentOptions.TopLeft);

                string presetText = preset != null ? ItemText(preset) : Loc.T("choose...", "wählen...");
                bool open = _openSlot == slot;
                RectTransform btn = ScarecrowPanel.Button(p, "Crop", new Vector2(Pad, -(y + 22f)),
                    new Vector2(PanelW - 2f * Pad, RowH), Loc.T("Swap with: ", "Tausch mit: ") + presetText,
                    () => { _openSlot = _openSlot == slot ? -1 : slot; Build(); }, open);
                Sprite icon = preset != null ? preset.GetIcon() : null;
                if (icon != null)
                    ScarecrowPanel.IconImage(btn, icon, new Vector2(8f, -6f), 28f);
                y += rowBlock;
            }

            if (_openSlot >= 0)
                BuildItemList();
        }

        private static void BuildItemList()
        {
            ArmorStand stand = _stand;
            int slot = _openSlot;
            Player player = Player.m_localPlayer;
            if (stand == null || player == null || slot < 0 || slot >= stand.m_slots.Count)
                return;

            // Items of the bag this slot can hold (same rule as vanilla: the stand must be able to show it).
            var types = stand.m_slots[slot].m_supportedTypes;
            var items = new List<ItemDrop.ItemData>();
            var seen = new HashSet<string>();
            foreach (ItemDrop.ItemData it in player.GetInventory().GetAllItems())
            {
                if (it.m_dropPrefab == null || !types.Contains(it.m_shared.m_itemType) || !ArmorStandSwap.StandCanShow(it))
                    continue;
                if (seen.Add(it.m_dropPrefab.name + "#" + it.m_quality))
                    items.Add(it);
            }

            const int cols = 4;
            int count = items.Count + 1; // first cell clears the preset
            int rows = Mathf.Max(1, (count + cols - 1) / cols);
            float w = 2f * 18f + cols * CellW + (cols - 1) * 6f;
            float h = 2f * 18f + rows * CellH + (rows - 1) * 6f;
            _list = ScarecrowPanel.NewFrame(_root, "SAC_ArmorStandItems", new Vector2(30f + PanelW + 8f, -150f), new Vector2(w, h));
            RectTransform lp = (RectTransform)_list.transform;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Vector2 pos = new Vector2(18f + col * (CellW + 6f), -(18f + row * (CellH + 6f)));
                if (i == 0)
                {
                    RectTransform none = ScarecrowPanel.Button(lp, "None", pos, new Vector2(CellW, CellH), "",
                        () => Pick(slot, null), false);
                    ScarecrowPanel.Label(none, Loc.T("None", "Keine"), new Vector2(4f, -30f), new Vector2(CellW - 8f, 34f), 15f,
                        Muted, TextAlignmentOptions.Top);
                    continue;
                }
                ItemDrop.ItemData item = items[i - 1];
                RectTransform cell = ScarecrowPanel.Button(lp, "Item" + i, pos, new Vector2(CellW, CellH), "",
                    () => Pick(slot, item), false);
                Sprite icon = item.GetIcon();
                if (icon != null)
                    ScarecrowPanel.IconImage(cell, icon, new Vector2((CellW - 44f) * 0.5f, -8f), 44f);
                ScarecrowPanel.Label(cell, ItemText(item), new Vector2(4f, -54f), new Vector2(CellW - 8f, 34f), 12f, Muted,
                    TextAlignmentOptions.Top);
            }
        }

        private static void Pick(int slot, ItemDrop.ItemData item)
        {
            if (_stand == null)
                return;
            ArmorStandSwap.SetPreset(_stand, slot, item);
            _openSlot = -1;
            Build();
        }

        private static string ItemText(ItemDrop.ItemData item)
        {
            string name = StationPullFilter.DisplayName(item.m_shared.m_name);
            return item.m_quality > 1 ? name + " (" + item.m_quality + ")" : name;
        }
    }
}
