using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace StoreAndCraftServer
{
    internal sealed class PieceRule
    {
        public bool? Store;
        public bool? Craft;
        public float? StoreRange;
        public float? CraftRange;
        public float? DumpRange;
        public readonly List<string> Allow = new List<string>();
        public readonly List<string> Deny = new List<string>();
    }

    internal static class RulesFile
    {
        private static readonly Dictionary<string, PieceRule> Pieces =
            new Dictionary<string, PieceRule>(StringComparer.OrdinalIgnoreCase);

        public static float? DefaultStoreRange { get; private set; }
        public static float? DefaultCraftRange { get; private set; }
        public static float? DefaultDumpRange { get; private set; }

        public static string Path
        {
            get { return System.IO.Path.Combine(Paths.ConfigPath, "StoreAndCraftServer.rules.yml"); }
        }

        public static void LoadOrCreate()
        {
            try
            {
                if (!File.Exists(Path))
                    File.WriteAllText(Path, DefaultText(), Encoding.UTF8);

                Parse(File.ReadAllLines(Path));
                Plugin.Log.LogInfo("StoreAndCraftServer rules loaded (" + Pieces.Count + " piece entries).");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("StoreAndCraftServer rules failed: " + ex.Message);
            }
        }

        public static PieceRule Get(string piecePrefab)
        {
            if (string.IsNullOrEmpty(piecePrefab))
                return null;

            PieceRule rule;
            return Pieces.TryGetValue(piecePrefab, out rule) ? rule : null;
        }

        public static bool AllowsStore(string piecePrefab, ItemDrop.ItemData item)
        {
            return Allows(piecePrefab, item, store: true);
        }

        public static bool AllowsCraft(string piecePrefab, string sharedName)
        {
            PieceRule rule = Get(piecePrefab);
            if (rule != null && rule.Craft.HasValue && !rule.Craft.Value)
                return false;
            if (rule == null)
                return true;
            return MatchesLists(rule, sharedName, null);
        }

        public static bool AllowsCraft(string piecePrefab, ItemDrop.ItemData item)
        {
            return Allows(piecePrefab, item, store: false);
        }

        private static bool Allows(string piecePrefab, ItemDrop.ItemData item, bool store)
        {
            PieceRule rule = Get(piecePrefab);
            if (rule == null)
                return true;

            if (store && rule.Store.HasValue && !rule.Store.Value)
                return false;
            if (!store && rule.Craft.HasValue && !rule.Craft.Value)
                return false;

            return MatchesLists(rule, ItemIds.SharedName(item), item);
        }

        private static bool MatchesLists(PieceRule rule, string sharedName, ItemDrop.ItemData item)
        {
            if (rule.Deny.Count > 0)
            {
                foreach (string token in rule.Deny)
                {
                    if (item != null && ItemIds.Matches(item, token))
                        return false;
                    if (item == null && NamesEqual(sharedName, token))
                        return false;
                }
            }

            if (rule.Allow.Count == 0)
                return true;

            foreach (string token in rule.Allow)
            {
                if (item != null && ItemIds.Matches(item, token))
                    return true;
                if (item == null && NamesEqual(sharedName, token))
                    return true;
            }

            return false;
        }

        private static bool NamesEqual(string sharedName, string token)
        {
            if (string.IsNullOrEmpty(sharedName) || string.IsNullOrEmpty(token))
                return false;
            if (string.Equals(sharedName, token, StringComparison.OrdinalIgnoreCase))
                return true;
            string resolved = ItemIds.SharedFromToken(token);
            return string.Equals(sharedName, resolved, StringComparison.OrdinalIgnoreCase);
        }

        public static float StoreRange(string piecePrefab, float fallback)
        {
            PieceRule rule = Get(piecePrefab);
            if (rule != null && rule.StoreRange.HasValue)
                return rule.StoreRange.Value;
            if (DefaultStoreRange.HasValue)
                return DefaultStoreRange.Value;
            return fallback;
        }

        public static float CraftRange(string piecePrefab, float fallback)
        {
            PieceRule rule = Get(piecePrefab);
            if (rule != null && rule.CraftRange.HasValue)
                return rule.CraftRange.Value;
            if (DefaultCraftRange.HasValue)
                return DefaultCraftRange.Value;
            return fallback;
        }

        public static float DumpRange(string piecePrefab, float fallback)
        {
            PieceRule rule = Get(piecePrefab);
            if (rule != null && rule.DumpRange.HasValue)
                return rule.DumpRange.Value;
            if (DefaultDumpRange.HasValue)
                return DefaultDumpRange.Value;
            return fallback;
        }

        public static float MaxScanRange(float cfgStore, float cfgCraft, float cfgDump)
        {
            float max = Mathf.Max(cfgStore, cfgCraft, cfgDump);
            if (DefaultStoreRange.HasValue)
                max = Mathf.Max(max, DefaultStoreRange.Value);
            if (DefaultCraftRange.HasValue)
                max = Mathf.Max(max, DefaultCraftRange.Value);
            if (DefaultDumpRange.HasValue)
                max = Mathf.Max(max, DefaultDumpRange.Value);

            foreach (PieceRule rule in Pieces.Values)
            {
                if (rule.StoreRange.HasValue)
                    max = Mathf.Max(max, rule.StoreRange.Value);
                if (rule.CraftRange.HasValue)
                    max = Mathf.Max(max, rule.CraftRange.Value);
                if (rule.DumpRange.HasValue)
                    max = Mathf.Max(max, rule.DumpRange.Value);
            }

            return max;
        }

        private static void Parse(string[] lines)
        {
            Pieces.Clear();
            DefaultStoreRange = null;
            DefaultCraftRange = null;
            DefaultDumpRange = null;
            string currentName = null;
            PieceRule current = null;
            string listMode = null;
            bool inDefaults = false;

            foreach (string raw in lines)
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int indent = 0;
                while (indent < line.Length && line[indent] == ' ')
                    indent++;
                line = line.Trim();

                if (indent == 0 && line.EndsWith(":") && !line.Equals("defaults:", StringComparison.OrdinalIgnoreCase)
                    && !line.Equals("pieces:", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.Equals("defaults:", StringComparison.OrdinalIgnoreCase))
                {
                    inDefaults = true;
                    currentName = null;
                    current = null;
                    listMode = null;
                    continue;
                }

                if (line.Equals("pieces:", StringComparison.OrdinalIgnoreCase))
                {
                    inDefaults = false;
                    currentName = null;
                    current = null;
                    listMode = null;
                    continue;
                }

                if (inDefaults)
                {
                    string dkey;
                    string dvalue;
                    if (!SplitKv(line, out dkey, out dvalue))
                        continue;
                    if (dkey.Equals("storeRange", StringComparison.OrdinalIgnoreCase))
                        DefaultStoreRange = ParseFloat(dvalue);
                    else if (dkey.Equals("craftRange", StringComparison.OrdinalIgnoreCase))
                        DefaultCraftRange = ParseFloat(dvalue);
                    else if (dkey.Equals("dumpRange", StringComparison.OrdinalIgnoreCase))
                        DefaultDumpRange = ParseFloat(dvalue);
                    continue;
                }

                if (indent <= 2 && line.EndsWith(":") && indent < 4 && !IsKeyValue(line))
                {
                    currentName = line.TrimEnd(':').Trim();
                    current = new PieceRule();
                    Pieces[currentName] = current;
                    listMode = null;
                    continue;
                }

                if (current == null)
                    continue;

                if (line.Equals("allow:", StringComparison.OrdinalIgnoreCase))
                {
                    listMode = "allow";
                    continue;
                }
                if (line.Equals("deny:", StringComparison.OrdinalIgnoreCase))
                {
                    listMode = "deny";
                    continue;
                }

                if (line.StartsWith("- "))
                {
                    string token = line.Substring(2).Trim().Trim('"');
                    if (listMode == "allow")
                        current.Allow.Add(token);
                    else if (listMode == "deny")
                        current.Deny.Add(token);
                    continue;
                }

                listMode = null;
                string key;
                string value;
                if (!SplitKv(line, out key, out value))
                    continue;

                if (key.Equals("store", StringComparison.OrdinalIgnoreCase))
                    current.Store = ParseBool(value);
                else if (key.Equals("craft", StringComparison.OrdinalIgnoreCase))
                    current.Craft = ParseBool(value);
                else if (key.Equals("storeRange", StringComparison.OrdinalIgnoreCase))
                    current.StoreRange = ParseFloat(value);
                else if (key.Equals("craftRange", StringComparison.OrdinalIgnoreCase))
                    current.CraftRange = ParseFloat(value);
                else if (key.Equals("dumpRange", StringComparison.OrdinalIgnoreCase))
                    current.DumpRange = ParseFloat(value);
            }
        }

        private static bool IsKeyValue(string line)
        {
            int i = line.IndexOf(':');
            return i > 0 && i < line.Length - 1;
        }

        private static bool SplitKv(string line, out string key, out string value)
        {
            key = null;
            value = null;
            int i = line.IndexOf(':');
            if (i <= 0)
                return false;
            key = line.Substring(0, i).Trim();
            value = line.Substring(i + 1).Trim().Trim('"');
            return key.Length > 0;
        }

        private static bool ParseBool(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static float ParseFloat(string value)
        {
            float n;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
                return n;
            return 0f;
        }

        private static string DefaultText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# StoreAndCraftServer rules");
            sb.AppendLine("# File: BepInEx/config/StoreAndCraftServer.rules.yml");
            sb.AppendLine("# Distances are in meters. Saving this file on the server reloads and syncs.");
            sb.AppendLine("#");
            sb.AppendLine("# storeRange = how far a chest pulls items from the ground");
            sb.AppendLine("# dumpRange  = how far dump / middle-click can send items from the player to a chest");
            sb.AppendLine("# craftRange = how far crafting and building can pull materials from a chest");
            sb.AppendLine("# store/craft = true/false to allow storing or crafting from that piece");
            sb.AppendLine("# allow = only these items (prefab name like Wood, or $item_wood). Empty = all items.");
            sb.AppendLine("# deny  = never store/craft these items");
            sb.AppendLine();
            sb.AppendLine("defaults:");
            sb.AppendLine("  storeRange: 10");
            sb.AppendLine("  dumpRange: 12");
            sb.AppendLine("  craftRange: 20");
            sb.AppendLine();
            sb.AppendLine("pieces:");
            sb.AppendLine("  piece_chest:");
            sb.AppendLine("    store: true");
            sb.AppendLine("    craft: true");
            sb.AppendLine("  piece_chest_wood:");
            sb.AppendLine("    store: true");
            sb.AppendLine("    craft: true");
            sb.AppendLine("    # storeRange: 15");
            sb.AppendLine("    # dumpRange: 15");
            sb.AppendLine("    # craftRange: 25");
            return sb.ToString();
        }
    }
}
