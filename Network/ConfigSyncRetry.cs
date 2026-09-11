using UnityEngine;

namespace StoreAndCraft
{
    internal class ConfigSyncRetry : MonoBehaviour
    {
        private float _nextRequest;
        private int _attempts;

        private void Update()
        {
            if (AdminUtil.IsServer())
            {
                enabled = false;
                return;
            }

            if (VersionGate.ClientVerified && ConfigSync.HasReceivedConfig)
            {
                enabled = false;
                return;
            }

            if (ZNet.instance == null)
                return;

            if (Time.time < _nextRequest)
                return;

            _attempts++;
            _nextRequest = Time.time + 2f;
            VersionGate.SendHello();

            if (ConfigSync.Registered && !ConfigSync.HasReceivedConfig)
                ConfigSync.RequestConfigFromServer();

            if (_attempts == 1 || _attempts % 5 == 0)
            {
                Plugin.Log.LogInfo(
                    "StoreAndCraft: waiting for server" +
                    (VersionGate.ClientVerified ? "" : " handshake") +
                    (ConfigSync.HasReceivedConfig ? "" : " config") +
                    " (attempt " + _attempts + ")...");
            }
        }
    }
}
