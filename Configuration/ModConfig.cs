using BepInEx.Configuration;
using UnityEngine;

namespace StoreAndCraft
{
    public class ModConfig
    {
        // Bump when package layout changes. v3 = cfg-only ranges (no YAML).
        public const int ProtocolVersion = 3;

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
        public ConfigEntry<float> StorageRange { get; }
        public ConfigEntry<bool> AutoStackEnabled { get; }
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
        public ConfigEntry<KeyboardShortcut> TakeStackKey { get; }

        public ModConfig(ConfigFile file)
        {
            LockConfig = file.Bind("1 - General", "LockConfig", true,
                "If true (recommended on dedicated), gameplay values below are owned by the SERVER. Edit com.morda.storeandcraft.cfg on the server only — clients receive them on join. Hotkeys stay local.");
            ModEnabled = file.Bind("1 - General", "ModEnabled", true,
                "Turns the whole mod on or off without uninstalling.");
            StoreEnabled = file.Bind("2 - Store", "StoreEnabled", true,
                "If enabled, ground items can be auto-stored and dump / middle-click store works.");
            CraftEnabled = file.Bind("3 - Craft", "CraftEnabled", true,
                "If enabled, crafting, building, and station refill ([E] on smelters, kilns, ovens, torches, fires, fermenters, turrets) can use items stored in nearby chests.");
            MustHaveExisting = file.Bind("2 - Store", "MustHaveExisting", true,
                "If enabled, a chest only accepts an item if that item is already inside it. Empty chests will not vacuum new item types.");
            LeaveOneItem = file.Bind("3 - Craft", "LeaveOneItem", true,
                "If enabled, every pull from a chest (craft, build, station [E], Ctrl+middle-click fill) leaves 1 item so auto-store can keep filling that stack.");
            IgnoreHotbar = file.Bind("2 - Store", "IgnoreHotbar", true,
                "If enabled, dump will not move items from the hotbar (first inventory row).");
            HighlightOnStore = file.Bind("2 - Store", "HighlightOnStore", true,
                "If enabled, a chest flashes when something is stored into it.");
            PingOnStore = file.Bind("2 - Store", "PingOnStore", false,
                "If enabled, a map ping is placed on the chest after a store.");
            PlayerDumpRange = file.Bind("2 - Store", "PlayerDumpRange", 8f,
                "Dump / middle-click store range in meters (player → chest). Synced from server when LockConfig is on.");
            StoreRange = file.Bind("2 - Store", "StoreRange", 10f,
                "Auto-store range in meters (ground item → chest). Synced from server when LockConfig is on.");
            StorageRange = file.Bind("2 - Store", "StorageRange", 10f,
                "Extra reach for take-stack, search, and storage displays (meters). Synced from server when LockConfig is on.");
            AutoStackEnabled = file.Bind("2 - Store", "AutoStackEnabled", false,
                "If enabled, stacks already inside a chest are compacted toward the Valheim max. Never moves items from your inventory; dump and middle-click do that.");
            CraftRange = file.Bind("3 - Craft", "CraftRange", 20f,
                "Craft / build / station-[E] pull range in meters (player → chest). Synced from server when LockConfig is on.");
            IntakeInterval = file.Bind("2 - Store", "IntakeInterval", 5f,
                "Seconds between automatic scans for ground items. Lower = snappier, higher = less CPU.");
            PauseSeconds = file.Bind("2 - Store", "PauseSeconds", 10f,
                "How many seconds auto-store stays paused after the pause hotkey.");
            MaxTransfersPerTick = file.Bind("1 - General", "MaxTransfersPerTick", 8,
                "Maximum item moves per frame. Raise only if storing feels too slow.");
            DumpKey = file.Bind("4 - Keys", "DumpKey", new KeyboardShortcut(KeyCode.Period),
                "Hotkey: move allowed inventory stacks into nearby chests that already hold those items.");
            HoverStoreKey = file.Bind("4 - Keys", "HoverStoreKey", new KeyboardShortcut(KeyCode.Mouse2),
                "Hotkey: store only the inventory item under the cursor into a nearby chest that already holds it.");
            PauseKey = file.Bind("4 - Keys", "PauseKey", new KeyboardShortcut(KeyCode.P, KeyCode.LeftAlt),
                "Hotkey: pause auto-store for PauseSeconds.");
            SearchKey = file.Bind("4 - Keys", "SearchKey", new KeyboardShortcut(KeyCode.Y),
                "Hold this key and click an inventory item to ping the nearest chest that contains it.");
            PreventPullKey = file.Bind("4 - Keys", "PreventPullKey", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt),
                "Hotkey: locally disable pulling from chests for crafting/building.");
            RenameKey = file.Bind("4 - Keys", "RenameKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt),
                "Look at a chest and hold this combo instead of opening it. Shift+E (Valheim alt-use) also renames. Prefix the name with [I] to ignore the container. The custom name is shown on hover.");
            TakeStackKey = file.Bind("4 - Keys", "TakeStackKey", new KeyboardShortcut(KeyCode.Mouse2, KeyCode.LeftControl),
                "Hotkey: fill the hovered inventory stack from nearby chests, only up to max stack / carry weight.");
        }

        public float MaxGameplayRange()
        {
            return Mathf.Max(PlayerDumpRange.Value, StoreRange.Value, StorageRange.Value, CraftRange.Value);
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
            pkg.Write(StorageRange.Value);
            pkg.Write(AutoStackEnabled.Value);
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
            StorageRange.Value = pkg.ReadSingle();
            AutoStackEnabled.Value = pkg.ReadBool();
            CraftRange.Value = pkg.ReadSingle();
            IntakeInterval.Value = pkg.ReadSingle();
            PauseSeconds.Value = pkg.ReadSingle();
            MaxTransfersPerTick.Value = pkg.ReadInt();
        }
    }
}
