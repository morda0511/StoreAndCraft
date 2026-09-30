using BepInEx.Configuration;
using UnityEngine;

namespace StoreAndCraft
{
    public class ModConfig
    {
        // Bump when package layout or shared station behavior changes. v18 = SAC-CATCHUP settings synced.
        // v19 = TorchAutoFillDefault synced.
        public const int ProtocolVersion = 19;
        public const float MaxRange = 1000f;

        public ConfigEntry<bool> LockConfig { get; }
        public ConfigEntry<bool> ModEnabled { get; }
        public ConfigEntry<bool> StoreEnabled { get; }
        public ConfigEntry<bool> AutoIntakeEnabled { get; }
        public ConfigEntry<bool> CraftEnabled { get; }
        public ConfigEntry<bool> MustHaveExisting { get; }
        public ConfigEntry<bool> LeaveOneItem { get; }
        public ConfigEntry<bool> IgnoreHotbar { get; }
        public ConfigEntry<bool> HighlightOnStore { get; }
        public ConfigEntry<bool> PingOnStore { get; }
        public ConfigEntry<float> PlayerDumpRange { get; }
        public ConfigEntry<float> StoreRange { get; }
        public ConfigEntry<float> StorageRange { get; }
        public ConfigEntry<float> DisplayRange { get; }
        public ConfigEntry<bool> AutoStackEnabled { get; }
        public ConfigEntry<float> CraftRange { get; }
        public ConfigEntry<float> AutoFillRange { get; }
        public ConfigEntry<float> AutoFillChestRange { get; }
        public ConfigEntry<float> IntakeInterval { get; }
        public ConfigEntry<int> MaxTransfersPerTick { get; }
        public ConfigEntry<KeyboardShortcut> DumpKey { get; }
        public ConfigEntry<KeyboardShortcut> HoverStoreKey { get; }
        public ConfigEntry<KeyboardShortcut> SearchKey { get; }
        public ConfigEntry<KeyboardShortcut> RenameKey { get; }
        public ConfigEntry<KeyboardShortcut> TakeStackKey { get; }
        public ConfigEntry<KeyboardShortcut> FavoriteKey { get; }
        public ConfigEntry<KeyboardShortcut> AutoFillKey { get; }
        public ConfigEntry<KeyboardShortcut> AutoDropKey { get; }
        public ConfigEntry<KeyboardShortcut> ActivityLogKey { get; }
        public ConfigEntry<bool> ActivityLogVisible { get; }
        public ConfigEntry<string> DisplayMenuStyle { get; }
        public ConfigEntry<KeyboardShortcut> DisplayRangeKey { get; }
        public ConfigEntry<KeyboardShortcut> BuildGrabKey { get; }
        public ConfigEntry<bool> FeedTroughEnabled { get; }
        public ConfigEntry<float> FeedTroughRange { get; }
        // SAC-CATCHUP
        public ConfigEntry<bool> CatchUpEnabled { get; }
        public ConfigEntry<float> CatchUpMaxHours { get; }
        public ConfigEntry<double> CatchUpSince { get; }
        public ConfigEntry<bool> TorchAutoFillDefault { get; }

