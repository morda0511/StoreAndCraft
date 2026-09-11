using BepInEx.Configuration;

namespace StoreAndCraftServer
{
    internal sealed class ModConfig
    {
        public ConfigEntry<bool> ModEnabled { get; }
        public ConfigEntry<bool> MustHaveExisting { get; }
        public ConfigEntry<float> StoreRange { get; }
        public ConfigEntry<float> IntakeInterval { get; }
        public ConfigEntry<int> MaxTransfersPerTick { get; }

        public ModConfig(ConfigFile file)
        {
            ModEnabled = file.Bind("1 - General", "ModEnabled", true,
                "Turns auto-store on or off.");
            MustHaveExisting = file.Bind("2 - Store", "MustHaveExisting", true,
                "A chest only takes an item type it already holds.");
            StoreRange = file.Bind("2 - Store", "StoreRange", 10f,
                "Fallback range in meters from a ground item to a chest. YAML storeRange overrides this.");
            IntakeInterval = file.Bind("2 - Store", "IntakeInterval", 5f,
                "Seconds between scans for ground items.");
            MaxTransfersPerTick = file.Bind("1 - General", "MaxTransfersPerTick", 8,
                "Maximum item moves per frame.");
        }
    }
}
