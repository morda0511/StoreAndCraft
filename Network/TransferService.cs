using System.Collections.Generic;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class TransferService
    {
        public const string RpcRemove = "KAC_Remove";
        public const string RpcDeposit = "KAC_Deposit";
        public const string RpcGrant = "KAC_Grant";
        public const string RpcStoreDrop = "KAC_StoreDrop";

        private static readonly Queue<PendingMove> Queue = new Queue<PendingMove>();
        private static readonly Dictionary<int, float> ClaimThrottle = new Dictionary<int, float>();
        private static bool _grantRegistered;

        private struct PendingMove
        {
            public Container Chest;
            public ItemDrop Drop;
            public ItemDrop.ItemData Item;
            public Inventory From;
            public int Amount;
            public float Deadline;
            public bool Withdraw;
            public string SharedName;
            public bool LeaveOne;
        }

        public static void RegisterGrant()
        {
            if (_grantRegistered || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.Register<ZPackage>(RpcGrant, RPC_Grant);
            _grantRegistered = true;
        }

        public static void Tick()
        {
            int budget = Plugin.Settings != null ? Plugin.Settings.MaxTransfersPerTick.Value : 8;
            int n = 0;
            int safety = Queue.Count;
            while (Queue.Count > 0 && n < budget && safety-- > 0)
            {
                PendingMove op = Queue.Dequeue();
                if (Time.time > op.Deadline)
                    continue;

                if (op.Chest == null || !ContainerFilter.IsUsable(op.Chest))
                    continue;

                if (TryOwner(op.Chest, true))
                {
                    Execute(op);
                    n++;
                }
                else
                {
                    Queue.Enqueue(op);
                    break;
                }
            }
        }

        public static bool StoreItem(Container chest, Inventory from, ItemDrop.ItemData item, int amount)
        {
            if (chest == null || from == null || item == null || amount <= 0)
                return false;

            if (TryOwner(chest, true))
                return DepositLocal(chest, from, item, amount);

            InvokeDeposit(chest, item, amount);
            Enqueue(new PendingMove
            {
                Chest = chest,
                From = from,
                Item = item,
                Amount = amount,
                Deadline = Time.time + 2f
            });
            return true;
        }

        public static bool StoreDrop(Container chest, ItemDrop drop)
        {
            if (chest == null || drop == null || drop.m_itemData == null)
                return false;

            ZNetView dropView = Refs.View(drop);
            if (TryOwner(chest, true) && dropView != null && dropView.IsOwner())
                return StoreDropLocal(chest, drop);

            if (dropView != null && dropView.IsValid())
                drop.RequestOwn();

            ZNetView chestView = Refs.View(chest);
            if (chestView != null && chestView.IsValid() && dropView != null && dropView.GetZDO() != null)
                chestView.InvokeRPC(RpcStoreDrop, dropView.GetZDO().m_uid);

            Enqueue(new PendingMove
            {
                Chest = chest,
                Drop = drop,
                Amount = drop.m_itemData.m_stack,
                Deadline = Time.time + 2.5f
            });
            return true;
        }

        public static int Withdraw(Container chest, string sharedName, int amount, Inventory playerInv, bool leaveOne)
        {
            if (chest == null || playerInv == null || amount <= 0 || string.IsNullOrEmpty(sharedName))
                return 0;

            if (TryOwner(chest, true))
                return WithdrawLocal(chest, sharedName, amount, playerInv, leaveOne);

            ZNetView view = Refs.View(chest);
            if (view != null && view.IsValid())
                view.InvokeRPC(RpcRemove, sharedName, amount, leaveOne ? 1 : 0);
            Enqueue(new PendingMove
            {
                Chest = chest,
                From = playerInv,
                SharedName = sharedName,
                Amount = amount,
                LeaveOne = leaveOne,
                Withdraw = true,
                Deadline = Time.time + 2f
            });
            return 0;
        }

        public static void RegisterOn(Container container)
        {
            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid())
                return;

            TryRegister(nv, RpcRemove, () => nv.Register<string, int, int>(RpcRemove,
                (long sender, string name, int amount, int leaveOne) =>
                    OnRemove(container, sender, name, amount, leaveOne != 0)));

            TryRegister(nv, RpcDeposit, () => nv.Register<ZPackage>(RpcDeposit,
                (long sender, ZPackage pkg) => OnDeposit(container, sender, pkg)));

            TryRegister(nv, RpcStoreDrop, () => nv.Register<ZDOID>(RpcStoreDrop,
                (long sender, ZDOID dropId) => OnStoreDrop(container, sender, dropId)));
        }

        private static void TryRegister(ZNetView nv, string name, System.Action register)
        {
            try
            {
                register();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug("RPC register " + name + ": " + ex.Message);
            }
        }

        internal static void OnRemove(Container container, long sender, string sharedName, int amount, bool leaveOne)
        {
            ZNetView nv = Refs.View(container);
            if (container == null || nv == null || !nv.IsOwner() || amount <= 0)
                return;
            if (!ValidateRpc(container, sender, Plugin.Settings != null ? Plugin.Settings.CraftRange.Value : 20f))
                return;
            if (!RulesFile.AllowsCraft(ContainerFilter.PiecePrefab(container), sharedName))
                return;

            Inventory inv = container.GetInventory();
            if (inv == null)
                return;

            int available = inv.CountItems(sharedName, -1, true);
            if (leaveOne && available > 0)
                available -= 1;
            int take = Mathf.Min(amount, available);
            if (take <= 0)
                return;

            inv.RemoveItem(sharedName, take, -1, true);
            var pkg = new ZPackage();
            pkg.Write(sharedName);
            pkg.Write(take);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcGrant, pkg);
        }

        internal static void OnDeposit(Container container, long sender, ZPackage pkg)
        {
            ZNetView nv = Refs.View(container);
            if (container == null || nv == null || !nv.IsOwner() || pkg == null)
                return;
            if (!ValidateRpc(container, sender, MaxStoreRange()))
                return;

            string name = pkg.ReadString();
            int stack = pkg.ReadInt();
            int quality = pkg.ReadInt();
            int variant = pkg.ReadInt();
            long crafterId = pkg.ReadLong();
            string crafterName = pkg.ReadString();
            if (stack <= 0)
                return;

            Inventory inv = container.GetInventory();
            if (inv == null)
                return;

            ItemDrop.ItemData added = inv.AddItem(name, stack, quality, variant, crafterId, crafterName, false, false);
            if (added == null)
                return;
            if (!RulesFile.AllowsStore(ContainerFilter.PiecePrefab(container), added))
            {
                inv.RemoveItem(added, added.m_stack);
                return;
            }

            Highlight(container);
        }

        internal static void OnStoreDrop(Container container, long sender, ZDOID dropId)
        {
            ZNetView nv = Refs.View(container);
            if (container == null || nv == null || !nv.IsOwner() || ZNetScene.instance == null)
                return;
            if (!ValidateRpc(container, sender, MaxStoreRange()))
                return;

            GameObject go = ZNetScene.instance.FindInstance(dropId);
            if (go == null)
                return;

            ItemDrop drop = go.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null)
                return;
            if (!RulesFile.AllowsStore(ContainerFilter.PiecePrefab(container), drop.m_itemData))
                return;
            if (Vector3.Distance(drop.transform.position, container.transform.position) > MaxStoreRange() + 6f)
                return;

            ZNetView dropView = Refs.View(drop);
            if (dropView != null && !dropView.IsOwner())
            {
                drop.RequestOwn();
                return;
            }

            StoreDropLocal(container, drop);
        }

        private static void RPC_Grant(long sender, ZPackage pkg)
        {
            Player player = Player.m_localPlayer;
            if (player == null || pkg == null)
                return;

            string shared = pkg.ReadString();
            int amount = pkg.ReadInt();
            GameObject prefab = ItemIds.PrefabFromToken(shared);
            if (prefab == null)
                prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(shared) : null;
            if (prefab == null)
                return;

            player.GetInventory().AddItem(prefab, amount);
        }

        private static bool Execute(PendingMove op)
        {
            if (op.Drop != null)
            {
                ZNetView dropView = Refs.View(op.Drop);
                if (dropView != null && !dropView.IsOwner())
                {
                    op.Drop.RequestOwn();
                    Queue.Enqueue(op);
                    return false;
                }
                return StoreDropLocal(op.Chest, op.Drop);
            }

            if (op.Withdraw)
                return WithdrawLocal(op.Chest, op.SharedName, op.Amount, op.From, op.LeaveOne) > 0;

            return DepositLocal(op.Chest, op.From, op.Item, op.Amount);
        }

        private static bool DepositLocal(Container chest, Inventory from, ItemDrop.ItemData item, int amount)
        {
            Inventory inv = chest.GetInventory();
            if (inv == null || item == null)
                return false;

            int take = Mathf.Min(amount, item.m_stack);
            if (take <= 0 || !inv.CanAddItem(item, take))
                return false;

            ItemDrop.ItemData clone = item.Clone();
            clone.m_stack = take;
            if (!inv.AddItem(clone))
                return false;

            from.RemoveItem(item, take);
            Highlight(chest);
            return true;
        }

        private static bool StoreDropLocal(Container chest, ItemDrop drop)
        {
            if (drop == null || drop.m_itemData == null)
                return false;

            Inventory inv = chest.GetInventory();
            if (inv == null || !inv.CanAddItem(drop.m_itemData, drop.m_itemData.m_stack))
                return false;

            ItemDrop.ItemData clone = drop.m_itemData.Clone();
            if (!inv.AddItem(clone))
                return false;

            ZNetView dropView = Refs.View(drop);
            if (dropView != null && dropView.IsValid() && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(drop.gameObject);
            else
                Object.Destroy(drop.gameObject);

            Highlight(chest);
            return true;
        }

        private static int WithdrawLocal(Container chest, string sharedName, int amount, Inventory playerInv, bool leaveOne)
        {
            Inventory inv = chest.GetInventory();
            if (inv == null || playerInv == null)
                return 0;

            int available = inv.CountItems(sharedName, -1, true);
            if (leaveOne && available > 0)
                available -= 1;
            int take = Mathf.Min(amount, available);
            if (take <= 0)
                return 0;

            GameObject prefab = ItemIds.PrefabFromToken(sharedName);
            if (prefab == null && ObjectDB.instance != null)
                prefab = ObjectDB.instance.GetItemPrefab(sharedName);
            if (prefab == null)
                return 0;

            if (!playerInv.CanAddItem(prefab, take))
            {
                while (take > 0 && !playerInv.CanAddItem(prefab, take))
                    take--;
                if (take <= 0)
                    return 0;
            }

            if (!playerInv.AddItem(prefab, take))
                return 0;

            inv.RemoveItem(sharedName, take, -1, true);
            return take;
        }

        private static float MaxStoreRange()
        {
            if (Plugin.Settings == null)
                return 12f;
            return RulesFile.MaxScanRange(
                Plugin.Settings.StoreRange.Value,
                Plugin.Settings.CraftRange.Value,
                Plugin.Settings.PlayerDumpRange.Value);
        }

        private static bool ValidateRpc(Container container, long sender, float range)
        {
            if (!ContainerFilter.IsUsable(container))
                return false;
            if (!PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
                return false;
            if (!RequirementBridge.SenderInRange(sender, container.transform.position, range))
                return false;
            return true;
        }

        private static bool TryOwner(Container chest, bool claim)
        {
            ZNetView nv = Refs.View(chest);
            if (nv == null || !nv.IsValid())
                return false;
            if (nv.IsOwner())
                return true;
            if (!claim)
                return false;

            int id = chest.GetInstanceID();
            float next;
            if (ClaimThrottle.TryGetValue(id, out next) && Time.time < next)
                return false;

            nv.ClaimOwnership();
            ClaimThrottle[id] = Time.time + 0.35f;
            return nv.IsOwner();
        }

        private static void InvokeDeposit(Container chest, ItemDrop.ItemData item, int amount)
        {
            ZNetView nv = Refs.View(chest);
            if (nv == null || !nv.IsValid() || item == null)
                return;

            var pkg = new ZPackage();
            pkg.Write(ItemIds.PrefabName(item) ?? ItemIds.SharedName(item) ?? "");
            pkg.Write(amount);
            pkg.Write(item.m_quality);
            pkg.Write(item.m_variant);
            pkg.Write(item.m_crafterID);
            pkg.Write(item.m_crafterName ?? "");
            nv.InvokeRPC(RpcDeposit, pkg);
        }

        private static void Enqueue(PendingMove op)
        {
            if (Queue.Count > 80)
                return;
            Queue.Enqueue(op);
        }

        public static void Highlight(Container chest)
        {
            if (chest == null || Plugin.Settings == null)
                return;

            if (Plugin.Settings.HighlightOnStore.Value)
            {
                WearNTear wnt = chest.GetComponent<WearNTear>();
                if (wnt != null)
                    wnt.Highlight();
            }

            if (Plugin.Settings.PingOnStore.Value && Chat.instance != null)
                Chat.instance.SendPing(chest.transform.position);
        }
    }
}
