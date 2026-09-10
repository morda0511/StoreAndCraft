namespace StoreAndCraft
{
    internal static class AdminUtil
    {
        public static bool IsServer()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }

        public static bool CanEditSettings()
        {
            var znet = ZNet.instance;
            if (znet == null)
                return true;
            if (znet.IsDedicated())
                return false;
            if (znet.IsServer())
                return true;
            if (ConfigSync.ServerGrantedEdit)
                return true;
            return znet.LocalPlayerIsAdminOrHost();
        }
    }
}
