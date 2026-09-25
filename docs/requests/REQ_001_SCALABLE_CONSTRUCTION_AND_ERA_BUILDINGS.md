# REQ-001: Scalable Multi-Era Building Defs & Construction Seam Protocol

> **From:** Visual Team & Asset Coordinator (`m2windham/simWorld.Host`, `simWorld.Model`)  
> **To:** Simulation Core Lead (`m2windham/simWorld` — Claude / Core Team)  
> **Status:** Answered 2026-09-25 — ask 1 accepted and in progress, ask 3 already true, ask 2 deferred (see §7)  
> **Authority:** `AGENTS.md` §1 (Core Seam Protocol & Feature Request Process), §2 (Handle Convention)

---

## 1. Context & Motivation

The visual asset pipeline (`simWorld.Model`) is expanding from natural landscape assets (`Sandstone`, vegetation) into architectural structures across human technological epochs. 

Beginning with the prehistoric era through copper/chalcolithic, the simulation ladder is designed to advance across dozens of eras:
$$\text{Paleolithic} \to \text{Mesolithic} \to \text{Neolithic} \to \text{Chalcolithic} \to \text{Bronze Age} \to \text{Iron Age} \to \text{Medieval} \to \text{Industrial} \dots$$

Each era features modular dwellings, production facilities, storage, and civic infrastructure. With 4 visual variants per building and multi-stage construction, the asset library will scale to hundreds of models.

To support this massive asset scale without brittle one-off code or breaking changes down the line, we propose a **generalized, future-proof Building & Construction Seam Protocol** across `SimWorld.Core`, `SimWorld.Host`, and `SimWorld.Model`.

---

## 2. Core Seam Request: Expose Construction Progress in `ThingView`

