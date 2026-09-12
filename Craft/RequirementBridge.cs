using UnityEngine;

namespace StoreAndCraft
{
    internal static class RequirementBridge
    {
        /// <summary>
        /// Count craftable mats in nearby chests for UI / HaveRequirements.
        /// Never applies LeaveOneItem here — that only matters when withdrawing.
        /// </summary>
        public static int CountNearby(Player player, string sharedName, int quality = -1)
        {
            if (player == null || Plugin.Settings == null || string.IsNullOrEmpty(sharedName))
                return 0;

            NearbyIndex.Tick();
            return NearbyIndex.CountItem(
                player.transform.position,
                0f,
                sharedName,
                leaveOne: false,
                quality: quality);
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
