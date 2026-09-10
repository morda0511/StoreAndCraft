using UnityEngine;

namespace StoreAndCraft
{
    internal class ConfigSyncRetry : MonoBehaviour
    {
        private float _nextRequest;
        private int _attempts;

        private void Update()
        {
            if (AdminUtil.IsServer() || ConfigSync.HasReceivedConfig)
            {
                enabled = false;
                return;
            }

            if (!ConfigSync.Registered || ZRoutedRpc.instance == null || ZNet.instance == null)
                return;

            if (Time.time < _nextRequest)
                return;

            _attempts++;
            _nextRequest = Time.time + 2f;
            ConfigSync.RequestConfigFromServer();
            VersionGate.SendHello();

            if (_attempts == 1 || _attempts % 5 == 0)
                Plugin.Log.LogInfo("StoreAndCraft: waiting for server config (attempt " + _attempts + ")...");
        }
    }
}
