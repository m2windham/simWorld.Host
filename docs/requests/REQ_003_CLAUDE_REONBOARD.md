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
