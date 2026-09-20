# The triangle budget, and why instancing does not cover for it

Lane zero's headline win was **112 → 61 draw calls** through URP's SRP Batcher, measured as exact
counts on both sides. That number is real and it still stands.

It also does not mean what it is easy to assume it means. Instancing makes it cheap to issue *many
draws of the same mesh*. It does nothing whatsoever about how heavy that mesh is. Fourteen thousand
instances of a 250-triangle rock and fourteen thousand instances of a 700,000-triangle rock are the
same number of draw calls and a factor of 2,800 apart in rasterisation work.

So the draw-call counter — the one instrument this project has been using to judge render health —
is structurally blind to the most expensive thing an asset lane can ship. Nothing in the Unity
console, the EditMode tests, or the frame-cost table would report it either. `tools/measure_triangles.py`
is the instrument that does.

## What the current models measure

Run from the repository root:

```sh
python3 tools/measure_triangles.py --all
```

It parses the binary FBX node tree directly and sums `PolygonVertexIndex`, counting `n - 2`
triangles per n-gon. That is what the GPU sees after Unity triangulates on import, so it matches
the Stats window rather than estimating from file size.

Measured at `6151f13`:

| Class | Example | Triangles | |
| :--- | :--- | ---: | :--- |
| Ground scatter (lane 1) | `Sandstone_a` | **258** | the shipped, reviewed standard |
| Ground scatter (lane 1) | `Granite_a` | 242 | |
| Flora | `WildPlant_a` | ~200 | in band |
| Items | `Bow_Short_a` | 174 | in band |
| Ore | `MineableGold_a` | 998 | in band |
| Dwellings | `House_Paleo_a` | 9,422 | see "the softer half" below |
| **Chunks** | `ChunkSandstone_c` | **93,299** | |
| **Chunks** | `ChunkLimestone_c` | **212,193** | |
| **Chunks** | `ChunkGranite_d` | **708,113** | |

`ChunkGranite_d` is **2,855× the triangle count of `Granite_d`**, the rock it is rubble from. It is
also, on its own, **1.8× the entire pre-asset scene** — 393,000 triangles across all 14,485
instances, per `docs/baseline/after.md`.

## Why chunks specifically are the bad case

Chunks are not rare set dressing. Reading the core:

- `MapGen/GenStep_Scatterers.cs:30` scatters chunks across every generated interior at
  `MapGenTuning.ChunkCellsPerItem`, which is 6,000 cells per chunk on a stone-poor tile and **500 on
  a stone-rich one**. A 200×200 interior is 40,000 cells, so a stone-rich map generates about
  **80 chunks before the player has done anything.**
- `GenStep_Ruins.cs:266` scatters more.
- `Buildings_Natural.xml` sets `<mineableThing>ChunkGranite</mineableThing>` and friends, so **every
  mined rock wall drops one.** The count grows for as long as the player keeps digging, without
  bound.

Eighty granite chunks at ~550,000 triangles average is **44 million triangles of ground rubble**, on
a map whose complete pre-asset geometry was 393,000. The scatter pass picks one chunk type for the
whole map, so an all-granite map is not a pathological case — it is one of three equally likely
outcomes.

## The budget

| Class | Budget | Basis |
| :--- | ---: | :--- |
| Ground scatter — chunks, rocks, flora, logs | **400** | **Sourced.** `Sandstone_a..d` shipped in lane 1 at 248–258 triangles, were reviewed against the reference board, and read correctly at `ViewSize 60`. 400 leaves headroom over what is known to work. |
| Items | 400 | Proposed |
| Ore | 1,200 | Proposed |
| Dwellings | 2,500 | Proposed |
| Anything unclassified | 1,200 | Proposed |

**Only a sourced budget fails the run.** A proposed budget prints a note and does not set the exit
code. The distinction is deliberate and it is the same discipline that governs every other
instrument here: a sourced budget is derived from something this repository already shipped and
accepted; a proposed one is a reviewer's judgement that has not been tested against a render.
Failing a lane on an opinion would make this tool an argument with an exit code.

At `6151f13`: **12 models over a sourced budget** (every chunk, by 170× to 1,770×) and **7 over a
proposed one** (the larger dwellings).

## The softer half, stated as such

The dwelling numbers are a note, not a finding. `House_Paleo_a` at 9,422 triangles is four times my
proposed budget, and my proposed budget is reasoning rather than measurement — "a class should not,
on its own, exceed the whole pre-asset scene" divided by a rough instance count. A house is the
thing the player actually looks at, it is large on screen, and there are tens of them rather than
hundreds. 9,422 may well be right.

The honest position is that **I do not know what a dwelling should cost, and the chunks do not
require me to know.** They fail by three orders of magnitude against a number that was shipped and
accepted in this repository. Those two claims deserve very different confidence and the tool now
reports them differently.

## What to do about the chunks

Decimate to the ground-scatter standard the lane-1 rocks already set — roughly 250 triangles, which
is what a watertight, four-variant, deterministic-by-thing-id rock turned out to need. The chunk
geometry itself is not the problem; it has simply not been decimated for the target. This is the
same silhouette note lane 1 arrived at from the other direction: at god-view zoom an object covers a
handful of pixels, and triangles below that threshold are paid for and never seen.

Unity cannot fix it on import. `meshCompression` changes storage, not triangle count, and the
`.meta` files currently set it to 0 anyway. The decimation has to happen in the modelling tool.

## Gating a lane on it

```sh
python3 tools/measure_triangles.py    # exit 1 if any model is over a sourced budget
```

Worth running before an asset drop rather than after, for the reason lane zero's step 0 existed: the
cheapest time to catch this is before it is in the history as 190 MB of binaries.

## What this does not claim

- **Not that the chunks are bad models.** They are almost certainly good geometry at the resolution
  they were authored for. The defect is that nothing downsampled them for a god-view instanced
  renderer, and nothing in the pipeline was watching.
- **Not a frame-time measurement.** No frame was rendered for this. These are exact counts read from
  the committed files, which is why they can be quoted without the framing caveats that dogged
  `docs/baseline/after.md`. What they predict about frame time is an inference, and a strong one at
  44 million triangles, but it is an inference.
- **Not that the drop should be reverted.** The flora, items, ore and rocks are all in band, and the
  dwellings are arguable. Twelve files need decimating.
