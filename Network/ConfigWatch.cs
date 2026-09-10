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

        public static void Start()
        {
            if (_watcher != null)
                return;

            try
            {
                _watcher = new FileSystemWatcher(Paths.ConfigPath);
                _watcher.Filter = "StoreAndCraft.*";
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.EnableRaisingEvents = true;
                Plugin.Log.LogInfo("StoreAndCraft config watcher on " + Paths.ConfigPath);
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
                if (Plugin.Instance != null)
                    Plugin.Instance.Config.Reload();

                RulesFile.LoadOrCreate();

                if (AdminUtil.IsServer())
                    ConfigSync.BroadcastConfig();

                Plugin.Log.LogInfo("StoreAndCraft config/rules reloaded from disk.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("StoreAndCraft reload failed: " + ex.Message);
            }
        }
    }
}
