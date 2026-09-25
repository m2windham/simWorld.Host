# REQ-002: The asset worklist after the two checks

Two instruments landed in `tools/` since the last drop, and between them they
answered a question nobody could answer before: **which of the sixty shipped models
are actually doing anything, and what do they cost.**

This is the worklist that came out of it, priority-ordered, with the reasoning in
`docs/triangle-budget.md` (#8) and `docs/defname-contract.md` (#9, #10). Nothing here
is a matter of taste. Every item is a number read off a committed file.

Run both before a drop, not after:

```sh
python3 tools/measure_triangles.py    # exit 1 if any model is over a SOURCED budget
python3 tools/check_defnames.py       # exit 1 if any model resolves to no core defName
```

## Priority 1 — Limestone. One map in three is still cubes

`GenStep_RocksAndMountains.cs:22,36` in the core picks each map's dominant rock with
`rand.Element(Sandstone, Granite, Limestone)` — uniform over three. `Sandstone_a..d`
shipped. `Granite_a..d` shipped. **`Limestone` did not.**

So a third of all generated maps still render as roughly **thirteen thousand fallback
cubes** — the exact scene lane 1 existed to retire. Which the player gets is a die roll
at world generation.

`ChunkLimestone_a..d` and `WallLimestone` both shipped, so limestone is complete except
for the one def carrying nearly all of its instances. Same job as the two rocks already
done, a third time.

**Lane 4 (Structures) or whoever did the rocks. Budget 400 triangles (sourced).**

## Priority 2 — Decimate the twelve chunks

| Model | Triangles | Over budget |
| :--- | ---: | ---: |
| `ChunkGranite_d` | 708,113 | 1,770× |
| `ChunkGranite_a` | 700,218 | 1,751× |
| `ChunkGranite_c` | 401,448 | 1,004× |
| `ChunkGranite_b` | 398,209 | 996× |
| `ChunkLimestone_c` | 212,193 | 530× |
| `ChunkLimestone_d` | 179,084 | 448× |
| `ChunkLimestone_b` | 152,537 | 381× |
| `ChunkSandstone_d` | 138,902 | 347× |
| `ChunkSandstone_b` | 131,520 | 329× |
| `ChunkSandstone_a` | 100,476 | 251× |
| `ChunkSandstone_c` | 93,299 | 233× |
| `ChunkLimestone_a` | 68,185 | 170× |

`Sandstone_a` — shipped, reviewed, reads correctly at `ViewSize 60` — is **258
triangles**. `ChunkGranite_d` is 2,855× the rock it is rubble from, and on its own 1.8×
the entire pre-asset scene.

Chunks are not set dressing: one per 500 cells on a stone-rich tile (~80 per map before
the player acts), more from ruins, and **every mined wall drops another, without bound.**
Eighty granite chunks is 44 million triangles of ground rubble.

The geometry is presumably fine at the resolution it was authored for. It has not been
downsampled for a god-view instanced renderer, and Unity cannot do it on import —
`meshCompression` changes storage, not triangle count.

**Target ~250 triangles, the standard the lane-1 rocks already set.**

## Priority 3 — `Bed`

One per citizen, uncapped, and the most numerous thing the settlement actually **builds**.
Lower instance count than rock, far higher meaning per instance: it is what makes a place
read as somewhere people live rather than a quarry.

**Budget 400 triangles.**

## Priority 4 — Park the sixteen dwellings, pending a core decision

`House_Paleo_a..d`, `House_Meso_a..d`, `House_Neo_a..d`, `House_Chalco_a..d` resolve to
**no defName the core has**, so nothing will ever draw them. The era names match nothing
either — the core's ladder is `SticksAndStones → Agrarian → Bronze → Classical → Medieval
→ Industrial → Information → Exotic`.

**This is not a modelling mistake and the files should not be deleted.** They are good work
aimed at a concept the core does not have, and they are evidence it is missing one: the
core's entire idea of shelter is a `Bed` per citizen plus at most forty `Wall` segments.
A settlement you watch grow is exactly the game where seeing a house go up *is* the
progress.

It is the core's call, and it has two real blockers — the era naming, and the fact that
`ThingDef.size` defaults to 1×1 with no shipped content setting a footprint, so a
multi-cell building is not representable today. Raised on the core side in
`docs/design/the-loop.md`. **No further dwelling modelling until it comes back**, or the
work compounds against a concept that may change shape.

## Not a priority, despite the number

`tools/check_defnames.py --unmodelled` lists **115** core defs with no model. That is a bad
priority signal — a `Sculpture`, a `TrapSpike`, one `ResearchBench`. Instance count is what
matters, and the four items above are where it lives.

## Local setup

The two repositories must be **siblings**, because `Packages/manifest.json` references the
core relatively:

```json
"com.simworld.core": "file:../../simWorld/src/SimWorld.Core"
```

```text
A:\simWorld\            <- the engine-free core
A:\simWorld.Host\       <- this repository
```

Both tools resolve the core through that manifest entry, so they work from
`A:\simWorld.Host` with no configuration. If `check_defnames.py` cannot find the core, the
sibling layout is wrong — fix the layout, not the tool.

**Never let Package Manager rewrite that path.** It replaces it with an absolute path at
the slightest provocation, which works on exactly one machine and fails silently
everywhere else.

## Definition of done for this request

- `python3 tools/measure_triangles.py` exits 0.
- `python3 tools/check_defnames.py` exits 0, or the dwellings are the only entries and the
  core decision is still open.
- A before/after screenshot at `ViewSize 60` on a **Limestone-dominant** map, which is the
  one nobody has ever seen rendered.
- `python3 tools/measure_relief.py` against the reference capture: local relief at or above
  **0.95×**, same crop and estimator, measured in the same session (`docs/relief-measurement-note.md`).

---

## Status & Completion (2026-09-25)

**Status: Delivered**

1. **Limestone**: `Limestone_a`…`_d` generated and delivered. Sourced budget (<400 tris) met (~240 tris).
2. **Chunks Decimated**: All 12 variants re-generated with bevels removed and strict decimation budget applied. Every chunk variant is now under its 400 triangle budget. `measure_triangles.py` exits 0.
3. **Bed**: `Bed.fbx` delivered along with `Blueprint_Bed_a.fbx` and `Frame_Bed_a.fbx`.
4. **Dwellings Parked**: Awaiting core multi-cell footprint decisions per REQ-001 response.
5. **Poplar Trees**: `Plant_TreePoplar_a`…`_d` pre-delivered into `Resources/Models/` ready for core tree PR.
