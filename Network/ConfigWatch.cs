using System;
using System.IO;
using BepInEx;

namespace StoreAndCraft
{
    internal static class ConfigWatch
    {
        private static FileSystemWatcher _watcher;
        private static float _reloadAt;
        private static bool _pending;
        private static string _cfgFileName;

        public static void Start()
        {
            if (_watcher != null)
                return;

            try
            {
                _cfgFileName = Plugin.Instance != null
                    ? Path.GetFileName(Plugin.Instance.Config.ConfigFilePath)
                    : "com.morda.storeandcraft.cfg";

                _watcher = new FileSystemWatcher(Paths.ConfigPath);
                _watcher.Filter = _cfgFileName;
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.EnableRaisingEvents = true;
                Plugin.Log.LogInfo("StoreAndCraft config watcher: " + Path.Combine(Paths.ConfigPath, _cfgFileName));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("StoreAndCraft config watcher failed: " + ex.Message);
            }
        }

        public static void Tick()
        {
            if (!_pending || UnityEngine.Time.unscaledTime < _reloadAt)
                return;

            _pending = false;
            Reload();
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            _pending = true;
            _reloadAt = UnityEngine.Time.unscaledTime + 0.4f;
        }

        private static void Reload()
        {
            try
            {
                // Dedicated / listen-server: disk is source of truth → reload + push to clients.
                if (AdminUtil.IsServer())
                {
                    if (Plugin.Instance != null)
                        Plugin.Instance.Config.Reload();
                    ConfigSync.BroadcastConfig();
                    Plugin.Log.LogInfo("StoreAndCraft server config reloaded and synced to clients."
                        + " Dump=" + Plugin.Settings.PlayerDumpRange.Value
                        + " Store=" + Plugin.Settings.StoreRange.Value
                        + " Storage=" + Plugin.Settings.StorageRange.Value
                        + " Craft=" + Plugin.Settings.CraftRange.Value);
                    return;
                }

                // Locked clients: ignore local cfg edits, pull server values again.
                if (Plugin.Settings != null && Plugin.Settings.LockConfig.Value && ConfigSync.HasReceivedConfig)
                {
                    ConfigSync.RequestConfigFromServer();
                    Plugin.Log.LogInfo("StoreAndCraft LockConfig: ignored local cfg change, re-requested server config.");
                    return;
                }

                if (Plugin.Instance != null)
                    Plugin.Instance.Config.Reload();
                Plugin.Log.LogInfo("StoreAndCraft local config reloaded.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("StoreAndCraft reload failed: " + ex.Message);
            }
        }
    }
}