        public ModConfig(ConfigFile file)
        {
            var range = new AcceptableValueRange<float>(0f, MaxRange);
            LockConfig = file.Bind("1 - General", "LockConfig", true,
                "If true (recommended on dedicated), gameplay values below are owned by the SERVER. Edit com.morda.storeandcraft.cfg on the server only — clients receive them on join. Hotkeys stay local.");
            ModEnabled = file.Bind("1 - General", "ModEnabled", true,
                "Turns the whole mod on or off without uninstalling.");
            StoreEnabled = file.Bind("2 - Store", "StoreEnabled", true,
                "If enabled, dump / middle-click store, take-stack, and auto-stack work. Ground auto-store is controlled separately by AutoIntakeEnabled.");
            AutoIntakeEnabled = file.Bind("2 - Store", "AutoIntakeEnabled", true,
                "If enabled, ground items are pulled into nearby matching chests automatically. Turn off to leave drops on the ground (feed pets, trade) while dump / middle-click still work. Synced from server when LockConfig is on. Chat: /store enable|disable.");
            CraftEnabled = file.Bind("3 - Craft", "CraftEnabled", true,
                "If enabled, crafting, building, station refill ([E] on smelters, kilns, ovens, torches, fires, fermenters, turrets), and kiln/smelter/torch auto-fill can use items stored in nearby chests.");
            MustHaveExisting = file.Bind("2 - Store", "MustHaveExisting", true,
                "If enabled, a chest only accepts an item if that item is already inside it. Empty chests will not vacuum new item types.");
            LeaveOneItem = file.Bind("3 - Craft", "LeaveOneItem", true,
                "If enabled, every pull from a chest (craft, build, plant, station [E], Ctrl+middle-click fill) leaves 1 item so auto-store can keep filling that stack. That leftover item cannot be spent (same as smelter [E]).");
            IgnoreHotbar = file.Bind("2 - Store", "IgnoreHotbar", true,
                "If enabled, dump will not move items from the hotbar (first inventory row). Equipment / quick-slot mod overflow (outside the live bag grid) is always skipped. Wider/Deeper Pockets bag rows are dumped normally.");
            HighlightOnStore = file.Bind("2 - Store", "HighlightOnStore", true,
                "If enabled, a chest flashes when something is stored into it.");
            PingOnStore = file.Bind("2 - Store", "PingOnStore", false,
                "If enabled, a map ping is placed on the chest after a store.");
            PlayerDumpRange = file.Bind("2 - Store", "PlayerDumpRange", 8f, new ConfigDescription(
                "Dump / middle-click store range in meters (player → chest). Synced from server when LockConfig is on.", range));
            StoreRange = file.Bind("2 - Store", "StoreRange", 10f, new ConfigDescription(
                "Auto-store range in meters (ground item → chest). Synced from server when LockConfig is on.", range));
            StorageRange = file.Bind("2 - Store", "StorageRange", 10f, new ConfigDescription(
                "Extra reach for take-stack and search (meters). Synced from server when LockConfig is on.", range));
            DisplayRange = file.Bind("2 - Store", "DisplayRange", 10f, new ConfigDescription(
                "Default chest-scan range for Storage Displays (meters). Each display can override with Alt+R (5–50). Synced from server when LockConfig is on.", range));
            AutoStackEnabled = file.Bind("2 - Store", "AutoStackEnabled", false,
                "If enabled, stacks already inside a chest are compacted toward the Valheim max. Never moves items from your inventory; dump and middle-click do that.");
            CraftRange = file.Bind("3 - Craft", "CraftRange", 20f, new ConfigDescription(
                "Craft / build / station-[E] pull range in meters (player → chest). Synced from server when LockConfig is on.", range));
            AutoFillRange = file.Bind("3 - Craft", "AutoFillRange", 20f, new ConfigDescription(
                "Auto-fill range in meters: player → station (kiln/smelter/blast furnace/oven/fermenter/torch). Stations farther away pause until you come closer. Chests are searched around each station (see AutoFillChestRange). Auto-fill never takes from your bag, only from matching linked / untagged chests. Independent from CraftRange. Synced from server when LockConfig is on.", range));
            AutoFillChestRange = file.Bind("3 - Craft", "AutoFillChestRange", 0f, new ConfigDescription(
                "Station → chest range in meters for auto-fill materials and auto-store output. 0 = same as AutoFillRange. Tip: large AutoFillRange (e.g. 80) with a small AutoFillChestRange (e.g. 10) keeps a whole base running while each station only uses chests right next to it. Synced from server when LockConfig is on.", range));
            IntakeInterval = file.Bind("2 - Store", "IntakeInterval", 5f,
                "Seconds between automatic scans for ground items. Lower = snappier, higher = less CPU.");
            MaxTransfersPerTick = file.Bind("1 - General", "MaxTransfersPerTick", 8,
                "Maximum item moves per frame. Raise only if storing feels too slow.");
            DumpKey = file.Bind("4 - Keys", "DumpKey", new KeyboardShortcut(KeyCode.Period),
                "Hotkey: move allowed inventory stacks into nearby chests that already hold those items.");
            HoverStoreKey = file.Bind("4 - Keys", "HoverStoreKey", new KeyboardShortcut(KeyCode.Mouse2),
                "Hotkey: store only the inventory item under the cursor into a nearby chest that already holds it.");
            SearchKey = file.Bind("4 - Keys", "SearchKey", new KeyboardShortcut(KeyCode.Y),
                "While inventory is open: hover an item and press to ping/blink the nearest chest that contains it (blinks 3 times).");
            RenameKey = file.Bind("4 - Keys", "RenameKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt),
                "Settings (default Alt+E): look at a chest (name / ignore / display / link), a small Storage Display (name/amount), or a multi-input kiln/smelter/grill (pull filter + link). Shift+E (Valheim alt-use) also opens chest settings. Prefix [I] = fully ignore; [H] = hide from dump/store/craft but still show on Storage Displays; [M] = manual fill (no dump/intake into chest, station pull + [lN] links still work).");
            TakeStackKey = file.Bind("4 - Keys", "TakeStackKey", new KeyboardShortcut(KeyCode.Mouse2, KeyCode.LeftControl),
                "Hotkey: fill the hovered inventory stack from nearby chests, only up to max stack / carry weight.");
            FavoriteKey = file.Bind("4 - Keys", "FavoriteKey", new KeyboardShortcut(KeyCode.F),
                "Hotkey: while inventory is open, hover an item and press to favorite / unfavorite. Favorites are skipped by dump, hover-store, and inventory sort (local, not synced).");
            AutoFillKey = file.Bind("4 - Keys", "AutoFillKey", new KeyboardShortcut(KeyCode.B),
                "Look at a kiln, smelter, blast furnace, cooking / stone oven, fermenter, or torch / fire with the inventory closed and press to toggle auto-fill. The station pull filter still applies. Local, not synced.");
            AutoDropKey = file.Bind("4 - Keys", "AutoDropKey", new KeyboardShortcut(KeyCode.N),
                "Look at a kiln/smelter/blast furnace, cooking spit/oven, fermenter, or beehive with the inventory closed and press to toggle auto-store. Finished bars / food / mead / honey go into a nearby chest (ground only if no space). Independent from auto-fill (B). Per piece.");
            ActivityLogKey = file.Bind("4 - Keys", "ActivityLogKey", new KeyboardShortcut(KeyCode.F10),
                "Opens the StoreAndCraft panel: activity log checkbox (anyone, local) and range sliders with Save (host / server admin from the adminlist; everyone in an offline / own world). Do not use F11, Valheim saves a screenshot on F11.");
            // Old default F11 is also Valheim's screenshot key.
            if (ActivityLogKey.Value.MainKey == KeyCode.F11 && !ActivityLogKey.Value.Modifiers.GetEnumerator().MoveNext())
                ActivityLogKey.Value = new KeyboardShortcut(KeyCode.F10);
            ActivityLogVisible = file.Bind("4 - Keys", "ActivityLogVisible", false,
                "Remember whether the activity log is shown. Local only, not synced. Changed by the F10 panel checkbox.");
            DisplayMenuStyle = file.Bind("4 - Keys", "DisplayMenuStyle", "Classic", new ConfigDescription(
                "Look of the Storage Display filter menu (E on a display): Classic = SAC artwork, "
                + "Vanilla = Valheim's own panels, buttons and checkboxes. Local only, not synced. Reopen the menu to see it.",
                new AcceptableValueList<string>("Classic", "Vanilla")));
            DisplayRangeKey = file.Bind("4 - Keys", "DisplayRangeKey", new KeyboardShortcut(KeyCode.R, KeyCode.LeftAlt),
                "Look at a Storage Display and press to set that board's chest-scan range (5–50 m). Per display.");
            BuildGrabKey = file.Bind("4 - Keys", "BuildGrabKey", new KeyboardShortcut(KeyCode.C),
                "Hold this key while confirming a hammer place to grab that piece's materials from nearby chests (nothing is placed). Default C — Shift stays free for no-snap. Local, not synced.");
            FeedTroughEnabled = file.Bind("5 - Feed Trough", "FeedTroughEnabled", true,
                "If enabled, the Feed Trough hammer piece is available. Hungry tames walk to it and eat matching food, like drops on the ground. Synced from server when LockConfig is on.");
            FeedTroughRange = file.Bind("5 - Feed Trough", "FeedTroughRange", 8f, new ConfigDescription(
                "How far (meters) hungry animals notice a Feed Trough and walk to it. Also limited by each animal's own food-search range. Synced from server when LockConfig is on.", range));
            // SAC-CATCHUP
            CatchUpEnabled = file.Bind("3 - Craft", "CatchUpWhileAway", false,
                "Kilns / smelters / blast furnaces / windmills / spinning wheels / eitr refineries with auto-fill (B) AND auto-store (N) catch up the time their zone was unloaded (nobody nearby). When someone comes back, ore + fuel are taken from the chests and the finished items are put into a chest, in packages of one stack. Only time after switching this on counts. Synced from server when LockConfig is on.");
            CatchUpMaxHours = file.Bind("3 - Craft", "CatchUpMaxHours", 8f, new ConfigDescription(
                "Most game time (hours) one station catches up after being unloaded. Synced from server when LockConfig is on.",
                new AcceptableValueRange<float>(0.5f, 48f)));
            CatchUpSince = file.Bind("3 - Craft", "CatchUpSince", 0d,
                "Set automatically: world time (seconds) when CatchUpWhileAway was switched on. Time before this is never caught up. Do not edit.");
            TorchAutoFillDefault = file.Bind("3 - Craft", "TorchAutoFillDefault", false,
                "If on, torches of every kind (standing, wall, green / blue / mist, modded *torch*) have auto-fill ON until someone presses B on them. Torches switched off with B stay off. Off = old behavior (B needed). Synced from server when LockConfig is on.");
        }

