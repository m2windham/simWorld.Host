using System;
using System.Linq;
using SimWorld.Content;
using SimWorld.Defs;
using SimWorld.God;
using UnityEngine;

namespace SimWorldHost
{
    /// <summary>
    /// Loads the shipped core content into <see cref="DefDatabase.Global"/> once. Without this,
    /// <c>GodViewSnapshot.Capture()</c> is not wrong, just empty — <c>BuildEdictOptions</c> iterates an
    /// empty <see cref="DefDatabase"/>, which reads as "no edicts" indistinguishably from "content never
    /// loaded". Mirrors <c>tools/bench/SimWorld.Bench/Bootstrap.cs</c> on the core side.
    ///
    /// <para/>Points <see cref="CoreContent.DataEnvironmentVariable"/> at the sibling repo explicitly
    /// rather than relying on <c>CoreContent</c>'s own directory-walking discovery: that walk looks for
    /// <c>src/SimWorld.Core/Data</c> under each ancestor of the running assembly's location, which finds
    /// it when the core is a subdirectory of the host but not when the two are sibling repos under
    /// <c>A:\dev\</c>, which is exactly this layout.
    ///
    /// <para/><b>Called from each consumer's own <c>Awake</c> rather than
    /// <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>.</b> That attribute fired too early in
    /// this Editor's Play-mode startup: <see cref="CoreContent.Load"/> returned success with zero defs —
    /// no exception, no error, just an empty database indistinguishable from "nothing wrong" until you
    /// count. Calling this from <c>Awake</c> (which runs after Unity's own scene/subsystem init, not
    /// before it) is a timing this session verified working live rather than a guess. The def-count
    /// check below exists specifically so that failure mode cannot go quiet again.
    /// </summary>
    internal static class CoreContentBootstrap
    {
        public static void EnsureLoaded()
        {
            if (DefDatabase.Global != null && DefDatabase<EdictDef>.DefCount > 0) return;

            string dataDir = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "..", "..", "simWorld", "src", "SimWorld.Core", "Data"));
            Environment.SetEnvironmentVariable(CoreContent.DataEnvironmentVariable, dataDir);

            var database = new DefDatabase();
            DefLoadResult result = CoreContent.Load(database, new DefTypeResolver(), new DefLoadOptions { BindDefOfs = true });
            if (!result.Success)
            {
                string details = string.Join("\n", result.Errors.Select(e => "  - " + e));
                Debug.LogError($"[CoreContentBootstrap] Core content failed to load from '{dataDir}':\n{details}");
                return;
            }

            int edictCount = database.For<EdictDef>().AllDefsListForReading.Count;
            if (edictCount == 0)
            {
                Debug.LogError(
                    $"[CoreContentBootstrap] Load reported success but found 0 EdictDefs from '{dataDir}'. " +
                    "Treating as a failure rather than an empty-but-fine content set.");
                return;
            }

            DefDatabase.Global = database;
            Debug.Log($"[CoreContentBootstrap] Loaded core content from '{dataDir}' ({edictCount} edicts).");
        }
    }
}
