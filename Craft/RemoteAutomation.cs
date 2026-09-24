using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Remote Automation: Toggle (Alt+E) + Link l1–l9. Feeds from / deposits into
    /// matching [lN] chests only. Pauses when any player is in AutoFillRange.
    /// Keep-alive pokes only zones for active remote stations (+ linked chests).
    /// </summary>
    internal static class RemoteAutomation
    {
        public const string ZdoKey = "SAC_remoteAuto";

        private const float Interval = 2.5f;
        private const int MaxStationsPerPulse = 2;
        private const float KeepAliveInterval = 2f;
        private const float AnchorScanInterval = 15f;
        private const string LazyVikingsGuid = "blacks7ar.LazyVikings";

        private static readonly MethodInfo CreateLocalZones =
            AccessTools.Method(typeof(ZoneSystem), "CreateLocalZones", new[] { typeof(Vector3) });
        private static readonly MethodInfo PokeLocalZone =
            AccessTools.Method(typeof(ZoneSystem), "PokeLocalZone");
        private static readonly MethodInfo ZdoFindObjects =
            AccessTools.Method(typeof(ZDOMan), "FindObjects");
        private static readonly MethodInfo SceneCreateObject =
            AccessTools.Method(typeof(ZNetScene), "CreateObject", new[] { typeof(ZDO) });
        private static readonly FieldInfo VisitedSectorIndices =
            AccessTools.Field(typeof(ZDOMan), "m_visitedSectorIndices");
        private static readonly FieldInfo ObjectsById =
            AccessTools.Field(typeof(ZDOMan), "m_objectsByID");
        private static readonly FieldInfo SpawnOreField =
            AccessTools.Field(typeof(ZDOVars), "s_spawnOre");
        private static readonly FieldInfo SpawnAmountField =
            AccessTools.Field(typeof(ZDOVars), "s_spawnAmount");
        private static readonly MethodInfo GetItemConversion =
            AccessTools.Method(typeof(Smelter), "GetItemConversion", new[] { typeof(string) });
        private static readonly FieldInfo ConversionTo =
            AccessTools.Field(AccessTools.Inner(typeof(Smelter), "ItemConversion"), "m_to");

        // After Spawn Prefix holds output, QueueProcessed may clear the ZDO — restore once.
        private static Smelter _holdRestoreSmelter;
        private static string _holdRestoreOre;
        private static int _holdRestoreStack;

        private static float _nextPulse;
        private static float _nextKeepAlive;
        private static float _nextAnchorScan;
        private static float _nextStatusLog;
        private static int _smelterCursor;
        private static int _ovenCursor;
        private static int _fermenterCursor;
        private static int _fireCursor;
        private static bool _lazyWarned;
        private static bool? _lazyPresent;
        /// <summary>Set while ZNetScene.CreateDestroyObjects runs so FindSectorObjects can extend.</summary>
        internal static int ExtendSectorLoad;

        private static readonly List<string> ActivityPulls = new List<string>(8);
        private static readonly List<string> ActivityPushes = new List<string>(4);
        private static bool _activityOpen;
        private static float _nextActivityMessage;
        private static readonly List<Vector3> KeepAlivePoints = new List<Vector3>(32);
        private static readonly List<ZDO> KeepAliveZdos = new List<ZDO>(128);
        private static readonly List<ZDO> KeepAliveZdoScratch = new List<ZDO>(64);
        private static readonly HashSet<ZDOID> KeepAliveZdoIds = new HashSet<ZDOID>();
        private static readonly List<Vector2s> KeepAliveZones = new List<Vector2s>(16);
        private const int ForceCreatesPerTick = 24;
        private static readonly Dictionary<long, Vector3> AnchorsByUid = new Dictionary<long, Vector3>();
        private static readonly Dictionary<long, List<Vector3>> ChestAnchorsByUid =
            new Dictionary<long, List<Vector3>>();

        /// <summary>
        /// Remote Automation UI / runtime gate. Keep false for public builds until
        /// long-range keep-alive is stable — code stays in tree for continued work.
        /// </summary>
        private static bool RemoteUiExposed => false;

        /// <summary>Station filter shows the Remote toggle only when this is true.</summary>
        public static bool UiExposed => RemoteUiExposed;

        public static bool Enabled()
        {
            // Hidden from players for this release (avoid broken distant keep-alive).
            if (!RemoteUiExposed)
                return false;
            if (Plugin.Settings == null || !Plugin.Settings.ModEnabled.Value)
                return false;
            if (!Plugin.Settings.CraftEnabled.Value)
                return false;
            if (!Plugin.Settings.RemoteAutomationEnabled.Value)
                return false;
            if (LazyVikingsPresent())
            {
                if (!_lazyWarned && Plugin.Log != null)
                {
                    _lazyWarned = true;
                    Plugin.Log.LogWarning(
                        "StoreAndCraft Remote Automation is off while LazyVikings is loaded (avoid double feed).");
                }
                return false;
            }
            return true;
        }

        public static bool IsOn(Component station)
        {
            return IsOn(View(station));
        }

        public static bool IsOn(ZNetView nv)
        {
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return false;
            return zdo.GetInt(ZdoKey, 0) != 0;
        }

        public static void SetOn(Component station, bool on)
        {
            ZNetView nv = View(station);
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return;
            if (!nv.IsOwner())
                nv.ClaimOwnership();
            nv.GetZDO().Set(ZdoKey, on ? 1 : 0);
            if (!on)
                ForgetAnchor(nv);
            else
                RememberStation(station);
        }

        public static void Toggle(Component station)
        {
            SetOn(station, !IsOn(station));
        }

        /// <summary>Toggle on AND link ≥ 1.</summary>
        public static bool IsActive(Component station)
        {
            return station != null && IsOn(station) && StationLink.Get(station) >= 1;
        }

        public static void AppendHover(ref string text, Component station)
        {
            if (!Enabled() || station == null || StationLink.Get(station) < 1)
                return;
            if (string.IsNullOrEmpty(text))
                text = "";
            bool on = IsOn(station);
            text += "\n" + Loc.T("Remote Automation", "Remote Automation")
                + " (" + (on ? Loc.T("on", "an") : Loc.T("off", "aus")) + ")";
        }

        public static void Tick()
        {
            if (!Enabled())
                return;

            TickKeepAlive();

            if (!StationFeed.Ready())
                return;

            if (Time.unscaledTime < _nextPulse)
                return;
            _nextPulse = Time.unscaledTime + Interval;

            Player player = Player.m_localPlayer;
            float range = RemoteRange();
            int budget = 0;

            budget = PulseList(StationAutoFill.RegisteredSmelters, ref _smelterCursor, range, budget, player,
                (s, p, r) => ProcessSmelter(s, p, r));
            if (budget < MaxStationsPerPulse)
                budget = PulseList(StationAutoFill.RegisteredOvens, ref _ovenCursor, range, budget, player,
                    (o, p, r) => ProcessOven(o, p, r));
            if (budget < MaxStationsPerPulse)
                budget = PulseList(StationAutoFill.RegisteredFermenters, ref _fermenterCursor, range, budget, player,
                    (f, p, r) => ProcessFermenter(f, p, r));
            if (budget < MaxStationsPerPulse)
                PulseList(StationAutoFill.RegisteredFires, ref _fireCursor, range, budget, player,
                    (f, p, r) =>
                    {
                        if (f != null && f.m_canRefill)
                            ProcessFire(f, p, r);
                    });

            MaybeStatusLog(budget);
        }

        /// <summary>Dedicated host: keep-alive + feed without a local player.</summary>
        public static void TickDedicated()
        {
            if (!Enabled())
                return;
            TickKeepAlive();
            if (!StationFeed.Ready())
                return;
            if (Time.unscaledTime < _nextPulse)
                return;
            _nextPulse = Time.unscaledTime + Interval;

            float range = RemoteRange();
            int budget = 0;
            budget = PulseList(StationAutoFill.RegisteredSmelters, ref _smelterCursor, range, budget, null,
                (s, p, r) => ProcessSmelter(s, p, r));
            if (budget < MaxStationsPerPulse)
                budget = PulseList(StationAutoFill.RegisteredOvens, ref _ovenCursor, range, budget, null,
                    (o, p, r) => ProcessOven(o, p, r));
            if (budget < MaxStationsPerPulse)
                budget = PulseList(StationAutoFill.RegisteredFermenters, ref _fermenterCursor, range, budget, null,
                    (f, p, r) => ProcessFermenter(f, p, r));
            if (budget < MaxStationsPerPulse)
                PulseList(StationAutoFill.RegisteredFires, ref _fireCursor, range, budget, null,
                    (f, p, r) =>
                    {
                        if (f != null && f.m_canRefill)
                            ProcessFire(f, p, r);
                    });

            MaybeStatusLog(budget);
        }

        private static void MaybeStatusLog(int pulsed)
        {
            if (Plugin.Log == null || AnchorsByUid.Count == 0)
                return;
            if (Time.unscaledTime < _nextStatusLog)
                return;
            _nextStatusLog = Time.unscaledTime + 15f;
            Plugin.Log.LogInfo(
                "Remote Automation: anchors=" + AnchorsByUid.Count
                + " zdos=" + KeepAliveZdos.Count
                + " pulsed=" + pulsed
                + " smeltersLoaded=" + CountLoaded(StationAutoFill.RegisteredSmelters));
        }

        private static int CountLoaded<T>(IReadOnlyList<T> list) where T : Component
        {
            if (list == null)
                return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                T c = list[i];
                if (c != null && c.gameObject != null && c.gameObject.scene.IsValid() && IsActive(c))
                    n++;
            }
            return n;
        }

        internal static bool HasKeepAliveAnchors()
        {
            return Enabled() && AnchorsByUid.Count > 0;
        }

        /// <summary>
        /// Cheap: add cached keep-alive ZDOs into vanilla's create/destroy list so
        /// RemoveObjects does not wipe remote stations. Cache is rebuilt on keep-alive tick.
        /// </summary>
        internal static void AppendKeepAliveSectorObjects(List<ZDO> sectorObjects)
        {
            if (sectorObjects == null || KeepAliveZdos.Count == 0)
                return;
            for (int i = 0; i < KeepAliveZdos.Count; i++)
            {
                ZDO zdo = KeepAliveZdos[i];
                if (zdo != null)
                    sectorObjects.Add(zdo);
            }
        }

        /// <summary>True when <paramref name="point"/> lies in a keep-alive zone (cached).</summary>
        internal static bool InKeepAliveActiveArea(Vector3 point)
        {
            if (KeepAliveZones.Count == 0)
                return false;
            Vector2s pz = ZoneSystem.GetZone(point);
            for (int i = 0; i < KeepAliveZones.Count; i++)
            {
                if (ZoneChebyshev(pz, KeepAliveZones[i]) <= 1)
                    return true;
            }
            return false;
        }

        private static int ZoneChebyshev(Vector2s a, Vector2s b)
        {
            return Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));
        }

        private static void RebuildKeepAliveZdoCache()
        {
            KeepAliveZdos.Clear();
            KeepAliveZdoIds.Clear();
            KeepAliveZones.Clear();
            if (!HasKeepAliveAnchors() || ZDOMan.instance == null || ZdoFindObjects == null)
                return;
            if (VisitedSectorIndices == null)
                return;

            object scratchVisited;
            try
            {
                scratchVisited = Activator.CreateInstance(VisitedSectorIndices.FieldType);
            }
            catch
            {
                return;
            }
            if (scratchVisited == null)
                return;

            float range = RemoteRange() + 4f;
            float rangeSq = range * range;

            void CollectAround(Vector3 origin)
            {
                Vector2s center = ZoneSystem.GetZone(origin);
                if (!KeepAliveZones.Contains(center))
                    KeepAliveZones.Add(center);
                // Only the station/chest zone ±1 — enough for the piece itself.
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var zone = new Vector2s(center.x + dx, center.y + dy);
                        if (!KeepAliveZones.Contains(zone))
                            KeepAliveZones.Add(zone);
                        KeepAliveZdoScratch.Clear();
                        try
                        {
                            ZdoFindObjects.Invoke(
                                ZDOMan.instance,
                                new object[] { zone, KeepAliveZdoScratch, scratchVisited });
                        }
                        catch
                        {
                            continue;
                        }
                        for (int i = 0; i < KeepAliveZdoScratch.Count; i++)
                        {
                            ZDO zdo = KeepAliveZdoScratch[i];
                            if (!IsSpawnableKeepAliveZdo(zdo))
                            {
                                // Quietly purge leftovers that cause Missing prefab hash: -1.
                                if (zdo != null && ZNet.instance != null && ZNet.instance.IsServer()
                                    && ZDOMan.instance != null)
                                {
                                    int prefab = 0;
                                    try { prefab = zdo.GetPrefab(); } catch { continue; }
                                    if (prefab == 0 || prefab == -1)
                                    {
                                        try { ZDOMan.instance.DestroyZDO(zdo); } catch { }
                                    }
                                }
                                continue;
                            }
                            // Only remote stations + linked chests — never every rock/piece.
                            if (!IsRemoteKeepAliveTarget(zdo))
                                continue;
                            Vector3 p = zdo.GetPosition();
                            float dxw = p.x - origin.x;
                            float dzw = p.z - origin.z;
                            if (dxw * dxw + dzw * dzw > rangeSq)
                                continue;
                            if (!KeepAliveZdoIds.Add(zdo.m_uid))
                                continue;
                            KeepAliveZdos.Add(zdo);
                        }
                    }
                }
            }

            int cap = MaxKeepAlive();
            int n = 0;
            foreach (KeyValuePair<long, Vector3> kv in AnchorsByUid)
            {
                if (n >= cap && cap > 0)
                    break;
                CollectAround(kv.Value);
                List<Vector3> chests;
                if (ChestAnchorsByUid.TryGetValue(kv.Key, out chests) && chests != null)
                {
                    for (int i = 0; i < chests.Count; i++)
                        CollectAround(chests[i]);
                }
                n++;
            }
        }

        private static bool IsRemoteKeepAliveTarget(ZDO zdo)
        {
            if (zdo == null)
                return false;
            if (zdo.GetInt(ZdoKey, 0) != 0)
                return true;
            return zdo.GetInt(StationLink.ZdoKey, 0) >= 1;
        }

        private static bool IsSpawnableKeepAliveZdo(ZDO zdo)
        {
            if (zdo == null || !zdo.IsValid())
                return false;
            ZDOID id = zdo.m_uid;
            if (id.IsNone() || (id.UserID == 0 && id.ID == 0))
                return false;
            int prefab = zdo.GetPrefab();
            if (prefab == 0 || prefab == -1)
                return false;
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
                return false;
            return scene.GetPrefab(prefab) != null;
        }

        /// <summary>
        /// Drop + quietly destroy ZDOs vanilla would log as "Missing prefab hash: -1" /
        /// "Destroyed invalid prefab ZDO:0:0" (leftovers from earlier keep-alive bugs).
        /// </summary>
        internal static void StripInvalidZdos(List<ZDO> list)
        {
            if (list == null || list.Count == 0)
                return;
            bool server = ZNet.instance != null && ZNet.instance.IsServer();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                ZDO zdo = list[i];
                if (IsSpawnableKeepAliveZdo(zdo))
                    continue;
                list.RemoveAt(i);
                if (!server || zdo == null || ZDOMan.instance == null)
                    continue;
                try
                {
                    // Only purge clearly broken prefab markers — never random live pieces.
                    int prefab = 0;
                    try { prefab = zdo.GetPrefab(); } catch { continue; }
                    if (prefab != 0 && prefab != -1)
                        continue;
                    ZDOMan.instance.DestroyZDO(zdo);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Vanilla CreateObjects only spawns ~100/frame nearest the player — remote ZDOs
        /// never win that budget. Force-create missing station/chest instances only.
        /// </summary>
        private static void ForceCreateKeepAliveInstances()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || KeepAliveZdos.Count == 0 || SceneCreateObject == null)
                return;

            int created = 0;
            for (int i = 0; i < KeepAliveZdos.Count && created < ForceCreatesPerTick; i++)
            {
                ZDO zdo = KeepAliveZdos[i];
                if (!IsSpawnableKeepAliveZdo(zdo) || !IsRemoteKeepAliveTarget(zdo))
                    continue;
                try
                {
                    if (scene.HaveInstance(zdo))
                        continue;
                    object go = SceneCreateObject.Invoke(scene, new object[] { zdo });
                    if (go != null)
                        created++;
                }
                catch
                {
                }
            }
        }

        private delegate void StationAction<T>(T station, Player player, float range) where T : Component;

        private static int PulseList<T>(
            IReadOnlyList<T> list,
            ref int cursor,
            float range,
            int budget,
            Player player,
            StationAction<T> action) where T : Component
        {
            if (list == null || list.Count == 0)
                return budget;

            int start = cursor % list.Count;
            for (int n = 0; n < list.Count && budget < MaxStationsPerPulse; n++)
            {
                int i = (start + n) % list.Count;
                T station = list[i];
                if (station == null || !station.gameObject.scene.IsValid())
                    continue;
                if (!IsActive(station))
                    continue;
                if (AnyPlayerNear(station))
                    continue;
                if (!EnsureOwner(station))
                    continue;

                RememberStation(station);
                cursor = i + 1;
                budget++;
                action(station, player, range);
            }
            return budget;
        }

        private static float RemoteRange()
        {
            if (Plugin.Settings == null)
                return 20f;
            return Mathf.Max(1f, Plugin.Settings.RemoteStationRange.Value);
        }

        private static int MaxKeepAlive()
        {
            if (Plugin.Settings == null)
                return 12;
            return Mathf.Clamp(Plugin.Settings.RemoteMaxKeepAliveStations.Value, 0, 64);
        }

        private static bool LazyVikingsPresent()
        {
            if (_lazyPresent.HasValue)
                return _lazyPresent.Value;
            try
            {
                _lazyPresent = BepInEx.Bootstrap.Chainloader.PluginInfos != null
                    && BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(LazyVikingsGuid);
            }
            catch
            {
                _lazyPresent = false;
            }
            return _lazyPresent.Value;
        }

        public static bool AnyPlayerNear(Component station)
        {
            if (station == null)
                return false;
            float autofill = Plugin.Settings != null ? Plugin.Settings.AutoFillRange.Value : 20f;
            float rangeSq = autofill * autofill;
            Vector3 pos = station.transform.position;

            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count == 0)
                return false;
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p == null || p.IsDead())
                    continue;
                if (ContainerFilter.SqrDistance(pos, p.transform.position) <= rangeSq)
                    return true;
            }
            return false;
        }

        private static bool EnsureOwner(Component station)
        {
            ZNetView nv = View(station);
            if (nv == null || !nv.IsValid())
                return false;
            if (!nv.IsOwner())
                nv.ClaimOwnership();
            return nv.IsOwner();
        }

        private static void ProcessSmelter(Smelter smelter, Player player, float range)
        {
            NearbyIndex.ForceRescanAt(smelter.transform.position, range);
            BeginActivity();
            StationFeed.BeginStationPull(smelter, range, chestOnly: true);
            try
            {
                StationAutoFill.RemoteFillSmelter(smelter, player);
            }
            finally
            {
                StationFeed.EndStationPull();
                FlushActivity();
            }
            RefreshChestAnchors(smelter, range);
        }

        private static void ProcessOven(CookingStation oven, Player player, float range)
        {
            NearbyIndex.ForceRescanAt(oven.transform.position, range);
            BeginActivity();
            StationFeed.BeginStationPull(oven, range, chestOnly: true);
            try
            {
                StationAutoFill.RemoteFillOven(oven, player);
            }
            finally
            {
                StationFeed.EndStationPull();
                FlushActivity();
            }
            RefreshChestAnchors(oven, range);
        }

        private static void ProcessFermenter(Fermenter fermenter, Player player, float range)
        {
            NearbyIndex.ForceRescanAt(fermenter.transform.position, range);
            BeginActivity();
            StationFeed.BeginStationPull(fermenter, range, chestOnly: true);
            try
            {
                StationAutoFill.RemoteFillFermenter(fermenter, player);
            }
            finally
            {
                StationFeed.EndStationPull();
                FlushActivity();
            }
            RefreshChestAnchors(fermenter, range);
        }

        private static void ProcessFire(Fireplace fire, Player player, float range)
        {
            NearbyIndex.ForceRescanAt(fire.transform.position, range);
            BeginActivity();
            StationFeed.BeginStationPull(fire, range, chestOnly: true);
            try
            {
                StationAutoFill.RemoteFillFire(fire, player);
            }
            finally
            {
                StationFeed.EndStationPull();
                FlushActivity();
            }
            RefreshChestAnchors(fire, range);
        }

        public enum DepositResult
        {
            PassThrough,
            Deposited,
            Hold
        }

        /// <summary>
        /// Deposit into linked chests, or hold in station when full / no chest.
        /// PassThrough when remote should not intercept (near player, off, AutoDrop).
        /// For smelters, <paramref name="oreOrItem"/> is the conversion key (m_from prefab name);
        /// we resolve and store the output (m_to).
        /// </summary>
        public static DepositResult TryHandleOutput(Component station, string oreOrItem, int stack)
        {
            if (!Enabled() || station == null || stack <= 0 || string.IsNullOrEmpty(oreOrItem))
                return DepositResult.PassThrough;
            if (!IsActive(station))
                return DepositResult.PassThrough;
            if (AnyPlayerNear(station))
                return DepositResult.PassThrough;

            CookingStation oven = station as CookingStation;
            if (oven != null && CookingAutoDrop.IsOn(oven))
                return DepositResult.PassThrough;

            if (!EnsureOwner(station))
                return DepositResult.Hold;

            string depositPrefab = oreOrItem;
            Smelter smelter = station as Smelter;
            if (smelter != null)
            {
                depositPrefab = ResolveSmelterOutputPrefab(smelter, oreOrItem);
                if (string.IsNullOrEmpty(depositPrefab))
                    return DepositResult.Hold;
            }

            ItemDrop.ItemData probe = MakeItem(depositPrefab, stack);
            if (probe == null)
                return DepositResult.Hold;

            float range = RemoteRange();
            Vector3 origin = station.transform.position;
            NearbyIndex.ForceRescanAt(origin, range);
            bool mustExist = Plugin.Settings != null && Plugin.Settings.MustHaveExisting.Value;

            int linkId = StationLink.Get(station);
            Container chest = FindLinkedChest(origin, range, linkId, probe, mustExist);
            if (chest == null && mustExist)
                chest = FindLinkedChest(origin, range, linkId, probe, mustExist: false);
            if (chest == null)
                return DepositResult.Hold;

            Inventory scratch = new Inventory("SAC_Remote", null, 1, 1);
            ItemDrop.ItemData held = scratch.AddItem(
                depositPrefab, stack, probe.m_quality, probe.m_variant, 0L, "", false, false);
            if (held == null)
                return DepositResult.Hold;

            if (!TransferService.StoreItem(chest, scratch, held, stack))
                return DepositResult.Hold;

            ItemDrop.ItemData left = scratch.GetItemAt(0, 0);
            if (left != null && left.m_stack > 0)
                return DepositResult.Hold;

            NotePush(ItemLabelFromPrefab(depositPrefab), stack);
            if (!_activityOpen)
                FlushActivity();
            return DepositResult.Deposited;
        }

        private static void BeginActivity()
        {
            ActivityPulls.Clear();
            ActivityPushes.Clear();
            _activityOpen = true;
        }

        /// <summary>Chest → station while Remote ForceChestOnly pull is active.</summary>
        public static void NotePull(string sharedName, int amount)
        {
            if (!_activityOpen || amount <= 0 || string.IsNullOrEmpty(sharedName))
                return;
            string label = StationPullFilter.DisplayName(sharedName);
            if (string.IsNullOrEmpty(label))
                label = sharedName;
            if (!ActivityPulls.Contains(label))
                ActivityPulls.Add(label);
        }

        private static void NotePush(string label, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(label))
                return;
            if (!_activityOpen)
            {
                ActivityPulls.Clear();
                ActivityPushes.Clear();
                _activityOpen = true;
            }
            if (!ActivityPushes.Contains(label))
                ActivityPushes.Add(label);
        }

        private static void FlushActivity()
        {
            _activityOpen = false;
            if (ActivityPulls.Count == 0 && ActivityPushes.Count == 0)
                return;

            var sb = new System.Text.StringBuilder(96);
            sb.Append(Loc.T("Remote Automation", "Remote Automation"));
            if (ActivityPulls.Count > 0)
            {
                sb.Append('\n');
                sb.Append(Loc.T("Pull", "Pull"));
                sb.Append(' ');
                sb.Append(string.Join(", ", ActivityPulls.ToArray()));
            }
            if (ActivityPushes.Count > 0)
            {
                sb.Append('\n');
                sb.Append(Loc.T("Push", "Push"));
                sb.Append(' ');
                sb.Append(string.Join(", ", ActivityPushes.ToArray()));
            }

            string msg = sb.ToString();
            ActivityPulls.Clear();
            ActivityPushes.Clear();

            if (Plugin.Log != null)
                Plugin.Log.LogInfo(msg.Replace("\n", " | "));

            // Throttle HUD so a busy base does not drown the screen.
            if (Time.unscaledTime < _nextActivityMessage)
                return;
            _nextActivityMessage = Time.unscaledTime + 1.25f;

            Player player = Player.m_localPlayer;
            if (player != null && !player.IsDead())
                player.Message(MessageHud.MessageType.TopLeft, msg, 0, null, false);
        }

        private static string ItemLabelFromPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return "?";
            GameObject go = ObjectDB.instance != null
                ? ObjectDB.instance.GetItemPrefab(prefabName)
                : null;
            if (go == null)
                go = ItemIds.PrefabFromToken(prefabName);
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            string shared = drop?.m_itemData?.m_shared != null
                ? drop.m_itemData.m_shared.m_name
                : null;
            if (!string.IsNullOrEmpty(shared))
                return StationPullFilter.DisplayName(shared);
            return prefabName;
        }

        /// <summary>Smelter Spawn/Queue use m_from prefab names; chests need m_to (bars, coal, …).</summary>
        private static string ResolveSmelterOutputPrefab(Smelter smelter, string oreName)
        {
            if (smelter == null || string.IsNullOrEmpty(oreName) || GetItemConversion == null)
                return null;
            try
            {
                object conv = GetItemConversion.Invoke(smelter, new object[] { oreName });
                if (conv == null || ConversionTo == null)
                    return null;
                ItemDrop to = ConversionTo.GetValue(conv) as ItemDrop;
                if (to == null)
                    return null;
                return ItemIds.StripClone(to.gameObject.name);
            }
            catch
            {
                return null;
            }
        }

        private static ItemDrop.ItemData MakeItem(string prefabName, int stack)
        {
            GameObject go = ObjectDB.instance != null
                ? ObjectDB.instance.GetItemPrefab(prefabName)
                : null;
            if (go == null)
                go = ItemIds.PrefabFromToken(prefabName);
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop?.m_itemData == null)
                return null;
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_stack = Mathf.Max(1, stack);
            item.m_dropPrefab = go;
            return item;
        }

        private static Container FindLinkedChest(
            Vector3 origin,
            float range,
            int linkId,
            ItemDrop.ItemData item,
            bool mustExist)
        {
            Container best = null;
            float bestDist = float.MaxValue;
            float rangeSq = range * range;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null || !StationLink.ChestAllowed(chest, linkId))
                    continue;
                if (ContainerFilter.SqrDistance(origin, chest.transform.position) > rangeSq)
                    continue;
                if (!ChestPicker.CanAccept(chest, item, origin, mustExist))
                    continue;

                float d = ContainerFilter.Distance(origin, chest.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = chest;
                }
            }

            return best;
        }

        private static void RememberStation(Component station)
        {
            ZNetView nv = View(station);
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return;
            long uid = AnchorKey(zdo);
            AnchorsByUid[uid] = station.transform.position;
        }

        private static void ForgetAnchor(ZNetView nv)
        {
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return;
            long uid = AnchorKey(zdo);
            AnchorsByUid.Remove(uid);
            ChestAnchorsByUid.Remove(uid);
        }

        private static void RefreshChestAnchors(Component station, float range)
        {
            ZNetView nv = View(station);
            ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
                return;

            int linkId = StationLink.Get(station);
            if (linkId < 1)
                return;

            long uid = AnchorKey(zdo);
            List<Vector3> list;
            if (!ChestAnchorsByUid.TryGetValue(uid, out list) || list == null)
            {
                list = new List<Vector3>(4);
                ChestAnchorsByUid[uid] = list;
            }
            else
                list.Clear();

            float rangeSq = range * range;
            Vector3 origin = station.transform.position;
            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null || !StationLink.ChestAllowed(chest, linkId))
                    continue;
                if (ContainerFilter.SqrDistance(origin, chest.transform.position) > rangeSq)
                    continue;
                list.Add(chest.transform.position);
            }
        }

        private static void TickKeepAlive()
        {
            if (Time.unscaledTime >= _nextAnchorScan)
            {
                _nextAnchorScan = Time.unscaledTime + AnchorScanInterval;
                ScanZdoAnchors();
            }

            if (Time.unscaledTime < _nextKeepAlive)
                return;
            _nextKeepAlive = Time.unscaledTime + KeepAliveInterval;

            PruneInactiveAnchors();

            KeepAlivePoints.Clear();
            int cap = MaxKeepAlive();
            if (cap <= 0)
                return;

            int n = 0;
            foreach (KeyValuePair<long, Vector3> kv in AnchorsByUid)
            {
                if (n >= cap)
                    break;
                KeepAlivePoints.Add(kv.Value);
                List<Vector3> chests;
                if (ChestAnchorsByUid.TryGetValue(kv.Key, out chests) && chests != null)
                {
                    for (int i = 0; i < chests.Count; i++)
                        KeepAlivePoints.Add(chests[i]);
                }
                n++;
            }

            ZoneSystem zs = ZoneSystem.instance;
            if (zs == null || KeepAlivePoints.Count == 0)
                return;

            for (int i = 0; i < KeepAlivePoints.Count; i++)
                PokeZone(zs, KeepAlivePoints[i]);

            RebuildKeepAliveZdoCache();
            ForceCreateKeepAliveInstances();
        }

        private static void PruneInactiveAnchors()
        {
            if (AnchorsByUid.Count == 0 || ObjectsById == null || ZDOMan.instance == null)
                return;

            var dict = ObjectsById.GetValue(ZDOMan.instance) as Dictionary<ZDOID, ZDO>;
            if (dict == null)
                return;

            var byKey = new Dictionary<long, ZDO>();
            foreach (KeyValuePair<ZDOID, ZDO> pair in dict)
            {
                if (pair.Value == null)
                    continue;
                byKey[AnchorKey(pair.Value)] = pair.Value;
            }

            var remove = new List<long>();
            var refresh = new List<KeyValuePair<long, Vector3>>();
            foreach (KeyValuePair<long, Vector3> kv in AnchorsByUid)
            {
                ZDO zdo;
                if (!byKey.TryGetValue(kv.Key, out zdo) || zdo == null)
                    continue; // not in memory — keep cached anchor
                if (zdo.GetInt(ZdoKey, 0) == 0 || zdo.GetInt(StationLink.ZdoKey, 0) < 1)
                    remove.Add(kv.Key);
                else
                    refresh.Add(new KeyValuePair<long, Vector3>(kv.Key, zdo.GetPosition()));
            }
            for (int i = 0; i < refresh.Count; i++)
                AnchorsByUid[refresh[i].Key] = refresh[i].Value;
            for (int i = 0; i < remove.Count; i++)
            {
                AnchorsByUid.Remove(remove[i]);
                ChestAnchorsByUid.Remove(remove[i]);
            }
        }

        private static void ScanZdoAnchors()
        {
            if (ZDOMan.instance == null || ObjectsById == null)
                return;

            var dict = ObjectsById.GetValue(ZDOMan.instance) as Dictionary<ZDOID, ZDO>;
            if (dict == null)
                return;

            int cap = MaxKeepAlive();
            if (AnchorsByUid.Count >= cap && cap > 0)
                return;

            foreach (KeyValuePair<ZDOID, ZDO> kv in dict)
            {
                ZDO zdo = kv.Value;
                if (zdo == null)
                    continue;
                if (zdo.GetInt(ZdoKey, 0) == 0)
                    continue;
                if (zdo.GetInt(StationLink.ZdoKey, 0) < 1)
                    continue;
                long uid = AnchorKey(zdo);
                if (!AnchorsByUid.ContainsKey(uid))
                    AnchorsByUid[uid] = zdo.GetPosition();
                if (AnchorsByUid.Count >= Mathf.Max(cap, 1) * 2)
                    break;
            }
        }

        private static long AnchorKey(ZDO zdo)
        {
            ZDOID id = zdo.m_uid;
            return (id.UserID << 32) ^ unchecked((long)id.ID);
        }

        private static void PokeZone(ZoneSystem zs, Vector3 pos)
        {
            try
            {
                // Prefer narrow poke; CreateLocalZones also ensures the zone actually loads.
                if (PokeLocalZone != null)
                {
                    object zoneId = ZoneSystem.GetZone(pos);
                    PokeLocalZone.Invoke(zs, new[] { zoneId });
                }
                if (CreateLocalZones != null)
                    CreateLocalZones.Invoke(zs, new object[] { pos });
            }
            catch
            {
                // Zone APIs differ slightly across patches; skip quietly.
            }
        }

        internal static void NoteHoldAfterSpawn(Smelter smelter, string ore, int stack)
        {
            _holdRestoreSmelter = smelter;
            _holdRestoreOre = ore;
            _holdRestoreStack = stack;
        }

        internal static void ApplyPendingHoldRestore(Smelter smelter)
        {
            if (_holdRestoreSmelter == null || smelter == null || _holdRestoreSmelter != smelter)
                return;
            string ore = _holdRestoreOre;
            int stack = _holdRestoreStack;
            _holdRestoreSmelter = null;
            _holdRestoreOre = null;
            _holdRestoreStack = 0;
            BufferOutput(smelter, ore, stack);
        }

        internal static bool TryClearSpawnBuffer(Smelter smelter)
        {
            ZDO zdo = SpawnZdo(smelter);
            if (zdo == null)
                return false;
            int oreHash = SpawnOreHash();
            int amountHash = SpawnAmountHash();
            if (oreHash == 0 || amountHash == 0)
                return false;
            zdo.Set(oreHash, "");
            zdo.Set(amountHash, 0, false);
            return true;
        }

        internal static bool TryReadSpawnBuffer(Smelter smelter, out string name, out int amount)
        {
            name = null;
            amount = 0;
            ZDO zdo = SpawnZdo(smelter);
            if (zdo == null)
                return false;
            int oreHash = SpawnOreHash();
            int amountHash = SpawnAmountHash();
            if (oreHash == 0 || amountHash == 0)
                return false;
            amount = zdo.GetInt(amountHash, 0);
            name = zdo.GetString(oreHash, "");
            return amount > 0 && !string.IsNullOrEmpty(name);
        }

        internal static void BufferOutput(Smelter smelter, string name, int stack)
        {
            ZDO zdo = SpawnZdo(smelter);
            if (zdo == null || string.IsNullOrEmpty(name) || stack <= 0)
                return;
            int oreHash = SpawnOreHash();
            int amountHash = SpawnAmountHash();
            if (oreHash == 0 || amountHash == 0)
                return;

            string existing = zdo.GetString(oreHash, "");
            int amount = zdo.GetInt(amountHash, 0);
            if (!string.IsNullOrEmpty(existing) && existing != name)
            {
                // Different product waiting — leave existing; drop this via vanilla is worse;
                // keep existing and ignore new (will retry next cycle when deposited).
                return;
            }
            zdo.Set(oreHash, name);
            zdo.Set(amountHash, amount + stack, false);
        }

        private static ZDO SpawnZdo(Smelter smelter)
        {
            ZNetView nv = smelter != null ? smelter.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid())
                return null;
            return nv.GetZDO();
        }

        private static int SpawnOreHash()
        {
            return SpawnOreField != null ? (int)SpawnOreField.GetValue(null) : 0;
        }

        private static int SpawnAmountHash()
        {
            return SpawnAmountField != null ? (int)SpawnAmountField.GetValue(null) : 0;
        }

        private static ZNetView View(Component c)
        {
            return c != null ? c.GetComponent<ZNetView>() : null;
        }
    }

    [HarmonyPatch(typeof(Smelter), "QueueProcessed")]
    internal static class RemoteSmelterQueueProcessedPatch
    {
        private static bool Prefix(Smelter __instance, string ore)
        {
            if (__instance == null || __instance.m_spawnStack || string.IsNullOrEmpty(ore))
                return true;
            if (!RemoteAutomation.IsActive(__instance) || RemoteAutomation.AnyPlayerNear(__instance))
                return true;

            RemoteAutomation.DepositResult result =
                RemoteAutomation.TryHandleOutput(__instance, ore, 1);
            if (result == RemoteAutomation.DepositResult.PassThrough)
                return true;
            if (result == RemoteAutomation.DepositResult.Deposited)
                return false;
            RemoteAutomation.BufferOutput(__instance, ore, 1);
            return false;
        }

        private static void Postfix(Smelter __instance)
        {
            // Spawn Prefix may have held output; vanilla then cleared the ZDO — put it back.
            RemoteAutomation.ApplyPendingHoldRestore(__instance);
        }
    }

    [HarmonyPatch(typeof(Smelter), "Spawn")]
    internal static class RemoteSmelterSpawnPatch
    {
        private static bool Prefix(Smelter __instance, string ore, int stack)
        {
            if (__instance == null || !RemoteAutomation.IsActive(__instance))
                return true;
            if (RemoteAutomation.AnyPlayerNear(__instance))
                return true;

            RemoteAutomation.DepositResult result =
                RemoteAutomation.TryHandleOutput(__instance, ore, stack);
            if (result == RemoteAutomation.DepositResult.PassThrough)
                return true;
            if (result == RemoteAutomation.DepositResult.Deposited)
                return false;
            // Hold: caller (QueueProcessed / SpawnProcessed) may clear ZDO after Spawn returns.
            RemoteAutomation.NoteHoldAfterSpawn(__instance, ore, stack);
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "SpawnProcessed")]
    internal static class RemoteSmelterSpawnProcessedPatch
    {
        private static bool Prefix(Smelter __instance)
        {
            if (__instance == null || !RemoteAutomation.IsActive(__instance))
                return true;
            if (RemoteAutomation.AnyPlayerNear(__instance))
                return true;
            if (!RemoteAutomation.TryReadSpawnBuffer(__instance, out string ore, out int amount))
                return true;

            RemoteAutomation.DepositResult result =
                RemoteAutomation.TryHandleOutput(__instance, ore, amount);
            if (result == RemoteAutomation.DepositResult.PassThrough)
                return true;
            if (result == RemoteAutomation.DepositResult.Deposited)
            {
                RemoteAutomation.TryClearSpawnBuffer(__instance);
                return false;
            }
            // Hold: leave buffer intact (do not call Spawn / clear).
            return false;
        }

        private static void Postfix(Smelter __instance)
        {
            RemoteAutomation.ApplyPendingHoldRestore(__instance);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "SpawnItem")]
    internal static class RemoteCookingSpawnPatch
    {
        private static bool Prefix(CookingStation __instance, string name)
        {
            if (__instance == null || string.IsNullOrEmpty(name))
                return true;
            RemoteAutomation.DepositResult result =
                RemoteAutomation.TryHandleOutput(__instance, name, 1);
            if (result == RemoteAutomation.DepositResult.PassThrough)
                return true;
            if (result == RemoteAutomation.DepositResult.Deposited)
                return false;
            // Hold: skip ground spawn to avoid AutoIntake into unlinked chests.
            return false;
        }
    }

    /// <summary>
    /// Vanilla CreateDestroyObjects only loads ZDOs near the player. While it runs,
    /// extend FindSectorObjects with remote keep-alive anchors so stations stay alive
    /// many zones away.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
    internal static class RemoteCreateDestroyObjectsPatch
    {
        private static void Prefix()
        {
            if (RemoteAutomation.HasKeepAliveAnchors())
                RemoteAutomation.ExtendSectorLoad++;
        }

        private static void Postfix()
        {
            if (RemoteAutomation.ExtendSectorLoad > 0)
                RemoteAutomation.ExtendSectorLoad--;
        }
    }

    /// <summary>
    /// Remove broken ZDOs before CreateObjectsSorted can spam
    /// "Missing prefab hash: -1" / "Destroyed invalid prefab ZDO:0:0".
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateObjects")]
    internal static class RemoteCreateObjectsStripPatch
    {
        private static void Prefix(List<ZDO> currentNearObjects, List<ZDO> currentDistantObjects)
        {
            RemoteAutomation.StripInvalidZdos(currentNearObjects);
            RemoteAutomation.StripInvalidZdos(currentDistantObjects);
        }
    }

    [HarmonyPatch(typeof(ZDOMan), "FindSectorObjects")]
    internal static class RemoteFindSectorObjectsPatch
    {
        private static void Postfix(List<ZDO> sectorObjects)
        {
            if (RemoteAutomation.ExtendSectorLoad <= 0)
                return;
            RemoteAutomation.AppendKeepAliveSectorObjects(sectorObjects);
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OutsideActiveArea), typeof(Vector3))]
    internal static class RemoteOutsideActiveAreaPatch
    {
        private static void Postfix(Vector3 point, ref bool __result)
        {
            if (!__result)
                return;
            if (RemoteAutomation.InKeepAliveActiveArea(point))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.InActiveArea), typeof(Vector3), typeof(Vector3))]
    internal static class RemoteInActiveAreaPatch
    {
        private static void Postfix(Vector3 point, Vector3 centerPosition, ref bool __result)
        {
            if (__result)
                return;
            if (RemoteAutomation.InKeepAliveActiveArea(point))
                __result = true;
        }
    }
}