        public float StationPullRange()
        {
            return Mathf.Max(CraftRange.Value, AutoFillRange.Value);
        }

        /// <summary>Station → chest reach for auto-fill / auto-store (0 falls back to AutoFillRange).</summary>
        public float AutoFillChestReach()
        {
            float chest = AutoFillChestRange.Value;
            return chest > 0f ? chest : AutoFillRange.Value;
        }

        /// <summary>Farthest player → chest distance auto-fill can need (player → station → chest).</summary>
        public float AutoFillPlayerToChestRange()
        {
            return AutoFillRange.Value + AutoFillChestReach();
        }

        public float MaxGameplayRange()
        {
            return Mathf.Max(
                PlayerDumpRange.Value,
                StoreRange.Value,
                StorageRange.Value,
                DisplayRange.Value,
                CraftRange.Value,
                AutoFillRange.Value,
                FeedTroughRange.Value);
        }

        public void WriteToPackage(ZPackage pkg)
        {
            pkg.Write(LockConfig.Value);
            pkg.Write(ModEnabled.Value);
            pkg.Write(StoreEnabled.Value);
            pkg.Write(AutoIntakeEnabled.Value);
            pkg.Write(CraftEnabled.Value);
            pkg.Write(MustHaveExisting.Value);
            pkg.Write(LeaveOneItem.Value);
            pkg.Write(PlayerDumpRange.Value);
            pkg.Write(StoreRange.Value);
            pkg.Write(StorageRange.Value);
            pkg.Write(DisplayRange.Value);
            pkg.Write(AutoStackEnabled.Value);
            pkg.Write(CraftRange.Value);
            pkg.Write(AutoFillRange.Value);
            pkg.Write(IntakeInterval.Value);
            pkg.Write(MaxTransfersPerTick.Value);
            pkg.Write(FeedTroughEnabled.Value);
            pkg.Write(FeedTroughRange.Value);
            pkg.Write(AutoFillChestRange.Value);
            pkg.Write(CatchUpEnabled.Value);
            pkg.Write(CatchUpMaxHours.Value);
            pkg.Write(CatchUpSince.Value);
            pkg.Write(TorchAutoFillDefault.Value);
        }

