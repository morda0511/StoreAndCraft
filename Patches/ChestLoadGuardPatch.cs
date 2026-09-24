using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StoreAndCraft
{
    /// <summary>
    /// If Container.Load applies an empty package over a full ZDO, vanilla then Save()s
    /// and the chest is gone. Block that empty save until a real load succeeds.
    /// </summary>
    internal static class ChestLoadGuard
    {
        private static readonly HashSet<int> BlockEmptySave = new HashSet<int>();

        public static void NoteLoadResult(Container container, bool localEmpty, bool zdoHasItems)
        {
            if (container == null)
                return;
            int id = container.GetInstanceID();
            if (localEmpty && zdoHasItems)
                BlockEmptySave.Add(id);
            else
                BlockEmptySave.Remove(id);
        }

        public static bool ShouldBlockEmptySave(Container container)
        {
            return container != null && BlockEmptySave.Contains(container.GetInstanceID());
        }
    }

    [HarmonyPatch]
    internal static class InventoryLoadWipePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo one = AccessTools.Method(typeof(Inventory), "Load", new[] { typeof(ZPackage) });
            if (one != null)
                yield return one;
            MethodInfo two = AccessTools.Method(typeof(Inventory), "Load", new[] { typeof(ZPackage), typeof(bool) });
            if (two != null)
                yield return two;
        }

        /// <summary>
        /// Do not clear a populated inventory when the incoming package is empty.
        /// That is the Storage Display / EnsureInventory chest-wipe race.
        /// Empty inventories can serialize to more than 8 bytes, so peek the item count.
        /// </summary>
        private static bool Prefix(Inventory __instance, ZPackage pkg)
        {
            if (__instance == null || pkg == null)
                return true;
            if (__instance.NrOfItems() <= 0)
                return true;

            int pos = pkg.GetPos();
            try
            {
                int leftover = pkg.Size() - pos;
                if (leftover <= 0)
                    return false;

                // Inventory.Load format: int itemCount, then items…
                int count = pkg.ReadInt();
                if (count <= 0)
                    return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    pkg.SetPos(pos);
                }
                catch
                {
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), "Save")]
    internal static class ContainerSaveWipePatch
    {
        private static bool Prefix(Container __instance)
        {
            if (__instance == null)
                return true;
            if (ContainerFilter.AllowEmptySave > 0)
                return true;

            Inventory inv = __instance.GetInventory();
            if (inv != null && inv.NrOfItems() > 0)
            {
                ChestLoadGuard.NoteLoadResult(__instance, false, false);
                return true;
            }

            // Open chest: player actually emptied it — allow.
            if (__instance.IsInUse())
                return true;

            // Explicit guard from a failed empty Load over a full ZDO.
            if (ChestLoadGuard.ShouldBlockEmptySave(__instance))
                return false;

            // Closed + empty local after travel / failed load. Never persist that.
            return false;
        }
    }
}
