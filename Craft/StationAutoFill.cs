using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Per-station auto-fill for every input station we can feed: kiln, smelter,
    /// blast furnace, eitr, windmill, cooking / stone oven, fermenter, torches / fires.
    /// Hover the station with the inventory closed and press B to toggle.
    /// Inventory-open F stays favorites. Chest pull filters still apply.
    /// </summary>
    internal static class StationAutoFill
    {
        public const string ZdoKey = "SAC_autoFill";
        private const float Interval = 3f;
        private const float QuietEmptySeconds = 20f;
        private const int MaxBurstsPerPulse = 1;

        internal static int Silence;

        private static readonly List<Smelter> Smelters = new List<Smelter>();
        private static readonly HashSet<int> SmelterIds = new HashSet<int>();
        private static readonly List<Fireplace> Fires = new List<Fireplace>();
        private static readonly HashSet<int> FireIds = new HashSet<int>();
        private static readonly List<CookingStation> Ovens = new List<CookingStation>();
        private static readonly HashSet<int> OvenIds = new HashSet<int>();
        private static readonly List<Fermenter> Fermenters = new List<Fermenter>();
        private static readonly HashSet<int> FermenterIds = new HashSet<int>();
        private static readonly Dictionary<string, float> QuietUntil = new Dictionary<string, float>();

        private static readonly MethodInfo SmelterGetFuel = AccessTools.Method(typeof(Smelter), "GetFuel");
        private static readonly MethodInfo SmelterGetQueue = AccessTools.Method(typeof(Smelter), "GetQueueSize");
        private static readonly MethodInfo SmelterAddFuel = AccessTools.Method(typeof(Smelter), "OnAddFuel");
        private static readonly MethodInfo SmelterAddOre = AccessTools.Method(typeof(Smelter), "OnAddOre");
        private static readonly MethodInfo FireGetFuel = AccessTools.Method(typeof(Fireplace), "GetFuel");
        private static readonly MethodInfo CookAddFuel = AccessTools.Method(typeof(CookingStation), "OnAddFuelSwitch");
        private static readonly MethodInfo CookUseItem = AccessTools.Method(typeof(CookingStation), "OnUseItem");
        private static readonly MethodInfo CookGetFuel = AccessTools.Method(typeof(CookingStation), "GetFuel");
        private static readonly MethodInfo CookIsEmpty = AccessTools.Method(typeof(CookingStation), "IsEmpty");
        private static readonly MethodInfo CookIsFull = AccessTools.Method(typeof(CookingStation), "IsStationFull");
        private static readonly MethodInfo FermenterAddItem = AccessTools.Method(typeof(Fermenter), "AddItem");
        private static readonly MethodInfo FermenterGetStatus = AccessTools.Method(typeof(Fermenter), "GetStatus");
        private static readonly int FuelHash = ReadFuelHash();

        private static float _nextPulse;
        private static float _nextPrune;

        public static void Register(Smelter smelter)
        {
            if (smelter == null)
                return;
            int id = smelter.GetInstanceID();
            if (!SmelterIds.Add(id))
                return;
            Smelters.Add(smelter);
        }

        public static void Register(Fireplace fire)
        {
            if (fire == null)
                return;
            int id = fire.GetInstanceID();
            if (!FireIds.Add(id))
                return;
            Fires.Add(fire);
        }

        public static void Register(CookingStation oven)
        {
            if (oven == null)
                return;
            int id = oven.GetInstanceID();
            if (!OvenIds.Add(id))
                return;
            Ovens.Add(oven);
        }

        public static void Register(Fermenter fermenter)
        {
            if (fermenter == null)
                return;
            int id = fermenter.GetInstanceID();
            if (!FermenterIds.Add(id))
                return;
            Fermenters.Add(fermenter);
        }

        public static void BootstrapExisting()
        {
            Smelter[] smelters = Resources.FindObjectsOfTypeAll<Smelter>();
            if (smelters != null)
            {
                foreach (Smelter s in smelters)
                {
                    if (s == null || !s.gameObject.scene.IsValid())
                        continue;
                    Register(s);
                }
            }

            Fireplace[] fires = Resources.FindObjectsOfTypeAll<Fireplace>();
            if (fires != null)
            {
                foreach (Fireplace f in fires)
                {
                    if (f == null || !f.gameObject.scene.IsValid())
                        continue;
                    Register(f);
                }
            }

            CookingStation[] ovens = Resources.FindObjectsOfTypeAll<CookingStation>();
            if (ovens != null)
            {
                foreach (CookingStation oven in ovens)
                {
                    if (oven == null || !oven.gameObject.scene.IsValid())
                        continue;
                    Register(oven);
                }
            }

            Fermenter[] fermenters = Resources.FindObjectsOfTypeAll<Fermenter>();
            if (fermenters == null)
                return;
            foreach (Fermenter fermenter in fermenters)
            {
                if (fermenter == null || !fermenter.gameObject.scene.IsValid())
                    continue;
                Register(fermenter);
            }
        }

        public static bool IsOn(ZNetView nv)
        {
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;
            return nv.GetZDO().GetInt(ZdoKey, 0) != 0;
        }

        public static bool IsOn(Smelter smelter)
        {
            return smelter != null && IsOn(smelter.GetComponent<ZNetView>());
        }

        public static bool IsOn(Fireplace fire)
        {
            return fire != null && IsOn(fire.GetComponent<ZNetView>());
        }

        public static void AppendHover(ref string text, ZNetView nv)
        {
            if (string.IsNullOrEmpty(text) || !StationFeed.Ready())
                return;
            if (nv == null || !nv.IsValid())
                return;

            string key = KeyUtil.Format(Plugin.Settings.AutoFillKey.Value);
            if (string.IsNullOrEmpty(key))
                key = "B";

            text += "\n[<color=yellow><b>" + key + "</b></color>] "
                + Loc.T("Auto-fill", "Auto-Fill")
                + " (" + (IsOn(nv) ? Loc.T("on", "an") : Loc.T("off", "aus")) + ")";
        }

        public static bool TryToggle()
        {
            if (InventoryGui.IsVisible() || StationFilterMenu.IsOpen || DisplayTypeMenu.IsOpen)
                return false;
            if (!StationFeed.Ready())
                return false;

            Player player = Player.m_localPlayer;
            if (player == null)
                return false;

            Smelter smelter = StationPullFilter.HoveredSmelter();
            CookingStation oven = smelter == null ? HoveredOven() : null;
            Fermenter fermenter = smelter == null && oven == null ? HoveredFermenter() : null;
            Fireplace fire = smelter == null && oven == null && fermenter == null ? HoveredFireplace() : null;

            Component station = (Component)smelter ?? oven ?? (Component)fermenter ?? fire;
            ZNetView nv = station != null ? station.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;

            Vector3 pos = station.transform.position;
            if (!PrivateArea.CheckAccess(pos, 0f, false, true))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone", 0, null, false);
                return true;
            }

            if (!nv.IsOwner())
                nv.ClaimOwnership();

            bool next = !IsOn(nv);
            nv.GetZDO().Set(ZdoKey, next ? 1 : 0);
            ClearQuiet(smelter);
            ClearQuiet(oven);
            ClearQuiet(fermenter);
            ClearQuiet(fire);

            player.Message(
                MessageHud.MessageType.Center,
                next
                    ? Loc.T("Auto-fill on", "Auto-Fill an")
                    : Loc.T("Auto-fill off", "Auto-Fill aus"),
                0, null, false);
            return true;
        }

        public static void Tick()
        {
            if (!StationFeed.Ready())
                return;

            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead())
                return;

            if (Time.unscaledTime < _nextPulse)
                return;
            _nextPulse = Time.unscaledTime + Interval;

            if (Time.unscaledTime >= _nextPrune)
            {
                PruneDead();
                _nextPrune = Time.unscaledTime + 10f;
            }

            float range = Plugin.Settings.AutoFillRange.Value;
            float rangeSq = range * range;
            Vector3 origin = player.transform.position;
            int bursts = 0;

            StationFeed.PullRangeOverride = range;
            try
            {

            for (int i = 0; i < Smelters.Count && bursts < MaxBurstsPerPulse; i++)
            {
                Smelter smelter = Smelters[i];
                if (smelter == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, smelter.transform.position) > rangeSq)
                    continue;
                if (!IsOn(smelter))
                    continue;
                if (!PrivateArea.CheckAccess(smelter.transform.position, 0f, false, true))
                    continue;
                if (FillSmelterWhenEmpty(smelter, player))
                    bursts++;
            }

            for (int i = 0; i < Fires.Count && bursts < MaxBurstsPerPulse; i++)
            {
                Fireplace fire = Fires[i];
                if (fire == null || !fire.m_canRefill || IsQuiet(fire, "fire"))
                    continue;
                if (ContainerFilter.SqrDistance(origin, fire.transform.position) > rangeSq)
                    continue;
                if (!IsOn(fire))
                    continue;
                if (!PrivateArea.CheckAccess(fire.transform.position, 0f, false, true))
                    continue;
                if (FillFireWhenEmpty(fire, player))
                    bursts++;
            }

            for (int i = 0; i < Ovens.Count && bursts < MaxBurstsPerPulse; i++)
            {
                CookingStation oven = Ovens[i];
                if (oven == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, oven.transform.position) > rangeSq)
                    continue;
                if (!IsOn(oven.GetComponent<ZNetView>()))
                    continue;
                if (!PrivateArea.CheckAccess(oven.transform.position, 0f, false, true))
                    continue;
                if (FillOvenWhenEmpty(oven, player))
                    bursts++;
            }

            for (int i = 0; i < Fermenters.Count && bursts < MaxBurstsPerPulse; i++)
            {
                Fermenter fermenter = Fermenters[i];
                if (fermenter == null || IsQuiet(fermenter, "mead"))
                    continue;
                if (ContainerFilter.SqrDistance(origin, fermenter.transform.position) > rangeSq)
                    continue;
                if (!IsOn(fermenter.GetComponent<ZNetView>()))
                    continue;
                if (!PrivateArea.CheckAccess(fermenter.transform.position, 0f, false, true))
                    continue;
                if (FillFermenterWhenEmpty(fermenter, player))
                    bursts++;
            }
            }
            finally
            {
                StationFeed.PullRangeOverride = 0f;
            }
        }

        private static bool FillSmelterWhenEmpty(Smelter smelter, Player player)
        {
            bool did = false;
            if (HasFuelSlot(smelter) && !IsQuiet(smelter, "fuel") && ReadNumber(SmelterGetFuel, smelter) <= 0.01f)
                did |= FillFuelToMax(smelter, player);
            if (HasOreSlot(smelter) && !IsQuiet(smelter, "ore") && ReadNumber(SmelterGetQueue, smelter) < 1f)
                did |= FillOreToMax(smelter, player);
            return did;
        }

        private static bool HasFuelSlot(Smelter smelter)
        {
            return smelter.m_fuelItem != null && smelter.m_addWoodSwitch != null && smelter.m_maxFuel > 0;
        }

        private static bool HasOreSlot(Smelter smelter)
        {
            return smelter.m_addOreSwitch != null && smelter.m_maxOre > 0;
        }

        private static bool FillFuelToMax(Smelter smelter, Player player)
        {
            string fuel = StationFeed.SharedFrom(smelter.m_fuelItem);
            if (string.IsNullOrEmpty(fuel))
                return false;

            ZNetView nv = smelter.GetComponent<ZNetView>();
            int need = smelter.m_maxFuel;
            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (StationFeed.LocalCount(player, fuel) >= 1)
                {
                    if (SmelterAddFuel == null || !InvokeAdd(SmelterAddFuel, smelter, smelter.m_addWoodSwitch, player))
                        break;
                    added++;
                    continue;
                }

                if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                {
                    Quiet(smelter, "fuel", QuietEmptySeconds);
                    return false;
                }

                if (nv == null || !nv.IsValid() || StationFeed.ConsumeFromChests(player, fuel, 1) < 1)
                    break;

                BeginSilence();
                try
                {
                    nv.InvokeRPC("RPC_AddFuel");
                }
                finally
                {
                    EndSilence();
                }
                added++;
            }

            return added > 0;
        }

        private static bool FillOreToMax(Smelter smelter, Player player)
        {
            List<string> allowed = StationPullFilter.AllowedOreNames(smelter);
            if (allowed == null || allowed.Count == 0)
            {
                Quiet(smelter, "ore", QuietEmptySeconds);
                return false;
            }

            ZNetView nv = smelter.GetComponent<ZNetView>();
            int need = smelter.m_maxOre;
            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (HasLocalAny(player, allowed))
                {
                    if (SmelterAddOre == null || !InvokeAdd(SmelterAddOre, smelter, smelter.m_addOreSwitch, player))
                        break;
                    added++;
                    continue;
                }

                if (added == 0 && !ChestsHaveAny(player, allowed))
                {
                    Quiet(smelter, "ore", QuietEmptySeconds);
                    return false;
                }

                string shared = FirstChestItem(player, allowed);
                string prefab = PrefabName(shared);
                if (nv == null || !nv.IsValid() || string.IsNullOrEmpty(prefab)
                    || StationFeed.ConsumeFromChests(player, shared, 1) < 1)
                    break;

                BeginSilence();
                try
                {
                    nv.InvokeRPC("RPC_AddOre", prefab, false);
                }
                finally
                {
                    EndSilence();
                }
                added++;
            }

            return added > 0;
        }

        private static bool FillFireWhenEmpty(Fireplace fire, Player player)
        {
            ZNetView nv = fire.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return false;

            string fuel = StationFeed.SharedFrom(fire.m_fuelItem);
            if (string.IsNullOrEmpty(fuel) || fire.m_maxFuel <= 0f)
                return false;

            if (ReadFireFuel(fire) > 0.01f)
                return false;

            int need = Mathf.Max(1, Mathf.FloorToInt(fire.m_maxFuel));
            Inventory inv = player.GetInventory();
            if (inv == null)
                return false;

            int added = 0;
            BeginSilence();
            try
            {
                for (int i = 0; i < need; i++)
                {
                    if (StationFeed.LocalCount(player, fuel) >= 1)
                    {
                        InventoryCountPatches.Skip++;
                        try
                        {
                            inv.RemoveItem(fuel, 1, -1, true);
                        }
                        finally
                        {
                            InventoryCountPatches.Skip--;
                        }
                    }
                    else
                    {
                        if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                        {
                            Quiet(fire, "fire", QuietEmptySeconds);
                            return false;
                        }
                        if (StationFeed.ConsumeFromChests(player, fuel, 1) < 1)
                            break;
                    }

                    nv.InvokeRPC("RPC_AddFuel");
                    added++;
                }
            }
            finally
            {
                EndSilence();
            }

            return added > 0;
        }

        private static bool FillOvenWhenEmpty(CookingStation oven, Player player)
        {
            bool did = false;
            if (oven.m_useFuel && oven.m_fuelItem != null && oven.m_maxFuel > 0
                && !IsQuiet(oven, "fuel") && ReadNumber(CookGetFuel, oven) <= 0.01f)
                did |= FillOvenFuel(oven, player);
            if (!IsQuiet(oven, "food") && IsTrue(CookIsEmpty, oven))
                did |= FillOvenFood(oven, player);
            return did;
        }

        private static bool FillOvenFuel(CookingStation oven, Player player)
        {
            string fuel = StationFeed.SharedFrom(oven.m_fuelItem);
            if (string.IsNullOrEmpty(fuel))
                return false;

            ZNetView nv = oven.GetComponent<ZNetView>();
            int need = Mathf.Max(1, oven.m_maxFuel);
            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (StationFeed.LocalCount(player, fuel) >= 1)
                {
                    if (CookAddFuel == null || !InvokeWith(CookAddFuel, oven, oven.m_addFuelSwitch, player))
                        break;
                    added++;
                    continue;
                }

                if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                {
                    Quiet(oven, "fuel", QuietEmptySeconds);
                    return false;
                }

                if (nv == null || !nv.IsValid() || StationFeed.ConsumeFromChests(player, fuel, 1) < 1)
                    break;

                BeginSilence();
                try
                {
                    nv.InvokeRPC("RPC_AddFuel");
                }
                finally
                {
                    EndSilence();
                }
                added++;
            }

            return added > 0;
        }

        private static bool FillOvenFood(CookingStation oven, Player player)
        {
            List<string> foods = CookingOnInteractPatch.FoodNames(oven);
            if (foods == null || foods.Count == 0)
                return false;

            ZNetView nv = oven.GetComponent<ZNetView>();
            int added = 0;
            int guard = oven.m_slots != null ? oven.m_slots.Length : 5;
            while (guard-- > 0 && !IsTrue(CookIsFull, oven))
            {
                if (HasLocalAny(player, foods))
                {
                    ItemDrop.ItemData item = FirstLocal(player, foods);
                    if (item == null || CookUseItem == null || !InvokeWith(CookUseItem, oven, player, item))
                        break;
                    added++;
                    continue;
                }

                if (added == 0 && !ChestsHaveAny(player, foods))
                {
                    Quiet(oven, "food", QuietEmptySeconds);
                    return added > 0;
                }

                string shared = FirstChestItem(player, foods);
                string prefab = PrefabName(shared);
                if (nv == null || !nv.IsValid() || string.IsNullOrEmpty(prefab)
                    || StationFeed.ConsumeFromChests(player, shared, 1) < 1)
                    break;

                BeginSilence();
                try
                {
                    nv.InvokeRPC("RPC_AddItem", prefab, false);
                }
                finally
                {
                    EndSilence();
                }
                added++;
            }

            return added > 0;
        }

        private static bool FillFermenterWhenEmpty(Fermenter fermenter, Player player)
        {
            if (ReadEnumInt(FermenterGetStatus, fermenter) != 0)
                return false;

            List<string> meads = FermenterInteractPatch.MeadNames(fermenter);
            if (meads == null || meads.Count == 0)
            {
                Quiet(fermenter, "mead", QuietEmptySeconds);
                return false;
            }

            if (HasLocalAny(player, meads))
            {
                ItemDrop.ItemData item = FirstLocal(player, meads);
                return item != null && InvokeWith(FermenterAddItem, fermenter, player, item);
            }

            if (!ChestsHaveAny(player, meads))
            {
                Quiet(fermenter, "mead", QuietEmptySeconds);
                return false;
            }

            string shared = FirstChestItem(player, meads);
            string prefab = PrefabName(shared);
            int hash = PrefabHash(prefab);
            ZNetView nv = fermenter.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || hash == 0
                || StationFeed.ConsumeFromChests(player, shared, 1) < 1)
            {
                Quiet(fermenter, "mead", QuietEmptySeconds);
                return false;
            }

            BeginSilence();
            try
            {
                nv.InvokeRPC("RPC_AddItem", hash, false);
            }
            finally
            {
                EndSilence();
            }
            return true;
        }

        private static ItemDrop.ItemData FirstLocal(Player player, List<string> sharedNames)
        {
            Inventory inv = player != null ? player.GetInventory() : null;
            if (inv == null || sharedNames == null)
                return null;
            foreach (string shared in sharedNames)
            {
                if (string.IsNullOrEmpty(shared))
                    continue;
                ItemDrop.ItemData item = inv.GetItem(shared, -1, false);
                if (item != null)
                    return item;
            }
            return null;
        }

        private static string FirstChestItem(Player player, List<string> sharedNames)
        {
            if (sharedNames == null)
                return null;
            foreach (string shared in sharedNames)
            {
                if (!string.IsNullOrEmpty(shared) && StationFeed.ChestsHave(player, shared))
                    return shared;
            }
            return null;
        }

        private static string PrefabName(string shared)
        {
            if (string.IsNullOrEmpty(shared))
                return null;
            GameObject prefab = ItemIds.PrefabFromToken(shared);
            return prefab != null ? ItemIds.StripClone(prefab.name) : null;
        }

        private static int PrefabHash(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return 0;
            MethodInfo hash = AccessTools.Method(typeof(StringExtensionMethods), "GetStableHashCode");
            if (hash == null)
                return 0;
            try
            {
                object value = hash.Invoke(null, new object[] { prefabName });
                return value is int ? (int)value : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool InvokeWith(MethodInfo method, object target, params object[] candidates)
        {
            if (method == null || target == null)
                return false;

            ParameterInfo[] ps;
            try
            {
                ps = method.GetParameters();
            }
            catch
            {
                return false;
            }

            object[] args = new object[ps.Length];
            var used = new bool[candidates.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type need = ps[i].ParameterType;
                bool found = false;
                for (int c = 0; c < candidates.Length; c++)
                {
                    if (used[c] || candidates[c] == null)
                        continue;
                    if (!need.IsInstanceOfType(candidates[c]))
                        continue;
                    args[i] = candidates[c];
                    used[c] = true;
                    found = true;
                    break;
                }

                if (!found && need.IsValueType)
                    args[i] = Activator.CreateInstance(need);
            }

            BeginSilence();
            try
            {
                object result = method.Invoke(target, args);
                return !(result is bool) || (bool)result;
            }
            catch
            {
                return false;
            }
            finally
            {
                EndSilence();
            }
        }

        private static bool ChestsHaveAny(Player player, List<string> sharedNames)
        {
            foreach (string shared in sharedNames)
            {
                if (StationFeed.ChestsHave(player, shared))
                    return true;
            }
            return false;
        }

        private static bool HasLocalAny(Player player, List<string> sharedNames)
        {
            foreach (string shared in sharedNames)
            {
                if (StationFeed.LocalCount(player, shared) >= 1)
                    return true;
            }
            return false;
        }

        private static bool InvokeAdd(MethodInfo method, Smelter smelter, Switch sw, Player player)
        {
            if (method == null || smelter == null)
                return false;
            BeginSilence();
            try
            {
                object result = method.Invoke(smelter, new object[] { sw, player, null });
                return !(result is bool) || (bool)result;
            }
            catch
            {
                return false;
            }
            finally
            {
                EndSilence();
            }
        }

        private static void BeginSilence()
        {
            Silence++;
        }

        private static void EndSilence()
        {
            if (Silence > 0)
                Silence--;
        }

        private static bool IsQuiet(UnityEngine.Object obj, string slot)
        {
            if (obj == null)
                return true;
            float until;
            return QuietUntil.TryGetValue(QuietKey(obj, slot), out until) && Time.unscaledTime < until;
        }

        private static void Quiet(UnityEngine.Object obj, string slot, float seconds)
        {
            if (obj == null)
                return;
            QuietUntil[QuietKey(obj, slot)] = Time.unscaledTime + seconds;
        }

        private static void ClearQuiet(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            int id = obj.GetInstanceID();
            QuietUntil.Remove(id + ":fuel");
            QuietUntil.Remove(id + ":ore");
            QuietUntil.Remove(id + ":fire");
            QuietUntil.Remove(id + ":food");
            QuietUntil.Remove(id + ":mead");
        }

        private static string QuietKey(UnityEngine.Object obj, string slot)
        {
            return obj.GetInstanceID() + ":" + slot;
        }

        private static float ReadFireFuel(Fireplace fire)
        {
            if (FireGetFuel != null)
            {
                try
                {
                    return ToFloat(FireGetFuel.Invoke(fire, null));
                }
                catch
                {
                }
            }

            ZNetView nv = fire.GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return 0f;
            if (FuelHash != 0)
                return zdo.GetFloat(FuelHash, 0f);
            return zdo.GetFloat("fuel", 0f);
        }

        private static bool IsTrue(MethodInfo method, object target)
        {
            if (method == null || target == null)
                return false;
            try
            {
                object value = method.Invoke(target, null);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadEnumInt(MethodInfo method, object target)
        {
            if (method == null || target == null)
                return -1;
            try
            {
                object value = method.Invoke(target, null);
                return value == null ? -1 : Convert.ToInt32(value);
            }
            catch
            {
                return -1;
            }
        }

        private static float ReadNumber(MethodInfo method, object target)
        {
            if (method == null || target == null)
                return 0f;
            try
            {
                return ToFloat(method.Invoke(target, null));
            }
            catch
            {
                return 0f;
            }
        }

        private static float ToFloat(object value)
        {
            if (value is float)
                return (float)value;
            if (value is int)
                return (int)value;
            if (value is double)
                return (float)(double)value;
            return 0f;
        }

        private static int ReadFuelHash()
        {
            FieldInfo field = AccessTools.Field(typeof(ZDOVars), "s_fuel");
            if (field == null)
                return 0;
            try
            {
                object value = field.GetValue(null);
                if (value is int)
                    return (int)value;
            }
            catch
            {
            }
            return 0;
        }

        private static Fireplace HoveredFireplace()
        {
            return HoveredComponent<Fireplace>();
        }

        private static CookingStation HoveredOven()
        {
            return HoveredComponent<CookingStation>();
        }

        private static Fermenter HoveredFermenter()
        {
            return HoveredComponent<Fermenter>();
        }

        private static T HoveredComponent<T>() where T : Component
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;
            GameObject hover = player.GetHoverObject();
            if (hover == null)
                return null;
            return hover.GetComponentInParent<T>();
        }

        private static void PruneDead()
        {
            SmelterIds.Clear();
            for (int i = Smelters.Count - 1; i >= 0; i--)
            {
                if (Smelters[i] == null)
                {
                    Smelters.RemoveAt(i);
                    continue;
                }
                SmelterIds.Add(Smelters[i].GetInstanceID());
            }

            FireIds.Clear();
            for (int i = Fires.Count - 1; i >= 0; i--)
            {
                if (Fires[i] == null)
                {
                    Fires.RemoveAt(i);
                    continue;
                }
                FireIds.Add(Fires[i].GetInstanceID());
            }

            OvenIds.Clear();
            for (int i = Ovens.Count - 1; i >= 0; i--)
            {
                if (Ovens[i] == null)
                {
                    Ovens.RemoveAt(i);
                    continue;
                }
                OvenIds.Add(Ovens[i].GetInstanceID());
            }

            FermenterIds.Clear();
            for (int i = Fermenters.Count - 1; i >= 0; i--)
            {
                if (Fermenters[i] == null)
                {
                    Fermenters.RemoveAt(i);
                    continue;
                }
                FermenterIds.Add(Fermenters[i].GetInstanceID());
            }

            if (QuietUntil.Count == 0)
                return;
            var dead = new List<string>();
            foreach (KeyValuePair<string, float> pair in QuietUntil)
            {
                if (Time.unscaledTime >= pair.Value)
                    dead.Add(pair.Key);
            }
            for (int i = 0; i < dead.Count; i++)
                QuietUntil.Remove(dead[i]);
        }
    }

    [HarmonyPatch]
    internal static class AutoFillSilencePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // Humanoid.Message is inherited from Character; GetDeclaredMethods(Humanoid)
            // returned nothing and Harmony crashed on load.
            foreach (MethodInfo method in MethodsNamed(typeof(Character), "Message"))
                yield return method;
            foreach (MethodInfo method in MethodsNamed(typeof(Humanoid), "Message"))
                yield return method;
            foreach (MethodInfo method in MethodsNamed(typeof(Player), "Message"))
                yield return method;
            foreach (MethodInfo method in MethodsNamed(typeof(MessageHud), "ShowMessage"))
                yield return method;
        }

        private static IEnumerable<MethodInfo> MethodsNamed(Type type, string name)
        {
            IEnumerable<MethodInfo> methods;
            try
            {
                methods = AccessTools.GetDeclaredMethods(type);
            }
            catch
            {
                yield break;
            }

            if (methods == null)
                yield break;

            foreach (MethodInfo method in methods)
            {
                if (method != null && method.Name == name)
                    yield return method;
            }
        }

        private static bool Prefix()
        {
            return StationAutoFill.Silence <= 0;
        }
    }

    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class SmelterAwakePatch
    {
        private static void Postfix(Smelter __instance)
        {
            StationAutoFill.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverEmptyOre")]
    internal static class SmelterHoverEmptyOreAutoFillPatch
    {
        private static void Postfix(Smelter __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(Fireplace), "Awake")]
    internal static class FireplaceAwakePatch
    {
        private static void Postfix(Fireplace __instance)
        {
            StationAutoFill.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "Awake")]
    internal static class CookingAwakeAutoFillPatch
    {
        private static void Postfix(CookingStation __instance)
        {
            StationAutoFill.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Fermenter), "Awake")]
    internal static class FermenterAwakeAutoFillPatch
    {
        private static void Postfix(Fermenter __instance)
        {
            StationAutoFill.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
    internal static class SmelterHoverAddFuelAutoFillPatch
    {
        private static void Postfix(Smelter __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    internal static class FireplaceHoverAutoFillPatch
    {
        private static void Postfix(Fireplace __instance, ref string __result)
        {
            if (__instance == null || !__instance.m_canRefill)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetHoverText))]
    internal static class CookingHoverAutoFillPatch
    {
        private static void Postfix(CookingStation __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnHoverFuelSwitch")]
    internal static class CookingHoverFuelAutoFillPatch
    {
        private static void Postfix(CookingStation __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
    internal static class FermenterHoverAutoFillPatch
    {
        private static void Postfix(Fermenter __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>());
        }
    }
}
