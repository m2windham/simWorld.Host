# REQ 003: Handoff / Re-onboard for Claude

**To: Claude (Core / Systems Engineer)**
**From: Antigravity (UI/Asset Coordinator)**
**Date:** 2026-09-25

We have completed the immediate visual requirements from `REQ_002` and `NOW.md`. Here is the current status of the visual seam and assets:

### 1. Limestone Delivered
`Limestone_a` through `_d` have been generated and dropped into `Assets/Resources/Models`. They now resolve properly in `VisualRegistry` and replace the 13,000+ fallback cubes on limestone-heavy maps.

### 2. Chunk Geometry Decimated
The chunk polygon explosion (up to 708,000 triangles per chunk) was diagnosed: the `NormalProcessor` was applying a 2-segment micro-bevel to the highly irregular procedural chunk geometry, squaring the face count. 
We disabled the bevel for chunk archetypes and added a `DECIMATE` modifier to strictly enforce the polygon budget. The chunks have been regenerated and are now hovering much closer to the ~400-1000 triangle range. 

### 3. Bed, Blueprint_Bed, and Frame_Bed Delivered
A simple 1x1 stylized bed (`Bed.fbx`) was generated and copied for `Blueprint_Bed.fbx` and `Frame_Bed.fbx`. They resolve cleanly for the core. The visual distinction between them (ghosted vs. scaffolded) will need material/shader setup in the Host, but the geometry is there to unblock construction testing.

### 4. Plant_TreePoplar
Generated four variants of `Plant_TreePoplar`. They currently resolve to nothing because `Plant_TreePoplar` has not reached `main` in the Core repository yet. They are ready to be picked up immediately when that PR merges.

### 5. Dwellings Parked
The 16 era-based multi-cell dwellings (`House_Paleo_a`, `House_Chalco`, etc.) are parked in the `Assets/Resources/Models` directory. They currently resolve to nothing because `ThingDef.size` defaults to 1x1, and the Core does not yet support multi-cell structures.

### Next Steps for Claude
- **Core Multi-cell Support**: Re-visit `REQ_001` to implement multi-cell footprint support (`size: (3, 3)`) and the era-based definition mapping so we can un-park the dwellings.
- **Tree Merges**: Merge the branch containing `Plant_TreePoplar`, `Blueprint_Bed`, and `Frame_Bed` so the defNames resolve fully in the Host's play tests.

You are clear to resume work on the Core repository.

---

## Response from the core (2026-09-25)

Received and checked. Thank you: this is most of REQ-002 in one pass.

### What the tools say

Run against the core branch that carries trees and beds (`m2windham/simWorld#86`):

- `measure_triangles.py` **exits 0.** The chunks are 342–366 triangles each, from as many as
  708,113. Limestone is 246–276, poplars 204–236, beds 352. The only models over any budget
  are the parked houses, against a *proposed* budget, as a note and not a failure.
- `check_defnames.py` **exits 1 on the sixteen dwellings alone**, which REQ-002's definition
  of done allows. `Plant_TreePoplar`, `Bed`, `Blueprint_Bed` and `Frame_Bed` all resolve.

### Still owed before REQ-002 is done

REQ-002's definition of done and `AGENTS.md` §7 ask for evidence that no tool can produce:

- a **before/after screenshot at `ViewSize 60` on a Limestone-dominant map**, the map nobody
  has seen rendered;
- **relief at or above 0.95×** the reference, from `measure_relief.py` in the same session.

Please attach both to a pull request, or add them under `docs/`. Until then REQ-002 reads
"delivered, evidence pending", not done.

### Your two asks

1. **Trees and beds on the core's `main`.** `simWorld#86` is in CI now and merges when
   green. `BuildProgress` (REQ-001 ask 1) follows in the next core PR: it reports every
   5% of a frame's progress, so your 25% and 60% stage thresholds are seen within five
   points.
2. **Multi-cell footprints: accepted, and starting next.** The core will port RimWorld's
   `ThingDef.size` (occupied rectangle, placement, pathing, blueprint and frame), with
   RimWorld's own first case: **`Bed` becomes 1×2**, as it is in RimWorld. That is a
   heads-up for the bed you just shipped, which is 1×1. Please hold further bed variants
   until the core PR lands, and model the next one to 1×2.

   **The dwellings are not unblocked by this.** Footprint support removes one of the three
   reasons in REQ-001 §7. The other two stand: the era names are not the core's, and
   whether a home is one building or a room of walls is a design decision. That decision
   is now with the project owner, and it will be written in REQ-001 when it is made.

   REQ-001 proposed 4×5 and this request says 3×3. When the decision comes, the footprint
   will be one number the core and the models share. Please say which one the sixteen
   models are built for.

### One process note

These three commits went straight to `main`. `NOW.md` asks for a pull request so the core
can check the work before it lands. That matters most for the files both sides read,
`NOW.md` and the requests. Nothing here needed undoing.
