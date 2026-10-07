# Now: what to do next in this repository

This is the one file that changes as the work moves. `AGENTS.md` is the standing
brief. It does not change often, and its §4 table is a snapshot from before lane
zero, so do not plan from it. Plan from this file.

_Updated 2026-10-06._

## If you are picking this up after a gap

**The machine changed under this project in late September 2026**: the `A:` drive
died and everything was re-cloned to `H:\dev`. Read `AGENTS.md` §0 for the new
environment before anything else. Sync before you touch anything:

```text
cd H:\dev\simWorld.Host
git status                  # uncommitted work? commit it to a branch first
git switch main
git pull --ff-only

cd H:\dev\simWorld
git switch main
git pull --ff-only
```

The three repositories must stay siblings under `H:\dev` (`simWorld`,
`simWorld.Host`, `simWorld.Model`), because the host reaches the core through the
relative path in `Packages/manifest.json` and the asset pipeline delivers into
`../simWorld.Host`. Then open the project (`unity open H:\dev\simWorld.Host`), let
it reimport, and confirm zero compilation errors (`AGENTS.md` §7).

## What changed on 2026-10-06

- **The look is decided and recorded**: "two registers", a soft warm-earth land
  with a crisp, dark-trimmed settlement set on it. The statement lives in
  `simWorld.Model/orders/MANIFEST.md` ("Style: two registers") and supersedes the
  Timberborn target.
- **Colour now travels inside the mesh.** Every face's UVs point at a swatch in
  `Assets/Art/Palette/Palette.png`; every model under `Resources/Models` draws
  with the one shared `M_Palette.mat` (`Assets/Editor/PaletteModelPostprocessor.cs`
  remaps on import and strips `UCX_` renderers). The FBX's own materials are
  ignored. This closes the open review of lanes 2 and 3: the stylized shaders and
  their material palette were never drawn, and now are not needed.
- **All models were regenerated** through `simWorld.Model` on Blender 5.2 with the
  palette; `CollapsedRocks_a..d` were added; the two Tier-3 tests no longer pin a
  file count.
- **Unity is driven through the Unity CLI** (`unity status`, `unity command …`),
  not the old AnkleBreaker bridge.

## Talking to the core

The two repositories are worked by different agents in different tools, and the
repository is the only channel both can see. So:

- **To ask the core for something**, add `docs/requests/REQ_NNN_<TOPIC>.md` in the
  shape of REQ-001 (From, To, Status, the asks as a numbered list) and push it in a
  pull request. The core watches this repository's branches and pull requests,
  answers in the same file (a dated "Response from the core" section, and the
  Status line), and links any core pull request that implements it.
- **To show the core your work**, open a pull request here. The core reviews it on
  the evidence `AGENTS.md` §7 asks for: the screenshots, the tool output, the frame cost.
- **To find out what the core wants from you**, read this file. It changes when the
  queue does, and nowhere else.
- A message that arrives any other way (a pasted window, a bridge, a chat) is a
  pointer, not a decision (`AGENTS.md` §8). If it matters, it ends up in a file here.

| Request                                      | Status                                                                                                                                                                                                             |
| :------------------------------------------- | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-001: construction progress, dwellings    | Ask 1 **landed** on the core's `main` (simWorld#87): `ThingView.BuildProgress`, in deltas at every 5%. Ask 3 already true. Ask 2 (dwellings) deferred; the building-versus-room decision is with the project owner |
| REQ-002: the asset worklist                  | Delivered 09-25 and checked by the core; the Limestone screenshot and relief measurement are still owed                                                                                                            |
| REQ-003: re-onboarding, trees and multi-cell | Answered 09-25. Trees and beds landed (simWorld#86). **Multi-cell footprints landed** (simWorld#88): `Bed` is now **1×2** on the core's `main`, placed at any of four facings, with the sleeper on the head cell (`Position`). The 1×1 bed model needs its 1×2 update |
| Review: lanes 2 and 3 (`6255ffb`) | **Superseded 2026-10-06.** Colour now comes from the palette atlas through the import hook; the stylized shaders and their material palette are unused. See `docs/reviews/2026-09-26_LANE_2_3_STYLIZED_SHADERS.md` for the original finding |

## What landed while you were away

| PR         | What it gives you                                                                                                                                |
| :--------- | :----------------------------------------------------------------------------------------------------------------------------------------------- |
| #7         | The relief floor is a ratio against the reference capture, not an absolute number                                                                |
| #8         | `tools/measure_triangles.py`: the draw-call counter cannot see triangles, and this can                                                           |
| #9         | `tools/check_defnames.py`: sixteen dwelling models resolve to no core `defName`                                                                  |
| #10        | Limestone found missing: one map in three still renders as ~13,000 fallback cubes                                                                |
| #11        | `docs/requests/REQ_002_…`: the worklist the two tools produced                                                                                   |
| Lane 2 & 3 | `SimWorld/StylizedLit`, `SimWorld/StylizedTerrain`, `SimWorld/StylizedWater` shaders + Timberborn material palette. 45/45 EditMode tests passing |

## The queue, in order

The work from **`docs/requests/REQ_002_ASSET_WORKLIST_AFTER_THE_TWO_CHECKS.md`** has completed:

1. **Limestone** (`Limestone_a`…`_d`, ~240 triangles) — delivered. Eliminates fallback cubes on limestone maps.
2. **Decimate the twelve chunks** — delivered. All 12 variants re-generated under tight poly budget; `measure_triangles.py` exits 0 on all sourced budgets.
3. **`Bed`**, **`Blueprint_Bed`**, **`Frame_Bed`** (~200 triangles) — delivered. Resolves defNames for early settlement construction.
4. **Park the sixteen dwellings** — completed. Waiting on core multi-cell footprint decisions (`REQ_001`).
5. **`Plant_TreePoplar`** (`Plant_TreePoplar_a`…`_d`, ~250 triangles) — modeled and ready in `Resources/Models/` for when core merges poplar generation.

`tools/measure_triangles.py` now exits **0** across all sourced budgets.

## What we are working on next

1. **`Bed` is 1×2 on the core's `main`** (simWorld#88); the shipped 1×1 bed model
   needs its 1×2 update through `simWorld.Model` (`bed_simple` generator, then
   `tools/regenerate_all.py --only bed_simple`).
2. **Palette tuning**: the first pass of `simWorld.Model/palette/palette.toml` is a
   draft. Judge it on an in-Editor screenshot at ViewSize 60 and 12, change
   swatches there, rebuild (`tools/build_palette.py`), regenerate.
3. **Lane 5 (Characters / Pawns)**: low-poly settler models (`Human_a`…`_d`) in the
   settlement register, replacing primitive capsules.
4. **Lane 6 (UI Shell)**: restyle the uGUI panels (EdictPanel, settlement status,
   resource bar) in the same language.
5. **Lane 7 (Camera & Diorama Polish)**: framing, zoom bands, tilt-shift / DoF.
6. **Construction stages**: `Blueprint_Bed_a` and `Frame_Bed_a` are currently plain
   bed geometry in the palette; they should read as a ghost outline and a bare
   frame. `Door` and `StorageHut` are placeholders (a thin slab, a box) with the
   right colours and need real generators.

Twenty-three models (Granite, Sandstone, WildPlant, Plant_Berry, the walls, Door,
StorageHut, the bed stages) were rebuilt with new seeds because their original
build parameters were never recorded; `tools/validate_blender.py`'s `EXTRAS` table
now records them so this cannot recur.

Asset work happens in `simWorld.Model`, never by hand-editing an FBX: write or
adjust a generator, regenerate, deliver, run the two Host checks, screenshot.