        public void ReadFromPackage(ZPackage pkg)
        {
            LockConfig.Value = pkg.ReadBool();
            ModEnabled.Value = pkg.ReadBool();
            StoreEnabled.Value = pkg.ReadBool();
            AutoIntakeEnabled.Value = pkg.ReadBool();
            CraftEnabled.Value = pkg.ReadBool();
            MustHaveExisting.Value = pkg.ReadBool();
            LeaveOneItem.Value = pkg.ReadBool();
            PlayerDumpRange.Value = pkg.ReadSingle();
            StoreRange.Value = pkg.ReadSingle();
            StorageRange.Value = pkg.ReadSingle();
            DisplayRange.Value = pkg.ReadSingle();
            AutoStackEnabled.Value = pkg.ReadBool();
            CraftRange.Value = pkg.ReadSingle();
            AutoFillRange.Value = pkg.ReadSingle();
            IntakeInterval.Value = pkg.ReadSingle();
            MaxTransfersPerTick.Value = pkg.ReadInt();
            FeedTroughEnabled.Value = pkg.ReadBool();
            FeedTroughRange.Value = pkg.ReadSingle();
            AutoFillChestRange.Value = pkg.ReadSingle();
            CatchUpEnabled.Value = pkg.ReadBool();
            CatchUpMaxHours.Value = pkg.ReadSingle();
            CatchUpSince.Value = pkg.ReadDouble();
            TorchAutoFillDefault.Value = pkg.ReadBool();
        }
    }
}
