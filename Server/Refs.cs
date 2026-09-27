using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraftServer
{
    internal static class Refs
    {
        private static readonly FieldInfo ItemDropNview = AccessTools.Field(typeof(ItemDrop), "m_nview");
        private static readonly FieldInfo ItemDropInstances = AccessTools.Field(typeof(ItemDrop), "s_instances");
        private static readonly FieldInfo ContainerNview = AccessTools.Field(typeof(Container), "m_nview");

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
    }
}
