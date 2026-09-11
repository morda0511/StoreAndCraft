namespace StoreAndCraft
{
    internal static class ConfigSync
    {
        public const string RpcSyncName = "KAC_SyncConfig";
        public const string RpcRequestName = "KAC_RequestConfig";
        public const string HandshakeName = "KAC_Hello";

        public static bool IsApplyingRemoteConfig { get; private set; }
        public static bool Registered { get; private set; }
        public static bool HasReceivedConfig { get; private set; }
        public static bool ServerGrantedEdit { get; private set; }

        public static void Register()
        {
            if (ZRoutedRpc.instance == null)
                return;

            if (!Registered)
            {
                ZRoutedRpc.instance.Register<ZPackage>(RpcSyncName, RPC_ReceiveConfig);
                ZRoutedRpc.instance.Register(RpcRequestName, RPC_RequestConfig);
                ZRoutedRpc.instance.Register<int, int>(HandshakeName, RPC_Hello);
                Registered = true;
                Plugin.Log.LogInfo("StoreAndCraft RPCs registered.");
            }

            if (AdminUtil.IsServer())
            {
                HasReceivedConfig = true;
                ServerGrantedEdit = true;
                BroadcastConfig();
            }
            else if (!HasReceivedConfig)
            {
                RequestConfigFromServer();
            }
        }

        public static void BroadcastConfig()
        {
            if (!AdminUtil.IsServer() || ZNet.instance == null || Plugin.Settings == null)
                return;

            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null || !peer.IsReady())
                    continue;
                SendToPeer(peer.m_uid);
            }
        }

        public static void SendToPeer(long peerId)
        {
            if (!AdminUtil.IsServer() || ZRoutedRpc.instance == null || Plugin.Settings == null)
                return;

            var peer = ZNet.instance != null ? ZNet.instance.GetPeer(peerId) : null;
            bool canEdit = PeerIsAdmin(peer);
            var pkg = new ZPackage();
            pkg.Write(ModConfigProtocol());
            pkg.Write(canEdit);
            Plugin.Settings.WriteToPackage(pkg);
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, RpcSyncName, pkg);
        }

        public static void RequestConfigFromServer()
        {
            if (AdminUtil.IsServer() || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.InvokeRoutedRPC(VersionGate.ServerPeerId(), RpcRequestName);
        }

        private static int ModConfigProtocol()
        {
            return ModConfig.ProtocolVersion;
        }

        public static bool PeerIsAdmin(ZNetPeer peer)
        {
            if (peer?.m_rpc == null || ZNet.instance == null)
                return false;
            try
            {
                var socket = peer.m_rpc.GetSocket();
                if (socket == null)
                    return false;
                string hostName = socket.GetHostName();
                return !string.IsNullOrEmpty(hostName) && ZNet.instance.IsAdmin(hostName);
            }
            catch
            {
                return false;
            }
        }

        private static void RPC_ReceiveConfig(long sender, ZPackage pkg)
        {
            if (AdminUtil.IsServer() || pkg == null || Plugin.Settings == null)
                return;

            int version = pkg.ReadInt();
            if (version != ModConfig.ProtocolVersion)
            {
                Plugin.Log.LogWarning("StoreAndCraft config protocol mismatch: " + version);
                return;
            }

            ServerGrantedEdit = pkg.ReadBool();
            IsApplyingRemoteConfig = true;
            try
            {
                Plugin.Settings.ReadFromPackage(pkg);
                HasReceivedConfig = true;
            }
            finally
            {
                IsApplyingRemoteConfig = false;
            }
        }

        private static void RPC_RequestConfig(long sender)
        {
            if (!AdminUtil.IsServer())
                return;
            SendToPeer(sender);
        }

        private static void RPC_Hello(long sender, int protocol, int major)
        {
            if (!AdminUtil.IsServer())
                return;
            VersionGate.Accept(sender, protocol, major);
            SendToPeer(sender);
        }
    }
}
