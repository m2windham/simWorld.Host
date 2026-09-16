using System.Collections.Generic;
using UnityEngine;

namespace SimWorldHost
{
    /// <summary>
    /// The one place a <c>defName</c> becomes something drawable.
    ///
    /// <para/><b>Why this lives in the host and not the core.</b> There is no <c>texPath</c>,
    /// <c>graphicData</c> or <c>graphicClass</c> anywhere in the shipped content — zero hits across every
    /// Def folder — and that is correct rather than an oversight: <c>SimWorld.Core</c> is engine-free by
    /// rule and may never reference <c>UnityEngine</c>. So the simulation says <i>what</i> a thing is and
    /// the host decides what it looks like, keyed by the only handle that crosses the seam. Every handle
    /// <c>Map/View</c> hands out is a <c>defName</c> string, so this mirrors that exactly.
    ///
    /// <para/><b>The fallback is the point.</b> A missing entry returns a primitive rather than throwing
    /// or drawing nothing. That is what lets the renderer be built and profiled before a single asset
    /// exists, and lets assets drop in one <c>defName</c> at a time without the scene regressing in
    /// between. The renderer never waits on the model pipeline and the pipeline never waits on the
    /// renderer.
    ///
    /// <para/><b>FBX, not glTF.</b> The model pipeline emits both. Unity imports FBX natively and needs a
    /// package (glTFast or UnityGLTF) for <c>.glb</c>, so the host reads the FBX and the <c>.glb</c> is
    /// there for engines that prefer it. Nothing here needs changing if that decision is revisited —
    /// <c>Resources.LoadAll</c> returns whatever Unity managed to import.
    ///
    /// <para/><b>Variants are chosen by thing id, never by a roll.</b> Rock ships as
    /// <c>Granite_a</c>…<c>Granite_d</c> because at the ~13,000 instances a measured map carries, one mesh
    /// reads as an obvious repeating grid. Which variant a given rock gets is a pure function of its
    /// <c>ThingId</c>, so it is stable across frames, across a save and load, and across machines — and it
    /// draws nothing from any random stream, which is the same discipline the core holds itself to
    /// (see <c>Economy/SettlementLarder</c> and friends: no draw inside a tick path, ever).
    /// </summary>
    public static class VisualRegistry
    {
        /// <summary>Everything under <c>Assets/Resources/&lt;ResourceFolder&gt;/</c> is a candidate visual.</summary>
        public const string ResourceFolder = "Models";

        /// <summary>Base <c>defName</c> to its variants, in a stable order.</summary>
        private static Dictionary<string, GameObject[]> _byDefName;

        private static Mesh _fallbackMesh;

        /// <summary>How many distinct <c>defName</c>s resolved to real art. Zero means the load found
        /// nothing, which reads exactly like "no art yet" and is worth logging rather than guessing at.</summary>
        public static int LoadedDefNameCount => _byDefName?.Count ?? 0;

        public static void EnsureLoaded()
        {
            if (_byDefName != null) return;
            Reload();
        }

        /// <summary>Rescans the resource folder. Exposed so an Editor tool can pick up a newly imported
        /// asset without a domain reload.</summary>
        public static void Reload()
        {
            var grouped = new Dictionary<string, List<GameObject>>();
            GameObject[] all = Resources.LoadAll<GameObject>(ResourceFolder);

            foreach (GameObject go in all)
            {
                string defName = BaseNameOf(go.name);
                if (!grouped.TryGetValue(defName, out List<GameObject> list))
                {
                    list = new List<GameObject>();
                    grouped[defName] = list;
                }
                list.Add(go);
            }

            _byDefName = new Dictionary<string, GameObject[]>(grouped.Count);
            foreach (KeyValuePair<string, List<GameObject>> kv in grouped)
            {
                // Sorted so variant order does not depend on the filesystem's enumeration order, which
                // would make the same rock look different on a different machine.
                kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                _byDefName[kv.Key] = kv.Value.ToArray();
            }
        }

        /// <summary>
        /// Strips a trailing variant suffix: <c>Granite_a</c> becomes <c>Granite</c>. A single trailing
        /// letter after an underscore is the convention the model pipeline writes; anything longer is
        /// part of the name itself, so <c>Plant_Berry</c> and <c>MineableSteel</c> survive untouched.
        /// </summary>
        public static string BaseNameOf(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return assetName;
            int underscore = assetName.LastIndexOf('_');
            if (underscore <= 0 || underscore != assetName.Length - 2) return assetName;
            char suffix = assetName[assetName.Length - 1];
            return char.IsLetter(suffix) && char.IsLower(suffix) ? assetName.Substring(0, underscore) : assetName;
        }

        /// <summary>
        /// The prefab to draw for this <c>defName</c>, or null when there is no art for it yet — in which
        /// case the caller draws <see cref="FallbackMesh"/>. <paramref name="thingId"/> selects among
        /// variants deterministically.
        /// </summary>
        public static GameObject Resolve(string defName, int thingId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(defName)) return null;
            if (!_byDefName.TryGetValue(defName, out GameObject[] variants) || variants.Length == 0) return null;
            if (variants.Length == 1) return variants[0];

            // Mask rather than Abs: int.MinValue has no positive counterpart and Abs throws on it, which
            // would be a crash that only appears once a thing id happens to land there.
            return variants[(thingId & int.MaxValue) % variants.Length];
        }

        public static bool Has(string defName)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(defName) && _byDefName.ContainsKey(defName);
        }

        /// <summary>How many variants exist for a <c>defName</c>; zero when it has no art.</summary>
        public static int VariantCount(string defName)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(defName) && _byDefName.TryGetValue(defName, out GameObject[] v) ? v.Length : 0;
        }

        /// <summary>A unit cube, sized to one grid cell, for everything with no art yet.</summary>
        public static Mesh FallbackMesh
        {
            get
            {
                if (_fallbackMesh != null) return _fallbackMesh;
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _fallbackMesh = temp.GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
                Object.DestroyImmediate(temp);
#else
                Object.Destroy(temp);
#endif
                return _fallbackMesh;
            }
        }
    }
}
