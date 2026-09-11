using HarmonyLib;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerAwakePatch
    {
        private static void Postfix(Container __instance)
        {
            TransferService.RegisterOn(__instance);
            if (__instance.GetComponent<ChestRenameReceiver>() == null)
                __instance.gameObject.AddComponent<ChestRenameReceiver>();
        }
    }

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    internal static class NewConnectionPatch
    {
        private static void Postfix(ZNetPeer peer)
        {
            if (peer == null)
                return;
            VersionGate.RegisterOn(peer.m_rpc);
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class GameStartPatch
    {
        private static void Postfix(Game __instance)
        {
            VersionGate.ResetClient();
            ConfigSync.Register();
            TransferService.RegisterGrant();
            VersionGate.SendHello();

            if (__instance != null && __instance.GetComponent<ConfigSyncRetry>() == null)
                __instance.gameObject.AddComponent<ConfigSyncRetry>();
            if (__instance != null && __instance.GetComponent<VersionGate>() == null)
                __instance.gameObject.AddComponent<VersionGate>();
        }
    }

    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    internal static class PeerInfoPatch
    {
        private static void Postfix(ZRpc rpc)
        {
            if (rpc == null || ZNet.instance == null)
                return;

            if (ZNet.instance.IsServer())
            {
                ZNetPeer peer = VersionGate.PeerFor(rpc);
                if (peer == null)
                    return;
                VersionGate.OnPeerReady(peer.m_uid);
                ConfigSync.SendToPeer(peer.m_uid);
                return;
            }

            VersionGate.SendHello(rpc);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]
    internal static class DisconnectPatch
    {
        private static void Postfix(ZNetPeer peer)
        {
            if (peer != null)
                VersionGate.Forget(peer.m_uid);
        }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class PlayerSpawnedPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == null || !__instance.IsOwner())
                return;
            if (AdminUtil.IsServer())
                return;
            VersionGate.SendHello();
            if (!ConfigSync.HasReceivedConfig)
                ConfigSync.RequestConfigFromServer();
        }
    }

    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class TerminalInitPatch
    {
        private static bool _added;

        private static void Postfix()
        {
            if (_added)
                return;
            _added = true;
            new Terminal.ConsoleCommand(
                "storesearch",
                "Find an item in nearby containers",
                SearchPing.OnCommand,
                false, false, false, false, false, false, null, false, false, false);
        }
    }
}