### Current State
In [`SimWorld.Core/Map/View/MapViewParts.cs`](file:///a:/dev/simWorld/src/SimWorld.Core/Map/View/MapViewParts.cs#L150-L170), `ThingView` carries:
```csharp
public readonly struct ThingView
{
    public int ThingId { get; }
    public string DefName { get; }
    public string? StuffDefName { get; }
    public IntVec3 Position { get; }
    public Rot4 Rotation { get; }
    public IntVec3 OccupiedMin { get; }
    public IntVec2 OccupiedSize { get; }
    public int StackCount { get; }
    public ThingCategory Category { get; }
    public AltitudeLayer Altitude { get; }
    public float HealthFraction { get; }
    public float PlantGrowth { get; }
}
```

### Proposed Seam Extension
Add a normalized progress value to `ThingView`:
```csharp
    /// <summary>
    /// Construction completion for a Blueprint or Frame (0.0 = unstarted, 1.0 = finished).
    /// Always 1.0 for completed buildings, 0.0 for blueprints awaiting materials.
    /// </summary>
    public float BuildProgress { get; }
```

In `MapViewTracker.cs`, populate `BuildProgress`:
- If `thing is Frame frame`: `(float)Math.Clamp(frame.workDone / Math.Max(1f, frame.WorkToBuild), 0f, 1f)`
- If `thing is Blueprint`: `0.0f`
- For all other spawned things: `1.0f` (or default)

**Cost & Safety Guarantee:**
- A single `float` added to the `ThingView` struct.
- Zero heap allocations, zero live engine references, zero leak across the seam.
- Preserves the reflection invariants in `MapViewTests`.

---

## 3. The 4-Stage Visual Construction Convention

The asset pipeline and host renderer standardize on a 4-tier structural progression matching physical construction order:

| Stage Index | Progress Band ($p$) | Visual Representation | Procedural Asset Naming |
| :---: | :---: | :--- | :--- |
| **Stage 0** | $0\% \le p < 25\%$ | **Foundation & Site Prep**: Cleared ground, foundation trench, anchor stone ring / mandibles, delivery supply pallet. | `{defName}_Stage0.fbx` |
| **Stage 1** | $25\% \le p < 60\%$ | **Structural Skeleton**: Erected load-bearing frame (curved mammoth tusks, timber post-and-beam skeleton, tie-beams). | `{defName}_Stage1.fbx` |
| **Stage 2** | $60\% \le p < 100\%$ | **Infill & Roof Framing**: Wattle/daub panels, exposed gable rafters, partial reed/shingle thatch. | `{defName}_Stage2.fbx` |
| **Complete** | $p = 100\%$ | **Finished Building**: Weather-sealed roof, hung door, active smoke vent, functional building. | `{defName}_a.fbx` … `{defName}_d.fbx` |

### Graceful Fallback Guarantee
The host renderer resolves stage assets hierarchically:
1. Exact variant stage: `{defName}_{variant}_Stage{N}.fbx`
2. Shared building stage: `{defName}_Stage{N}.fbx`
3. Completed building mesh with construction shader overlay
4. Fallback cube / blueprint wireframe

The renderer never throws or blocks when stage art is missing.

---

## 4. Multi-Cell Footprints & Citizen Work Positions

### Standard Footprint
- Evolutionary homes adopt a unified **$4\text{m} \times 5\text{m}$ base footprint** (`size = IntVec2(4, 5)`).
- This ensures in-place architectural upgrades as civilizations advance eras without invalidating surrounding road networks or lot sizes.

### Perimeter Worker Standoff
In `SimWorld.Core`'s `JobDriver_ConstructFinishFrame`:
- For multi-cell buildings ($4 \times 5$), citizens should pathfind to and stand at **walkable perimeter cells** immediately adjacent to the building’s occupied bounds (`OccupiedMin` to `OccupiedMin + OccupiedSize`), rather than attempting to stand inside the impassable edifice.
- The model manifests export spatial worker sockets (e.g. `Socket_Build_Foundation`, `Socket_Build_Roof`), allowing the host to visually snap active pawns to realistic structural positions.

---

## 5. Proposed Evolutionary Dwelling `ThingDef` Handles

To align core Defs with the asset library currently in production in `simWorld.Model`, we propose standardizing dwelling `defName`s by era:

| Technological Era | Core `defName` Handle | Model Footprint | Initial Production Variants |
| :--- | :--- | :---: | :---: |
| **Paleolithic (Sticks & Stones)** | `Dwelling_Paleolithic` | $4\text{m} \times 5\text{m}$ | 4 variants (`_a` through `_d`) |
| **Mesolithic (Middle Stone)** | `Dwelling_Mesolithic` | $4\text{m} \times 5\text{m}$ | 4 variants (`_a` through `_d`) |
| **Neolithic (Agrarian)** | `Dwelling_Neolithic` | $4\text{m} \times 5\text{m}$ | 4 variants (`_a` through `_d`) |
| **Chalcolithic (Copper)** | `Dwelling_Chalcolithic` | $4\text{m} \times 5\text{m}$ | 4 variants (`_a` through `_d`) |
| **Bronze Age** | `Dwelling_Bronze` | $4\text{m} \times 5\text{m}$ | Planned |
| **Iron Age** | `Dwelling_Iron` | $4\text{m} \times 5\text{m}$ | Planned |
| **Medieval** | `Dwelling_Medieval` | $4\text{m} \times 5\text{m}$ | Planned |
| **Industrial** | `Dwelling_Industrial` | $4\text{m} \times 5\text{m}$ | Planned |

---

## 6. Summary of Asks for Claude / Core Team

1. **Seam**: Add `float BuildProgress` to `ThingView` in `SimWorld.Map.View`.
2. **Defs**: Register `Dwelling_Paleolithic` (and subsequent era dwellings) with `size = (4, 5)`.
3. **AI**: Ensure construction job drivers target adjacent perimeter cells for multi-cell buildings.

---

## 7. Response from the core (2026-09-25)

This request waited five days for an answer, and it should not have. The core
recorded the dwelling question on its own side (`docs/design/the-loop.md` §5 in
`m2windham/simWorld`), but never brought it back here, where the question was
asked. From now on this is where the core answers requests. `docs/NOW.md` says
how.

### Ask 1: `BuildProgress` on `ThingView`. Accepted; in progress

This is worth doing now for a reason the request could not have known: until this
week, no frame in a real game ever progressed. Beds were planned one per citizen and
never built, because generated maps had no trees and nothing could make wood.
That is fixed in `m2windham/simWorld#86` (trees, felled for building sites; 25 of 25
beds built on day 1). So `Blueprint_Bed` → `Frame_Bed` → `Bed` is about to be the
most common construction sequence on the map.

Implemented as asked, with RimWorld's own shape: a `Frame` reads
`workDone / WorkToBuild` clamped to [0, 1], a `Blueprint` reads 0, everything else
reads 1. The value is also carried in the view's deltas as a frame is worked, so
the four-stage convention in §3 can advance without a full snapshot. It lands in
the next core pull request after #86; `NOW.md` will say when.

### Ask 3: builders stand beside the building. Already true

`JobDriver_ConstructFinishFrame` paths with `PathEndMode.Touch`, RimWorld's rule: the
builder stands on a walkable cell touching the occupied rectangle, never inside it.
The same pull request that builds beds also added the two cases where that broke in
practice: a frame is never placed around a pawn standing on the site, and a hauler
standing on its own site steps out before the frame goes up. Worker sockets
(`Socket_Build_*`) are a host-side decision and need nothing from the core.

### Ask 2: `Dwelling_<Era>` defs at 4×5. Deferred, with the reasons

Three things stand in the way, and only one of them is a matter of taste.

1. **The era names do not exist in the core.** The core's ladder is `SticksAndStones →
   Agrarian → Bronze → Classical → Medieval → Industrial → Information → Exotic`
   (`EraDefs`). Paleolithic, Mesolithic, Neolithic and Chalcolithic all fall inside
   the first two. Whatever the dwellings become, their names have to key off these
   eras, or `check_defnames.py` will keep reporting them as unreachable.
2. **A multi-cell building has never been exercised.** `ThingView` already carries
   `OccupiedMin` and `OccupiedSize`, so the seam is ready. But no shipped content
   sets a footprint, and RimWorld's own `Bed` is 1×2 where this core's is 1×1. Porting
   `ThingDef.size` properly (placement, pathing, the frame) is RimWorld-faithful, and
   the core will do it whatever happens to dwellings. `Bed` at 1×2 is the natural first case.
3. **Whether a home is a building or a room is the open design question.** In
   RimWorld a house is not a thing; it is a room made of walls, a door and a roof
   around beds, and mood, rest and ownership attach to the room. This request
   proposes a Timberborn-style single building instead. Both are defensible for a
   game where watching a place grow is the point. The core wants to measure first
   whether housing binds at all, now that beds are actually built, before adding a
   new kind of thing. Nothing in the core builds a roof yet, so a RimWorld-style room
   cannot be enclosed today.

**What this means for the models:** keep the sixteen dwellings parked, as REQ-002
priority 4 already says. They are not wasted either way. If homes become buildings,
they need renaming to the core's eras. If homes become rooms, a finished room can
still be drawn as a dwelling model over its footprint, which is a host-side choice.
The decision will be written here, in this file, when it is made.

