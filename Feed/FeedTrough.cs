using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>Marker + server-side auto-feed for the feed trough piece.</summary>
    internal class FeedTrough : MonoBehaviour
    {
        public const string PrefabName = "sac_feed_trough";
        private const float FeedInterval = 2f;

        private static readonly MethodInfo OnConsumedItem = AccessTools.Method(typeof(Tameable), "OnConsumedItem", new[] { typeof(ItemDrop) });
        private static readonly List<Character> Nearby = new List<Character>();
        private static bool _feeding;

        private float _nextFeed;
        private Container _container;
        private ZNetView _view;

        private void Awake()
        {
            _container = GetComponent<Container>();
            _view = GetComponent<ZNetView>();
        }

        private void Update()
        {
            if (_feeding)
                return;
            if (Plugin.Settings == null || !Plugin.Settings.FeedTroughEnabled.Value)
                return;
            // Host / dedicated authority — ClaimOwnership below covers listen-server piece owners.
            if (ZNet.instance == null || !ZNet.instance.IsServer())
                return;
            if (Time.time < _nextFeed)
                return;

            _nextFeed = Time.time + FeedInterval;
            TryFeedNearby();
        }

        private void TryFeedNearby()
        {
            if (_container == null)
                _container = GetComponent<Container>();
            if (_view == null)
                _view = GetComponent<ZNetView>();
            if (_container == null || _view == null || !_view.IsValid())
                return;

            if (!_view.IsOwner() && !_container.IsInUse())
                _view.ClaimOwnership();
            if (!_view.IsOwner())
                return;

            NearbyIndex.EnsureInventory(_container);
            Inventory inv = _container.GetInventory();
            if (inv == null || inv.NrOfItems() <= 0)
                return;

            float range = Plugin.Settings.FeedTroughRange.Value;
            if (range <= 0f)
                return;

            Nearby.Clear();
            Character.GetCharactersInRange(transform.position, range, Nearby);

            for (int i = 0; i < Nearby.Count; i++)
            {
                Character ch = Nearby[i];
                if (ch == null || ch is Player)
                    continue;

                Tameable tame = ch.GetComponent<Tameable>();
                if (tame == null || !tame.IsHungry())
                    continue;

                MonsterAI ai = ch.GetComponent<MonsterAI>();
                if (ai == null || ai.m_consumeItems == null || ai.m_consumeItems.Count == 0)
                    continue;

                ZNetView animalView = ch.GetComponent<ZNetView>();
                if (animalView == null || !animalView.IsValid())
                    continue;
                if (!animalView.IsOwner())
                    animalView.ClaimOwnership();
                if (!animalView.IsOwner())
                    continue;

                if (!TryFeedOne(inv, tame, ai))
                    continue;

                ContainerFilter.SaveInventory(_container);
                // One animal per pulse keeps cost low when many tames stand near one trough.
                break;
            }
        }

        private static bool TryFeedOne(Inventory inv, Tameable tame, MonsterAI ai)
        {
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            if (items == null || items.Count == 0)
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData stack = items[i];
                if (stack == null || stack.m_stack <= 0 || stack.m_shared == null)
                    continue;

                ItemDrop dropPrefab = FindConsumePrefab(ai, stack);
                if (dropPrefab == null)
                    continue;

                _feeding = true;
                try
                {
                    inv.RemoveItem(stack, 1);
                    if (OnConsumedItem != null)
                        OnConsumedItem.Invoke(tame, new object[] { dropPrefab });
                }
                finally
                {
                    _feeding = false;
                }

                return true;
            }

            return false;
        }

        /// <summary>Mirrors private MonsterAI.CanConsume — match shared item name against m_consumeItems.</summary>
        private static ItemDrop FindConsumePrefab(MonsterAI ai, ItemDrop.ItemData stack)
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
}
