using UnityEngine;

namespace StoreAndCraft
{
    internal static class ContainerFilter
    {
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

            if (!PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
                return false;

            return true;
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }
    }
}
