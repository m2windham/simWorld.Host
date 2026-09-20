using System.Collections.Generic;

// ThingCategory and AltitudeLayer are plain enums that ThingView carries by value. Importing them
// is not a crack in the seam: an enum is a number with names, not a Def, and no reference to a
// live object comes with it.
using SimWorld.Defs;
using SimWorld.Things;
using SimWorld.Map.View;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// UnityEngine.TerrainLayer is a real type and com.unity.modules.terrain is in the manifest, so a bare
// TerrainLayer here is ambiguous rather than wrong-in-an-obvious-way. Same family as the Map.Map and
// World.World shadowing the core's CLAUDE.md warns about: reach for the qualified form first.
using ViewTerrainLayer = SimWorld.Map.View.TerrainLayer;

namespace SimWorldHost
{
    /// <summary>
    /// Steps 1-3 of the renderer brief (<c>docs/host/renderer-brief.md</c> in the core repo): terrain as a
    /// palette-decoded texture, things as GPU-instanced boxes batched per defName, pawns as capsules
    /// interpolated between ticks by their stable <c>ThingId</c>.
    ///
    /// <para/><b>Binds to <c>SimWorld.Map.View</c> and nothing else.</b> Everything that crosses into this
    /// file is a defName string or a number. No <c>Def</c>, <c>Thing</c> or <c>Map</c> can reach it — the
    /// core side enforces that structurally with a reflection test over the whole seam namespace, and this
    /// file is written so that test stays the only guard anyone needs.
    ///
    /// <para/><b>Primitives on purpose.</b> Nothing here needs a single art asset; that is the point of the
    /// brief's build order. <see cref="VisualRegistry"/> is consulted for a real mesh and falls back to a
    /// coloured box, so a defName gains a model without this file changing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRenderer : MonoBehaviour
    {
        /// <summary>
        /// Instances per <c>RenderMeshInstanced</c> call. <c>Graphics.DrawMeshInstanced</c> documents a hard
        /// cap of 1023 and the docs for its replacement do not promise a larger one, so batch at the
        /// documented limit rather than discover the truth at 12,991 rock on someone else's GPU.
        /// </summary>
        public const int MaxInstancesPerBatch = 1023;

        /// <summary>
        /// Roofs draw just above the floor rather than at their real height, and this is a deliberate
        /// choice worth stating. Under the fixed orthographic camera a plane at height h is displaced in
        /// screen space by a constant offset — at the brief's 30 degree pitch, a roof at head height lands
        /// almost four cells away from the wall it sits on. Registering the overlay with the terrain it
        /// describes matters more than its altitude, so it renders flat; the cost is that tall geometry
        /// depth-occludes it, which reads correctly anyway.
        /// </summary>
        private const float RoofOverlayHeight = 0.02f;

        [Header("Layers")]
        [SerializeField] private bool _drawTerrain = true;
        [SerializeField] private bool _drawRoofs = true;
        [SerializeField] private bool _drawThings = true;
        [SerializeField] private bool _drawPawns = true;

        [Header("Optional status readout")]
        [SerializeField] private Text _status;

        // ---- seam state ----
        private MapViewVersions _held;
        private bool _hasScene;

        // ---- terrain and roofs: one version each, whole-layer, rebuilt only when the version moves ----
        private Mesh _groundQuad;
        private Texture2D _terrainTex;
        private Texture2D _roofTex;
        private Material _terrainMat;
        private Material _roofMat;
        private int _sizeX;
        private int _sizeZ;

        // ---- things: chunk is the batch boundary, which is why they are chunked at all ----
        private readonly Dictionary<int, Dictionary<string, List<Matrix4x4>>> _chunkBatches =
            new Dictionary<int, Dictionary<string, List<Matrix4x4>>>();
        private readonly Dictionary<string, List<Matrix4x4>> _thingBatches =
            new Dictionary<string, List<Matrix4x4>>();
        private bool _thingBatchesDirty;

        // ---- pawns: never chunked, never versioned, always complete ----
        private readonly Dictionary<int, PawnDraw> _pawns = new Dictionary<int, PawnDraw>();
        private readonly Dictionary<string, List<Matrix4x4>> _pawnBatches =
            new Dictionary<string, List<Matrix4x4>>();
        private readonly List<int> _retired = new List<int>();
        private Mesh _capsule;
        private int _lastTicksGame = int.MinValue;
        private float _lastTickRealTime;
        private float _tickPeriod = 1f / 60f;

        // ---- shared caches ----
        private readonly Dictionary<string, Mesh> _meshByBatchKey = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, Material> _matByBatchKey = new Dictionary<string, Material>();
        private static readonly Matrix4x4[] Scratch = new Matrix4x4[MaxInstancesPerBatch];

        /// <summary>What the renderer is doing, in the words the seam used. Never an exception: "no game",
        /// "no settlement on that tile" and "no interior yet" are three different sentences to show.</summary>
        public string Status { get; private set; } = "Waiting for content.";

        /// <summary>Whether a map is currently built and drawing. Public so a test or a HUD can ask.</summary>
        public bool HasScene => _hasScene;

        /// <summary>Cells across, of the map currently built. Zero when there is no scene.</summary>
        public int SizeX => _sizeX;

        /// <summary>Cells deep, of the map currently built. Zero when there is no scene.</summary>
        public int SizeZ => _sizeZ;

        /// <summary>Distinct instanced batches things currently draw in — one per defName per variant.</summary>
        public int ThingBatchCount => _thingBatches.Count;

        /// <summary>Pawns currently drawn. A pawn that left the map or died is retired, not stale.</summary>
        public int PawnCount => _pawns.Count;

        private void Awake()
        {
            CoreContentBootstrap.EnsureLoaded();
            VisualRegistry.EnsureLoaded();
            _groundQuad = BuildUnitQuadDoubleSided();
            _capsule = BuiltinMesh(PrimitiveType.Capsule);
        }

        private void Update()
        {
            Pump();
            Draw();
            if (_status != null) _status.text = Status;
        }

        // ------------------------------------------------------------------ the loop from the brief

        private void Pump()
        {
            if (!_hasScene)
            {
                MapViewSnapshot snapshot = MapViewSnapshot.Capture();

                // ContentLoaded before anything else: an unloaded host otherwise gets a valid-looking
                // empty snapshot, which reads exactly like a civilization that has not started. The god
                // view was bitten by this once already.
                if (!snapshot.ContentLoaded) { Status = "Core content not loaded."; return; }
                if (!snapshot.HasMap) { Status = snapshot.AbsenceReason; return; }

                BuildScene(snapshot);
                _held = snapshot.Versions;
                _hasScene = true;
                return;
            }

            MapViewDelta delta = MapViewSnapshot.CaptureChanges(_held);
            if (!delta.HasMap) { DropScene(); Status = delta.AbsenceReason; return; }

            // A resync means the held versions describe a map this one no longer is. Drop and let the
            // branch above rebuild from a full capture next frame, rather than patch across the seam.
            if (delta.FullResync) { DropScene(); return; }

            if (delta.Terrain != null) RebuildTerrain(delta.Terrain);
            if (delta.Roofs != null) RebuildRoofs(delta.Roofs);
            for (int i = 0; i < delta.ChangedChunks.Count; i++) RebuildChunk(delta.ChangedChunks[i]);
            SetPawns(delta.Pawns, delta.TicksGame);

            _held = delta.Versions;
            Status = $"{_sizeX}x{_sizeZ} — tick {delta.TicksGame} — {_pawns.Count} pawns, {_thingBatches.Count} batches";
        }

        private void BuildScene(MapViewSnapshot snapshot)
        {
            _sizeX = snapshot.SizeX;
            _sizeZ = snapshot.SizeZ;
            RebuildTerrain(snapshot.Terrain);
            RebuildRoofs(snapshot.Roofs);
            for (int i = 0; i < snapshot.Chunks.Count; i++) RebuildChunk(snapshot.Chunks[i]);
            SetPawns(snapshot.Pawns, snapshot.TicksGame);
            Status = $"Opened {snapshot.SettlementName} — {_sizeX}x{_sizeZ}, {_pawns.Count} pawns";
        }

        private void DropScene()
        {
            _hasScene = false;
            _held = null;
            _sizeX = 0;
            _sizeZ = 0;
            _chunkBatches.Clear();
            _thingBatches.Clear();
            _pawns.Clear();
            _pawnBatches.Clear();
            _thingBatchesDirty = false;
            _lastTicksGame = int.MinValue;
            if (_terrainTex != null) { Destroy(_terrainTex); _terrainTex = null; }
            if (_roofTex != null) { Destroy(_roofTex); _roofTex = null; }
        }

        // ------------------------------------------------------------------ terrain and roofs

        private void RebuildTerrain(ViewTerrainLayer layer)
        {
            _sizeX = layer.SizeX;
            _sizeZ = layer.SizeZ;

            var palette = new Color32[layer.Palette.Count];
            for (int i = 0; i < palette.Length; i++) palette[i] = StableColor(layer.Palette[i]);

            EnsureTexture(ref _terrainTex, "SimWorld/Terrain", layer.SizeX, layer.SizeZ);
            var pixels = new Color32[layer.Cells.Count];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = palette[layer.Cells[i]];
            _terrainTex.SetPixels32(pixels);
            _terrainTex.Apply(false);

            if (_terrainMat == null)
            {
                _terrainMat = new Material(RequireShader("Universal Render Pipeline/Unlit")) { name = "SimWorld/TerrainMat" };
            }
            _terrainMat.mainTexture = _terrainTex;
        }

        private void RebuildRoofs(RoofLayer layer)
        {
            var palette = new Color32[layer.Palette.Count];
            for (int i = 0; i < palette.Length; i++)
            {
                RoofView roof = layer.Palette[i];

                // Never hardcode "RoofRockThick". IsThickRoof and IsNatural are carried for exactly this
                // reason, and getting it wrong draws a solid mountain as open sky — on the settlements
                // where roofs matter most.
                Color c = roof.IsNatural
                    ? (roof.IsThickRoof ? new Color(0.13f, 0.12f, 0.14f, 0.88f) : new Color(0.28f, 0.27f, 0.30f, 0.55f))
                    : new Color(0.62f, 0.58f, 0.50f, 0.32f);
                palette[i] = c;
            }

            EnsureTexture(ref _roofTex, "SimWorld/Roofs", layer.SizeX, layer.SizeZ);
            var pixels = new Color32[layer.Cells.Count];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                int p = layer.Cells[i];
                pixels[i] = p == RoofLayer.Unroofed ? clear : palette[p];
            }
            _roofTex.SetPixels32(pixels);
            _roofTex.Apply(false);

            if (_roofMat == null)
            {
                _roofMat = new Material(RequireShader("Universal Render Pipeline/Unlit")) { name = "SimWorld/RoofMat" };
                if (_roofMat.HasProperty("_Surface"))
                {
                    _roofMat.SetFloat("_Surface", 1.0f); // Transparent
                    _roofMat.SetFloat("_Blend", 0.0f);   // Alpha blend
                    _roofMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _roofMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _roofMat.SetFloat("_ZWrite", 0.0f);
                    _roofMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    _roofMat.SetOverrideTag("RenderType", "Transparent");
                    _roofMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
            }
            _roofMat.mainTexture = _roofTex;
        }

        private static void EnsureTexture(ref Texture2D tex, string name, int w, int h)
        {
            if (tex != null && tex.width == w && tex.height == h) return;
            if (tex != null) Destroy(tex);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        // ------------------------------------------------------------------ things

        private void RebuildChunk(MapViewChunk chunk)
        {
            // Replace this chunk's contribution wholesale. A multi-cell thing is emitted once, in the chunk
            // that owns its Position, even where OccupiedSize spills into the neighbour — so rebuilding the
            // neighbour alone can never clear geometry this chunk placed.
            Dictionary<string, List<Matrix4x4>> batches;
            if (!_chunkBatches.TryGetValue(chunk.Index, out batches))
            {
                batches = new Dictionary<string, List<Matrix4x4>>();
                _chunkBatches[chunk.Index] = batches;
            }
            else
            {
                foreach (KeyValuePair<string, List<Matrix4x4>> kv in batches) kv.Value.Clear();
            }

            IReadOnlyList<ThingView> things = chunk.Things;
            for (int i = 0; i < things.Count; i++)
            {
                ThingView t = things[i];
                if (t.Category == ThingCategory.Mote || t.Category == ThingCategory.Ethereal) continue;

                string key = BatchKeyFor(t.DefName, t.ThingId);
                List<Matrix4x4> list;
                if (!batches.TryGetValue(key, out list))
                {
                    list = new List<Matrix4x4>();
                    batches[key] = list;
                }

                float height = HeightFor(t);
                float sx = Mathf.Max(1, t.OccupiedSize.x);
                float sz = Mathf.Max(1, t.OccupiedSize.z);
                var pos = new Vector3(t.OccupiedMin.x + sx * 0.5f, height * 0.5f, t.OccupiedMin.z + sz * 0.5f);
                list.Add(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(sx, height, sz)));
            }

            _thingBatchesDirty = true;
        }

        /// <summary>
        /// How tall a thing's box stands. Plant growth is read here and will not refresh on its own: stack
        /// count, hit points and growth are in-place field writes with no grid call to hook, so they dirty
        /// no chunk. That is the seam's documented trap 1, not a bug to chase — a renderer that wants live
        /// growth stages must re-pull them on its own schedule.
        /// </summary>
        private static float HeightFor(ThingView t)
        {
            switch (t.Category)
            {
                case ThingCategory.Building:
                    return t.Altitude == AltitudeLayer.Floor || t.Altitude == AltitudeLayer.FloorEmplacement
                        ? 0.08f
                        : 1f;
                case ThingCategory.Plant:
                    return 0.25f + 0.75f * Mathf.Clamp01(t.PlantGrowth);
                case ThingCategory.Item:
                    return 0.3f;
                case ThingCategory.Filth:
                    return 0.03f;
                case ThingCategory.Blueprint:
                    return 0.12f;
                default:
                    return 0.35f;
            }
        }

        private void MergeThingBatches()
        {
            foreach (KeyValuePair<string, List<Matrix4x4>> kv in _thingBatches) kv.Value.Clear();

            foreach (KeyValuePair<int, Dictionary<string, List<Matrix4x4>>> chunk in _chunkBatches)
            {
                foreach (KeyValuePair<string, List<Matrix4x4>> kv in chunk.Value)
                {
                    if (kv.Value.Count == 0) continue;
                    List<Matrix4x4> merged;
                    if (!_thingBatches.TryGetValue(kv.Key, out merged))
                    {
                        merged = new List<Matrix4x4>();
                        _thingBatches[kv.Key] = merged;
                    }
                    merged.AddRange(kv.Value);
                }
            }

            _thingBatchesDirty = false;
        }

        // ------------------------------------------------------------------ pawns

        private void SetPawns(IReadOnlyList<PawnView> pawns, int ticksGame)
        {
            // Pawns arrive complete every call, so a tick boundary is simply a change in TicksGame. Measure
            // the real time one took rather than assume 60 Hz: the harness drives ticks as fast as it can.
            bool newTick = ticksGame != _lastTicksGame;
            if (newTick)
            {
                if (_lastTicksGame != int.MinValue)
                {
                    _tickPeriod = Mathf.Clamp(Time.unscaledTime - _lastTickRealTime, 1f / 240f, 0.5f);
                }
                _lastTicksGame = ticksGame;
                _lastTickRealTime = Time.unscaledTime;
            }

            foreach (KeyValuePair<int, PawnDraw> kv in _pawns) kv.Value.Seen = false;

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnView p = pawns[i];
                var to = new Vector3(p.Position.x + 0.5f, 0f, p.Position.z + 0.5f);

                PawnDraw draw;
                if (!_pawns.TryGetValue(p.ThingId, out draw))
                {
                    draw = new PawnDraw { From = to };
                    _pawns[p.ThingId] = draw;
                }
                else if (newTick)
                {
                    draw.From = draw.To;
                }

                draw.To = to;
                draw.Seen = true;
                draw.Lying = p.Lying;
                draw.BodySize = Mathf.Max(0.2f, p.BodySize);
                draw.Key = PawnBatchKey(p);
            }

            // A pawn absent from Pawns has left the map or died. Retire what we drew for it — do not let a
            // stale capsule stand where a citizen used to be.
            _retired.Clear();
            foreach (KeyValuePair<int, PawnDraw> kv in _pawns)
            {
                if (!kv.Value.Seen) _retired.Add(kv.Key);
            }
            for (int i = 0; i < _retired.Count; i++) _pawns.Remove(_retired[i]);
        }

