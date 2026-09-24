using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Feed trough: animals path to this piece and eat from a bait ItemDrop on the trough,
    /// same loop as vanilla ground food (MoveTo → RemoveOne → OnConsumedItem).
    /// </summary>
    internal class FeedTrough : MonoBehaviour
    {
        public const string PrefabName = "sac_feed_trough";
        internal const string RpcEat = "SAC_TroughEat";
        private const string BaitRootName = "SacTroughEat";
        private const string VisualName = "SacTroughFoodVfx";
        private const float RefreshInterval = 0.4f;

        /// <summary>
        /// Eat points just outside the trough on every side so animals can approach
        /// from left/right/ends (not only one open face into the mesh).
        /// </summary>
        private static readonly Vector3[] BaitOffsets =
        {
            new Vector3(0.85f, 0.08f, 0f),
            new Vector3(-0.85f, 0.08f, 0f),
            new Vector3(0f, 0.08f, 1.15f),
            new Vector3(0f, 0.08f, -1.15f),
            new Vector3(0.85f, 0.08f, 0.7f),
            new Vector3(-0.85f, 0.08f, 0.7f),
            new Vector3(0.85f, 0.08f, -0.7f),
            new Vector3(-0.85f, 0.08f, -0.7f)
        };

        private static readonly MethodInfo CanConsume = AccessTools.Method(
            typeof(MonsterAI), "CanConsume", new[] { typeof(ItemDrop.ItemData) });

        internal static readonly List<FeedTrough> Live = new List<FeedTrough>();

        private Container _container;
        private ZNetView _view;
        private Transform _eatRoot;
        private GameObject _visual;
        private string _visualToken;
        private float _nextRefresh;
        private bool _rpc;
        private bool _invHooked;
        private bool _hasFood;
        private readonly List<ItemDrop> _baits = new List<ItemDrop>(8);

        private void Awake()
        {
            _container = GetComponent<Container>();
            _view = GetComponent<ZNetView>();
        }

        private void OnEnable()
        {
            if (!Live.Contains(this))
                Live.Add(this);
        }

        private void OnDisable()
        {
            Live.Remove(this);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            UnhookInventory();
        }

        private void Start()
        {
            TryRegisterRpc();
            if (_container == null)
                _container = GetComponent<Container>();
            // Make sure a freshly placed trough has a live inventory bag before the first open.
            if (_container != null && _container.GetInventory() == null)
                NearbyIndex.EnsureInventory(_container);
            HookInventory();
            RefreshOfferings();
        }

        private void Update()
        {
            if (Plugin.Settings == null || !Plugin.Settings.FeedTroughEnabled.Value)
            {
                SetHasFood(false);
                return;
            }

            if (!Placed())
                return;

            TryRegisterRpc();
            HookInventory();

            if (Time.time < _nextRefresh)
                return;
            _nextRefresh = Time.time + RefreshInterval;
            RefreshOfferings();
        }

        private bool Placed()
        {
            if (_view == null)
                _view = GetComponent<ZNetView>();
            return _view != null && _view.IsValid();
        }

        private void TryRegisterRpc()
        {
            if (_rpc)
                return;
            if (_view == null)
                _view = GetComponent<ZNetView>();
            if (_view == null || !_view.IsValid())
                return;

            _view.Register<string>(RpcEat, RPC_Eat);
            _rpc = true;
        }

        private void HookInventory()
        {
            if (_invHooked)
                return;
            if (_container == null)
                _container = GetComponent<Container>();
            Inventory inv = _container != null ? _container.GetInventory() : null;
            if (inv == null)
                return;

            inv.m_onChanged += OnInventoryChanged;
            _invHooked = true;
        }

        private void UnhookInventory()
        {
            if (!_invHooked || _container == null)
                return;
            Inventory inv = _container.GetInventory();
            if (inv != null)
                inv.m_onChanged -= OnInventoryChanged;
            _invHooked = false;
        }

        private void OnInventoryChanged()
        {
            _nextRefresh = 0f;
            RefreshOfferings();
        }

        private void RPC_Eat(long sender, string sharedName)
        {
            if (_view == null)
                _view = GetComponent<ZNetView>();
            if (_view == null || !_view.IsValid())
                return;
            if (!_view.IsOwner())
                _view.ClaimOwnership();
            if (!_view.IsOwner())
                return;
            ConsumeLocal(sharedName);
        }

        internal bool TryAnimalEat(ItemDrop bait)
        {
            if (bait == null)
                return false;
            if (_container != null && _container.IsInUse())
                return false;

            string shared = bait.m_itemData?.m_shared != null
                ? bait.m_itemData.m_shared.m_name
                : null;
            if (string.IsNullOrEmpty(shared))
                return false;

            Inventory inv = ReadFood();
            if (!HasShared(inv, shared))
                return false;

            if (_view == null)
                _view = GetComponent<ZNetView>();
            if (_view == null || !_view.IsValid())
                return false;

            if (!_view.IsOwner())
            {
                _view.ClaimOwnership();
                if (!_view.IsOwner())
                {
                    _view.InvokeRPC(RpcEat, new object[] { shared });
                    // Debit may land on owner next tick — still allow the eat animation.
                    return true;
                }
            }

            return ConsumeLocal(shared);
        }

        private bool ConsumeLocal(string sharedName)
        {
            if (_view == null)
                _view = GetComponent<ZNetView>();
            if (_view == null || !_view.IsValid())
                return false;
            if (!_view.IsOwner())
            {
                _view.ClaimOwnership();
                if (!_view.IsOwner())
                    return false;
            }

            Inventory inv = ReadFood();
            ItemDrop.ItemData stack = FindShared(inv, sharedName);
            if (stack == null || stack.m_stack <= 0)
                return false;

            inv.RemoveItem(stack, 1);
            ContainerFilter.SaveInventory(_container);
            RefreshOfferings();
            return true;
        }

        private Inventory ReadFood()
        {
            if (_container == null)
                _container = GetComponent<Container>();
            if (_container == null)
                return null;

            Inventory inv = _container.GetInventory();
            if (inv != null && inv.NrOfItems() > 0)
                return inv;

            // Open trough: trust the live bag — do not ZDO-Load over it.
            if (_container.IsInUse() && inv != null)
                return inv;

            if (ContainerFilter.ZdoHasItemPayload(_view != null && _view.IsValid() ? _view.GetZDO() : null))
                NearbyIndex.EnsureInventory(_container, force: true);
            return _container.GetInventory();
        }

        internal ItemDrop FindBaitFor(MonsterAI ai, Vector3 from, float maxRange, float bestDist)
        {
            if (ai == null || !Placed() || !_hasFood)
                return null;
            if (_container != null && _container.IsInUse())
                return null;

            float cap = maxRange;
            if (Plugin.Settings != null)
                cap = Mathf.Min(maxRange, Plugin.Settings.FeedTroughRange.Value);
            if (cap <= 0f)
                return null;

            ItemDrop best = null;
            float bestLocal = bestDist;
            for (int i = 0; i < _baits.Count; i++)
            {
                ItemDrop bait = _baits[i];
                if (bait == null || bait.m_itemData == null)
                    continue;
                if (!ItemIsFoodFor(ai, bait.m_itemData))
                    continue;

                float dist = Vector3.Distance(from, bait.transform.position);
                if (dist > cap || dist >= bestLocal)
                    continue;

                bestLocal = dist;
                best = bait;
            }

            return best;
        }

        internal static bool ItemIsFoodFor(MonsterAI ai, ItemDrop.ItemData stack)
        {
            if (ai == null || stack == null)
                return false;

            if (CanConsume != null)
            {
                try
                {
                    object result = CanConsume.Invoke(ai, new object[] { stack });
                    if (result is bool ok)
                        return ok;
                }
                catch
                {
                }
            }

            return FindConsumePrefab(ai, stack) != null;
        }

        internal static ItemDrop FindConsumePrefab(MonsterAI ai, ItemDrop.ItemData stack)
        {
            if (ai?.m_consumeItems == null || stack?.m_shared == null)
                return null;

            string want = stack.m_shared.m_name;
            for (int i = 0; i < ai.m_consumeItems.Count; i++)
            {
                ItemDrop drop = ai.m_consumeItems[i];
                if (drop == null || drop.m_itemData?.m_shared == null)
                    continue;
                if (drop.m_itemData.m_shared.m_name == want)
                    return drop;
            }

            return null;
        }

        private void RefreshOfferings()
        {
            if (!Placed())
                return;

            Inventory inv = ReadFood();
            List<ItemDrop.ItemData> items = inv != null ? inv.GetAllItems() : null;
            bool any = items != null && CountFood(items) > 0;
            SetHasFood(any);

            if (!any)
            {
                ClearBaits();
                HideVisual();
                return;
            }

            SyncBaits(items);
            ShowVisual(FirstFood(items));
        }

        private static int CountFood(List<ItemDrop.ItemData> items)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData stack = items[i];
                if (stack != null && stack.m_stack > 0 && stack.m_shared != null)
                    n += stack.m_stack;
            }
            return n;
        }

        private static ItemDrop.ItemData FirstFood(List<ItemDrop.ItemData> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData stack = items[i];
                if (stack != null && stack.m_stack > 0 && stack.m_shared != null)
                    return stack;
            }
            return null;
        }

        private static bool HasShared(Inventory inv, string shared)
        {
            return FindShared(inv, shared) != null;
        }

        private static ItemDrop.ItemData FindShared(Inventory inv, string shared)
        {
            if (inv == null || string.IsNullOrEmpty(shared))
                return null;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            if (items == null)
                return null;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData stack = items[i];
                if (stack == null || stack.m_stack <= 0 || stack.m_shared == null)
                    continue;
                if (stack.m_shared.m_name == shared)
                    return stack;
            }
            return null;
        }

        private void SyncBaits(List<ItemDrop.ItemData> items)
        {
            EnsureEatRoot();

            var wanted = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData stack = items[i];
                if (stack == null || stack.m_stack <= 0 || stack.m_shared == null)
                    continue;
                string shared = stack.m_shared.m_name;
                if (string.IsNullOrEmpty(shared))
                    continue;

                for (int s = 0; s < BaitOffsets.Length; s++)
                {
                    string key = BaitKey(shared, s);
                    wanted.Add(key);
                    ItemDrop bait = FindBaitByKey(key);
                    if (bait == null)
                        CreateBait(stack, s, key);
                    else
                        AssignItem(bait, stack);
                }
            }

            for (int i = _baits.Count - 1; i >= 0; i--)
            {
                ItemDrop bait = _baits[i];
                FeedTroughBait mark = bait != null ? bait.GetComponent<FeedTroughBait>() : null;
                string key = mark != null ? mark.Key : null;
                if (bait == null || string.IsNullOrEmpty(key) || !wanted.Contains(key))
                {
                    if (bait != null)
                        Object.Destroy(bait.gameObject);
                    _baits.RemoveAt(i);
                }
            }
        }

        private static string BaitKey(string shared, int side)
        {
            return shared + "#" + side;
        }

        private ItemDrop FindBaitByKey(string key)
        {
            for (int i = 0; i < _baits.Count; i++)
            {
                ItemDrop bait = _baits[i];
                if (bait == null)
                    continue;
                FeedTroughBait mark = bait.GetComponent<FeedTroughBait>();
                if (mark != null && mark.Key == key)
                    return bait;
            }
            return null;
        }

        private ItemDrop CreateBait(ItemDrop.ItemData stack, int side, string key)
        {
            EnsureEatRoot();
            Vector3 offset = side >= 0 && side < BaitOffsets.Length
                ? BaitOffsets[side]
                : Vector3.zero;

            var go = new GameObject(BaitRootName);
            go.SetActive(false);
            go.transform.SetParent(_eatRoot, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            ItemDrop drop = go.AddComponent<ItemDrop>();
            drop.m_autoPickup = false;
            drop.m_autoDestroy = false;
            AssignItem(drop, stack);

            FeedTroughBait mark = go.AddComponent<FeedTroughBait>();
            mark.Trough = this;
            mark.Key = key;

            go.SetActive(true);
            go.transform.localScale = Vector3.one;
            _baits.Add(drop);
            return drop;
        }

        private static void AssignItem(ItemDrop drop, ItemDrop.ItemData stack)
        {
            if (drop == null || stack == null)
                return;

            if (stack.m_customData == null)
                stack.m_customData = new Dictionary<string, string>();

            ItemDrop.ItemData copy;
            try
            {
                copy = stack.Clone();
            }
            catch
            {
                copy = stack;
            }

            copy.m_stack = 99;
            drop.m_itemData = copy;
        }

        private void ClearBaits()
        {
            for (int i = 0; i < _baits.Count; i++)
            {
                if (_baits[i] != null)
                    Object.Destroy(_baits[i].gameObject);
            }
            _baits.Clear();
        }

        private void EnsureEatRoot()
        {
            if (_eatRoot != null)
                return;

            Transform hit = transform.Find("SacTroughHit");
            _eatRoot = hit != null ? hit : transform;
        }

        private void SetHasFood(bool has)
        {
            _hasFood = has;
            if (!has)
                HideVisual();
        }

        private void ShowVisual(ItemDrop.ItemData sample)
        {
            string token = sample != null ? "food" : null;
            if (_visual != null && _visualToken == token)
            {
                if (!_visual.activeSelf)
                    _visual.SetActive(true);
                return;
            }

            HideVisual();
            if (sample == null)
                return;

            EnsureEatRoot();
            _visual = new GameObject(VisualName);
            _visual.transform.SetParent(_eatRoot, false);
            _visual.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            _visual.transform.localRotation = Quaternion.identity;
            _visual.transform.localScale = Vector3.one;
            BuildSparkle(_visual);
            _visualToken = token;
        }

        private void HideVisual()
        {
            if (_visual != null)
            {
                Object.Destroy(_visual);
                _visual = null;
            }
            _visualToken = null;
        }

        /// <summary>
        /// Tiny looping sparkle built in code — no prefab clone (avoids ZSyncTransform crash).
        /// </summary>
        private static void BuildSparkle(GameObject host)
        {
            if (host == null)
                return;

            var go = new GameObject("SacTroughSparkle");
            go.transform.SetParent(host.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new Color(1f, 0.92f, 0.55f, 0.9f);
            main.maxParticles = 28;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 10f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.4f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = grad;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                Material mat = FindSparkleMaterial();
                if (mat != null)
                    rend.sharedMaterial = mat;
            }

            ps.Play(true);
        }

        private static Material _sparkleMat;

        private static Material FindSparkleMaterial()
        {
            if (_sparkleMat != null)
                return _sparkleMat;

            Shader shader = Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Particles/Additive")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            _sparkleMat = new Material(shader);
            _sparkleMat.color = new Color(1f, 0.9f, 0.5f, 0.85f);
            return _sparkleMat;
        }

        public static bool IsTrough(Container container)
        {
            if (container == null)
                return false;
            if (container.GetComponent<FeedTrough>() != null)
                return true;
            Piece piece = container.GetComponent<Piece>();
            if (piece == null)
                piece = container.GetComponentInParent<Piece>();
            return IsTroughPiece(piece);
        }

        public static bool IsTroughPiece(Piece piece)
        {
            if (piece == null)
                return false;
            if (piece.GetComponent<FeedTrough>() != null)
                return true;
            return string.Equals(ItemIds.StripClone(piece.gameObject.name), PrefabName, System.StringComparison.Ordinal);
        }
    }

    /// <summary>Marks the dummy ItemDrop animals path to. Not a real world drop.</summary>
    internal class FeedTroughBait : MonoBehaviour
    {
        public FeedTrough Trough;
        public string Key;

        public static bool IsBait(ItemDrop drop)
        {
            return drop != null && drop.GetComponent<FeedTroughBait>() != null;
        }

        public static FeedTrough OwnerOf(ItemDrop drop)
        {
            if (drop == null)
                return null;
            FeedTroughBait mark = drop.GetComponent<FeedTroughBait>();
            if (mark != null && mark.Trough != null)
                return mark.Trough;
            return drop.GetComponentInParent<FeedTrough>();
        }
    }
}
