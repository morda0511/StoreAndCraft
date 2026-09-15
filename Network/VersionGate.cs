using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Handshake over the peer's ZRpc (same socket as vanilla PeerInfo), not only ZRoutedRpc.
    /// Timed-out hellos are logged; only a real protocol mismatch disconnects.
    /// </summary>
    internal class VersionGate : MonoBehaviour
    {
        public const int Major = 1;
        public const string ZHelloName = "KAC_ZHello";
        public const string ZAckName = "KAC_ZAck";

        private static readonly HashSet<long> Verified = new HashSet<long>();
        private static readonly HashSet<long> WarnedMissing = new HashSet<long>();
        private static readonly Dictionary<long, float> FirstSeen = new Dictionary<long, float>();

        public static bool ClientVerified { get; private set; }

        public static void ResetClient()
        {
            ClientVerified = false;
        }

        public static void RegisterOn(ZRpc rpc)
        {
            if (rpc == null)
                return;

            try
            {
                rpc.Register<int, int>(ZHelloName, RPC_ZHello);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug("ZHello register: " + ex.Message);
            }

            try
            {
                rpc.Register<int>(ZAckName, RPC_ZAck);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug("ZAck register: " + ex.Message);
            }
        }

        public static void OnPeerReady(long peerId)
        {
            if (!AdminUtil.IsServer() || peerId == 0)
                return;
            if (!FirstSeen.ContainsKey(peerId))
                FirstSeen[peerId] = Time.time;
        }

        public static void Forget(long peerId)
        {
            Verified.Remove(peerId);
            WarnedMissing.Remove(peerId);
            FirstSeen.Remove(peerId);
        }

        public static void Accept(long peerId, int protocol, int major)
        {
            if (!AdminUtil.IsServer())
                return;

            if (protocol != ModConfig.ProtocolVersion || major != Major)
            {
                Plugin.Log.LogWarning(
                    "StoreAndCraft: rejecting peer " + peerId +
                    " (protocol " + protocol + "/" + ModConfig.ProtocolVersion +
                    ", major " + major + "/" + Major + ").");
                Kick(peerId);
                return;
            }

            if (Verified.Add(peerId))
                Plugin.Log.LogInfo("StoreAndCraft: peer " + peerId + " handshake ok.");
            WarnedMissing.Remove(peerId);
        }

        public static void SendHello(ZRpc rpc = null)
        {
            if (AdminUtil.IsServer())
                return;

            if (rpc == null && ZNet.instance != null)
            {
                ZNetPeer server = ZNet.instance.GetServerPeer();
                if (server != null)
                    rpc = server.m_rpc;
            }

            if (rpc != null)
            {
                try
                {
                    rpc.Invoke(ZHelloName, ModConfig.ProtocolVersion, Major);
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogDebug("ZHello invoke: " + ex.Message);
                }
            }

            if (ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.InvokeRoutedRPC(
                ServerPeerId(),
                ConfigSync.HandshakeName,
                ModConfig.ProtocolVersion,
                Major);
        }

        private void Update()
        {
            if (!AdminUtil.IsServer() || ZNet.instance == null)
                return;

            var gone = new List<long>();
            foreach (long id in FirstSeen.Keys)
            {
                if (ZNet.instance.GetPeer(id) == null)
                    gone.Add(id);
            }
            foreach (long id in gone)
                Forget(id);

            foreach (var pair in FirstSeen)
            {
                if (Verified.Contains(pair.Key) || WarnedMissing.Contains(pair.Key))
                    continue;
                if (Time.time < pair.Value + 30f)
                    continue;

                WarnedMissing.Add(pair.Key);
                Plugin.Log.LogWarning(
                    "StoreAndCraft: peer " + pair.Key +
                    " has not sent a handshake yet (mod missing or still loading). Not kicking.");
            }
        }

        private static void RPC_ZHello(ZRpc rpc, int protocol, int major)
        {
            if (!AdminUtil.IsServer() || rpc == null || ZNet.instance == null)
                return;

            ZNetPeer peer = PeerFor(rpc);
            if (peer == null)
                return;

            Accept(peer.m_uid, protocol, major);
            if (!Verified.Contains(peer.m_uid))
                return;

            try
            {
                rpc.Invoke(ZAckName, 1);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug("ZAck invoke: " + ex.Message);
            }

            ConfigSync.SendToPeer(peer.m_uid);
        }

        private static void RPC_ZAck(ZRpc rpc, int ok)
        {
            if (AdminUtil.IsServer())
                return;

            bool was = ClientVerified;
            ClientVerified = ok != 0;
            if (ClientVerified && !was)
                Plugin.Log.LogInfo("StoreAndCraft: handshake acknowledged.");
        }

        internal static long ServerPeerId()
        {
            if (ZNet.instance != null)
            {
                ZNetPeer server = ZNet.instance.GetServerPeer();
                if (server != null && server.m_uid != 0)
                    return server.m_uid;
            }

            return ZRoutedRpc.Everybody;
        }

        internal static ZNetPeer PeerFor(ZRpc rpc)
        {
            if (rpc == null || ZNet.instance == null)
                return null;

            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer != null && peer.m_rpc == rpc)
                    return peer;
            }

            return null;
        }

        private static void Kick(long peerId)
        {
            if (ZNet.instance == null)
                return;
            ZNetPeer peer = ZNet.instance.GetPeer(peerId);
            if (peer != null)
                ZNet.instance.Disconnect(peer);
        }
    }
}