        private static string PawnBatchKey(PawnView p)
        {
            // '\0' cannot occur in a defName, so these three never collide with a real faction.
            if (p.Dead) return "\0dead";
            if (p.MentalStateDefName != null) return "\0berserk";
            return p.FactionDefName ?? "\0wild";
        }

        private void RebuildPawnBatches()
        {
            foreach (KeyValuePair<string, List<Matrix4x4>> kv in _pawnBatches) kv.Value.Clear();

            float t = Mathf.Clamp01((Time.unscaledTime - _lastTickRealTime) / _tickPeriod);

            foreach (KeyValuePair<int, PawnDraw> kv in _pawns)
            {
                PawnDraw d = kv.Value;

                List<Matrix4x4> list;
                if (!_pawnBatches.TryGetValue(d.Key, out list))
                {
                    list = new List<Matrix4x4>();
                    _pawnBatches[d.Key] = list;
                }

                Vector3 pos = Vector3.Lerp(d.From, d.To, t);
                float r = 0.3f * d.BodySize;
                float half = 0.45f * d.BodySize;   // the built-in capsule is 2 units tall

                if (d.Lying)
                {
                    pos.y = r;
                    list.Add(Matrix4x4.TRS(pos, Quaternion.Euler(90f, 0f, 0f), new Vector3(r * 2f, half, r * 2f)));
                }
                else
                {
                    pos.y = half;
                    list.Add(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(r * 2f, half, r * 2f)));
                }
            }
        }

        private sealed class PawnDraw
        {
            public Vector3 From;
            public Vector3 To;
            public bool Seen;
            public bool Lying;
            public float BodySize = 1f;
            public string Key = "\0wild";
        }

        // ------------------------------------------------------------------ drawing

        private void Draw()
        {
            if (!_hasScene) return;

            var bounds = new Bounds(
                new Vector3(_sizeX * 0.5f, 2f, _sizeZ * 0.5f),
                new Vector3(_sizeX + 8f, 16f, _sizeZ + 8f));

            if (_drawTerrain && _terrainMat != null)
            {
                Graphics.RenderMesh(
                    new RenderParams(_terrainMat) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off },
                    _groundQuad, 0,
                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_sizeX, 1f, _sizeZ)));
            }

            if (_drawRoofs && _roofMat != null)
            {
                Graphics.RenderMesh(
                    new RenderParams(_roofMat) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off },
                    _groundQuad, 0,
                    Matrix4x4.TRS(new Vector3(0f, RoofOverlayHeight, 0f), Quaternion.identity, new Vector3(_sizeX, 1f, _sizeZ)));
            }

            if (_drawThings)
            {
                if (_thingBatchesDirty) MergeThingBatches();
                DrawBatches(_thingBatches, bounds, null);
            }

            if (_drawPawns)
            {
                RebuildPawnBatches();
                DrawBatches(_pawnBatches, bounds, _capsule);
            }
        }

        private void DrawBatches(Dictionary<string, List<Matrix4x4>> batches, Bounds bounds, Mesh forced)
        {
            foreach (KeyValuePair<string, List<Matrix4x4>> kv in batches)
            {
                List<Matrix4x4> list = kv.Value;
                if (list.Count == 0) continue;

                Mesh mesh = forced != null ? forced : MeshFor(kv.Key);
                if (mesh == null) continue;

                var rp = new RenderParams(MaterialFor(kv.Key))
                {
                    worldBounds = bounds,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off,
                };

                for (int start = 0; start < list.Count; start += MaxInstancesPerBatch)
                {
                    int n = Mathf.Min(MaxInstancesPerBatch, list.Count - start);
                    list.CopyTo(start, Scratch, 0, n);
                    Graphics.RenderMeshInstanced(rp, mesh, 0, Scratch, n);
                }
            }
        }

        // ------------------------------------------------------------------ meshes, materials, colour

        /// <summary>
        /// The batch key is the resolved prefab's name where a model exists and the defName otherwise.
        /// That matters: rock ships as Granite_a..Granite_d and the variants are different meshes, so they
        /// have to batch apart — while still sharing one colour, which is keyed off the defName.
        /// </summary>
        private static string BatchKeyFor(string defName, int thingId)
        {
            GameObject prefab = VisualRegistry.Resolve(defName, thingId);
            return prefab != null ? prefab.name : defName;
        }

        private Mesh MeshFor(string batchKey)
        {
            Mesh mesh;
            if (_meshByBatchKey.TryGetValue(batchKey, out mesh)) return mesh;

            GameObject prefab = VisualRegistry.Resolve(batchKey, 0);
            if (prefab != null)
            {
                MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh == null) mesh = VisualRegistry.FallbackMesh;

            _meshByBatchKey[batchKey] = mesh;
            return mesh;
        }

        private Material MaterialFor(string batchKey)
        {
            Material mat;
            if (_matByBatchKey.TryGetValue(batchKey, out mat)) return mat;

            // One material per batch key, so colour is an ordinary material property and never an
            // instancing-buffer question. Per-batch is exactly the granularity the brief asks for.
            Shader shader = RequireShader("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = "SimWorld/" + batchKey, enableInstancing = true };
            Color color = StableColor(VisualRegistry.BaseNameOf(batchKey));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            _matByBatchKey[batchKey] = mat;
            return mat;
        }

        /// <summary>
        /// A colour per defName, the same colour in every session and on every machine.
        /// <c>string.GetHashCode</c> is explicitly not stable across processes or runtimes, so this is
        /// FNV-1a: a rock that is olive today must be olive after a save, a reload and on someone else's
        /// GPU, for the same reason the core picks mesh variants from a ThingId rather than a roll.
        /// </summary>
        public static Color StableColor(string key)
        {
            if (string.IsNullOrEmpty(key)) return new Color(0.8f, 0f, 0.8f);   // unmistakably wrong

            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < key.Length; i++)
                {
                    h ^= key[i];
                    h *= 16777619u;
                }

                float hue = (h % 3600u) / 3600f;
                float sat = 0.32f + ((h >> 12) % 43u) / 100f;   // 0.32 .. 0.74
                float val = 0.42f + ((h >> 21) % 47u) / 100f;   // 0.42 .. 0.88
                return DefColors.Family(key, hue, sat, val);
            }
        }

        /// <summary>
        /// A unit quad in the XZ plane, wound both ways. The shaders it carries are unlit, so a back face
        /// costs nothing and lights nothing wrong — and it removes the one bug that is genuinely tedious to
        /// diagnose against a camera that never rotates: a quad that is simply invisible because its
        /// winding faced away.
        /// </summary>
        public static Mesh BuildUnitQuadDoubleSided()
        {
            var mesh = new Mesh { name = "SimWorld/GroundQuad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(1f, 0f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
                throw new System.InvalidOperationException(
                    $"Shader '{name}' not found. It is almost certainly a Built-in " +
                    "pipeline shader that does not exist under the active render " +
                    "pipeline. Renderer materials must name URP shaders.");
            return shader;
        }

        private static Mesh BuiltinMesh(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            return mesh;
        }
    }
}
