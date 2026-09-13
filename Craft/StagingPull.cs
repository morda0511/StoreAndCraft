using UnityEngine;

namespace StoreAndCraft
{
    internal static class StagingPull
    {
        public static bool Active
        {
            get
            {
                return Plugin.Settings != null
                    && Plugin.Settings.ModEnabled.Value
                    && Plugin.Settings.CraftEnabled.Value;
            }
        }

        /// <summary>
        /// Pay craft/build costs from nearby chests without moving stacks into the backpack.
        /// Staging into inventory filled free slots and made the craft result fail to add
        /// (classic "mats appear, craft fizzles" — arrows still worked onto an existing stack).
        /// </summary>
        public static void ConsumeRequirements(
            Player player,
            Piece.Requirement[] requirements,
            int qualityLevel,
            int itemQuality,
            int multiplier)
        {
            if (player == null || requirements == null || !Active)
                return;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return;

            NearbyIndex.Tick();
            bool leaveOne = Plugin.Settings.LeaveOneItem.Value;
            float cfgCraft = Plugin.Settings.CraftRange.Value;
            Vector3 origin = player.transform.position;
            CraftingStation station = player.GetCurrentCraftingStation();

            InventoryCountPatches.Skip++;
            try
            {
                foreach (Piece.Requirement req in requirements)
                {
                    if (req == null || req.m_resItem == null)
                        continue;

                    // Mirror Player.ConsumeResources: skip resources that vanilla will not consume.
                    if (station != null && station.m_upgrader != req.m_upgraderResource)
                        continue;
                    if (station == null && req.m_upgraderResource)
                        continue;

                    int need = req.GetAmount(qualityLevel) * Mathf.Max(1, multiplier);
                    if (need <= 0)
                        continue;

                    string shared = req.m_resItem.m_itemData != null
                        ? req.m_resItem.m_itemData.m_shared.m_name
                        : null;
                    if (string.IsNullOrEmpty(shared))
                        continue;

                    int have = inv.CountItems(shared, itemQuality, true);
                    int deficit = need - have;
                    if (deficit <= 0)
                        continue;

                    foreach (Container chest in NearbyIndex.Current)
                    {
                        if (deficit <= 0)
                            break;

                        if (chest == null || ChestNames.IsIgnored(chest))
                            continue;

                        if (ContainerFilter.Distance(origin, chest.transform.position) > cfgCraft)
                            continue;

                        NearbyIndex.EnsureInventory(chest);
                        Inventory chestInv = chest.GetInventory();
                        if (chestInv == null)
                            continue;

                        int avail = chestInv.CountItems(shared, itemQuality, true);
                        avail -= PendingChestDebit.Of(chest, shared);
                        if (leaveOne && avail > 0)
                            avail -= 1;
                        if (avail <= 0)
                            continue;

                        int ask = Mathf.Min(deficit, avail);
                        int took = TransferService.Consume(chest, shared, ask, leaveOne, itemQuality);
                        deficit -= took;
                    }
                }
            }
            finally
            {
                InventoryCountPatches.Skip--;
            }
        }
    }
}
