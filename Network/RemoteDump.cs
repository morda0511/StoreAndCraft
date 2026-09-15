using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// EXPERIMENTAL: remote inventory dump via a buddy who has base chests loaded.
    ///
    /// Easy removal:
    /// 1. Delete this file.
    /// 2. Remove every block marked BEGIN REMOTE_DUMP … END REMOTE_DUMP
    ///    (InventoryDump, Plugin, ModConfig, ConfigSync/NetworkPatches, TransferService).
    /// 3. Rebuild. No other feature depends on this.
    /// </summary>
    internal static class RemoteDump
    {
        public const string RpcPing = "KAC_RDPing";
        public const string RpcPong = "KAC_RDPong";
        public const string RpcReq = "KAC_RDReq";
        public const string RpcRes = "KAC_RDRes";

        private static bool _registered;
        private static float _cooldownUntil;
        private static PendingRequest _pending;
        private static RelayJob _relayJob;
        private static int _nextRequestId = 1;

        private struct StackPkg
        {
            public string Prefab;
            public int Stack;
            public int Quality;
            public int Variant;
            public long CrafterId;
            public string CrafterName;
            public int WorldLevel;
            public Vector2i GridPos;
        }

        private sealed class PendingRequest
        {
            public int RequestId;
            public float PingDeadline;
            public float ResultDeadline;
            public bool SentReq;
            public long BestRelay;
            public int BestChests;
            public List<StackPkg> Stacks;
        }

        private sealed class RelayJob
        {
            public long Requester;
            public int RequestId;
            public float Deadline;
            public Queue<StackPkg> Stacks;
            public int Stored;
        }

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register(RpcPing, RPC_Ping);
            ZRoutedRpc.instance.Register<int>(RpcPong, RPC_Pong);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReq, RPC_Req);
            ZRoutedRpc.instance.Register<ZPackage>(RpcRes, RPC_Res);
            _registered = true;
            Plugin.Log.LogInfo("StoreAndCraft RemoteDump RPCs registered (experimental).");
        }

        public static void Tick()
        {
            if (!Enabled())
            {
                _pending = null;
                _relayJob = null;
                return;
            }

            TickPending();
            TickRelayJob();
        }

        /// <summary>
        /// After a local dump/store found nothing (or left leftovers), try buddy relay.
        /// Returns true if a remote attempt was started (caller should skip "nothing" message).
        /// </summary>
        public static bool TryStartFromDump(Inventory inv, List<ItemDrop.ItemData> candidates)
        {
            if (!Enabled() || inv == null || candidates == null || candidates.Count == 0)
                return false;
            if (ZRoutedRpc.instance == null || ZNet.instance == null)
                return false;
            if (_pending != null)
                return false;
            if (Time.time < _cooldownUntil)
            {
                Message(Loc.T("Remote dump cooling down.", "Remote-Dump noch abkühlen."));
                return true;
            }

            int maxStacks = MaxStacks();
            var packs = new List<StackPkg>(Mathf.Min(candidates.Count, maxStacks));
            foreach (ItemDrop.ItemData item in candidates)
            {
                if (packs.Count >= maxStacks)
                    break;
                if (!InventoryDump.ShouldDump(item, inv))
                    continue;
                StackPkg pkg;
                if (!TryPack(item, out pkg))
                    continue;
                packs.Add(pkg);
            }

            if (packs.Count == 0)
                return false;

            if (!AnyOtherPeer())
            {
                Message(Loc.T(
                    "No buddy near base chests (need someone loading that area).",
                    "Kein Buddy bei den Base-Kisten (jemand muss die Area laden)."));
                return true;
            }

            _pending = new PendingRequest
            {
                RequestId = _nextRequestId++,
                PingDeadline = Time.time + 0.85f,
                ResultDeadline = 0f,
                SentReq = false,
                BestRelay = 0,
                BestChests = 0,
                Stacks = packs
            };

            BroadcastPing();
            Message(Loc.T("Looking for a buddy near chests…", "Suche Buddy bei Kisten…"));
            return true;
        }

        public static bool TryStartOne(Inventory inv, ItemDrop.ItemData item)
        {
            if (item == null)
                return false;
            var list = new List<ItemDrop.ItemData>(1) { item };
            return TryStartFromDump(inv, list);
        }

        private static void TickPending()
        {
            if (_pending == null)
                return;

            if (!_pending.SentReq)
            {
                if (Time.time <= _pending.PingDeadline)
                    return;

                if (_pending.BestRelay == 0 || _pending.BestChests <= 0)
                {
                    Message(Loc.T(
                        "No buddy near base chests.",
                        "Kein Buddy bei den Base-Kisten."));
                    _pending = null;
                    _cooldownUntil = Time.time + Cooldown();
                    return;
                }

                if (!CommitAndSend(_pending))
                {
                    Message(Loc.T("Remote dump failed.", "Remote-Dump fehlgeschlagen."));
                    _pending = null;
                    _cooldownUntil = Time.time + Cooldown();
                    return;
                }

                _pending.SentReq = true;
                _pending.ResultDeadline = Time.time + 12f;
                return;
            }

            if (Time.time > _pending.ResultDeadline)
            {
                Message(Loc.T(
                    "Remote dump timed out (check inventory / refunds).",
                    "Remote-Dump Timeout (Inventar / Refunds prüfen)."));
                _pending = null;
                _cooldownUntil = Time.time + Cooldown();
            }
        }

        private static void TickRelayJob()
        {
            if (_relayJob == null || _relayJob.Stacks == null)
                return;

            if (Time.time > _relayJob.Deadline)
            {
                RefundRemaining(_relayJob);
                SendResult(_relayJob.Requester, _relayJob.RequestId, _relayJob.Stored, true);
                _relayJob = null;
                return;
            }

            Player local = Player.m_localPlayer;
            if (local == null)
            {
                RefundRemaining(_relayJob);
                SendResult(_relayJob.Requester, _relayJob.RequestId, _relayJob.Stored, true);
                _relayJob = null;
                return;
            }

            NearbyIndex.Tick();
            int budget = Plugin.Settings != null ? Plugin.Settings.MaxTransfersPerTick.Value : 8;
            int n = 0;
            while (_relayJob.Stacks.Count > 0 && n < budget)
            {
                StackPkg stack = _relayJob.Stacks.Dequeue();
                if (ApplyOne(local, _relayJob.Requester, stack))
                    _relayJob.Stored++;
                n++;
            }

            if (_relayJob.Stacks.Count == 0)
            {
                SendResult(_relayJob.Requester, _relayJob.RequestId, _relayJob.Stored, false);
                _relayJob = null;
            }
        }

        private static bool ApplyOne(Player local, long requester, StackPkg stack)
        {
            ItemDrop.ItemData probe = BuildProbe(stack);
            if (probe == null)
            {
                GrantBack(requester, stack);
                return false;
            }

            Container chest = FindRelayTarget(local.transform.position, probe);
            if (chest == null)
            {
                GrantBack(requester, stack);
                return false;
            }

            int fit = ChestPicker.AmountThatFits(chest.GetInventory(), probe);
            if (fit <= 0)
            {
                GrantBack(requester, stack);
                return false;
            }

            int take = Mathf.Min(fit, stack.Stack);
            if (take <= 0)
            {
                GrantBack(requester, stack);
                return false;
            }

            if (take < stack.Stack)
            {
                StackPkg rest = stack;
                rest.Stack = stack.Stack - take;
                GrantBack(requester, rest);
                stack.Stack = take;
            }

            return TransferService.DepositPackagedForRelay(
                chest,
                requester,
                stack.Prefab,
                stack.Stack,
                stack.Quality,
                stack.Variant,
                stack.CrafterId,
                stack.CrafterName ?? "");
        }

        private static Container FindRelayTarget(Vector3 origin, ItemDrop.ItemData item)
        {
            if (item == null || Plugin.Settings == null)
                return null;

            float range = Mathf.Max(Plugin.Settings.StoreRange.Value, Plugin.Settings.PlayerDumpRange.Value);
            bool mustExist = Plugin.Settings.MustHaveExisting.Value;
            Container best = null;
            float bestDist = float.MaxValue;

            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null)
                    continue;
                float d = ContainerFilter.Distance(origin, chest.transform.position);
                if (d > range)
                    continue;
                if (!ChestPicker.CanAccept(chest, item, origin, mustExist))
                    continue;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = chest;
                }
            }

            return best;
        }

        private static bool CommitAndSend(PendingRequest pending)
        {
            Player player = Player.m_localPlayer;
            Inventory inv = player != null ? player.GetInventory() : null;
            if (inv == null || ZRoutedRpc.instance == null)
                return false;

            var committed = new List<StackPkg>(pending.Stacks.Count);
            foreach (StackPkg want in pending.Stacks)
            {
                ItemDrop.ItemData live = FindMatching(inv, want);
                if (live == null || live.m_stack <= 0)
                    continue;

                int take = Mathf.Min(want.Stack, live.m_stack);
                if (take <= 0)
                    continue;

                StackPkg got = want;
                got.Stack = take;
                got.GridPos = live.m_gridPos;
                inv.RemoveItem(live, take);
                committed.Add(got);
            }

            Refs.NotifyChanged(inv);
            if (committed.Count == 0)
                return false;

            pending.Stacks = committed;
            var pkg = new ZPackage();
            pkg.Write(pending.RequestId);
            pkg.Write(committed.Count);
            foreach (StackPkg s in committed)
                WriteStack(pkg, s);

            ZRoutedRpc.instance.InvokeRoutedRPC(pending.BestRelay, RpcReq, pkg);
            _cooldownUntil = Time.time + Cooldown();
            return true;
        }

        private static ItemDrop.ItemData FindMatching(Inventory inv, StackPkg want)
        {
            if (inv == null)
                return null;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (item == null || item.m_stack <= 0 || item.m_equipped)
                    continue;
                if (Favorites.IsFavorite(item))
                    continue;
                string prefab = ItemIds.PrefabName(item) ?? "";
                if (prefab != want.Prefab)
                    continue;
                if (item.m_quality != want.Quality || item.m_variant != want.Variant)
                    continue;
                if (item.m_crafterID != want.CrafterId)
                    continue;
                return item;
            }

            return null;
        }

        private static void BroadcastPing()
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null)
                return;

            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer == null || !peer.IsReady())
                    continue;
                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, RpcPing);
            }
        }

        private static void RPC_Ping(long sender)
        {
            if (!Enabled() || ZRoutedRpc.instance == null)
                return;
            if (Player.m_localPlayer == null)
                return;

            NearbyIndex.Tick();
            int chests = CountRelayChests(Player.m_localPlayer.transform.position);
            if (chests <= 0)
                return;

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcPong, chests);
        }

        private static void RPC_Pong(long sender, int chestCount)
        {
            if (_pending == null || _pending.SentReq)
                return;
            if (chestCount <= 0)
                return;
            if (chestCount > _pending.BestChests)
            {
                _pending.BestChests = chestCount;
                _pending.BestRelay = sender;
            }
        }

        private static void RPC_Req(long sender, ZPackage pkg)
        {
            if (!Enabled() || pkg == null || Player.m_localPlayer == null)
                return;

            // One job at a time — refund immediately if busy.
            int requestId = pkg.ReadInt();
            int count = pkg.ReadInt();
            if (count <= 0 || count > MaxStacks() + 4)
                return;

            var stacks = new Queue<StackPkg>();
            for (int i = 0; i < count; i++)
                stacks.Enqueue(ReadStack(pkg));

            if (_relayJob != null)
            {
                while (stacks.Count > 0)
                    GrantBack(sender, stacks.Dequeue());
                SendResult(sender, requestId, 0, true);
                return;
            }

            _relayJob = new RelayJob
            {
                Requester = sender,
                RequestId = requestId,
                Deadline = Time.time + 10f,
                Stacks = stacks,
                Stored = 0
            };
        }

        private static void RPC_Res(long sender, ZPackage pkg)
        {
            if (_pending == null || pkg == null)
                return;

            int requestId = pkg.ReadInt();
            if (requestId != _pending.RequestId)
                return;

            int stored = pkg.ReadInt();
            bool timedOut = pkg.ReadBool();
            _pending = null;

            if (stored > 0)
            {
                Message(Loc.T(
                    "Remote stored " + stored + " stacks via buddy.",
                    "Remote: " + stored + " Stapel über Buddy eingelagert."));
            }
            else if (timedOut)
            {
                Message(Loc.T(
                    "Remote dump incomplete (refunds if any).",
                    "Remote-Dump unvollständig (ggf. Refunds)."));
            }
            else
            {
                Message(Loc.T(
                    "Buddy found no matching chest (items returned).",
                    "Buddy: keine passende Kiste (Items zurück)."));
            }
        }

        private static void SendResult(long peer, int requestId, int stored, bool timedOut)
        {
            if (ZRoutedRpc.instance == null)
                return;
            var pkg = new ZPackage();
            pkg.Write(requestId);
            pkg.Write(stored);
            pkg.Write(timedOut);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, RpcRes, pkg);
        }

        private static void RefundRemaining(RelayJob job)
        {
            if (job?.Stacks == null)
                return;
            while (job.Stacks.Count > 0)
                GrantBack(job.Requester, job.Stacks.Dequeue());
        }

        private static void GrantBack(long peer, StackPkg stack)
        {
            TransferService.GrantToPeer(
                peer,
                stack.Prefab,
                stack.Stack,
                stack.Quality,
                stack.Variant,
                stack.CrafterId,
                stack.CrafterName ?? "",
                stack.WorldLevel);
        }

        private static int CountRelayChests(Vector3 origin)
        {
            if (Plugin.Settings == null)
                return 0;
            float range = Mathf.Max(Plugin.Settings.StoreRange.Value, Plugin.Settings.PlayerDumpRange.Value);
            int n = 0;
            foreach (Container chest in NearbyIndex.Current)
            {
                if (chest == null || !ContainerFilter.IsUsable(chest))
                    continue;
                if (!ContainerFilter.IsPlayerBuiltStorage(chest))
                    continue;
                if (ChestNames.IsIgnored(chest))
                    continue;
                if (ContainerFilter.Distance(origin, chest.transform.position) > range)
                    continue;
                n++;
            }

            return n;
        }

        private static bool TryPack(ItemDrop.ItemData item, out StackPkg pkg)
        {
            pkg = default(StackPkg);
            if (item?.m_shared == null || item.m_stack <= 0)
                return false;
            string prefab = ItemIds.PrefabName(item);
            if (string.IsNullOrEmpty(prefab))
                return false;

            pkg = new StackPkg
            {
                Prefab = prefab,
                Stack = item.m_stack,
                Quality = item.m_quality,
                Variant = item.m_variant,
                CrafterId = item.m_crafterID,
                CrafterName = item.m_crafterName ?? "",
                WorldLevel = item.m_worldLevel,
                GridPos = item.m_gridPos
            };
            return true;
        }

        private static void WriteStack(ZPackage pkg, StackPkg s)
        {
            pkg.Write(s.Prefab ?? "");
            pkg.Write(s.Stack);
            pkg.Write(s.Quality);
            pkg.Write(s.Variant);
            pkg.Write(s.CrafterId);
            pkg.Write(s.CrafterName ?? "");
            pkg.Write(s.WorldLevel);
        }

        private static StackPkg ReadStack(ZPackage pkg)
        {
            return new StackPkg
            {
                Prefab = pkg.ReadString(),
                Stack = pkg.ReadInt(),
                Quality = pkg.ReadInt(),
                Variant = pkg.ReadInt(),
                CrafterId = pkg.ReadLong(),
                CrafterName = pkg.ReadString(),
                WorldLevel = pkg.ReadInt()
            };
        }

        private static ItemDrop.ItemData BuildProbe(StackPkg stack)
        {
            GameObject prefab = ItemIds.PrefabFromToken(stack.Prefab);
            if (prefab == null)
                return null;
            ItemDrop.ItemData probe = prefab.GetComponent<ItemDrop>()?.m_itemData?.Clone();
            if (probe == null)
                return null;
            probe.m_stack = stack.Stack;
            probe.m_quality = stack.Quality;
            probe.m_variant = stack.Variant;
            probe.m_crafterID = stack.CrafterId;
            probe.m_crafterName = stack.CrafterName ?? "";
            probe.m_worldLevel = stack.WorldLevel;
            probe.m_dropPrefab = prefab;
            return probe;
        }

        private static bool AnyOtherPeer()
        {
            if (ZNet.instance == null)
                return false;
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer != null && peer.IsReady())
                    return true;
            }

            return false;
        }

        private static bool Enabled()
        {
            return Plugin.Settings != null
                && Plugin.Settings.ModEnabled.Value
                && Plugin.Settings.StoreEnabled.Value
                && Plugin.Settings.RemoteDumpEnabled.Value;
        }

        private static float Cooldown()
        {
            return Plugin.Settings != null ? Mathf.Max(0.5f, Plugin.Settings.RemoteDumpCooldown.Value) : 3f;
        }

        private static int MaxStacks()
        {
            return Plugin.Settings != null ? Mathf.Clamp(Plugin.Settings.RemoteDumpMaxStacks.Value, 1, 64) : 24;
        }

        private static void Message(string text)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;
            player.Message(MessageHud.MessageType.TopLeft, text, 0, null, false);
        }
    }
}
