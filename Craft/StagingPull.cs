using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class StagingPull
    {
        public static bool PullingEnabled = true;

        private static float _retryCraftAt;
        private static bool _retryCraft;

        public static bool Active
        {
            get
            {
                return Plugin.Settings != null
                    && Plugin.Settings.ModEnabled.Value
                    && Plugin.Settings.CraftEnabled.Value
                    && PullingEnabled;
            }
        }

        public static void Tick()
        {
            if (!_retryCraft || Time.time < _retryCraftAt)
                return;

            _retryCraft = false;
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (gui == null || player == null || Refs.CraftRecipe(gui) == null)
                return;

            AccessToolsDoCraft(gui, player);
        }

        public static bool EnsureRecipe(Player player, Recipe recipe, int amount, out bool waiting)
        {
            waiting = false;
            if (!Active || player == null || recipe == null || recipe.m_resources == null)
                return true;

            bool allLocal = PullRequirements(
                player,
                recipe.m_resources,
                recipe.m_item != null ? recipe.m_item.m_itemData.m_quality : 1,
                -1,
                amount,
                out waiting);
            return allLocal;
        }

        public static bool EnsurePiece(Player player, Piece piece, out bool waiting)
        {
            waiting = false;
            if (!Active || player == null || piece == null || piece.m_resources == null)
                return true;

            return PullRequirements(player, piece.m_resources, 1, -1, 1, out waiting);
        }

        public static bool PullRequirements(
            Player player,
            Piece.Requirement[] requirements,
            int qualityLevel,
            int itemQuality,
            int multiplier,
            out bool waiting)
        {
            waiting = false;
            if (player == null || requirements == null)
                return true;

            Inventory inv = player.GetInventory();
            if (inv == null)
                return true;

            NearbyIndex.Tick();
            bool leaveOne = Plugin.Settings.LeaveOneItem.Value;
            float cfgCraft = Plugin.Settings.CraftRange.Value;
            Vector3 origin = player.transform.position;
            bool allOwned = true;

            foreach (Piece.Requirement req in requirements)
            {
                if (req == null || req.m_resItem == null)
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

                    if (chest == null)
                        continue;

                    string pieceName = ContainerFilter.PiecePrefab(chest);
                    if (!RulesFile.AllowsCraft(pieceName, shared))
                        continue;
                    float chestRange = RulesFile.CraftRange(pieceName, cfgCraft);
                    if (ContainerFilter.Distance(origin, chest.transform.position) > chestRange)
                        continue;

                    ZNetView nv = Refs.View(chest);
                    if (nv == null || !nv.IsOwner())
                    {
                        TransferService.Withdraw(chest, shared, deficit, inv, leaveOne);
                        allOwned = false;
                        waiting = true;
                        continue;
                    }

                    int took = TransferService.Withdraw(chest, shared, deficit, inv, leaveOne);
                    deficit -= took;
                }
            }

            return allOwned;
        }

        public static void ScheduleCraftRetry()
        {
            _retryCraft = true;
            _retryCraftAt = Time.time + 0.2f;
        }

        private static void AccessToolsDoCraft(InventoryGui gui, Player player)
        {
            var method = HarmonyLib.AccessTools.Method(typeof(InventoryGui), "DoCrafting");
            if (method != null)
                method.Invoke(gui, new object[] { player });
        }
    }
}
