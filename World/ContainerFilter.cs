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
        // Valheim serializes chest contents under ZDOVars.s_items.
        private static readonly FieldInfo ItemsHash = AccessTools.Field(typeof(ZDOVars), "s_items");
        private static float _nextEmptyWriteLog;

        public static bool IsUsable(Container container)
        {
            if (container == null)
                return false;

            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid())
                return false;

            if (container.GetInventory() == null)
                return false;

            // Normal chests / boxes
            if (container.GetComponent<Piece>() != null)
                return true;

            // Carts (class name Vagon) & ships — mining runs / hauling
            if (container.GetComponentInParent<Vagon>() != null)
                return true;
            if (container.GetComponentInParent<Ship>() != null)
                return true;

            return false;
        }

        public static string PiecePrefab(Container container)
        {
            if (container == null)
                return null;

            Piece piece = container.GetComponent<Piece>();
            if (piece == null)
                piece = container.GetComponentInParent<Piece>();
            if (piece != null)
                return ItemIds.StripClone(piece.gameObject.name);

            Vagon cart = container.GetComponentInParent<Vagon>();
            if (cart != null)
                return ItemIds.StripClone(cart.gameObject.name);

            Ship ship = container.GetComponentInParent<Ship>();
            if (ship != null)
                return ItemIds.StripClone(ship.gameObject.name);

            return ItemIds.StripClone(container.gameObject.name);
        }

        public static bool PlayerMayUse(Container container, Vector3 from)
        {
            if (!IsUsable(container))
                return false;

            if (ChestNames.IsFullyIgnored(container))
                return false;

            if (!PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
                return false;

            return true;
        }

        /// <summary>
        /// Dump / auto-store targets only. World chests (crypts, house spawns, …) have
        /// no creator and must not steal stacks from the player's base sorting.
        /// Carts and ships are always allowed.
        /// </summary>
        public static bool IsPlayerBuiltStorage(Container container)
        {
            if (container == null)
                return false;

            // Feed troughs are animal food only — not dump / auto-store / craft targets.
            if (FeedTrough.IsTrough(container))
                return false;

            if (container.GetComponentInParent<Vagon>() != null)
                return true;
            if (container.GetComponentInParent<Ship>() != null)
                return true;

            Piece piece = container.GetComponent<Piece>();
            if (piece == null)
                piece = container.GetComponentInParent<Piece>();
            if (piece == null)
                return false;

            return piece.IsPlacedByPlayer();
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

        /// <summary>
        /// Force-Load before mutating a chest. Returns false when the local inventory looks
        /// empty while the ZDO still has an item payload — writing in that state can wipe the chest.
        /// </summary>
        public static bool TryReadyForWrite(Container container)
        {
            if (container == null)
                return false;

            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid() || !nv.IsOwner())
                return false;

            RefreshInventory(container);
            Inventory inv = container.GetInventory();
            if (inv == null)
                return false;

            if (inv.NrOfItems() <= 0 && ZdoHasItemPayload(nv.GetZDO()))
            {
                if (Plugin.Log != null && Time.unscaledTime >= _nextEmptyWriteLog)
                {
                    _nextEmptyWriteLog = Time.unscaledTime + 10f;
                    Plugin.Log.LogWarning(
                        "Skipped chest write: local inventory empty but ZDO still has items (load race).");
                }
                return false;
            }

            return true;
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

        /// <summary>
        /// True when the ZDO still carries a serialized inventory larger than an empty package.
        /// Used to detect stale empty local views after ClaimOwnership / early Load.
        /// </summary>
        public static bool ZdoHasItemPayload(ZDO zdo)
        {
            if (zdo == null || ItemsHash == null)
                return false;

            try
            {
                object key = ItemsHash.GetValue(null);
                if (key == null)
                    return false;

                byte[] data = null;
                if (key is int hash)
                    data = zdo.GetByteArray(hash);
                else if (key is string name)
                    data = zdo.GetByteArray(name);

                // Empty inventories serialize to a tiny header; real contents are larger.
                return data != null && data.Length > 8;
            }
            catch
            {
                return false;
            }
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }

        public static float SqrDistance(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude;
        }
    }
}
