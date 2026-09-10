using UnityEngine;

namespace StoreAndCraft
{
    internal static class RequirementBridge
    {
        public static int CountNearby(Player player, string sharedName)
        {
            if (player == null || Plugin.Settings == null || string.IsNullOrEmpty(sharedName))
                return 0;

            return NearbyIndex.CountItem(
                player.transform.position,
                0f,
                sharedName,
                Plugin.Settings.LeaveOneItem.Value);
        }

        public static bool SenderInRange(long sender, Vector3 target, float range)
        {
            if (ZNet.instance == null)
                return true;

            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return true;

            float slack = 6f;
            return Vector3.Distance(peer.m_refPos, target) <= range + slack;
        }
    }
}
