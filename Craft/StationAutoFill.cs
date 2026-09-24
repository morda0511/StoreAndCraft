using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Per-station auto-fill for every input station we can feed: kiln, smelter,
    /// blast furnace, eitr, windmill, Frost Foundry, cooking / stone oven, fermenter,
    /// torches / fires, Mistlands ballistas (Turret), and food serving trays (ItemStand).
    /// Hover the station with the inventory closed and press B to toggle.
    /// Inventory-open F stays favorites. Chest pull filters still apply.
    /// </summary>
    internal static class StationAutoFill
    {
        public const string ZdoKey = "SAC_autoFill";
        private const float Interval = 5f;
        private const float QuietEmptySeconds = 25f;
        private const int MaxBurstsPerPulse = 4;
        /// <summary>Cap how many needy stations we evaluate per pulse (after range/IsOn filters).</summary>
        private const int MaxChecksPerPulse = 12;

        internal static int Silence;

        private static readonly List<Smelter> Smelters = new List<Smelter>();
        private static readonly HashSet<int> SmelterIds = new HashSet<int>();
        private static readonly List<Fireplace> Fires = new List<Fireplace>();
        private static readonly HashSet<int> FireIds = new HashSet<int>();
        private static readonly List<CookingStation> Ovens = new List<CookingStation>();
        private static readonly HashSet<int> OvenIds = new HashSet<int>();
        private static readonly List<Fermenter> Fermenters = new List<Fermenter>();
        private static readonly HashSet<int> FermenterIds = new HashSet<int>();
        private static readonly List<Turret> Turrets = new List<Turret>();
        private static readonly HashSet<int> TurretIds = new HashSet<int>();
        private static readonly List<ItemStand> FoodTrays = new List<ItemStand>();
        private static readonly HashSet<int> FoodTrayIds = new HashSet<int>();
        private static readonly Dictionary<string, float> QuietUntil = new Dictionary<string, float>();

        private static readonly MethodInfo SmelterGetFuel = AccessTools.Method(typeof(Smelter), "GetFuel");
        private static readonly MethodInfo SmelterSetFuel = AccessTools.Method(typeof(Smelter), "SetFuel");
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
        private static readonly MethodInfo TurretGetAmmo = AccessTools.Method(typeof(Turret), "GetAmmo");
        private static readonly MethodInfo TurretGetAmmoType = AccessTools.Method(typeof(Turret), "GetAmmoType");
        private static readonly MethodInfo TurretFindAmmo = AccessTools.Method(typeof(Turret), "FindAmmoItem");
        private static readonly MethodInfo TurretUseItem = AccessTools.Method(typeof(Turret), "UseItem");
        private static readonly MethodInfo ItemStandUseItem = AccessTools.Method(typeof(ItemStand), "UseItem");
        private static readonly MethodInfo ItemStandHaveAttachment = AccessTools.Method(typeof(ItemStand), "HaveAttachment");
        private static readonly MethodInfo ItemStandCanAttach = AccessTools.Method(typeof(ItemStand), "CanAttach");
        private static readonly int FuelHash = ReadFuelHash();

        private static float _nextPulse;
        private static float _nextPrune;
        private static int _smelterCursor;
        private static int _fireCursor;
        private static int _ovenCursor;
        private static int _fermenterCursor;
        private static int _turretCursor;
        private static int _trayCursor;

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

        public static void Register(Turret turret)
        {
            if (turret == null)
                return;
            int id = turret.GetInstanceID();
            if (!TurretIds.Add(id))
                return;
            Turrets.Add(turret);
        }

        public static void Register(ItemStand stand)
        {
            if (stand == null || !IsFoodServingTray(stand))
                return;
            int id = stand.GetInstanceID();
            if (!FoodTrayIds.Add(id))
                return;
            FoodTrays.Add(stand);
        }

        internal static IReadOnlyList<Smelter> RegisteredSmelters { get { return Smelters; } }
        internal static IReadOnlyList<Fireplace> RegisteredFires { get { return Fires; } }
        internal static IReadOnlyList<CookingStation> RegisteredOvens { get { return Ovens; } }
        internal static IReadOnlyList<Fermenter> RegisteredFermenters { get { return Fermenters; } }

        /// <summary>Remote Automation feed — caller must BeginStationPull / EndStationPull.</summary>
        internal static bool RemoteFillSmelter(Smelter smelter, Player player)
        {
            if (smelter == null)
                return false;
            bool did = false;
            if (HasFuelSlot(smelter))
                did |= FillFuelToMax(smelter, player);
            if (HasOreSlot(smelter))
                did |= FillOreToMax(smelter, player);
            return did;
        }

        internal static bool RemoteFillOven(CookingStation oven, Player player)
        {
            return oven != null && FillOvenWhenEmpty(oven, player);
        }

        internal static bool RemoteFillFire(Fireplace fire, Player player)
        {
            return fire != null && FillFireWhenEmpty(fire, player);
        }

        internal static bool RemoteFillFermenter(Fermenter fermenter, Player player)
        {
            return fermenter != null && FillFermenterWhenEmpty(fermenter, player);
        }

        /// <summary>Serving trays accept consumable food; weapon stands / boss trophies do not.</summary>
        internal static bool IsFoodServingTray(ItemStand stand)
        {
            if (stand == null)
                return false;
            if (stand.m_guardianPower != null)
                return false;

            if (stand.m_supportedTypes != null)
            {
                for (int i = 0; i < stand.m_supportedTypes.Count; i++)
                {
                    if (stand.m_supportedTypes[i] == ItemDrop.ItemData.ItemType.Consumable)
                        return true;
                }
            }

            if (stand.m_supportedItems != null)
            {
                for (int i = 0; i < stand.m_supportedItems.Count; i++)
                {
                    ItemDrop drop = stand.m_supportedItems[i];
                    if (drop?.m_itemData?.m_shared != null
                        && drop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
                        return true;
                }
            }

            return false;
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
            if (fermenters != null)
            {
                foreach (Fermenter fermenter in fermenters)
                {
                    if (fermenter == null || !fermenter.gameObject.scene.IsValid())
                        continue;
                    Register(fermenter);
                }
            }

            Turret[] turrets = Resources.FindObjectsOfTypeAll<Turret>();
            if (turrets != null)
            {
                foreach (Turret turret in turrets)
                {
                    if (turret == null || !turret.gameObject.scene.IsValid())
                        continue;
                    Register(turret);
                }
            }

            ItemStand[] stands = Resources.FindObjectsOfTypeAll<ItemStand>();
            if (stands != null)
            {
                foreach (ItemStand stand in stands)
                {
                    if (stand == null || !stand.gameObject.scene.IsValid())
                        continue;
                    Register(stand);
                }
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
            AppendHover(ref text, nv, includeManualFill: true);
        }

        public static void AppendHover(ref string text, ZNetView nv, bool includeManualFill)
        {
            if (!StationFeed.Ready())
                return;
            if (nv == null || !nv.IsValid())
                return;

            if (string.IsNullOrEmpty(text))
                text = "";

            // Chord keys first, then single keys (same order everywhere).
            if (includeManualFill)
                AppendFillToMaxLine(ref text);
            AppendAutoFillLine(ref text, nv);
        }

        /// <summary>
        /// After vanilla hover: Alt+E filter → Shift+E fill to max → B auto-fill.
        /// Link / remote are status (link stays on top).
        /// </summary>
        public static void AppendSmelterHover(ref string text, Smelter smelter)
        {
            if (smelter == null || !StationFeed.Ready())
                return;

            ZNetView nv = smelter.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return;

            if (string.IsNullOrEmpty(text))
                text = "";

            StationPullFilter.AppendFilterHover(ref text, smelter, prependLink: false);
            AppendFillToMaxLine(ref text);
            AppendAutoFillLine(ref text, nv);
            RemoteAutomation.AppendHover(ref text, smelter);
            StationLink.PrependHover(ref text, StationLink.Get(smelter), chest: false);
        }

        private static void AppendFillToMaxLine(ref string text)
        {
            text += "\n[<color=yellow><b>" + Loc.T("Shift+E", "Umschalt+E") + "</b></color>] "
                + Loc.T("Fill to max", "Auffüllen bis voll");
        }

        private static void AppendAutoFillLine(ref string text, ZNetView nv)
        {
            string key = KeyUtil.Format(Plugin.Settings.AutoFillKey.Value);
            if (string.IsNullOrEmpty(key))
                key = "B";

            text += "\n[<color=yellow><b>" + key + "</b></color>] "
                + Loc.T("Auto-fill", "Auto-Fill")
                + " (" + (IsOn(nv) ? Loc.T("on", "an") : Loc.T("off", "aus")) + ")";
        }

        /// <summary>
        /// After vanilla [E] / [1-8]: Alt+E → Shift+E → B → N. Link on top.
        /// </summary>
        public static void AppendCookingHover(ref string text, CookingStation station)
        {
            if (station == null)
                return;

            StationPullFilter.AppendFilterHover(ref text, station);
            AppendHover(ref text, station.GetComponent<ZNetView>());
            CookingAutoDrop.AppendHover(ref text, station);
            RemoteAutomation.AppendHover(ref text, station);
            StationLink.PrependHover(ref text, StationLink.Get(station), chest: false);
        }

        /// <summary>
        /// Fireplace / fermenter: Alt+E (if filter) → Shift+E → B. Same chord→single order.
        /// </summary>
        public static void AppendFuelStationHover(ref string text, Component station, ZNetView nv)
        {
            if (station == null || nv == null || !nv.IsValid() || !StationFeed.Ready())
                return;

            if (station is Fireplace fire)
                StationPullFilter.AppendFilterHover(ref text, fire);
            else if (station is Fermenter fermenter)
                StationPullFilter.AppendFilterHover(ref text, fermenter);

            AppendHover(ref text, nv);
            RemoteAutomation.AppendHover(ref text, station);
        }

        public static bool TryToggle()
        {
            if (InventoryGui.IsVisible() || StationFilterMenu.IsOpen || DisplayTypeMenu.IsOpen || DisplayRangeMenu.IsOpen)
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
            Turret turret = smelter == null && oven == null && fermenter == null && fire == null
                ? HoveredTurret() : null;
            ItemStand tray = smelter == null && oven == null && fermenter == null && fire == null && turret == null
                ? HoveredFoodTray() : null;

            Component station = (Component)smelter ?? oven ?? (Component)fermenter ?? fire
                ?? (Component)turret ?? tray;
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
            ClearQuiet(turret);
            ClearQuiet(tray);

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

            // No station with auto-fill ON in range → no chest snapshot / fill work.
            if (!AnyOnInRange(origin, rangeSq))
                return;

            int bursts = 0;
            int checks = 0;

            StationFeed.PullRangeOverride = range;
            StationFeed.BeginAutoFillPulse(player, range);
            try
            {
                // Fires/torches first: cheap RPC; used to starve behind smelters (MaxBursts=1).
                checks = RoundRobinFill(
                    Fires, ref _fireCursor, origin, rangeSq, player, ref bursts, checks,
                    f => f != null && f.m_canRefill && !IsQuiet(f, "fire") && IsOn(f)
                        && PrivateArea.CheckAccess(f.transform.position, 0f, false, true),
                    FillFireWhenEmpty);

                if (bursts < MaxBurstsPerPulse && checks < MaxChecksPerPulse)
                {
                    checks = RoundRobinFill(
                        Smelters, ref _smelterCursor, origin, rangeSq, player, ref bursts, checks,
                        s => IsOn(s) && PrivateArea.CheckAccess(s.transform.position, 0f, false, true),
                        FillSmelterWhenEmpty);
                }

                if (bursts < MaxBurstsPerPulse && checks < MaxChecksPerPulse)
                {
                    checks = RoundRobinFill(
                        Ovens, ref _ovenCursor, origin, rangeSq, player, ref bursts, checks,
                        o => IsOn(o.GetComponent<ZNetView>())
                            && PrivateArea.CheckAccess(o.transform.position, 0f, false, true),
                        FillOvenWhenEmpty);
                }

                if (bursts < MaxBurstsPerPulse && checks < MaxChecksPerPulse)
                {
                    checks = RoundRobinFill(
                        Fermenters, ref _fermenterCursor, origin, rangeSq, player, ref bursts, checks,
                        f => f != null && !IsQuiet(f, "mead") && IsOn(f.GetComponent<ZNetView>())
                            && PrivateArea.CheckAccess(f.transform.position, 0f, false, true),
                        FillFermenterWhenEmpty);
                }

                if (bursts < MaxBurstsPerPulse && checks < MaxChecksPerPulse)
                {
                    checks = RoundRobinFill(
                        Turrets, ref _turretCursor, origin, rangeSq, player, ref bursts, checks,
                        t => t != null && !IsQuiet(t, "ammo") && IsOn(t.GetComponent<ZNetView>())
                            && PrivateArea.CheckAccess(t.transform.position, 0f, false, true),
                        FillTurretWhenEmpty);
                }

                if (bursts < MaxBurstsPerPulse && checks < MaxChecksPerPulse)
                {
                    RoundRobinFill(
                        FoodTrays, ref _trayCursor, origin, rangeSq, player, ref bursts, checks,
                        s => s != null && !IsQuiet(s, "tray") && IsOn(s.GetComponent<ZNetView>())
                            && PrivateArea.CheckAccess(s.transform.position, 0f, false, true),
                        FillFoodTrayWhenEmpty);
                }
            }
            finally
            {
                StationFeed.EndAutoFillPulse();
                StationFeed.PullRangeOverride = 0f;
            }
        }

        private static bool AnyOnInRange(Vector3 origin, float rangeSq)
        {
            for (int i = 0; i < Smelters.Count; i++)
            {
                Smelter s = Smelters[i];
                if (s == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, s.transform.position) > rangeSq)
                    continue;
                if (IsOn(s))
                    return true;
            }

            for (int i = 0; i < Fires.Count; i++)
            {
                Fireplace f = Fires[i];
                if (f == null || !f.m_canRefill)
                    continue;
                if (ContainerFilter.SqrDistance(origin, f.transform.position) > rangeSq)
                    continue;
                if (IsOn(f))
                    return true;
            }

            for (int i = 0; i < Ovens.Count; i++)
            {
                CookingStation o = Ovens[i];
                if (o == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, o.transform.position) > rangeSq)
                    continue;
                if (IsOn(o.GetComponent<ZNetView>()))
                    return true;
            }

            for (int i = 0; i < Fermenters.Count; i++)
            {
                Fermenter f = Fermenters[i];
                if (f == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, f.transform.position) > rangeSq)
                    continue;
                if (IsOn(f.GetComponent<ZNetView>()))
                    return true;
            }

            for (int i = 0; i < Turrets.Count; i++)
            {
                Turret t = Turrets[i];
                if (t == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, t.transform.position) > rangeSq)
                    continue;
                if (IsOn(t.GetComponent<ZNetView>()))
                    return true;
            }

            for (int i = 0; i < FoodTrays.Count; i++)
            {
                ItemStand s = FoodTrays[i];
                if (s == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, s.transform.position) > rangeSq)
                    continue;
                if (IsOn(s.GetComponent<ZNetView>()))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Walk the list from a rotating cursor. Only ON / candidate stations consume the check budget.
        /// </summary>
        private static int RoundRobinFill<T>(
            List<T> list,
            ref int cursor,
            Vector3 origin,
            float rangeSq,
            Player player,
            ref int bursts,
            int checks,
            System.Func<T, bool> isCandidate,
            System.Func<T, Player, bool> tryFill) where T : Component
        {
            if (list == null || list.Count == 0 || isCandidate == null || tryFill == null)
                return checks;

            int start = cursor % list.Count;
            for (int n = 0; n < list.Count; n++)
            {
                if (bursts >= MaxBurstsPerPulse || checks >= MaxChecksPerPulse)
                    break;

                int i = (start + n) % list.Count;
                T station = list[i];
                if (station == null)
                    continue;
                if (ContainerFilter.SqrDistance(origin, station.transform.position) > rangeSq)
                    continue;
                if (!isCandidate(station))
                    continue;

                cursor = i + 1;
                checks++;
                StationLink.PushStation(station);
                try
                {
                    if (tryFill(station, player))
                        bursts++;
                }
                finally
                {
                    StationLink.Pop();
                }
            }

            return checks;
        }

        /// <summary>
        /// Manual Shift+[E] fill: top up fuel and/or ore from bag/chests without waiting for auto-fill.
        /// </summary>
        internal static bool ManualFillSmelterToMax(Smelter smelter, Player player, bool fuel, bool ore)
        {
            if (smelter == null || player == null)
                return false;
            StationLink.PushStation(smelter);
            StationFeed.PullRangeOverride = Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : 0f;
            try
            {
                bool did = false;
                if (fuel && HasFuelSlot(smelter))
                    did |= FillFuelToMax(smelter, player);
                if (ore && HasOreSlot(smelter))
                    did |= FillOreToMax(smelter, player);
                return did;
            }
            finally
            {
                StationFeed.PullRangeOverride = 0f;
                StationLink.Pop();
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
            // Fuel works via OnAddFuel or RPC_AddFuel; Switch refs are optional.
            return smelter != null && smelter.m_fuelItem != null && smelter.m_maxFuel > 0;
        }

        private static bool HasOreSlot(Smelter smelter)
        {
            return smelter != null && smelter.m_maxOre > 0
                && smelter.m_conversion != null && smelter.m_conversion.Count > 0;
        }

        private static bool UsePlayerInventory()
        {
            if (StationFeed.ForceChestOnly)
                return false;
            return Plugin.Settings == null || !Plugin.Settings.StationFillSkipInventory.Value;
        }

        private static int AutoFillLocalCount(Player player, string shared)
        {
            if (!UsePlayerInventory())
                return 0;
            return StationFeed.LocalCount(player, shared);
        }

        private static bool AutoFillHasLocalAny(Player player, List<string> sharedNames)
        {
            if (!UsePlayerInventory())
                return false;
            return HasLocalAny(player, sharedNames);
        }

        /// <summary>Vanilla RPC_AddFuel accepts while fuel ≤ max−1.</summary>
        private static int SmelterFuelFree(Smelter smelter)
        {
            if (smelter == null || smelter.m_maxFuel <= 0)
                return 0;
            float fuel = ReadSmelterFuel(smelter);
            // Prior remote bug could push ZDO past max — clamp so we can run again.
            if (fuel > smelter.m_maxFuel)
            {
                WriteSmelterFuel(smelter, smelter.m_maxFuel);
                fuel = smelter.m_maxFuel;
            }
            if (fuel > smelter.m_maxFuel - 1f)
                return 0;
            return Mathf.Max(0, Mathf.FloorToInt(smelter.m_maxFuel - fuel));
        }

        private static int SmelterOreFree(Smelter smelter)
        {
            if (smelter == null || smelter.m_maxOre <= 0)
                return 0;
            int have = Mathf.Max(0, Mathf.RoundToInt(ReadNumber(SmelterGetQueue, smelter)));
            if (have >= smelter.m_maxOre)
                return 0;
            return smelter.m_maxOre - have;
        }

        private static float ReadSmelterFuel(Smelter smelter)
        {
            float viaMethod = ReadNumber(SmelterGetFuel, smelter);
            ZNetView nv = smelter != null ? smelter.GetComponent<ZNetView>() : null;
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return viaMethod;
            if (FuelHash != 0)
                return zdo.GetFloat(FuelHash, viaMethod);
            return zdo.GetFloat("fuel", viaMethod);
        }

        private static void WriteSmelterFuel(Smelter smelter, float fuel)
        {
            if (smelter == null)
                return;
            if (SmelterSetFuel != null)
            {
                try
                {
                    SmelterSetFuel.Invoke(smelter, new object[] { fuel });
                    return;
                }
                catch
                {
                }
            }

            ZNetView nv = smelter.GetComponent<ZNetView>();
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return;
            if (FuelHash != 0)
                zdo.Set(FuelHash, fuel);
            else
                zdo.Set("fuel", fuel);
        }

        private static bool FillFuelToMax(Smelter smelter, Player player)
        {
            string fuel = StationFeed.SharedFrom(smelter.m_fuelItem);
            if (string.IsNullOrEmpty(fuel))
                return false;

            ZNetView nv = smelter.GetComponent<ZNetView>();
            int need = SmelterFuelFree(smelter);
            if (need <= 0)
                return false;

            int added = 0;
            for (int i = 0; i < need; i++)
            {
                // Match vanilla: full when fuel > max−1 (do not consume if RPC would no-op).
                if (ReadSmelterFuel(smelter) > smelter.m_maxFuel - 1f)
                    break;

                if (AutoFillLocalCount(player, fuel) >= 1)
                {
                    if (smelter.m_addWoodSwitch != null
                        && SmelterAddFuel != null
                        && InvokeAdd(SmelterAddFuel, smelter, smelter.m_addWoodSwitch, player))
                    {
                        added++;
                        continue;
                    }

                    // No add-fuel Switch: take from bag and RPC like the chest path.
                    Inventory inv = player.GetInventory();
                    if (inv == null || nv == null || !nv.IsValid() || !PlayerBag.RemoveOneFromBag(inv, fuel))
                        break;
                    if (!nv.IsOwner())
                        nv.ClaimOwnership();
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
                    continue;
                }

                if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                {
                    // Remote misses must not Quiet — that blocks near Auto-fill for 25s.
                    if (!StationFeed.ForceChestOnly)
                        Quiet(smelter, "fuel", QuietEmptySeconds);
                    return false;
                }

                if (nv == null || !nv.IsValid() || StationFeed.ConsumeFromChests(player, fuel, 1) < 1)
                    break;

                if (!nv.IsOwner())
                    nv.ClaimOwnership();
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
                if (!StationFeed.ForceChestOnly)
                    Quiet(smelter, "ore", QuietEmptySeconds);
                return false;
            }

            ZNetView nv = smelter.GetComponent<ZNetView>();
            int need = SmelterOreFree(smelter);
            if (need <= 0)
                return false;

            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (Mathf.RoundToInt(ReadNumber(SmelterGetQueue, smelter)) >= smelter.m_maxOre)
                    break;

                if (AutoFillHasLocalAny(player, allowed))
                {
                    if (smelter.m_addOreSwitch != null
                        && SmelterAddOre != null
                        && InvokeAdd(SmelterAddOre, smelter, smelter.m_addOreSwitch, player))
                    {
                        added++;
                        continue;
                    }

                    ItemDrop.ItemData local = FirstLocal(player, allowed);
                    string sharedLocal = local?.m_shared != null ? local.m_shared.m_name : null;
                    string prefabLocal = local != null ? ItemIds.PrefabName(local) : PrefabName(sharedLocal);
                    if (string.IsNullOrEmpty(prefabLocal))
                        prefabLocal = PrefabName(sharedLocal);
                    Inventory inv = player.GetInventory();
                    if (local == null || inv == null || nv == null || !nv.IsValid()
                        || string.IsNullOrEmpty(prefabLocal) || !inv.RemoveOneItem(local))
                        break;
                    if (!nv.IsOwner())
                        nv.ClaimOwnership();
                    BeginSilence();
                    try
                    {
                        nv.InvokeRPC("RPC_AddOre", prefabLocal, false);
                    }
                    finally
                    {
                        EndSilence();
                    }
                    added++;
                    continue;
                }

                if (added == 0 && !ChestsHaveAny(player, allowed))
                {
                    if (!StationFeed.ForceChestOnly)
                        Quiet(smelter, "ore", QuietEmptySeconds);
                    return false;
                }

                string shared = FirstChestItem(player, allowed);
                string prefab = PrefabName(shared);
                if (nv == null || !nv.IsValid() || string.IsNullOrEmpty(prefab)
                    || StationFeed.ConsumeFromChests(player, shared, 1) < 1)
                    break;

                if (!nv.IsOwner())
                    nv.ClaimOwnership();
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
            if (fire == null)
                return false;
            if (ReadFireFuel(fire) > 0.01f)
                return false;
            return FillFireToMax(fire, player);
        }

        /// <summary>Top up fireplace/torch fuel to m_maxFuel from bag and chests (Shift+[E]).</summary>
        internal static bool ManualFillFireToMax(Fireplace fire, Player player)
        {
            if (fire == null || player == null || !fire.m_canRefill)
                return false;
            StationLink.PushStation(fire);
            StationFeed.PullRangeOverride = Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : 0f;
            try
            {
                return FillFireToMax(fire, player);
            }
            finally
            {
                StationFeed.PullRangeOverride = 0f;
                StationLink.Pop();
            }
        }

        private static bool FillFireToMax(Fireplace fire, Player player)
        {
            ZNetView nv = fire.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return false;

            string fuel = StationFeed.SharedFrom(fire.m_fuelItem);
            if (string.IsNullOrEmpty(fuel) || fire.m_maxFuel <= 0f || fire.m_infiniteFuel)
                return false;

            // Vanilla: add while Ceil(fuel) < m_maxFuel.
            int have = Mathf.Max(0, Mathf.CeilToInt(ReadFireFuel(fire)));
            int max = Mathf.Max(1, Mathf.FloorToInt(fire.m_maxFuel));
            int need = max - have;
            if (need <= 0)
                return false;

            bool remotePull = StationFeed.ForceChestOnly;
            if (!remotePull)
                QuietUntil.Remove(QuietKey(fire, "fire"));

            bool useBag = !remotePull && UsePlayerInventory() && player != null;
            Inventory inv = useBag ? player.GetInventory() : null;
            if (useBag && inv == null)
                return false;

            int added = 0;
            BeginSilence();
            try
            {
                for (int i = 0; i < need; i++)
                {
                    if (Mathf.CeilToInt(ReadFireFuel(fire)) >= max)
                        break;

                    if (inv != null && AutoFillLocalCount(player, fuel) >= 1)
                    {
                        if (!PlayerBag.RemoveOneFromBag(inv, fuel))
                            break;
                    }
                    else
                    {
                        if (added == 0 && !StationFeed.ChestsHave(player, fuel))
                        {
                            if (!remotePull)
                                Quiet(fire, "fire", QuietEmptySeconds);
                            return false;
                        }
                        if (StationFeed.ConsumeFromChests(player, fuel, 1) < 1)
                            break;
                    }

                    if (!nv.IsOwner())
                        nv.ClaimOwnership();
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
            // Free slots, not only fully empty: stone oven / spit can top up after
            // auto-drop clears done food. OnUseItem needs a lit fire under stone ovens;
            // FillOvenFood uses RPC_AddItem so baking can queue without that block.
            if (!IsQuiet(oven, "food") && !IsTrue(CookIsFull, oven))
                did |= FillOvenFood(oven, player);
            return did;
        }

        private static bool FillOvenFuel(CookingStation oven, Player player)
        {
            string fuel = StationFeed.SharedFrom(oven.m_fuelItem);
            if (string.IsNullOrEmpty(fuel))
                return false;

            ZNetView nv = oven.GetComponent<ZNetView>();
            int have = Mathf.Max(0, Mathf.FloorToInt(ReadNumber(CookGetFuel, oven)));
            int need = Mathf.Max(0, oven.m_maxFuel - have);
            if (need <= 0)
                return false;

            int added = 0;
            for (int i = 0; i < need; i++)
            {
                if (ReadNumber(CookGetFuel, oven) > oven.m_maxFuel - 1f)
                    break;

                if (AutoFillLocalCount(player, fuel) >= 1)
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

                if (!nv.IsOwner())
                    nv.ClaimOwnership();
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

        /// <summary>Shift+[E] on cooking fuel: top up wood/fuel to max.</summary>
        internal static bool ManualFillOvenFuelToMax(CookingStation oven, Player player)
        {
            if (oven == null || player == null || !oven.m_useFuel || oven.m_fuelItem == null || oven.m_maxFuel <= 0)
                return false;
            StationLink.PushStation(oven);
            StationFeed.PullRangeOverride = Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : 0f;
            try
            {
                return FillOvenFuel(oven, player);
            }
            finally
            {
                StationFeed.PullRangeOverride = 0f;
                StationLink.Pop();
            }
        }

        private static bool FillOvenFood(CookingStation oven, Player player)
        {
            List<string> foods = CookingOnInteractPatch.FoodNames(oven);
            if (foods == null || foods.Count == 0)
                return false;

            ZNetView nv = oven.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return false;

            // Remote / chest-only: pull straight into the oven via RPC (no bag staging).
            if (!UsePlayerInventory())
                return FillOvenFoodFromChests(oven, player, foods, nv);

            int added = 0;
            int guard = oven.m_slots != null ? oven.m_slots.Length : 5;
            while (guard-- > 0 && !IsTrue(CookIsFull, oven))
            {
                // Same pull as pressing [E]: move one cookable from chests into the bag.
                if (!StationFeed.EnsureAny(player, foods, 1))
                {
                    if (added == 0)
                        Quiet(oven, "food", QuietEmptySeconds);
                    break;
                }

                ItemDrop.ItemData item = FirstLocal(player, foods);
                if (item == null || item.m_shared == null)
                    break;

                StationFeed.EnsureCookDropPrefab(oven, item);

                // Campfire spit / sticks: OnUseItem matches manual [E].
                // Stone oven: OnUseItem can no-op when m_requireFire and the under-fire is out —
                // fall back to RPC_AddItem like before.
                bool placed = CookUseItem != null && InvokeWith(CookUseItem, oven, player, item);
                if (!placed)
                {
                    string prefab = StationFeed.CookPrefabName(oven, item.m_shared.m_name);
                    if (string.IsNullOrEmpty(prefab))
                        prefab = ItemIds.PrefabName(item);
                    if (string.IsNullOrEmpty(prefab))
                        prefab = PrefabName(item.m_shared.m_name);
                    if (string.IsNullOrEmpty(prefab))
                        break;

                    Inventory inv = player.GetInventory();
                    if (inv == null)
                        break;

                    InventoryCountPatches.Skip++;
                    try
                    {
                        if (!inv.RemoveOneItem(item))
                            break;
                    }
                    finally
                    {
                        InventoryCountPatches.Skip--;
                    }

                    if (!nv.IsOwner())
                        nv.ClaimOwnership();

                    BeginSilence();
                    try
                    {
                        nv.InvokeRPC("RPC_AddItem", prefab, false);
                    }
                    finally
                    {
                        EndSilence();
                    }
                }

                added++;
            }

            return added > 0;
        }

        private static bool FillOvenFoodFromChests(
            CookingStation oven,
            Player player,
            List<string> foods,
            ZNetView nv)
        {
            int added = 0;
            int guard = oven.m_slots != null ? oven.m_slots.Length : 5;
            while (guard-- > 0 && !IsTrue(CookIsFull, oven))
            {
                if (!ChestsHaveAny(player, foods))
                {
                    if (added == 0)
                        Quiet(oven, "food", QuietEmptySeconds);
                    break;
                }

                string shared = FirstChestItem(player, foods);
                string prefab = StationFeed.CookPrefabName(oven, shared);
                if (string.IsNullOrEmpty(prefab))
                    prefab = PrefabName(shared);
                if (string.IsNullOrEmpty(prefab) || string.IsNullOrEmpty(shared))
                    break;
                if (StationFeed.ConsumeFromChests(player, shared, 1) < 1)
                    break;

                if (!nv.IsOwner())
                    nv.ClaimOwnership();
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

            if (AutoFillHasLocalAny(player, meads))
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

        private static bool FillTurretWhenEmpty(Turret turret, Player player)
        {
            if (turret == null || player == null || turret.m_maxAmmo <= 0)
                return false;

            int ammo = Mathf.RoundToInt(ReadNumber(TurretGetAmmo, turret));
            if (ammo >= turret.m_maxAmmo)
                return false;

            ZNetView nv = turret.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid())
                return false;

            bool lockType = ammo > 0;
            Inventory inv = player.GetInventory();
            int added = 0;
            int need = turret.m_maxAmmo - ammo;

            for (int i = 0; i < need; i++)
            {
                ItemDrop.ItemData local = null;
                if (UsePlayerInventory() && inv != null && TurretFindAmmo != null)
                {
                    try
                    {
                        local = TurretFindAmmo.Invoke(turret, new object[] { inv, lockType }) as ItemDrop.ItemData;
                    }
                    catch
                    {
                        local = null;
                    }
                }

                if (local != null)
                {
                    if (!InvokeWith(TurretUseItem, turret, player, local))
                    {
                        // Fallback: remove + RPC with prefab name.
                        string prefab = ItemIds.PrefabName(local);
                        if (string.IsNullOrEmpty(prefab) || !inv.RemoveOneItem(local))
                            break;
                        if (!nv.IsOwner())
                            nv.ClaimOwnership();
                        BeginSilence();
                        try
                        {
                            nv.InvokeRPC("RPC_AddAmmo", prefab);
                        }
                        finally
                        {
                            EndSilence();
                        }
                    }
                    added++;
                    lockType = true;
                    continue;
                }

                List<string> allowed = TurretAmmoSharedNames(turret, lockType);
                if (allowed == null || allowed.Count == 0)
                {
                    if (added == 0)
                        Quiet(turret, "ammo", QuietEmptySeconds);
                    break;
                }

                if (!StationFeed.EnsureAny(player, allowed, 1))
                {
                    if (added == 0)
                        Quiet(turret, "ammo", QuietEmptySeconds);
                    break;
                }

                ItemDrop.ItemData pulled = FirstLocal(player, allowed);
                if (pulled == null)
                    break;

                string name = ItemIds.PrefabName(pulled);
                if (string.IsNullOrEmpty(name))
                    name = PrefabName(pulled.m_shared != null ? pulled.m_shared.m_name : null);
                if (string.IsNullOrEmpty(name))
                    break;

                Inventory bag = player.GetInventory();
                if (bag == null || !bag.RemoveOneItem(pulled))
                    break;

                if (!nv.IsOwner())
                    nv.ClaimOwnership();
                BeginSilence();
                try
                {
                    nv.InvokeRPC("RPC_AddAmmo", name);
                }
                finally
                {
                    EndSilence();
                }
                added++;
                lockType = true;
            }

            return added > 0;
        }

        private static List<string> TurretAmmoSharedNames(Turret turret, bool onlyLoadedType)
        {
            var names = new List<string>();
            if (turret == null)
                return names;

            if (onlyLoadedType)
            {
                string loaded = TurretGetAmmoType != null
                    ? TurretGetAmmoType.Invoke(turret, null) as string
                    : null;

                if (!string.IsNullOrEmpty(loaded))
                {
                    // GetAmmoType returns prefab name; map to shared token when possible.
                    GameObject go = ItemIds.PrefabFromToken(loaded);
                    ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
                    string shared = StationFeed.SharedFrom(drop);
                    if (!string.IsNullOrEmpty(shared))
                        names.Add(shared);
                    else
                        names.Add(loaded);
                    return names;
                }
            }

            if (turret.m_defaultAmmo != null)
            {
                string shared = StationFeed.SharedFrom(turret.m_defaultAmmo);
                if (!string.IsNullOrEmpty(shared))
                    names.Add(shared);
            }

            if (turret.m_allowedAmmo != null)
            {
                for (int i = 0; i < turret.m_allowedAmmo.Count; i++)
                {
                    ItemDrop ammoDrop = turret.m_allowedAmmo[i].m_ammo;
                    if (ammoDrop == null)
                        continue;
                    string shared = StationFeed.SharedFrom(ammoDrop);
                    if (string.IsNullOrEmpty(shared) || names.Contains(shared))
                        continue;
                    names.Add(shared);
                }
            }

            return names;
        }

        private static bool FillFoodTrayWhenEmpty(ItemStand stand, Player player)
        {
            if (stand == null || player == null)
                return false;

            if (IsTrue(ItemStandHaveAttachment, stand))
                return false;

            ItemDrop.ItemData local = FindLocalTrayFood(player, stand);
            if (local != null)
            {
                if (InvokeWith(ItemStandUseItem, stand, player, local))
                    return true;
            }

            List<string> foods = TrayFoodSharedNames(stand);
            if (foods.Count == 0)
            {
                // Type-only trays: only bag items that CanAttach; no chest scan without a whitelist.
                if (local == null)
                    Quiet(stand, "tray", QuietEmptySeconds);
                return false;
            }

            if (!StationFeed.EnsureAny(player, foods, 1))
            {
                Quiet(stand, "tray", QuietEmptySeconds);
                return false;
            }

            ItemDrop.ItemData item = FirstLocal(player, foods);
            if (item == null || !TrayCanAttach(stand, item))
            {
                Quiet(stand, "tray", QuietEmptySeconds);
                return false;
            }

            return InvokeWith(ItemStandUseItem, stand, player, item);
        }

        private static List<string> TrayFoodSharedNames(ItemStand stand)
        {
            var names = new List<string>();
            if (stand?.m_supportedItems == null)
                return names;

            for (int i = 0; i < stand.m_supportedItems.Count; i++)
            {
                ItemDrop drop = stand.m_supportedItems[i];
                if (drop?.m_itemData?.m_shared == null)
                    continue;
                if (drop.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
                    continue;
                string shared = StationFeed.SharedFrom(drop);
                if (string.IsNullOrEmpty(shared) || names.Contains(shared))
                    continue;
                names.Add(shared);
            }

            return names;
        }

        private static ItemDrop.ItemData FindLocalTrayFood(Player player, ItemStand stand)
        {
            if (!UsePlayerInventory() || player == null || stand == null)
                return null;
            Inventory inv = player.GetInventory();
            if (inv == null)
                return null;

            List<ItemDrop.ItemData> all = inv.GetAllItems();
            if (all == null)
                return null;

            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData item = all[i];
                if (item?.m_shared == null)
                    continue;
                if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
                    continue;
                if (TrayCanAttach(stand, item))
                    return item;
            }

            return null;
        }

        private static bool TrayCanAttach(ItemStand stand, ItemDrop.ItemData item)
        {
            if (stand == null || item == null || ItemStandCanAttach == null)
                return false;
            try
            {
                object result = ItemStandCanAttach.Invoke(stand, new object[] { item });
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
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
                ItemDrop.ItemData item = PlayerBag.FindInBag(inv, shared);
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
            QuietUntil.Remove(id + ":ammo");
            QuietUntil.Remove(id + ":tray");
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

        private static Turret HoveredTurret()
        {
            return HoveredComponent<Turret>();
        }

        private static ItemStand HoveredFoodTray()
        {
            ItemStand stand = HoveredComponent<ItemStand>();
            return IsFoodServingTray(stand) ? stand : null;
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

            TurretIds.Clear();
            for (int i = Turrets.Count - 1; i >= 0; i--)
            {
                if (Turrets[i] == null)
                {
                    Turrets.RemoveAt(i);
                    continue;
                }
                TurretIds.Add(Turrets[i].GetInstanceID());
            }

            FoodTrayIds.Clear();
            for (int i = FoodTrays.Count - 1; i >= 0; i--)
            {
                if (FoodTrays[i] == null)
                {
                    FoodTrays.RemoveAt(i);
                    continue;
                }
                FoodTrayIds.Add(FoodTrays[i].GetInstanceID());
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
            StationAutoFill.AppendSmelterHover(ref __result, __instance);
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

    [HarmonyPatch(typeof(Turret), "Awake")]
    internal static class TurretAwakeAutoFillPatch
    {
        private static void Postfix(Turret __instance)
        {
            StationAutoFill.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemStand), "Awake")]
    internal static class ItemStandAwakeAutoFillPatch
    {
        private static void Postfix(ItemStand __instance)
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
            StationAutoFill.AppendSmelterHover(ref __result, __instance);
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    internal static class FireplaceHoverAutoFillPatch
    {
        private static void Postfix(Fireplace __instance, ref string __result)
        {
            if (__instance == null || !__instance.m_canRefill)
                return;
            StationAutoFill.AppendFuelStationHover(ref __result, __instance, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetHoverText))]
    internal static class CookingHoverAutoFillPatch
    {
        private static void Postfix(CookingStation __instance, ref string __result)
        {
            // Stone oven: vanilla returns "" when m_addFoodSwitch is set (food = Switch,
            // wood = OnHoverFuelSwitch). Appending here invented a third empty-body hover.
            if (__instance == null || __instance.m_addFoodSwitch != null)
                return;
            if (string.IsNullOrEmpty(__result))
                return;
            StationAutoFill.AppendCookingHover(ref __result, __instance);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnHoverFuelSwitch")]
    internal static class CookingHoverFuelAutoFillPatch
    {
        private static void Postfix(CookingStation __instance, ref string __result)
        {
            StationAutoFill.AppendCookingHover(ref __result, __instance);
        }
    }

    /// <summary>
    /// Stone oven food door uses Switch.m_hoverText, and CookingStation.GetHoverText
    /// returns "" when m_addFoodSwitch is set — so spit hover patches never ran there.
    /// Fuel switch already goes through OnHoverFuelSwitch (do not append twice).
    /// </summary>
    [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
    internal static class CookingSwitchHoverAutoFillPatch
    {
        private static void Postfix(Switch __instance, ref string __result)
        {
            if (__instance == null || string.IsNullOrEmpty(__result))
                return;
            CookingStation oven = __instance.GetComponentInParent<CookingStation>();
            if (oven == null || __instance != oven.m_addFoodSwitch)
                return;
            StationAutoFill.AppendCookingHover(ref __result, oven);
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
    internal static class FermenterHoverAutoFillPatch
    {
        private static void Postfix(Fermenter __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendFuelStationHover(ref __result, __instance, __instance.GetComponent<ZNetView>());
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
    internal static class TurretHoverAutoFillPatch
    {
        private static void Postfix(Turret __instance, ref string __result)
        {
            if (__instance == null)
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>(), includeManualFill: false);
        }
    }

    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
    internal static class ItemStandHoverAutoFillPatch
    {
        private static void Postfix(ItemStand __instance, ref string __result)
        {
            if (__instance == null || !StationAutoFill.IsFoodServingTray(__instance))
                return;
            StationAutoFill.AppendHover(ref __result, __instance.GetComponent<ZNetView>(), includeManualFill: false);
        }
    }
}
