using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Scarecrow hammer piece. Carrier = clone of the vanilla wood pole (Piece + ZNetView +
    /// WearNTear, no own interaction). The Unity model SM_Scarecrow from sac_displays replaces the
    /// pole look when the bundle has it; until then the pole stays as placeholder.
    /// Same registration pattern as FeedTroughPrefab.
    /// </summary>
    internal static class ScarecrowPrefab
    {
        internal const string ModelPrefab = "SM_Scarecrow";
        internal const string ModelName = "SacScarecrowModel";
        private const string CarrierPrefab = "wood_pole2";

        private static GameObject _prefab;
        private static GameObject _hide;
        private static bool _busy;
        private static readonly Dictionary<int, GameObject> ByHash = new Dictionary<int, GameObject>();

        public static GameObject PrefabForHash(int hash)
        {
            GameObject go;
            return ByHash.TryGetValue(hash, out go) ? go : null;
        }

        public static void RegisterForScene(ZNetScene scene)
        {
            if (scene == null || _busy)
                return;
            _busy = true;
            try
            {
                RegisterScene(scene);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("StoreAndCraft scarecrow skipped: " + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void RegisterScene(ZNetScene scene)
        {
            if (_prefab != null)
            {
                EnsureNamed(scene, _prefab);
                return;
            }
            GameObject carrier = scene.GetPrefab(CarrierPrefab);
            GameObject woodChest = scene.GetPrefab("piece_chest_wood");
            if (carrier == null)
            {
                Plugin.Log.LogWarning("StoreAndCraft: " + CarrierPrefab + " missing, scarecrow skipped.");
                return;
            }
            if (_hide == null)
            {
                _hide = new GameObject("sac_scarecrow_hide");
                Object.DontDestroyOnLoad(_hide);
                _hide.SetActive(false);
            }

            bool wasActive = carrier.activeSelf;
            carrier.SetActive(false);
            GameObject clone = Object.Instantiate(carrier, _hide.transform);
            carrier.SetActive(wasActive);
            clone.name = Scarecrow.PrefabName;
            clone.SetActive(true);

            ZNetView znv = clone.GetComponent<ZNetView>();
            Piece piece = clone.GetComponent<Piece>();
            if (znv == null || piece == null)
            {
                Plugin.Log.LogWarning("StoreAndCraft: scarecrow carrier lost ZNetView/Piece.");
                Object.Destroy(clone);
                return;
            }
            znv.m_persistent = true;

            bool custom = TryAttachModel(clone);

            piece.m_name = Loc.T("Scarecrow", "Vogelscheuche");
            piece.m_description = Loc.T(
                "Stands in the middle of its field grid, plants it with seeds from nearby chests and harvests the whole field once everything is ripe. Alt+E: crop, grid size, link. B: on/off.",
                "Steht in der Mitte ihres Feld-Rasters, bepflanzt es mit Samen aus Kisten in der Nähe und erntet das ganze Feld, sobald alles reif ist. Alt+E: Pflanze, Rastergröße, Link. B: an/aus.");
            Piece chestPiece = woodChest != null ? woodChest.GetComponent<Piece>() : null;
            if (chestPiece != null)
            {
                piece.m_category = chestPiece.m_category;
                piece.m_usage = chestPiece.m_usage;
            }
            piece.m_canBeRemoved = true;
            piece.m_isUpgrade = false;
            piece.m_resources = Recipe();
            if (piece.m_placeEffect == null)
                piece.m_placeEffect = new EffectList { m_effectPrefabs = new EffectList.EffectData[0] };
            Sprite icon = UiAssets.Get("icon_scarecrow.png");
            if (icon != null)
                piece.m_icon = icon;

            if (clone.GetComponent<Scarecrow>() == null)
                clone.AddComponent<Scarecrow>();

            _prefab = clone;
            ByHash[Scarecrow.PrefabName.GetStableHashCode()] = clone;
            EnsureNamed(scene, clone);
            Plugin.Log.LogInfo("StoreAndCraft scarecrow registered (" + (custom ? "Unity model" : "pole placeholder") + ").");
        }

        /// <summary>Unity model when the bundle has it: pole renderers/colliders off, model box as hit.</summary>
        private static bool TryAttachModel(GameObject clone)
        {
            GameObject prefab = DisplayVisual.LoadPrefab(ModelPrefab);
            if (prefab == null)
                return false;
            foreach (Renderer r in clone.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            foreach (Collider c in clone.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            GameObject model = Object.Instantiate(prefab, clone.transform, false);
            model.name = ModelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            DisplayVisual.ApplyValheimShaders(model);
            var hit = new GameObject("SacScarecrowHit");
            hit.transform.SetParent(clone.transform, false);
            BoxCollider box = hit.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(0.5f, 2.2f, 0.4f);
            return true;
        }

        private static Piece.Requirement[] Recipe()
        {
            var list = new List<Piece.Requirement>();
            AddReq(list, "Wood", 6);
            AddReq(list, "LeatherScraps", 2);
            AddReq(list, "Resin", 2);
            return list.ToArray();
        }

        private static void AddReq(List<Piece.Requirement> list, string item, int amount)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null)
                return;
            list.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount, m_amountPerLevel = 0, m_recover = true });
        }

        public static void TryRegisterHammer()
        {
            if (_prefab == null || ObjectDB.instance == null)
                return;
            GameObject hammer = ObjectDB.instance.GetItemPrefab("Hammer");
            ItemDrop drop = hammer != null ? hammer.GetComponent<ItemDrop>() : null;
            PieceTable table = drop != null && drop.m_itemData?.m_shared != null ? drop.m_itemData.m_shared.m_buildPieces : null;
            if (table == null || table.m_pieces == null)
                return;
            // Recipe needs ObjectDB: rebuild once it exists.
            Piece piece = _prefab.GetComponent<Piece>();
            if (piece != null && (piece.m_resources == null || piece.m_resources.Length == 0))
                piece.m_resources = Recipe();
            if (!table.m_pieces.Contains(_prefab))
                table.m_pieces.Add(_prefab);
        }

        private static void EnsureNamed(ZNetScene scene, GameObject prefab)
        {
            if (scene.m_prefabs != null && !scene.m_prefabs.Contains(prefab))
                scene.m_prefabs.Add(prefab);
            FieldInfo named = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            var map = named != null ? named.GetValue(scene) as Dictionary<int, GameObject> : null;
            if (map != null)
                map[prefab.name.GetStableHashCode()] = prefab;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetSceneScarecrowPatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            ScarecrowPrefab.RegisterForScene(__instance);
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.GetPrefab), typeof(int))]
    internal static class ZNetSceneGetPrefabScarecrowPatch
    {
        private static void Postfix(int hash, ref GameObject __result)
        {
            if (__result != null)
                return;
            GameObject found = ScarecrowPrefab.PrefabForHash(hash);
            if (found != null)
                __result = found;
        }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class PlayerScarecrowTablePatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == null || !__instance.IsOwner())
                return;
            if (ZNetScene.instance != null)
                ScarecrowPrefab.RegisterForScene(ZNetScene.instance);
            ScarecrowPrefab.TryRegisterHammer();
        }
    }
}
