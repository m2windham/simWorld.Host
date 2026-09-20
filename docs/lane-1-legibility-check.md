# Lane 1 review: the scene got flatter, measured against your own invariant

Lane 1 and the Sandstone delivery are real work and most of it is right. The
rocks are watertight, four variants, 192 triangles, resolved deterministically by
thing id — exactly what `VisualRegistry` was built for, and 12,989 placeholder
cubes are gone. The reference board came first, as §11 asked.

One measured result runs against the goal, and it is the kind that no EditMode
test and no frame-cost number can catch.

## What the pixels say

Measured on the two committed screenshots, same 800×450 region of terrain, same
method on both:

| Local relief (mean neighbour delta) | 2.22 | **1.05** | **−53%** |
| :--- | ---: | ---: | ---: |
| Local relief (mean neighbour-pixel delta) | 2.22 | **1.05** | **−53%** |
| Overall contrast (stdev of luminance) | 23.17 | **15.50** | **−33%** |

Local relief halved. That is a direct measurement of how much the surface reads
as three-dimensional at this zoom, and it went down after a lighting pass whose
stated purpose was to add contact shadowing and depth.

## Why that is a miss, by the board's own standard

`docs/reference-board.md` sets two invariants this contradicts:

> 1. **Chunky, Legible Silhouettes**: Every building, rock, and pawn must be
>    instantly recognizable as a geometric silhouette at **60–200 unit view
>    size**.
> 2. **Warm, High-Value-Contrast Palette** … Elements separate by **tonal
>    value**, not artificial black outlines.

The screenshot is at `ViewSize = 60`, inside that band. Tonal separation is the
named mechanism, and it measurably decreased.

## The likely cause, and it is not the rocks

Two candidates, and the second looks larger:

1. **Rock profile at distance.** A 192-triangle boulder is a rounder, lower form
   than a unit cube. At map zoom each instance covers a handful of pixels, where
   a cube's hard edges and flat faces read as a block and a boulder reads as a
   dot. Better geometry, worse silhouette — at *this* zoom.
2. **The ambient fill.** "Trilight ambient fill eliminating pitch-black
   crevices" is exactly the operation that removes tonal separation. Combined
   with `DirectLightingStrength = 0.25` on SSAO and `Exposure +0.15`, the
   shadows that carry the relief are being filled back in faster than SSAO adds
   them. `Contrast +14.0` is applied globally and cannot recover local relief
   that was never rendered.

I cannot tell which dominates without the editor, and I am not going to guess in
a document. The split is cheap to measure: render the same frame with the Global
Volume disabled, then with ambient fill dropped, then with both. The
configuration whose local-relief number goes back above the pre-lane-1 2.22 is
the answer.

## What I am not saying

Not that the lighting settings are wrong individually — warm key, soft shadows
and a neutral tonemap are all right for the target. Not that the rocks should be
reverted; they are better geometry and the fallback cubes were never the goal.

The claim is narrower and it is measurable: **the aggregate result at god-view
zoom moved away from the board's own legibility invariant, and the numbers say so
in the direction that matters.**

## Suggested next step

Treat local relief as a lane deliverable, not an impression. It costs one line
of measurement per screenshot and it is the only number so far that tracks what
the reference board actually asks for:

- [ ] Re-render at `ViewSize = 60` with ambient fill reduced and SSAO
      `DirectLightingStrength` lowered, and report local relief alongside frame
      cost.
- [ ] Hold **local relief ≥ 2.2** at that zoom as the floor, since that is what
      the placeholder cubes achieved and no art pass should read flatter than
      the thing it replaced.
- [ ] If the rocks turn out to be the cause, that is a silhouette note for the
      model pipeline — taller, more angular forms at this scale — not a reason
      to keep cubes.
