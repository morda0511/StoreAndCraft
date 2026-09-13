using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Per-station auto-fill for kilns, smelters, and torches / fireplaces.
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
        private static readonly Dictionary<string, float> QuietUntil = new Dictionary<string, float>();

        private static readonly MethodInfo SmelterGetFuel = AccessTools.Method(typeof(Smelter), "GetFuel");
        private static readonly MethodInfo SmelterGetQueue = AccessTools.Method(typeof(Smelter), "GetQueueSize");
        private static readonly MethodInfo SmelterAddFuel = AccessTools.Method(typeof(Smelter), "OnAddFuel");
        private static readonly MethodInfo SmelterAddOre = AccessTools.Method(typeof(Smelter), "OnAddOre");
        private static readonly MethodInfo FireGetFuel = AccessTools.Method(typeof(Fireplace), "GetFuel");
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
            if (fires == null)
                return;
            foreach (Fireplace f in fires)
            {
                if (f == null || !f.gameObject.scene.IsValid())
                    continue;
                Register(f);
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
            Fireplace fire = smelter == null ? HoveredFireplace() : null;
            ZNetView nv = smelter != null
                ? smelter.GetComponent<ZNetView>()
                : (fire != null ? fire.GetComponent<ZNetView>() : null);
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;

            Vector3 pos = smelter != null ? smelter.transform.position : fire.transform.position;
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
            if (string.IsNullOrEmpty(fuel) || SmelterAddFuel == null)
                return false;

            int need = smelter.m_maxFuel;
            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (StationFeed.LocalCount(player, fuel) < 1)
                {
                    if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                    {
                        Quiet(smelter, "fuel", QuietEmptySeconds);
                        return false;
                    }
                    StationFeed.EnsureInInventory(player, fuel, 1);
                    if (StationFeed.LocalCount(player, fuel) < 1)
                        break;
                }

                if (!InvokeAdd(SmelterAddFuel, smelter, smelter.m_addWoodSwitch, player))
                    break;
                added++;
            }

            return added > 0;
        }

        private static bool FillOreToMax(Smelter smelter, Player player)
        {
            if (SmelterAddOre == null)
                return false;

            List<string> allowed = StationPullFilter.AllowedOreNames(smelter);
            if (allowed == null || allowed.Count == 0)
            {
                Quiet(smelter, "ore", QuietEmptySeconds);
                return false;
            }

            int need = smelter.m_maxOre;
            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (!HasLocalAny(player, allowed))
                {
                    if (added == 0 && !ChestsHaveAny(player, allowed))
                    {
                        Quiet(smelter, "ore", QuietEmptySeconds);
                        return false;
                    }
                    StationFeed.EnsureAny(player, allowed, 1);
                    if (!HasLocalAny(player, allowed))
                        break;
                }

                if (!InvokeAdd(SmelterAddOre, smelter, smelter.m_addOreSwitch, player))
                    break;
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
                    if (StationFeed.LocalCount(player, fuel) < 1)
                    {
                        if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                        {
                            Quiet(fire, "fire", QuietEmptySeconds);
                            return false;
                        }
                        StationFeed.EnsureInInventory(player, fuel, 1);
                        if (StationFeed.LocalCount(player, fuel) < 1)
                            break;
                    }

                    InventoryCountPatches.Skip++;
                    try
                    {
                        inv.RemoveItem(fuel, 1, -1, true);
                    }
                    finally
                    {
                        InventoryCountPatches.Skip--;
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
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;
            GameObject hover = player.GetHoverObject();
            if (hover == null)
                return null;
            return hover.GetComponentInParent<Fireplace>();
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

    [HarmonyPatch(typeof(Fireplace), "Awake")]
    internal static class FireplaceAwakePatch
    {
        private static void Postfix(Fireplace __instance)
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
}
