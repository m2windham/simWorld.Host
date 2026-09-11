using NUnit.Framework;
using SimWorld.Defs;
using SimWorld.God;
using SimWorld.God.View;

namespace SimWorldHost.Tests
{
    /// <summary>
    /// Proves the Unity host actually reaches the engine-free core, the same way
    /// <c>tests/SimWorld.Core.Tests/God/GodViewTests.cs</c> proves the core's side of the seam. Runs in
    /// EditMode — no scene, no Play mode needed, since <see cref="GodViewSnapshot.Capture"/> and
    /// <see cref="GodCommands"/> are plain calls, not MonoBehaviour lifecycle.
    /// </summary>
    public sealed class GodViewSeamTests
    {
        [SetUp]
        public void SetUp() => CoreContentBootstrap.EnsureLoaded();

        [Test]
        public void CoreContentLoads()
        {
            Assert.Greater(DefDatabase<EdictDef>.DefCount, 0,
                "Content failed to load, or loaded empty — see CoreContentBootstrap.");
        }

        [Test]
        public void CaptureIsSafeBeforeAWorldExists()
        {
            GodViewSnapshot snapshot = GodViewSnapshot.Capture();

            Assert.AreEqual(0, snapshot.Civilization.SettlementCount);
            Assert.Greater(snapshot.Edicts.Count, 0, "Edict content should be present even with no world.");
        }

        [Test]
        public void IssueEdictRoundTripsThroughTheSnapshot()
        {
            GodViewSnapshot before = GodViewSnapshot.Capture();
            EdictOption option = FindAvailable(before);

            GodCommandResult result = GodCommands.IssueEdict(option.DefName);
            Assert.AreEqual(GodCommandOutcome.Done, result.Outcome);

            GodViewSnapshot after = GodViewSnapshot.Capture();
            EdictOption updated = Find(after, option.DefName);
            Assert.AreEqual(EdictAvailability.Active, updated.Availability);

            // Leave the world as this test found it, for whichever test runs next.
            GodCommands.RescindEdict(option.DefName);
        }

        private static EdictOption FindAvailable(GodViewSnapshot snapshot)
        {
            foreach (EdictOption option in snapshot.Edicts)
            {
                if (option.Availability == EdictAvailability.Available) return option;
            }

            Assert.Fail("No edict is Available right now — nothing to issue in this test.");
            return null!; // unreachable; Assert.Fail throws
        }

        private static EdictOption Find(GodViewSnapshot snapshot, string defName)
        {
            foreach (EdictOption option in snapshot.Edicts)
            {
                if (option.DefName == defName) return option;
            }

            Assert.Fail($"'{defName}' is not in the snapshot's edict list.");
            return null!;
        }
    }
}
