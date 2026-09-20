using NUnit.Framework;
using SimWorldHost;
using UnityEngine;

namespace SimWorldHost.Tests
{
    /// <summary>
    /// <see cref="VisualRegistry"/> is the seam where a simulation <c>defName</c> becomes something
    /// drawable, and the two properties worth pinning are the ones a renderer silently depends on: a
    /// missing asset must degrade to a primitive rather than throw or draw nothing, and a variant choice
    /// must be the same every time for the same thing.
    /// </summary>
    public class VisualRegistryTests
    {
        [SetUp]
        public void Setup() => VisualRegistry.Reload();

        [Test]
        public void An_unknown_defName_resolves_to_nothing_rather_than_throwing()
        {
            // The whole point of the fallback: the renderer is built and profiled before art exists, and
            // assets land one defName at a time without the scene regressing in between.
            Assert.IsNull(VisualRegistry.Resolve("NoSuchDefEverShipped", 0));
            Assert.IsFalse(VisualRegistry.Has("NoSuchDefEverShipped"));
            Assert.AreEqual(0, VisualRegistry.VariantCount("NoSuchDefEverShipped"));
        }

        [Test]
        public void There_is_always_a_fallback_mesh_to_draw()
        {
            Mesh fallback = VisualRegistry.FallbackMesh;
            Assert.IsNotNull(fallback);
            Assert.Greater(fallback.vertexCount, 0);
        }

        [Test]
        public void Null_and_empty_defNames_are_answered_rather_than_crashing()
        {
            Assert.IsNull(VisualRegistry.Resolve(null, 0));
            Assert.IsNull(VisualRegistry.Resolve(string.Empty, 0));
            Assert.IsFalse(VisualRegistry.Has(null));
        }

        [Test]
        public void A_variant_suffix_is_stripped_but_a_real_underscore_is_not()
        {
            // Granite_a is one of four rocks; Plant_Berry is a defName that merely contains an underscore.
            Assert.AreEqual("Granite", VisualRegistry.BaseNameOf("Granite_a"));
            Assert.AreEqual("Granite", VisualRegistry.BaseNameOf("Granite_d"));
            Assert.AreEqual("Plant_Berry", VisualRegistry.BaseNameOf("Plant_Berry"));
            Assert.AreEqual("MineableSteel", VisualRegistry.BaseNameOf("MineableSteel"));
            Assert.AreEqual("Wall", VisualRegistry.BaseNameOf("Wall"));
        }

        [Test]
        public void The_same_thing_gets_the_same_variant_every_time()
        {
            // Stability across frames, across a save and load, and across machines. If this ever drifts,
            // rock would shimmer between shapes as the camera moved.
            for (int thingId = 0; thingId < 64; thingId++)
            {
                GameObject first = VisualRegistry.Resolve("Granite", thingId);
                GameObject second = VisualRegistry.Resolve("Granite", thingId);
                Assert.AreSame(first, second, $"variant for thing {thingId} was not stable");
            }
        }

        [Test]
        public void Variant_selection_survives_a_negative_thing_id()
        {
            // int.MinValue has no positive counterpart, so Math.Abs throws on it. Masking does not.
            Assert.DoesNotThrow(() => VisualRegistry.Resolve("Granite", int.MinValue));
            Assert.DoesNotThrow(() => VisualRegistry.Resolve("Granite", -1));
        }

        [Test]
        public void Rock_ships_more_than_one_silhouette()
        {
            // At the ~13,000 rock instances a measured settlement interior carries, a single mesh reads
            // as an obvious repeating grid. This asserts the variants are actually present, not that any
            // particular count is right.
            if (VisualRegistry.VariantCount("Granite") == 0)
            {
                Assert.Ignore("No Granite art imported yet; the fallback path covers this case.");
            }
            Assert.Greater(VisualRegistry.VariantCount("Granite"), 1);
        }

        [Test]
        public void Distinct_things_do_not_all_get_the_same_variant()
        {
            if (VisualRegistry.VariantCount("Granite") < 2)
            {
                Assert.Ignore("Needs at least two Granite variants imported.");
            }

            var seen = new System.Collections.Generic.HashSet<GameObject>();
            for (int thingId = 0; thingId < 256; thingId++)
            {
                seen.Add(VisualRegistry.Resolve("Granite", thingId));
            }
            Assert.Greater(seen.Count, 1, "every thing id chose the same variant; the spread is not working");
        }

        [Test]
        public void Sandstone_ships_four_variants_and_resolves_cleanly()
        {
            if (VisualRegistry.VariantCount("Sandstone") == 0)
            {
                Assert.Ignore("No Sandstone art imported yet; the fallback path covers this case.");
            }
            Assert.AreEqual(4, VisualRegistry.VariantCount("Sandstone"));

            var seen = new System.Collections.Generic.HashSet<GameObject>();
            for (int thingId = 0; thingId < 256; thingId++)
            {
                seen.Add(VisualRegistry.Resolve("Sandstone", thingId));
            }
            Assert.AreEqual(4, seen.Count, "all 4 Sandstone variants should be selected across thing ids");
        }

        [Test]
        public void Multi_era_dwellings_ship_four_variants_per_era_and_resolve_cleanly()
        {
            string[] eras = { "House_Paleo", "House_Meso", "House_Neo", "House_Chalco" };

            foreach (string era in eras)
            {
                Assert.AreEqual(4, VisualRegistry.VariantCount(era), $"{era} must have exactly 4 variants imported.");

                var seen = new System.Collections.Generic.HashSet<GameObject>();
                for (int thingId = 0; thingId < 256; thingId++)
                {
                    GameObject go = VisualRegistry.Resolve(era, thingId);
                    Assert.IsNotNull(go, $"{era} must resolve to a valid prefab for thingId {thingId}");
                    seen.Add(go);
                }
                Assert.AreEqual(4, seen.Count, $"all 4 variants of {era} should be selected across thing ids");
            }
        }
    }
}
