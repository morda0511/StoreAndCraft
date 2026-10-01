using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Scarecrow: stands in the centre cell of its planting grid (odd cols × rows, spacing — ZDO). When every
    /// planted spot is ripe it harvests the whole field (drops get its link → auto-intake stores them)
    /// and replants the empty, cultivated spots with seeds from (linked) chests.
    /// Event driven: Plant.Grow postfix, load / placement / setting change. Only a slow 60 s re-check
    /// while something is growing (crops grown on another client's machine). Only the ZDO owner with
    /// the local player in auto-fill range works it; on/off = the station auto-fill flag ([B]).
    /// </summary>
    internal class Scarecrow : MonoBehaviour, Hoverable, Interactable
    {
        internal const string PrefabName = "sac_scarecrow";
        private const string KeyCols = "SAC_farmCols";
        private const string KeyRows = "SAC_farmRows";
        private const string KeySpacing = "SAC_farmSpacing";
        private const string KeyCrop = "SAC_farmCrop";
        private const string KeyHarvest = "SAC_farmHarvest";
        private const string KeyPlant = "SAC_farmPlant";
        internal const int MaxSize = 21; // odd: the scarecrow stands in the centre cell
        private const float RecheckGrowingSeconds = 60f;
        private const float RetryMissingSeconds = 30f;

        internal static readonly List<Scarecrow> Live = new List<Scarecrow>();

        private ZNetView _nv;
        private bool _dirty = true;
        private float _nextCheck;
        private bool _missingSeeds;
        private GameObject _preview;
        private int _previewHash;

        internal bool MissingSeeds => _missingSeeds;

        // ---- settings (ZDO)

        private ZDO Zdo => _nv != null && _nv.IsValid() ? _nv.GetZDO() : null;
        internal int Cols => Odd(Zdo != null ? Zdo.GetInt(KeyCols, 5) : 5);
        internal int Rows => Odd(Zdo != null ? Zdo.GetInt(KeyRows, 5) : 5);

        /// <summary>Odd size 1..MaxSize so there is a real centre cell (even values round up).</summary>
        internal static int Odd(int v)
        {
            v = Mathf.Clamp(v, 1, MaxSize);
            return v % 2 == 0 ? Mathf.Min(v + 1, MaxSize) : v;
        }
        internal string CropSeed => Zdo != null ? Zdo.GetString(KeyCrop, "") : "";
        internal bool Harvest => Zdo == null || Zdo.GetBool(KeyHarvest, true);
        internal bool PlantOn => Zdo == null || Zdo.GetBool(KeyPlant, true);

        /// <summary>Spacing: stored value, never below what the crop needs (else it would not grow).</summary>
        internal float Spacing
        {
            get
            {
                float min = MinSpacing();
                float v = Zdo != null ? Zdo.GetFloat(KeySpacing, 0f) : 0f;
                return v <= 0f ? min : Mathf.Max(min, v);
            }
        }

        internal float MinSpacing()
        {
            CropMap.Crop crop = CropMap.ForSeed(CropSeed);
            float r = crop != null ? crop.GrowRadius : 0.5f;
            return Mathf.Max(0.5f, Mathf.Ceil(r * 2f * 10f) / 10f + 0.05f);
        }

        internal void SetInt(string key, int v) => Write(z => z.Set(key, v));
        internal void SetCols(int v) => SetInt(KeyCols, Odd(v));
        internal void SetRows(int v) => SetInt(KeyRows, Odd(v));
        internal void SetSpacing(float v) => Write(z => z.Set(KeySpacing, Mathf.Clamp(v, MinSpacing(), 3f)));
        internal void SetCrop(string seed) => Write(z => z.Set(KeyCrop, seed ?? ""));
        internal void SetHarvest(bool on) => Write(z => z.Set(KeyHarvest, on));
        internal void SetPlant(bool on) => Write(z => z.Set(KeyPlant, on));

        private void Write(System.Action<ZDO> set)
        {
            if (_nv == null || !_nv.IsValid())
                return;
            if (!_nv.IsOwner())
                _nv.ClaimOwnership();
            set(_nv.GetZDO());
            MarkDirty();
        }

        internal void MarkDirty()
        {
            _dirty = true;
            _nextCheck = 0f;
        }

        // ---- lifecycle

        private void Awake()
        {
            _nv = GetComponent<ZNetView>();
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

        private void Start()
        {
            // New scarecrow: working by default (same flag as [B] auto-fill).
            if (_nv != null && _nv.IsValid() && _nv.IsOwner() && _nv.GetZDO().GetInt(StationAutoFill.ZdoKey, -1) < 0)
                _nv.GetZDO().Set(StationAutoFill.ZdoKey, 1);
            MarkDirty();
        }

        private void Update()
        {
            bool ghost = _nv == null || _nv.GetZDO() == null;
            UpdatePreview(ghost || StationFilterMenu.IsEditing(this));
            if (ghost)
                return;
            if (!_dirty && Time.time < _nextCheck)
                return;
            if (!CanWork())
            {
                _nextCheck = Time.time + 5f;
                return;
            }
            _dirty = false;
            _nextCheck = float.MaxValue;
            Work();
        }

        private bool CanWork()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !_nv.IsValid() || !_nv.IsOwner() || Plugin.Settings == null)
                return false;
            if (!StationAutoFill.IsOn(_nv))
                return false;
            float range = Plugin.Settings.AutoFillRange.Value;
            return ContainerFilter.SqrDistance(player.transform.position, transform.position) <= range * range;
        }

        // ---- grid

        /// <summary>Index of the centre cell (the scarecrow's own spot, never planted).</summary>
        internal int CenterIndex => (Rows / 2) * Cols + Cols / 2;

        /// <summary>World positions of the grid points (ground height), row-major, centred on the scarecrow.</summary>
        internal List<Vector3> Points()
        {
            var pts = new List<Vector3>(Cols * Rows);
            float sp = Spacing;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f)
                fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 start = transform.position - right * ((Cols - 1) * sp * 0.5f) - fwd * ((Rows - 1) * sp * 0.5f);
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    Vector3 p = start + right * (c * sp) + fwd * (r * sp);
                    if (ZoneSystem.instance != null)
                        p.y = ZoneSystem.instance.GetGroundHeight(p);
                    pts.Add(p);
                }
            }
            return pts;
        }

        internal bool Contains(Vector3 pos)
        {
            List<Vector3> pts = Points();
            float tol = Spacing * 0.5f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 d = pts[i] - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= tol * tol)
                    return true;
            }
            return false;
        }

        // ---- work

        private enum Spot { Empty, Growing, Ripe, Blocked }

        private void Work()
        {
            CropMap.Crop crop = CropMap.ForSeed(CropSeed);
            if (crop == null)
                return;
            Player player = Player.m_localPlayer;
            List<Vector3> pts = Points();
            var state = new Spot[pts.Count];
            var ripe = new Pickable[pts.Count];
            Scan(pts, crop, state, ripe);
            state[CenterIndex] = Spot.Blocked; // the scarecrow stands here
            ripe[CenterIndex] = null;

            int growing = 0;
            int ripeCount = 0;
            for (int i = 0; i < state.Length; i++)
            {
                if (state[i] == Spot.Growing) growing++;
                else if (state[i] == Spot.Ripe) ripeCount++;
            }

            int link = StationLink.Get(this);
            // Whole field ripe → harvest everything at once.
            if (Harvest && ripeCount > 0 && growing == 0)
            {
                for (int i = 0; i < ripe.Length; i++)
                {
                    if (ripe[i] == null)
                        continue;
                    ZNetView pnv = ripe[i].GetComponent<ZNetView>();
                    if (pnv == null || !pnv.IsValid())
                        continue;
                    if (!pnv.IsOwner())
                        pnv.ClaimOwnership();
                    Vector3 at = ripe[i].transform.position;
                    pnv.InvokeRPC("RPC_Pick", 0);
                    if (link > 0)
                        StationOutput.QueueIntakeLinkTag(at, link, null);
                    state[i] = Spot.Empty;
                }
                ActivityLog.Note(Label(), Loc.T("Harvested ", "Geerntet: ") + ripeCount + " " + DisplayFilters.ItemLabel(crop.Seed));
            }

            if (PlantOn)
                PlantEmpty(player, crop, pts, state, link);

            growing = 0;
            int empty = 0;
            for (int i = 0; i < state.Length; i++)
            {
                if (state[i] == Spot.Growing) growing++;
                else if (state[i] == Spot.Empty) empty++;
            }
            // Crops on another client's machine do not call our Grow hook → slow re-check.
            if (growing > 0)
                _nextCheck = Time.time + RecheckGrowingSeconds;
            // Missing seeds, or spots still empty (ground not cultivated yet / plant switched off):
            // look again later instead of waiting for a menu change.
            if (_missingSeeds || (PlantOn && empty > 0))
                _nextCheck = Mathf.Min(_nextCheck, Time.time + RetryMissingSeconds);
        }

        private void Scan(List<Vector3> pts, CropMap.Crop crop, Spot[] state, Pickable[] ripe)
        {
            float sp = Spacing;
            float tol = sp * 0.45f;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
                center += pts[i];
            center /= Mathf.Max(1, pts.Count);
            Vector3 half = new Vector3(Cols * sp * 0.5f + 0.5f, 3f, Rows * sp * 0.5f + 0.5f);
            Collider[] hits = Physics.OverlapBox(center, half, Quaternion.LookRotation(Flat(transform.forward)));
            var seen = new HashSet<int>();
            for (int h = 0; h < hits.Length; h++)
            {
                Plant plant = hits[h].GetComponentInParent<Plant>();
                Pickable pick = plant == null ? hits[h].GetComponentInParent<Pickable>() : null;
                Component c = (Component)plant ?? pick;
                if (c == null || !seen.Add(c.GetInstanceID()))
                    continue;
                int idx = Nearest(pts, c.transform.position, tol);
                if (idx < 0)
                    continue;
                if (plant != null)
                    state[idx] = Spot.Growing;
                else if (!pick.GetPicked() && CropMap.ForRipe(pick.gameObject) == crop)
                {
                    if (state[idx] == Spot.Empty)
                    {
                        state[idx] = Spot.Ripe;
                        ripe[idx] = pick;
                    }
                }
                else if (state[idx] == Spot.Empty)
                    state[idx] = Spot.Blocked;
            }
        }

        private void PlantEmpty(Player player, CropMap.Crop crop, List<Vector3> pts, Spot[] state, int link)
        {
            var targets = new List<int>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (state[i] != Spot.Empty)
                    continue;
                if (crop.NeedCultivated)
                {
                    Heightmap hm = Heightmap.FindHeightmap(pts[i]);
                    if (hm == null || !hm.IsCultivated(pts[i]))
                        continue;
                }
                targets.Add(i);
            }
            if (targets.Count == 0)
            {
                SetMissing(false, crop);
                return;
            }

            int got;
            StationFeed.PullOriginOverride = transform.position;
            StationFeed.PullRangeOverride = Plugin.Settings.AutoFillChestReach();
            try
            {
                got = StationFeed.ConsumeFromChests(player, crop.Seed, targets.Count, link);
            }
            finally
            {
                StationFeed.PullOriginOverride = null;
                StationFeed.PullRangeOverride = 0f;
            }

            for (int k = 0; k < got && k < targets.Count; k++)
            {
                int i = targets[k];
                GameObject go = Instantiate(crop.Sapling, pts[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Piece piece = go.GetComponent<Piece>();
                if (piece != null)
                {
                    SetCreator(piece, player);
                    piece.m_placeEffect?.Create(pts[i], go.transform.rotation, go.transform);
                }
                state[i] = Spot.Growing;
            }
            if (got > 0)
                ActivityLog.FromChest(Label(), got, DisplayFilters.ItemLabel(crop.Seed));
            SetMissing(got < targets.Count, crop);
        }

        private static System.Reflection.MethodInfo _setCreator;
        private static object _platformUser;
        private static bool _creatorResolved;

        /// <summary>
        /// Piece.SetCreator(playerId, PlatformUserID) like Player.PlacePiece. PlatformUserID lives in the
        /// Splatform assembly (not referenced) → reflection: PlatformManager.DistributionPlatform
        /// .LocalUser.PlatformUserID.
        /// </summary>
        private static void SetCreator(Piece piece, Player player)
        {
            try
            {
                if (!_creatorResolved)
                {
                    _creatorResolved = true;
                    System.Type pm = AccessTools.TypeByName("PlatformManager");
                    object platform = pm != null ? AccessTools.Property(pm, "DistributionPlatform")?.GetValue(null, null) : null;
                    object user = platform != null ? AccessTools.Property(platform.GetType(), "LocalUser")?.GetValue(platform, null) : null;
                    _platformUser = user != null ? AccessTools.Property(user.GetType(), "PlatformUserID")?.GetValue(user, null) : null;
                    _setCreator = AccessTools.Method(typeof(Piece), "SetCreator");
                }
                if (_setCreator != null && _platformUser != null)
                    _setCreator.Invoke(piece, new object[] { player.GetPlayerID(), _platformUser });
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug("Scarecrow SetCreator: " + ex.Message);
            }
        }

        private void SetMissing(bool missing, CropMap.Crop crop)
        {
            _missingSeeds = missing;
            if (missing)
                StationMissingLabel.SetMissing(this, "seed", DisplayFilters.ItemLabel(crop.Seed));
            else
                StationMissingLabel.ClearMissing(this, "seed");
        }

        private static int Nearest(List<Vector3> pts, Vector3 pos, float tol)
        {
            int best = -1;
            float bestSq = tol * tol;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 d = pts[i] - pos;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }

        private string Label()
        {
            return Loc.T("Scarecrow", "Vogelscheuche");
        }

        // ---- grid preview (ghost + open menu only)

        private void UpdatePreview(bool show)
        {
            if (!show)
            {
                if (_preview != null && _preview.activeSelf)
                    _preview.SetActive(false);
                return;
            }
            int hash = Cols * 7919 ^ Rows * 104729 ^ Spacing.GetHashCode()
                ^ transform.position.GetHashCode() ^ transform.rotation.GetHashCode();
            if (_preview == null)
            {
                _preview = new GameObject("SacFarmGrid");
                _previewHash = 0;
            }
            if (!_preview.activeSelf)
                _preview.SetActive(true);
            if (hash == _previewHash)
                return;
            _previewHash = hash;
            BuildPreview();
        }

        private void BuildPreview()
        {
            for (int i = _preview.transform.childCount - 1; i >= 0; i--)
                Destroy(_preview.transform.GetChild(i).gameObject);
            List<Vector3> pts = Points();
            int cols = Cols, rows = Rows;
            Material mat = PreviewMaterial();
            // Row lines and column lines through the planting spots, 5 cm above ground.
            for (int r = 0; r < rows; r++)
            {
                var line = new List<Vector3>();
                for (int c = 0; c < cols; c++)
                    line.Add(pts[r * cols + c] + Vector3.up * 0.05f);
                AddLine(line, mat);
            }
            for (int c = 0; c < cols; c++)
            {
                var line = new List<Vector3>();
                for (int r = 0; r < rows; r++)
                    line.Add(pts[r * cols + c] + Vector3.up * 0.05f);
                AddLine(line, mat);
            }
            // Small cross on every planting spot (not on the scarecrow's centre cell).
            float s = 0.12f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == CenterIndex)
                    continue;
                Vector3 p = pts[i];
                AddLine(new List<Vector3> { p + new Vector3(-s, 0.06f, 0f), p + new Vector3(s, 0.06f, 0f) }, mat);
                AddLine(new List<Vector3> { p + new Vector3(0f, 0.06f, -s), p + new Vector3(0f, 0.06f, s) }, mat);
            }
        }

        private void AddLine(List<Vector3> pts, Material mat)
        {
            if (pts.Count < 2)
            {
                if (pts.Count == 1)
                    pts.Add(pts[0] + Vector3.up * 0.3f);
                else
                    return;
            }
            var go = new GameObject("Line");
            go.transform.SetParent(_preview.transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.widthMultiplier = 0.04f;
            lr.material = mat;
            lr.startColor = lr.endColor = new Color(0.55f, 0.95f, 0.35f, 0.85f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private static Material _previewMat;

        private static Material PreviewMaterial()
        {
            if (_previewMat != null)
                return _previewMat;
            Shader sh = Shader.Find("Sprites/Default");
            _previewMat = new Material(sh != null ? sh : Shader.Find("Standard"));
            return _previewMat;
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_preview != null)
                Destroy(_preview);
        }

        // ---- hover / interact

        public string GetHoverName()
        {
            return Label();
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        public string GetHoverText()
        {
            string crop = string.IsNullOrEmpty(CropSeed)
                ? Loc.T("no crop selected", "keine Pflanze gewählt")
                : DisplayFilters.ItemLabel(CropSeed);
            string text = Label() + " (" + crop + ", " + Cols + " × " + Rows + ")";
            text += "\n[<color=yellow><b>" + ChestRename.PromptLabel() + "</b></color>] " + Loc.T("Settings", "Einstellungen");
            StationAutoFill.AppendHover(ref text, _nv, includeManualFill: false);
            StationLink.PrependHover(ref text, StationLink.Get(this), chest: false);
            return text;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            return false;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }

    /// <summary>A crop finished growing: scarecrows whose grid holds it re-check (no polling).</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
    internal static class ScarecrowPlantGrowPatch
    {
        private static void Postfix(GameObject __result)
        {
            if (__result == null || Scarecrow.Live.Count == 0)
                return;
            Vector3 pos = __result.transform.position;
            for (int i = 0; i < Scarecrow.Live.Count; i++)
            {
                Scarecrow s = Scarecrow.Live[i];
                if (s != null && s.Contains(pos))
                    s.MarkDirty();
            }
        }
    }
}
