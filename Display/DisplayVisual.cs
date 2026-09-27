using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Loads the Unity display bundle (built with the same Unity version as Valheim)
    /// and uses the authored ItemGrid transform as the item area.
    /// </summary>
    internal static class DisplayVisual
    {
        private const string BundleFile = "sac_displays";
        private const string ModelName = "SacModel";

        private static AssetBundle _bundle;
        private static bool _bundleFailed;
        private static readonly HashSet<string> Logged = new HashSet<string>();

        public static bool Available(string visualBase)
        {
            if (string.IsNullOrEmpty(visualBase))
                return false;
            if (visualBase == "medium")
                return HasPrefab("medium_vertical");
            if (visualBase == "large")
                return HasPrefab("large_vertical");
            return HasPrefab(visualBase);
        }

        public static void Ensure(StorageDisplayBoard board)
        {
            if (board == null || string.IsNullOrEmpty(board.VisualBase))
                return;

            string id = board.CurrentVisualId();
            if (string.IsNullOrEmpty(id))
                return;

            Transform existing = board.transform.Find(ModelName);
            if (board.AppliedVisual == id && existing != null)
            {
                // Already applied — do not re-lift snaps / re-align canvas every Ensure
                // (that fought the UI rebuild and flashed the board on scale/layout).
                return;
            }

            GameObject prefab = LoadPrefab(id);
            if (prefab == null)
            {
                LogOnce("missing " + id, "Storage display bundle has no prefab '" + id + "'. Keeping the wood sign.");
                return;
            }

            try
            {
                if (existing != null)
                    UnityEngine.Object.Destroy(existing.gameObject);

                var model = UnityEngine.Object.Instantiate(prefab, board.transform, false);
                model.name = ModelName;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                ClearLegacyScale(board);
                ApplyValheimShaders(model);
                HideMarker(model);
                HideVanillaRenderers(board.gameObject, model.transform);
                FitCollider(board.gameObject, model);
                LiftSnapPoints(board, model);
                AlignCanvas(board, model);
                board.AppliedVisual = id;
                LogOnce("loaded " + id, "Storage display prefab loaded: " + id + ".");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Storage display prefab " + id + " failed: " + ex.Message);
            }
        }

        private static bool HasPrefab(string id)
        {
            return LoadPrefab(id) != null;
        }

        private static GameObject LoadPrefab(string id)
        {
            AssetBundle bundle = Bundle();
            if (bundle == null)
                return null;
            GameObject prefab = bundle.LoadAsset<GameObject>(id);
            if (prefab != null)
                return prefab;

            string[] names = bundle.GetAllAssetNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith("/" + id + ".prefab", StringComparison.OrdinalIgnoreCase))
                    return bundle.LoadAsset<GameObject>(names[i]);
            }
            return null;
        }

        private static AssetBundle Bundle()
        {
            if (_bundle != null)
                return _bundle;
            if (_bundleFailed)
                return null;

            string path = Path.Combine(DisplaysDir(), BundleFile);
            if (!File.Exists(path))
            {
                _bundleFailed = true;
                LogOnce("nobundle", "Storage display bundle missing (" + path + "). Keeping the wood signs.");
                return null;
            }

            try
            {
                _bundle = AssetBundle.LoadFromFile(path);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Storage display bundle failed: " + ex.Message);
            }

            if (_bundle == null)
            {
                _bundleFailed = true;
                LogOnce("badbundle", "Storage display bundle could not be loaded. It must be built with Unity 6000.0.75f1.");
            }
            return _bundle;
        }

        private static string DisplaysDir()
        {
            string dll = Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(dll) ?? "", "displays");
        }

        private static void ApplyValheimShaders(GameObject model)
        {
            Shader piece = Shader.Find("Custom/Piece");
            if (piece == null)
            {
                LogOnce("noshader", "Valheim piece shader was not found. Displays keep the Unity preview material.");
                return;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.name == "ItemGrid")
                    continue;
                Material shared = renderer.sharedMaterial;
                if (shared != null && shared.shader == piece)
                    continue;
                Material[] mats = renderer.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null)
                        continue;
                    Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : mat.mainTexture;
                    Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                    float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.3f;
                    string n = mat.name.ToLowerInvariant();
                    mat.shader = piece;
                    if (tex != null && mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", tex);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", color);
                    if (mat.HasProperty("_Metallic"))
                        mat.SetFloat("_Metallic", metallic);
                    if (mat.HasProperty("_Glossiness"))
                        mat.SetFloat("_Glossiness", gloss);
                    if (mat.HasProperty("_MetalColor"))
                    {
                        Color metal = (n.Contains("gold") || n.Contains("nail"))
                            ? new Color(1f, 0.72f, 0.28f)
                            : new Color(0.75f, 0.75f, 0.78f);
                        mat.SetColor("_MetalColor", metal);
                    }
                }
                renderer.materials = mats;
            }
        }

        private static void HideMarker(GameObject model)
        {
            Transform grid = FindNamed(model.transform, "ItemGrid");
            if (grid == null)
                return;
            Renderer renderer = grid.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
            Collider collider = grid.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private static void HideVanillaRenderers(GameObject root, Transform model)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.transform.IsChildOf(model))
                    continue;
                renderer.enabled = false;
            }
        }

        private static void FitCollider(GameObject root, GameObject model)
        {
            bool any = false;
            Bounds local = new Bounds(Vector3.zero, Vector3.zero);
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] == null || filters[i].gameObject.name == "ItemGrid")
                    continue;
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;
                Encapsulate(ref local, ref any, root.transform, filters[i].transform, mesh.bounds);
            }
            if (!any)
                return;

            Vector3 size = local.size;
            if (size.z < 0.12f)
                size.z = 0.12f;

            BoxCollider[] boxes = root.GetComponentsInChildren<BoxCollider>(true);
            BoxCollider box = boxes.Length > 0 ? boxes[0] : root.AddComponent<BoxCollider>();
            if (box.transform == root.transform)
            {
                box.center = local.center;
                box.size = size;
            }
            box.isTrigger = false;
            for (int i = 1; i < boxes.Length; i++)
            {
                if (boxes[i] != null)
                    boxes[i].enabled = false;
            }
        }

        private static void Encapsulate(ref Bounds local, ref bool any, Transform root, Transform piece, Bounds meshBounds)
        {
            Vector3 c = meshBounds.center;
            Vector3 e = meshBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 inRoot = root.InverseTransformPoint(piece.TransformPoint(corner));
                if (!any)
                {
                    local = new Bounds(inRoot, Vector3.zero);
                    any = true;
                }
                else
                {
                    local.Encapsulate(inRoot);
                }
            }
        }

        private static void AlignCanvas(StorageDisplayBoard board, GameObject model)
        {
            Sign sign = board.GetComponent<Sign>();
            if (sign == null || sign.m_textWidget == null || sign.m_textWidget.canvas == null)
                return;

            Transform grid = FindNamed(model.transform, "ItemGrid");
            if (grid == null)
                return;

            Canvas canvas = sign.m_textWidget.canvas;
            float worldW = Mathf.Abs(grid.lossyScale.x);
            float worldH = Mathf.Abs(grid.lossyScale.y);
            if (worldW < 0.05f || worldH < 0.05f)
                return;

            canvas.transform.SetPositionAndRotation(grid.position + grid.forward * 0.02f, grid.rotation);

            Vector3 canvasScale = canvas.transform.lossyScale;
            float sx = Mathf.Abs(canvasScale.x);
            float sy = Mathf.Abs(canvasScale.y);
            if (sx < 0.0001f || sy < 0.0001f)
                return;

            RectTransform text = sign.m_textWidget.rectTransform;
            text.anchorMin = new Vector2(0.5f, 0.5f);
            text.anchorMax = new Vector2(0.5f, 0.5f);
            text.pivot = new Vector2(0.5f, 0.5f);
            text.anchoredPosition = Vector2.zero;
            text.localRotation = Quaternion.identity;
            text.localScale = Vector3.one;
            text.sizeDelta = new Vector2(worldW / sx, worldH / sy);
        }

        /// <summary>
        /// Valheim only cycles snap points that are direct children of the piece.
        /// Empties authored on the Unity model are nested, so move them up.
        /// </summary>
        private static void LiftSnapPoints(StorageDisplayBoard board, GameObject model)
        {
            var found = new List<Transform>();
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform snap = all[i];
                if (snap == null || snap == model.transform || snap.GetComponent<Renderer>() != null)
                    continue;
                if (!IsSnapPoint(snap))
                    continue;
                found.Add(snap);
            }
            if (found.Count == 0)
                return;

            Transform root = board.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null || child == model.transform)
                    continue;
                if (child.name == "SacSnap" || IsSnapPoint(child))
                    UnityEngine.Object.Destroy(child.gameObject);
            }

            for (int i = 0; i < found.Count; i++)
            {
                Transform snap = found[i];
                snap.SetParent(root, true);
                snap.name = "SacSnap";
                try
                {
                    snap.gameObject.tag = "snappoint";
                }
                catch (Exception)
                {
                    // Valheim defines this tag. If it is missing, the name still marks the point.
                }
            }
        }

        private static bool IsSnapPoint(Transform snap)
        {
            if (snap.name.StartsWith("snappoint", StringComparison.OrdinalIgnoreCase))
                return true;
            try
            {
                return snap.CompareTag("snappoint");
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void ClearLegacyScale(StorageDisplayBoard board)
        {
            Vector3 scale = board.transform.localScale;
            bool medium = Near(scale.x, 2.5f) && Near(scale.y, 2.1f);
            bool large = Near(scale.x, 7.5f) && Near(scale.y, 6.3f);
            if (!medium && !large)
                return;

            ZNetView view = board.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner())
                view.SetLocalScale(Vector3.one);
            else
                board.transform.localScale = Vector3.one;
        }

        private static bool Near(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.08f;
        }

        private static void LogOnce(string key, string message)
        {
            if (!Logged.Add(key))
                return;
            Plugin.Log.LogInfo(message);
        }
    }
}
