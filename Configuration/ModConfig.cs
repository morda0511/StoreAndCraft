using BepInEx.Configuration;
using UnityEngine;

namespace StoreAndCraft
{
    public class ModConfig
    {
        public const int ProtocolVersion = 1;

        public ConfigEntry<bool> LockConfig { get; }
        public ConfigEntry<bool> ModEnabled { get; }
        public ConfigEntry<bool> StoreEnabled { get; }
        public ConfigEntry<bool> CraftEnabled { get; }
        public ConfigEntry<bool> MustHaveExisting { get; }
        public ConfigEntry<bool> LeaveOneItem { get; }
        public ConfigEntry<bool> IgnoreHotbar { get; }
        public ConfigEntry<bool> HighlightOnStore { get; }
        public ConfigEntry<bool> PingOnStore { get; }
        public ConfigEntry<float> PlayerDumpRange { get; }
        public ConfigEntry<float> StoreRange { get; }
        public ConfigEntry<float> CraftRange { get; }
        public ConfigEntry<float> IntakeInterval { get; }
        public ConfigEntry<float> PauseSeconds { get; }
        public ConfigEntry<int> MaxTransfersPerTick { get; }
        public ConfigEntry<KeyboardShortcut> DumpKey { get; }
        public ConfigEntry<KeyboardShortcut> HoverStoreKey { get; }
        public ConfigEntry<KeyboardShortcut> PauseKey { get; }
        public ConfigEntry<KeyboardShortcut> SearchKey { get; }
        public ConfigEntry<KeyboardShortcut> PreventPullKey { get; }
        public ConfigEntry<KeyboardShortcut> RenameKey { get; }

        public ModConfig(ConfigFile file)
        {
            LockConfig = file.Bind("1 - General", "LockConfig", true,
                "If enabled, gameplay settings come from the server and clients cannot override them.");
            ModEnabled = file.Bind("1 - General", "ModEnabled", true,
                "Turns the whole mod on or off without uninstalling.");
            StoreEnabled = file.Bind("2 - Store", "StoreEnabled", true,
                "If enabled, ground items can be auto-stored and dump / middle-click store works.");
            CraftEnabled = file.Bind("3 - Craft", "CraftEnabled", true,
                "If enabled, crafting and building can use items stored in nearby chests.");
            MustHaveExisting = file.Bind("2 - Store", "MustHaveExisting", true,
                "If enabled, a chest only accepts an item if that item is already inside it. Empty chests will not vacuum new item types.");
            LeaveOneItem = file.Bind("3 - Craft", "LeaveOneItem", true,
                "If enabled, crafting/building leaves 1 item in each chest so auto-store can keep filling that stack.");
            IgnoreHotbar = file.Bind("2 - Store", "IgnoreHotbar", true,
                "If enabled, dump will not move items from the hotbar (first inventory row).");
            HighlightOnStore = file.Bind("2 - Store", "HighlightOnStore", true,
                "If enabled, a chest flashes when something is stored into it.");
            PingOnStore = file.Bind("2 - Store", "PingOnStore", false,
                "If enabled, a map ping is placed on the chest after a store.");
            PlayerDumpRange = file.Bind("2 - Store", "PlayerDumpRange", 8f,
                "Fallback dump / middle-click range in meters (player to chest). YAML dumpRange overrides this.");
            StoreRange = file.Bind("2 - Store", "StoreRange", 10f,
                "Fallback auto-store range in meters (item on ground to chest). YAML storeRange overrides this.");
            CraftRange = file.Bind("3 - Craft", "CraftRange", 20f,
                "Fallback craft/build range in meters (player to chest). YAML craftRange overrides this.");
            IntakeInterval = file.Bind("2 - Store", "IntakeInterval", 5f,
                "Seconds between automatic scans for ground items. Lower = snappier, higher = less CPU.");
            PauseSeconds = file.Bind("2 - Store", "PauseSeconds", 10f,
                "How many seconds auto-store stays paused after the pause hotkey.");
            MaxTransfersPerTick = file.Bind("1 - General", "MaxTransfersPerTick", 8,
                "Maximum item moves per frame. Raise only if storing feels too slow.");
            DumpKey = file.Bind("4 - Keys", "DumpKey", new KeyboardShortcut(KeyCode.Period),
                "Hotkey: move allowed inventory stacks into nearby chests that already hold those items.");
            HoverStoreKey = file.Bind("4 - Keys", "HoverStoreKey", new KeyboardShortcut(KeyCode.Mouse2),
                "Hotkey: store the inventory item under the cursor. If no item is hovered, dumps all allowed stacks.");
            PauseKey = file.Bind("4 - Keys", "PauseKey", new KeyboardShortcut(KeyCode.P, KeyCode.LeftAlt),
                "Hotkey: pause auto-store for PauseSeconds.");
            SearchKey = file.Bind("4 - Keys", "SearchKey", new KeyboardShortcut(KeyCode.Y),
                "Hold this key and click an inventory item to ping the nearest chest that contains it.");
            PreventPullKey = file.Bind("4 - Keys", "PreventPullKey", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt),
                "Hotkey: locally disable pulling from chests for crafting/building.");
            RenameKey = file.Bind("4 - Keys", "RenameKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt),
                "Look at a chest and hold this combo instead of opening it. Shift+E (Valheim alt-use) also renames. The custom name is shown on hover.");
        }

        public void WriteToPackage(ZPackage pkg)
        {
            pkg.Write(LockConfig.Value);
            pkg.Write(ModEnabled.Value);
            pkg.Write(StoreEnabled.Value);
            pkg.Write(CraftEnabled.Value);
            pkg.Write(MustHaveExisting.Value);
            pkg.Write(LeaveOneItem.Value);
            pkg.Write(PlayerDumpRange.Value);
            pkg.Write(StoreRange.Value);
            pkg.Write(CraftRange.Value);
            pkg.Write(IntakeInterval.Value);
            pkg.Write(PauseSeconds.Value);
            pkg.Write(MaxTransfersPerTick.Value);
        }

        public void ReadFromPackage(ZPackage pkg)
        {
            LockConfig.Value = pkg.ReadBool();
            ModEnabled.Value = pkg.ReadBool();
            StoreEnabled.Value = pkg.ReadBool();
            CraftEnabled.Value = pkg.ReadBool();
            MustHaveExisting.Value = pkg.ReadBool();
            LeaveOneItem.Value = pkg.ReadBool();
            PlayerDumpRange.Value = pkg.ReadSingle();
            StoreRange.Value = pkg.ReadSingle();
            CraftRange.Value = pkg.ReadSingle();
            IntakeInterval.Value = pkg.ReadSingle();
            PauseSeconds.Value = pkg.ReadSingle();
            MaxTransfersPerTick.Value = pkg.ReadInt();
        }
    }
}
