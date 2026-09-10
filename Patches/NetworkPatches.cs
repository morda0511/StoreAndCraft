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

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class GameStartPatch
    {
        private static void Postfix(Game __instance)
        {
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
            if (!AdminUtil.IsServer() || rpc == null || ZNet.instance == null)
                return;

            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null || peer.m_rpc != rpc)
                    continue;
                VersionGate.OnPeerReady(peer.m_uid);
                ConfigSync.SendToPeer(peer.m_uid);
                break;
            }
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
