using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class ContainerFilter
    {
        private static readonly MethodInfo LoadInventory = AccessTools.Method(typeof(Container), "Load");
        private static readonly MethodInfo SaveInventoryMethod = AccessTools.Method(typeof(Container), "Save");
        private static readonly FieldInfo LastRevision = AccessTools.Field(typeof(Container), "m_lastRevision");

        public static bool IsUsable(Container container)
        {
            if (container == null)
                return false;

            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid())
                return false;

            if (container.GetInventory() == null)
                return false;

            Piece piece = container.GetComponent<Piece>();
            if (piece == null)
                return false;

            return true;
        }

        public static string PiecePrefab(Container container)
        {
            Piece piece = container != null ? container.GetComponent<Piece>() : null;
            if (piece == null)
                return null;
            return ItemIds.StripClone(piece.gameObject.name);
        }

        public static bool PlayerMayUse(Container container, Vector3 from)
        {
            if (!IsUsable(container))
                return false;

            if (ChestNames.IsIgnored(container))
                return false;

            if (!PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
                return false;

            return true;
        }

        public static void RefreshInventory(Container container)
        {
            if (container == null || LoadInventory == null)
                return;

            try
            {
                if (LastRevision != null)
                    LastRevision.SetValue(container, (uint)0);
                LoadInventory.Invoke(container, null);
            }
            catch
            {
            }
        }

        public static void SaveInventory(Container container)
        {
            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid() || !nv.IsOwner() || SaveInventoryMethod == null)
                return;
            if (container.GetInventory() == null)
                return;

            try
            {
                SaveInventoryMethod.Invoke(container, null);
            }
            catch
            {
            }
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }
    }
}
