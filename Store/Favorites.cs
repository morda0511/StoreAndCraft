using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Local-only favorites (not server-synced). Protects item types from dump / hover-store.
    /// Key = shared name, or shared|quality for unique gear.
    /// </summary>
    internal static class Favorites
    {
        private static readonly HashSet<string> Keys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string FilePath
        {
            get { return Path.Combine(Paths.ConfigPath, "StoreAndCraft.favorites.txt"); }
        }

        public static Color Tint = new Color(1f, 0.82f, 0.25f, 1f);

        public static void Load()
        {
            Keys.Clear();
            try
            {
                if (!File.Exists(FilePath))
                    return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string key = line.Trim();
                    if (key.Length > 0 && !key.StartsWith("#"))
                        Keys.Add(key);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Favorites load: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# StoreAndCraft favorites (local). One key per line.");
                sb.AppendLine("# Hover an inventory item and press the Favorite key (default F) to toggle.");
                foreach (string key in Keys)
                    sb.AppendLine(key);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Favorites save: " + ex.Message);
            }
        }

        public static string KeyFor(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return null;

            string shared = item.m_shared.m_name;
            if (string.IsNullOrEmpty(shared))
                return null;

            // Unique gear: keep quality so Iron sword ≠ Black metal sword tiers stay distinct.
            if (item.m_shared.m_maxStackSize <= 1)
                return shared + "|" + item.m_quality;

            return shared;
        }

        public static bool IsFavorite(ItemDrop.ItemData item)
        {
            string key = KeyFor(item);
            return !string.IsNullOrEmpty(key) && Keys.Contains(key);
        }

        public static bool Toggle(ItemDrop.ItemData item)
        {
            string key = KeyFor(item);
            if (string.IsNullOrEmpty(key))
                return false;

            if (Keys.Contains(key))
            {
                Keys.Remove(key);
                Save();
                return false;
            }

            Keys.Add(key);
            Save();
            return true;
        }

        public static void TryToggleHovered()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            if (!InventoryGui.IsVisible())
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T("Open inventory first.", "Zuerst Inventar öffnen."),
                    0, null, false);
                return;
            }

            ItemDrop.ItemData item = HoverStore.GetHoveredPlayerItem();
            if (item == null)
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T("Hover an inventory item first.", "Zuerst über ein Inventar-Item zeigen."),
                    0, null, false);
                return;
            }

            bool nowFav = Toggle(item);
            string name = item.m_shared != null
                ? Localization.instance.Localize(item.m_shared.m_name)
                : "?";

            player.Message(
                MessageHud.MessageType.Center,
                nowFav
                    ? Loc.T("★ Favorite: " + name + " (protected from dump)", "★ Favorit: " + name + " (vor Dump geschützt)")
                    : Loc.T("Favorite removed: " + name, "Favorit entfernt: " + name),
                0, null, false);
        }
    }
}
