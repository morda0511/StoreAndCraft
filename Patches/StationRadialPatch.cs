using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Valheim.UI;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(ItemGroupConfig), "InitRadialConfig")]
    internal static class StationRadialPatch
    {
        private static readonly FieldInfo CustomList = AccessTools.Field(typeof(ItemGroupConfig), "m_customItemList");
        private static readonly FieldInfo StoredList = AccessTools.Field(typeof(ItemGroupConfig), "m_storedList");

        private static void Prefix(ItemGroupConfig __instance)
        {
            try
            {
                if (!StationFeed.Ready() || CustomList == null || StoredList == null)
                    return;

                var custom = CustomList.GetValue(__instance) as List<string>;
                if (custom == null || custom.Count == 0)
                    return;

                Player player = Player.m_localPlayer;
                Inventory inv = player != null ? player.GetInventory() : null;
                if (inv == null)
                    return;

                NearbyIndex.Tick();
                var combined = new List<ItemDrop.ItemData>();
                var inPlayer = new HashSet<string>();

                foreach (ItemDrop.ItemData item in inv.GetAllItemsInGridOrder())
                {
                    if (item?.m_shared == null || !custom.Contains(item.m_shared.m_name))
                        continue;
                    combined.Add(item);
                    inPlayer.Add(item.m_shared.m_name);
                }

                int extras = 0;
                foreach (string name in custom)
                {
                    if (string.IsNullOrEmpty(name) || inPlayer.Contains(name))
                        continue;
                    if (RequirementBridge.CountNearby(player, name) <= 0)
                        continue;
                    ItemDrop.ItemData sample = StationFeed.SampleNearby(player, name);
                    if (sample == null)
                        continue;
                    combined.Add(sample);
                    extras++;
                }

                if (extras > 0 && combined.Count > 0)
                    StoredList.SetValue(__instance, combined);
            }
            catch
            {
            }
        }
    }
}
