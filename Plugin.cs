using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.morda.storeandcraft";
        public const string ModName = "StoreAndCraft";
        public const string ModVersion = "1.3.4-test";
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
            try
            {
                Config.Save();
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning("Config save: " + ex.Message);
            }

            Config.SettingChanged += OnSettingChanged;
            ConfigWatch.Start();
            Favorites.Load();

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            EpicLootBridge.TryRegister();

            Logger.LogInfo(ModName + " v" + ModVersion + " by " + ModAuthor + " loaded.");
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (ConfigSync.IsApplyingRemoteConfig)
                return;
            if (!AdminUtil.IsServer())
                return;
            ConfigSync.BroadcastConfig();
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
            PendingChestDebit.Tick();
            // Listen host: intake around every player, not only the host.
            // Clients still run local intake so they can RPC drops they own.
            if (ZNet.instance != null && ZNet.instance.IsServer())
                AutoIntake.TickDedicated();
            else
                AutoIntake.Tick();
            AutoStack.Tick();
            SearchPing.Tick();
            DisplayTypeMenu.Tick();
            DisplayRangeMenu.Tick();
            StationFilterMenu.Tick();
            StationAutoFill.Tick();
        }

        private void LateUpdate()
        {
            // Never leave chest-counting enabled across frames (inventory FPS collapse).
            StationHover.ResetFrame();

            if (Player.m_localPlayer == null)
                return;

            Hotkeys.Tick();
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            EpicLootBridge.TryUnregister();
            _harmony?.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }
    }
}
