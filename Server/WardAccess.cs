using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraftServer
{
    internal static class WardAccess
    {
        private static readonly FieldInfo AllAreas =
            AccessTools.Field(typeof(PrivateArea), "m_allAreas");
        private static readonly MethodInfo IsEnabled =
            AccessTools.Method(typeof(PrivateArea), "IsEnabled");
        private static readonly MethodInfo IsInside =
            AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) });
        private static readonly MethodInfo IsPermitted =
            AccessTools.Method(typeof(PrivateArea), "IsPermitted", new[] { typeof(long) });

        public static bool PlayersMayUse(Container container)
        {
            if (!ContainerFilter.IsUsable(container))
                return false;

            List<PrivateArea> covering = Covering(container.transform.position);
            if (covering.Count == 0)
                return true;

            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count == 0)
                return false;

            foreach (Player player in players)
            {
                if (player == null || player.IsDead())
                    continue;
                if (HasAccess(covering, player.GetPlayerID()))
                    return true;
            }

            return false;
        }

        private static bool HasAccess(List<PrivateArea> covering, long playerId)
        {
            foreach (PrivateArea area in covering)
            {
                if (area == null)
                    return false;

                Piece piece = area.GetComponent<Piece>();
                if (piece != null && piece.GetCreator() == playerId)
                    continue;

                if (IsPermitted != null && (bool)IsPermitted.Invoke(area, new object[] { playerId }))
                    continue;

                return false;
            }

            return true;
        }

        private static List<PrivateArea> Covering(Vector3 point)
        {
            var result = new List<PrivateArea>();
            IEnumerable areas = AllAreas != null ? AllAreas.GetValue(null) as IEnumerable : null;
            if (areas == null)
                return result;

            foreach (object raw in areas)
            {
                PrivateArea area = raw as PrivateArea;
                if (area == null)
                    continue;
                if (IsEnabled != null && !(bool)IsEnabled.Invoke(area, null))
                    continue;
                if (IsInside != null && !(bool)IsInside.Invoke(area, new object[] { point, 0f }))
                    continue;
                result.Add(area);
            }

            return result;
        }
    }
}
