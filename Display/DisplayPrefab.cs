using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal enum DisplayKind
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    internal static class DisplayPrefab
    {
        public const string MediumName = "sac_storage_display";
        public const string SmallName = "sac_storage_display_small";
        public const string LargeName = "sac_storage_display_large";

        private static readonly Dictionary<int, GameObject> ByHash = new Dictionary<int, GameObject>();
        private static readonly List<GameObject> AllPrefabs = new List<GameObject>();
        private static GameObject _hide;
        private static bool _busy;

        public static bool IsDisplay(Piece piece)
        {
            return piece != null && piece.GetComponent<StorageDisplayBoard>() != null;
        }

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
                Plugin.Log.LogWarning("StoreAndCraft storage display skipped: " + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        public static void TryRegisterHammer()
        {
            try
            {
                RegisterHammer();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug("Storage display hammer: " + ex.Message);
            }
        }

        private static void RegisterScene(ZNetScene scene)
        {
            if (scene == null)
                return;

            if (AllPrefabs.Count > 0)
            {
                for (int i = 0; i < AllPrefabs.Count; i++)
                    EnsureNamed(scene, AllPrefabs[i]);
                return;
            }

            GameObject sign = scene.GetPrefab("sign") ?? scene.GetPrefab("Sign");
            if (sign == null)
            {
                Plugin.Log.LogWarning("StoreAndCraft: wood sign prefab not found, storage display skipped.");
                return;
            }

            if (_hide == null)
            {
                _hide = new GameObject("sac_display_hide");
                Object.DontDestroyOnLoad(_hide);
                _hide.SetActive(false);
            }

            BuildVariant(scene, sign, SmallName, DisplayKind.Small,
                "Small Storage Display",
                "Shows the total of one item from nearby chests. Look at it and press hotbar 1-8 to set the item.",
                Vector3.one);

            BuildVariant(scene, sign, MediumName, DisplayKind.Medium,
                "Medium Storage Display",
                "Shows nearby chest totals for one item type. Press [E] and click a type.",
                new Vector3(2.5f, 2.1f, 1f));

            BuildVariant(scene, sign, LargeName, DisplayKind.Large,
                "Large Storage Display",
                "Wide board with flowing category sections from nearby chests. Press [E] and click types.",
                new Vector3(7.5f, 6.3f, 1f));

            Plugin.Log.LogInfo("StoreAndCraft storage displays registered (small / medium / large).");
        }

        private static void BuildVariant(
            ZNetScene scene,
            GameObject sign,
            string prefabName,
            DisplayKind kind,
            string pieceName,
            string pieceDesc,
            Vector3 scaleMul)
        {
            bool wasActive = sign.activeSelf;
            sign.SetActive(false);
            GameObject clone = Object.Instantiate(sign, _hide.transform);
            sign.SetActive(wasActive);

            clone.name = prefabName;
            clone.SetActive(true);

            ZNetView znv = clone.GetComponent<ZNetView>();
            if (znv == null)
            {
                Plugin.Log.LogWarning("StoreAndCraft: storage display clone lost ZNetView (" + prefabName + ").");
                Object.Destroy(clone);
                return;
            }

            znv.m_persistent = true;
            // Keep distant sync closer to normal furniture — scaled signs otherwise stay visible too far.
            znv.m_distant = false;

            clone.transform.localScale = Vector3.Scale(clone.transform.localScale, scaleMul);

            Piece piece = clone.GetComponent<Piece>();
            Piece source = sign.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = pieceName;
                piece.m_description = pieceDesc;
                if (piece.m_placeEffect == null && source != null)
                    piece.m_placeEffect = source.m_placeEffect;
                if (piece.m_placeEffect == null)
                    piece.m_placeEffect = new EffectList { m_effectPrefabs = new EffectList.EffectData[0] };
                if (piece.m_resources == null && source != null)
                    piece.m_resources = source.m_resources;
            }

            // Soften colliders after clone is fully set up (must stay solid enough to place).
            SoftenColliders(clone, kind);

            StorageDisplayBoard board = clone.GetComponent<StorageDisplayBoard>();
            if (board == null)
                board = clone.AddComponent<StorageDisplayBoard>();
            board.Configure(kind);

            int hash = prefabName.GetStableHashCode();
            ByHash[hash] = clone;
            AllPrefabs.Add(clone);
            EnsureNamed(scene, clone);
        }

        /// <summary>
        /// Large/Medium signs scale into full walls. Keep solid colliders (placement needs them)
        /// but shrink depth so they are harder to stand on / use as traps.
        /// Do not use triggers — that breaks PlacePiece (mats eaten, nothing built).
        /// </summary>
        private static void SoftenColliders(GameObject clone, DisplayKind kind)
        {
            if (clone == null || kind == DisplayKind.Small)
                return;

            // Local Z thickness after root scale: keep a real solid slab, just thinner.
            float depth = kind == DisplayKind.Large ? 0.04f : 0.06f;

            Collider[] cols = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null)
                    continue;

                // Never destroy — Destroy() on a prefab breaks later PlacePiece/GetPrefab use.
                BoxCollider box = col as BoxCollider;
                if (box != null)
                {
                    Vector3 size = box.size;
                    // Prefer thinning the shallowest axis (sign face depth).
                    if (size.z <= size.x && size.z <= size.y)
                        size.z = Mathf.Min(size.z, depth);
                    else if (size.x <= size.y)
                        size.x = Mathf.Min(size.x, depth);
                    else
                        size.y = Mathf.Min(size.y, depth);
                    box.size = size;
                    box.isTrigger = false;
                    continue;
                }

                // Mesh colliders stay as-is (needed for placement); cannot safely thin them.
            }
        }

        private static void RegisterHammer()
        {
            if (AllPrefabs.Count == 0 || ObjectDB.instance == null)
                return;

            GameObject hammer = ObjectDB.instance.GetItemPrefab("Hammer");
            if (hammer == null)
                return;
            ItemDrop drop = hammer.GetComponent<ItemDrop>();
            PieceTable table = drop != null && drop.m_itemData?.m_shared != null
                ? drop.m_itemData.m_shared.m_buildPieces
                : null;
            if (table == null || table.m_pieces == null)
                return;

            for (int i = 0; i < AllPrefabs.Count; i++)
            {
                GameObject prefab = AllPrefabs[i];
                if (prefab != null && !table.m_pieces.Contains(prefab))
                    table.m_pieces.Add(prefab);
            }
        }

        private static void EnsureNamed(ZNetScene scene, GameObject prefab)
        {
            if (prefab == null || scene == null)
                return;

            if (scene.m_prefabs != null && !scene.m_prefabs.Contains(prefab))
                scene.m_prefabs.Add(prefab);

            FieldInfo named = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            var map = named != null ? named.GetValue(scene) as Dictionary<int, GameObject> : null;
            if (map != null)
                map[prefab.name.GetStableHashCode()] = prefab;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetSceneDisplayPatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            DisplayPrefab.RegisterForScene(__instance);
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.GetPrefab), typeof(int))]
    internal static class ZNetSceneGetPrefabDisplayPatch
    {
        private static void Postfix(int hash, ref GameObject __result)
        {
            if (__result != null)
                return;
            GameObject found = DisplayPrefab.PrefabForHash(hash);
            if (found != null)
                __result = found;
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class GameStartDisplayPatch
    {
        private static void Postfix()
        {
            if (ZNetScene.instance != null)
                DisplayPrefab.RegisterForScene(ZNetScene.instance);
            DisplayPrefab.TryRegisterHammer();
        }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class PlayerDisplayTablePatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == null || !__instance.IsOwner())
                return;
            if (ZNetScene.instance != null)
                DisplayPrefab.RegisterForScene(ZNetScene.instance);
            DisplayPrefab.TryRegisterHammer();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class PlaceDisplayPatch
    {
        private static void Prefix(Piece piece, ref bool cheated)
        {
            if (cheated && DisplayPrefab.IsDisplay(piece))
                cheated = false;
        }

        private static System.Exception Finalizer(System.Exception __exception)
        {
            if (__exception == null)
                return null;
            Plugin.Log.LogWarning("PlacePiece failed: " + __exception.Message);
            return null;
        }
    }

    [HarmonyPatch(typeof(Sign), "UpdateText")]
    internal static class SignDisplayUpdateTextPatch
    {
        private static bool Prefix(Sign __instance)
        {
            return __instance == null || __instance.GetComponent<StorageDisplayBoard>() == null;
        }
    }

    [HarmonyPatch(typeof(Sign), nameof(Sign.Interact))]
    internal static class SignDisplayInteractPatch
    {
        private static bool Prefix(Sign __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (hold || __instance == null)
                return true;
            StorageDisplayBoard board = __instance.GetComponent<StorageDisplayBoard>();
            if (board == null)
                return true;
            __result = board.TryOpenMenu(character);
            return false;
        }
    }

    [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
    internal static class SignDisplayHoverPatch
    {
        private static void Postfix(Sign __instance, ref string __result)
        {
            StorageDisplayBoard board = __instance.GetComponent<StorageDisplayBoard>();
            if (board == null)
                return;
            __result = board.HoverLabel();
        }
    }

    [HarmonyPatch(typeof(Player), "UseHotbarItem")]
    internal static class PlayerHotbarDisplayPatch
    {
        private static bool Prefix(Player __instance, int index)
        {
            return !StorageDisplayBoard.TryAssignFromHotbar(__instance, index);
        }
    }
}
