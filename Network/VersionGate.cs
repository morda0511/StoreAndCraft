using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// If the mod is on the server, every client must have the same major version.
    /// Peers that never handshake are disconnected.
    /// </summary>
    internal class VersionGate : MonoBehaviour
    {
        public const int Major = 1;
        public const float GraceSeconds = 12f;

        private static readonly Dictionary<long, float> Deadline = new Dictionary<long, float>();
        private static readonly HashSet<long> Verified = new HashSet<long>();

        public static void OnPeerReady(long peerId)
        {
            if (!AdminUtil.IsServer() || peerId == 0)
                return;
            if (Verified.Contains(peerId))
                return;
            if (!Deadline.ContainsKey(peerId))
                Deadline[peerId] = Time.time + GraceSeconds;
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

            Verified.Add(peerId);
            Deadline.Remove(peerId);
        }

        public static void SendHello()
        {
            if (AdminUtil.IsServer() || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody,
                ConfigSync.HandshakeName,
                ModConfig.ProtocolVersion,
                Major);
        }

        private void Update()
        {
            if (!AdminUtil.IsServer() || ZNet.instance == null)
                return;

            if (Deadline.Count == 0)
                return;

            var expired = new List<long>();
            foreach (var pair in Deadline)
            {
                if (Verified.Contains(pair.Key))
                    continue;
                if (Time.time >= pair.Value)
                    expired.Add(pair.Key);
            }

            foreach (long peerId in expired)
            {
                Deadline.Remove(peerId);
                Plugin.Log.LogWarning(
                    "StoreAndCraft: kicking peer " + peerId +
                    " (missing StoreAndCraft or handshake timeout).");
                Kick(peerId);
            }
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
