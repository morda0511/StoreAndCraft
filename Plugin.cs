using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.morda.storeandcraft";
        public const string ModName = "StoreAndCraft";
        public const string ModVersion = "1.1.2";
        public const string ModAuthor = "Morda";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal static ModConfig Settings { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Settings = new ModConfig(Config);
            RulesFile.LoadOrCreate();
            ConfigWatch.Start();

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo(ModName + " v" + ModVersion + " by " + ModAuthor + " loaded.");
        }

        private void Update()
        {
            ConfigWatch.Tick();
            TransferService.Tick();

            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                AutoIntake.TickDedicated();
                return;
            }

            if (Player.m_localPlayer == null)
                return;

            NearbyIndex.Tick();
            AutoIntake.Tick();
            AutoStack.Tick();
            StagingPull.Tick();
            DisplayTypeMenu.Tick();
        }

        private void LateUpdate()
        {
            if (Player.m_localPlayer == null)
                return;

            Hotkeys.Tick();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }
    }
}
