using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class Refs
    {
        private static readonly FieldInfo ItemDropNview = AccessTools.Field(typeof(ItemDrop), "m_nview");
        private static readonly FieldInfo ItemDropInstances = AccessTools.Field(typeof(ItemDrop), "s_instances");
        private static readonly FieldInfo ContainerNview = AccessTools.Field(typeof(Container), "m_nview");
        private static readonly FieldInfo ContainerName = AccessTools.Field(typeof(Container), "m_name");
        private static readonly FieldInfo GuiRecipe = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe");
        private static readonly FieldInfo GuiMulti = AccessTools.Field(typeof(InventoryGui), "m_multiCrafting");

        public static ZNetView View(Container container)
        {
            if (container == null)
                return null;
            if (ContainerNview != null)
            {
                var nv = ContainerNview.GetValue(container) as ZNetView;
                if (nv != null)
                    return nv;
            }
            return container.GetComponent<ZNetView>();
        }

        public static string VanillaHoverName(Container container)
        {
            if (container == null || ContainerName == null)
                return null;
            return ContainerName.GetValue(container) as string;
        }

        public static ZNetView View(ItemDrop drop)
        {
            if (drop == null)
                return null;
            if (ItemDropNview != null)
            {
                var nv = ItemDropNview.GetValue(drop) as ZNetView;
                if (nv != null)
                    return nv;
            }
            return drop.GetComponent<ZNetView>();
        }

        public static List<ItemDrop> Drops()
        {
            if (ItemDropInstances == null)
                return null;
            return ItemDropInstances.GetValue(null) as List<ItemDrop>;
        }

        public static Recipe CraftRecipe(InventoryGui gui)
        {
            return gui != null && GuiRecipe != null ? GuiRecipe.GetValue(gui) as Recipe : null;
        }

        public static bool MultiCrafting(InventoryGui gui)
        {
            if (gui == null || GuiMulti == null)
                return false;
            object v = GuiMulti.GetValue(gui);
            return v is bool && (bool)v;
        }
    }
}
