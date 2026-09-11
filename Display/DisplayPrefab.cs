using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class DisplayPrefab
    {
        public const string PrefabName = "sac_storage_display";

        private static GameObject _prefab;
        private static GameObject _hide;
        private static bool _busy;
        private static int _prefabHash;

        public static bool IsDisplay(Piece piece)
        {
            return piece != null && piece.GetComponent<StorageDisplayBoard>() != null;
        }

        public static GameObject Prefab
        {
            get { return _prefab; }
        }

        public static int PrefabHash
        {
            get
            {
                if (_prefabHash == 0)
                    _prefabHash = PrefabName.GetStableHashCode();
                return _prefabHash;
            }
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

            if (_prefab != null)
            {
                EnsureNamed(scene, _prefab);
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

            bool wasActive = sign.activeSelf;
            sign.SetActive(false);
            GameObject clone = Object.Instantiate(sign, _hide.transform);
            sign.SetActive(wasActive);

            clone.name = PrefabName;
            clone.SetActive(true);

            ZNetView znv = clone.GetComponent<ZNetView>();
            if (znv == null)
            {
                Plugin.Log.LogWarning("StoreAndCraft: storage display clone lost ZNetView, skipped.");
                Object.Destroy(clone);
                return;
            }

            znv.m_persistent = true;

            clone.transform.localScale = Vector3.Scale(
                clone.transform.localScale,
                new Vector3(2.5f, 2.1f, 1f));

            Piece piece = clone.GetComponent<Piece>();
            Piece source = sign.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = "Storage Display";
                piece.m_description = "Shows nearby chest totals for one item type. Press [E] and click a type.";
                if (piece.m_placeEffect == null && source != null)
                    piece.m_placeEffect = source.m_placeEffect;
                if (piece.m_placeEffect == null)
                    piece.m_placeEffect = new EffectList { m_effectPrefabs = new EffectList.EffectData[0] };
                if (piece.m_resources == null && source != null)
                    piece.m_resources = source.m_resources;
            }

            if (clone.GetComponent<StorageDisplayBoard>() == null)
                clone.AddComponent<StorageDisplayBoard>();

            _prefab = clone;
            _prefabHash = PrefabName.GetStableHashCode();
            EnsureNamed(scene, _prefab);
            Plugin.Log.LogInfo("StoreAndCraft storage display registered.");
        }

        private static void RegisterHammer()
        {
            if (_prefab == null || ObjectDB.instance == null)
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
            if (!table.m_pieces.Contains(_prefab))
                table.m_pieces.Add(_prefab);
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
                map[PrefabHash] = prefab;
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
            if (__result != null || DisplayPrefab.Prefab == null)
                return;
            if (hash == DisplayPrefab.PrefabHash)
                __result = DisplayPrefab.Prefab;
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
}
