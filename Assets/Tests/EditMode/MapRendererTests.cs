using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

namespace SimWorldHost.Tests
{
    /// <summary>
    /// Covers the parts of the renderer that are logic rather than pixels: the colour policy, the quad it
    /// draws terrain on, and — structurally — that nothing from the core leaks across the seam into it.
    /// Runs in EditMode; none of it needs a scene, a camera or a game.
    /// </summary>
    public sealed class MapRendererTests
    {
        // ---------------------------------------------------------------- colour: stability

        /// <summary>
        /// The whole point of not using <c>string.GetHashCode</c>. Reimplemented here rather than compared
        /// against a magic number, so the test pins the algorithm and not a value nobody can source.
        /// </summary>
        [Test]
        public void HashIsFnv1a()
        {
            foreach (string s in new[] { "Sandstone", "Soil", "WaterShallow", "", "a", "Plant_Berry" })
            {
                uint expected = 2166136261u;
                unchecked
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        expected ^= s[i];
                        expected *= 16777619u;
                    }
                }

                Assert.AreEqual(expected, DefColors.Hash(s), $"FNV-1a mismatch for '{s}'");
            }
        }

        [Test]
        public void SameDefNameIsAlwaysTheSameColour()
        {
            Color first = DefColors.For("Sandstone");
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(first, DefColors.For("Sandstone"),
                    "A rock that changes colour between calls would change it between sessions too.");
            }
        }

        [Test]
        public void MissingDefNameIsTheReservedColour()
        {
            Assert.AreEqual(DefColors.Missing, DefColors.For(null));
            Assert.AreEqual(DefColors.Missing, DefColors.For(string.Empty));
        }

        // ---------------------------------------------------------------- colour: families

        [Test]
        public void WaterIsBlueSoilIsBrownPlantsAreGreen()
        {
            AssertHueNear("WaterShallow", 0.56f);
            AssertHueNear("WaterDeep", 0.56f);
            AssertHueNear("Soil", 0.07f);
            AssertHueNear("SoilRich", 0.07f);
            AssertHueNear("WildPlant", 0.28f);
            AssertHueNear("Plant_Berry", 0.28f);
        }

        /// <summary>
        /// The ordering trap the family table is written around: "Sandstone" contains both "stone" and
        /// "sand", and it is stone. Stone is near-grey, sand is not, so saturation tells them apart without
        /// asserting a colour literal.
        /// </summary>
        [Test]
        public void SandstoneIsStoneRatherThanSand()
        {
            float stoneSat = Saturation("Sandstone");
            float sandSat = Saturation("SandBeach");

            Assert.Less(stoneSat, 0.20f, "Stone should come out near-grey.");
            Assert.Greater(sandSat, 0.20f, "Sand should keep its colour.");
            Assert.Less(stoneSat, sandSat);
        }

        [Test]
        public void ChunkGraniteIsStoneToo()
        {
            Assert.Less(Saturation("ChunkGranite"), 0.20f);
            Assert.Less(Saturation("ChunkLimestone"), 0.20f);
        }

        [Test]
        public void TwoDefsInOneFamilyStillReadApart()
        {
            // The family sets the hue; the hash still has to move something, or every soil is one colour.
            Assert.AreNotEqual(DefColors.For("Soil"), DefColors.For("SoilRich"));
            Assert.AreNotEqual(DefColors.For("WildPlant"), DefColors.For("Plant_Berry"));
        }

        [Test]
        public void ADefNameInNoFamilyStillGetsAColour()
        {
            // Falls through to the hash, which has to stay good on its own rather than be a path nobody
            // looks at — a content pack whose stone def is not spelled "stone" lands here.
            Color c = DefColors.For("Zzyzx_Peculiar_Widget");
            Assert.AreNotEqual(DefColors.Missing, c);

            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            Assert.Greater(v, 0.3f, "Nothing should come out black.");
            Assert.Greater(s, 0.2f, "An unfamilied def should still be distinguishable.");
        }

        // ---------------------------------------------------------------- the ground quad

        /// <summary>
        /// Wound both ways on purpose. The bug this removes is the tedious one: a quad that is simply
        /// invisible because its winding faced away from a camera that never rotates.
        /// </summary>
        [Test]
        public void GroundQuadIsDoubleSided()
        {
            Mesh mesh = MapRenderer.BuildUnitQuadDoubleSided();
            try
            {
                Assert.AreEqual(4, mesh.vertexCount, "Four corners is all a quad needs.");

                int[] tris = mesh.triangles;
                Assert.AreEqual(12, tris.Length, "Two triangles, each wound both ways.");

                // Every front-facing triangle must appear reversed among the back-facing ones.
                var front = new List<int[]> { new[] { tris[0], tris[1], tris[2] }, new[] { tris[3], tris[4], tris[5] } };
                var back = new List<int[]> { new[] { tris[6], tris[7], tris[8] }, new[] { tris[9], tris[10], tris[11] } };

                foreach (int[] f in front)
                {
                    bool mirrored = false;
                    foreach (int[] b in back)
                    {
                        if (SameTriangleOppositeWinding(f, b)) { mirrored = true; break; }
                    }
                    Assert.IsTrue(mirrored, $"Triangle ({f[0]},{f[1]},{f[2]}) has no reversed twin.");
                }

                Assert.AreEqual(new Vector3(1f, 0f, 1f), mesh.bounds.size,
                    "A unit quad in XZ, so one matrix scales it to any map size.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // ---------------------------------------------------------------- the seam, structurally

        /// <summary>
        /// The host's half of the rule the core already enforces from its side: bind to the View
        /// namespaces, hold nothing else. A field typed <c>ThingDef</c> or <c>Map</c> would mean the host
        /// had got hold of a live object, and no amount of care elsewhere would take it back.
        ///
        /// <para/>Enums are allowed through: <c>ThingCategory</c> is a number with names, carried by value
        /// on <c>ThingView</c>, and nothing reachable hangs off it.
        /// </summary>
        [Test]
        public void MapRendererHoldsNothingFromTheCoreOutsideTheViewNamespaces()
        {
            var offenders = new List<string>();
            var seen = new HashSet<Type>();
            Inspect(typeof(MapRenderer), offenders, seen);

            Assert.IsEmpty(offenders,
                "These fields reach past the seam:\n  " + string.Join("\n  ", offenders.ToArray()));
        }

        private static void Inspect(Type owner, List<string> offenders, HashSet<Type> seen)
        {
            if (!seen.Add(owner)) return;

            FieldInfo[] fields = owner.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo field in fields)
            {
                foreach (Type t in Flatten(field.FieldType))
                {
                    string ns = t.Namespace ?? string.Empty;

                    if (ns.StartsWith("SimWorldHost", StringComparison.Ordinal))
                    {
                        Inspect(t, offenders, seen);   // our own nested holders are fair game, so recurse
                        continue;
                    }

                    if (!ns.StartsWith("SimWorld", StringComparison.Ordinal)) continue;
                    if (t.IsEnum) continue;
                    if (ns == "SimWorld.Map.View" || ns == "SimWorld.God.View") continue;

                    offenders.Add($"{owner.Name}.{field.Name} : {t.FullName}");
                }
            }
        }

        private static IEnumerable<Type> Flatten(Type t)
        {
            yield return t;

            if (t.IsArray && t.GetElementType() != null)
            {
                foreach (Type inner in Flatten(t.GetElementType())) yield return inner;
            }

            if (t.IsGenericType)
            {
                foreach (Type arg in t.GetGenericArguments())
                {
                    foreach (Type inner in Flatten(arg)) yield return inner;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void AssertHueNear(string defName, float expected)
        {
            float h = Hue(defName);

            // The family sets the hue and the hash jitters it by +/-0.025, so this is a band, not a value.
            float delta = Mathf.Abs(Mathf.DeltaAngle(h * 360f, expected * 360f)) / 360f;
            Assert.Less(delta, 0.04f, $"'{defName}' hue {h:F3} is not in the family at {expected:F3}.");
        }

        private static float Hue(string defName)
        {
            float h, s, v;
            Color.RGBToHSV(DefColors.For(defName), out h, out s, out v);
            return h;
        }

        private static float Saturation(string defName)
        {
            float h, s, v;
            Color.RGBToHSV(DefColors.For(defName), out h, out s, out v);
            return s;
        }

        private static bool SameTriangleOppositeWinding(int[] a, int[] b)
        {
            return (a[0] == b[0] && a[1] == b[2] && a[2] == b[1])
                || (a[0] == b[1] && a[1] == b[0] && a[2] == b[2])
                || (a[0] == b[2] && a[1] == b[1] && a[2] == b[0]);
        }
    }
}
