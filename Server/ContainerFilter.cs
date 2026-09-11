using UnityEngine;

namespace StoreAndCraftServer
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

            return container.GetComponent<Piece>() != null;
        }

        public static string PiecePrefab(Container container)
        {
            Piece piece = container != null ? container.GetComponent<Piece>() : null;
            if (piece == null)
                return null;
            return ItemIds.StripClone(piece.gameObject.name);
        }
    }
}
