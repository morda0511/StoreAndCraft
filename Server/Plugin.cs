using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace StoreAndCraftServer
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInIncompatibility("com.morda.storeandcraft")]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.morda.storeandcraft.server";
        public const string ModName = "StoreAndCraftServer";
        public const string ModVersion = "1.0.0";
        public const string ModAuthor = "Morda";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal static ModConfig Settings { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Settings = new ModConfig(Config);
            RulesFile.LoadOrCreate();
            Logger.LogInfo(ModName + " v" + ModVersion + " loaded (server-side auto-store).");
        }

        private void Update()
        {
            AutoStore.Tick();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
